using System;
using System.Collections.Generic;
using System.IO;
using Brain.Data;
using Brain.Data.Collection;
using Brain.IO;

namespace Brain;

public sealed class Network
{
    // per-step buffer for total synaptic current arriving at each postsynaptic neuron
    private float[] _current;

    // indices of spiking neurons from the last step. propagation walks this instead of checking every neuron spike
    // which
    // would be O(n)
    private int[] _spikes;
    private int   _spikeCount;

    /// <summary>Creates a network with preallocation for <paramref name="capacity" /> neurons.</summary>
    public Network(int capacity = 0) : this(new Connectome<float>(new NeuronPool(capacity)), 0)
    {
    }

    /// <summary>
    ///     <para>
    ///         Creates a network around an already-filled connectome and the pool it was built on, resuming at
    ///         <paramref name="time" />. Loaders build both first, so a failed read throws before any network exists.
    ///     </para>
    ///     <para>
    ///         Takes the pool from the connectome rather than as its own parameter, so the two can't come from different
    ///         networks.
    ///     </para>
    /// </summary>
    internal Network(Connectome<float> connectome, double time)
    {
        Connectome = connectome;
        Neurons    = connectome.Pool;
        Time       = time;

        // adhere to pool's final capacity since the raw parameter is consumer-provided
        _current = new float[Neurons.Capacity];
        _spikes  = new int[Neurons.Capacity]; // sized to worst case where every neuron fires on the same step

        // gather spikes that are about to occur so files don't need to explicitly record them
        var lastSpike = Neurons.LastSpike;

        for (var i = 0; i < lastSpike.Length; i++)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (lastSpike[i] == time)
                _spikes[_spikeCount++] = i;
        }
    }

    /// <summary>
    ///     <para>Tracks both inbound and outbound synapses and weights via two compressed sparse row matrices.</para>
    ///     <para>
    ///         Exchanges some algorithmic complexity (and some SIMD and eager caching) for massively less spatial complexity
    ///         in comparison to conventional dense 2D matrices. <i>Drosophila melanogaster</i>'s brain contains almost 400
    ///         times more synapses than soma on average, yet CSR still saves over 95% memory since their synaptic density is
    ///         still nowhere near <c>soma²</c>.
    ///     </para>
    /// </summary>
    public Connectome<float> Connectome { get; }

    /// <summary>SoA for all neurons</summary>
    public NeuronPool Neurons { get; }

    public int Count => Neurons.Count;

    /// <summary>Simulated time elapsed (ms)</summary>
    public double Time { get; private set; }

    public Diagnoser? IntegrationDiagnoser { get; set; }
    public Diagnoser? SpikeDiagnoser       { get; set; }

    /// <summary>
    ///     Raises when <see cref="RemoveNeuron(int)" /> calls. This is for consumers who store neural indices that risk
    ///     staleness due to a remove-swap. <see cref="SwapRemove.Successor" /> moves to <see cref="SwapRemove.Remover" />
    ///     unless <see cref="SwapRemove.IsSwap" /> is false.
    /// </summary>
    public event Action<Network, SwapRemove>? NeuronRemoved;

    /// <summary>Raises after <see cref="Clear" /> for consumers who store neural indices.</summary>
    public event Action<Network>? Cleared;

    /// <summary>
    ///     Remove every neuron and synapse and rewinds <see cref="Time" /> to 0. This stays the same object (allocations
    ///     included), so references to it and subscriptions on it stay valid.
    /// </summary>
    public void Clear()
    {
        Connectome.Clear();
        Neurons.Clear();

        _spikeCount = 0;
        Time        = 0;

        Cleared?.Invoke(this);
    }

    /// <summary>
    ///     Create a standalone neuron with default parameters.
    /// </summary>
    /// <returns>The new <see cref="Neuron" /></returns>
    public Neuron AddNeuron()
    {
        var i = Neurons.Add();

        RefitCaches();

        return Neurons[i];
    }

    public IEnumerable<Neuron> AddNeurons(int count = 1)
    {
        var i = Neurons.Add(count);

        RefitCaches();

        return Neurons[i..];
    }

    private void RefitCaches()
    {
        if (_current.Length >= Neurons.Capacity)
            return;

        Array.Resize(ref _current, Neurons.Capacity);
        Array.Resize(ref _spikes,  Neurons.Capacity);
    }

    /// <summary>
    ///     <para>Create a new network from the Brain network file at <paramref name="path" />.</para>
    /// </summary>
    /// <exception cref="InvalidDataException">The file isn't a valid Brain network file</exception>
    public static Network FromFile(string path)
    {
        var network = new Network();

        using var stream = File.OpenRead(path);
        return FromStream(stream);
    }

    /// <summary>Create a new <see cref="Network" /> from native Brain format in <paramref name="stream" />.</summary>
    /// <exception cref="InvalidDataException">The stream isn't a valid Brain network file</exception>
    public static Network FromStream(Stream stream)
    {
        return BrainFormat.Read(stream);
    }

    /// <summary>
    ///     Write this network to <paramref name="path" /> in native Brain format. Commits pending changes in the connectome
    ///     first.
    /// </summary>
    public void SaveFile(string path)
    {
        using var stream = File.Create(path);
        Save(stream);
    }

    /// <summary>
    ///     Write this network to <paramref name="stream" /> in native Brain format. Commits pending changes in the connectome
    ///     first.
    /// </summary>
    public void Save(Stream stream)
    {
        BrainFormat.Write(this, stream);
    }

    /// <summary>
    ///     Delete the neuron at <paramref name="i" /> and every relevant synapse. Uses swap-remove, which may invalidate
    ///     external caching (see <see cref="NeuronRemoved" />).
    /// </summary>
    public void RemoveNeuron(int i)
    {
        if (!Neurons.Exists(i))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i),
                i,
                $"Attempt to remove nonexistent neuron at index {i} (count {Count})"
            );
        }

        // ensure connectome is up to date before deletion passthrough
        Connectome.CommitChanges();

        // sever synapses to and from the removing neuron
        // iterating a row while writing removals is safe because To/From spans don't change
        foreach (var pre in Connectome.To(i).Columns)
            Connectome[pre, i] = 0f;

        foreach (var post in Connectome.From(i).Columns)
            Connectome[i, post] = 0f;

        // commit so the successor's rows below no longer contain synapses to or from `i`. otherwise a severed
        // `i → successor` would still be listed in To(successor) and get rewritten as `i → i`
        Connectome.CommitChanges();

        // the pool decides who swaps in; ask before removing so the successor still exists (and passes the
        // connectome's range check) while its synapses move
        var successor = Neurons.SuccessorOf(i);

        if (successor >= 0)
        {
            foreach (var (pre, weight) in Connectome.To(successor))
            {
                // self-synapse needs an exception since `successor` is about to move to `i`
                Connectome[pre == successor ? i : pre, i] = weight;
                Connectome[pre, successor]                = 0f;
            }

            foreach (var (post, weight) in Connectome.From(successor))
            {
                // self-synapse already moved above; handling it again would point it back at `successor`
                if (post == successor)
                    continue;

                Connectome[i, post]         = weight;
                Connectome[successor, post] = 0f;
            }

            Connectome.CommitChanges();
        }

        // swap-remove, moving exactly the successor asked for above
        Neurons.RemoveAt(i);

        for (var j = _spikeCount - 1; j >= 0; j--)
        {
            if (_spikes[j] == i)
                _spikes[j] = _spikes[--_spikeCount]; // swap-remove the dead neuron's spike if present
            else if (_spikes[j] == successor)
                _spikes[j] = i; // update the moving neuron's index in the spike list
        }

        NeuronRemoved?.Invoke(this, new SwapRemove(i, successor));
    }

    /// <summary>
    ///     <para>Advance the simulation by <paramref name="time" />.</para>
    ///     <para>Accepts a <see cref="TimeSpan" /> for convenience and uses milliseconds internally.</para>
    /// </summary>
    public void Step(TimeSpan time)
    {
        var dt = (float)time.TotalMilliseconds;

        // propagation needs to see up-to-date rows and columns
        Connectome.CommitChanges();

        // TODO: change to single-arg Array.Clear(arr) on unity 6.8 CoreCLR
        Array.Clear(_current, 0, _current.Length);

        SpikeDiagnoser?.Start();

        // synaptic events delivered this step so the hot loop doesn't repeatedly probe its diagnoser
        var events = 0u;

        // gather synaptic current from last step
        // walks only outbound rows in contrast to every (pre, post) pair for 2D arrays
        // TODO: spike list still holds previous results which means dt bottlenecks propagation speed
        for (var i = 0; i < _spikeCount; i++)
        {
            // avoid foreach in tight loop
            var row = Connectome.From(_spikes[i]);

            for (var j = 0; j < row.Count; j++)
                _current[row.Columns[j]] += row.Values[j];

            events += (uint)row.Count;
        }

        SpikeDiagnoser?.Stop(events);

        // store spans in variables to avoid creating a new one per property access
        var inputCurrent = Neurons.InputCurrent;

        var potential      = Neurons.Potential;
        var restPotential  = Neurons.RestPotential;
        var resetPotential = Neurons.ResetPotential;
        var leakRate       = Neurons.LeakRate;

        var lastSpike = Neurons.LastSpike;
        var threshold = Neurons.Threshold;

        // spikes occur at end of step
        Time += time.TotalMilliseconds;

        // discard spikes in last step and begin recording new spikes
        _spikeCount = 0;

        IntegrationDiagnoser?.Start();

        // integrate, leak, and check voltage threshold
        for (var i = 0; i < Count; i++)
        {
            // asymptotically leak towards rest
            var leak = leakRate[i] * (restPotential[i] - potential[i]);

            // integrate all current flowing into this neuron this step
            var current = inputCurrent[i] + _current[i];

            // forward Euler integration (value += rate * dt)
            potential[i] += (leak + current) * dt;

            // skip if no spike
            if (potential[i] < threshold[i])
                continue;

            potential[i] = resetPotential[i];
            lastSpike[i] = Time;

            _spikes[_spikeCount++] = i;
        }

        IntegrationDiagnoser?.Stop((uint)Count);
    }
}

// in Network.cs because this is mostly for consumers; NeuronPool's swap logic is internal
public readonly record struct SwapRemove(int Remover, int Successor)
{
    // NeuronPool.RemoveAt returns -1 for no swap (no successor)
    public bool IsSwap => Successor >= 0;
}