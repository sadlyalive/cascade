using System;
using Brain.Data;
using Brain.Data.Collection;

namespace Brain;

public sealed class Network
{
    // per-step buffer for total synaptic current arriving at each postsynaptic neuron
    private float[] _current;

    //indices of spiking neurons from the last step. propagation walks this instead of checking every neuron spike which
    // would be O(n)
    private int[] _spikes;
    private int   _spikeCount;

    /// <summary>Creates a network of <paramref name="capacity" /> unconnected neurons with default parameters.</summary>
    public Network(int capacity)
    {
        Neurons    = new NeuronPool(capacity);
        Connectome = new Connectome<float>(capacity);

        // adhere to pool's final capacity since the raw parameter is consumer-provided
        _current = new float[Neurons.Capacity];
        _spikes  = new int[Neurons.Capacity]; // sized to worst case where every neuron fires on the same step
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

    /// <summary>SoA for every neuron</summary>
    public NeuronPool Neurons { get; }

    public int Count => Neurons.Count;

    /// <summary>Simulated time elapsed (ms)</summary>
    public double Time { get; private set; }

    public Diagnoser Diagnoser { get; } = new(1024);

    /// <summary>
    ///     Raises when <see cref="RemoveNeuron(int)" /> calls and passes <c>(removed, moved)</c>. This is for consumers who
    ///     store neural indices that risk staleness due to the remove-swap. <c>moved</c> assumes <c>removed</c>'s position.
    ///     <c> moved = -1</c> implies the last neuron was removed.
    /// </summary>
    public event Action<int, int>? NeuronRemoved;

    /// <summary>
    ///     Creates a standalone neuron with default parameters.
    /// </summary>
    /// <returns>The new <see cref="Neuron" /></returns>
    public Neuron AddNeuron()
    {
        var i = Neurons.Add();

        // adjust metadata arrays if the pool just grew from this addition
        // ReSharper disable once InvertIf
        if (_current.Length < Neurons.Capacity)
        {
            Array.Resize(ref _current, Neurons.Capacity);
            Array.Resize(ref _spikes,  Neurons.Capacity);
        }

        return Neurons[i];
    }

    /// <summary>
    ///     Deletes the neuron at <paramref name="i" /> and every relevant synapse. Uses swap-remove, which may invalidate
    ///     external caching (see <see cref="NeuronRemoved" />).
    /// </summary>
    public void RemoveNeuron(int i)
    {
        // uint cast catches i < 0 (what .NET runtime does as well)
        if ((uint)i >= (uint)Count)
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

        // commit so no stale synapses exist when NeuronPool.RemoveAt() moves the last neuron to the remover's index
        Connectome.CommitChanges();

        // swap-remove
        var oldIndex = Neurons.RemoveAt(i);

        // move all synapses at the last index to the remover's now-vacant index
        if (oldIndex >= 0)
        {
            foreach (var (pre, weight) in Connectome.To(oldIndex))
            {
                Connectome[pre, i]        = weight;
                Connectome[pre, oldIndex] = 0f;
            }

            foreach (var (post, weight) in Connectome.From(oldIndex))
            {
                Connectome[i, post]        = weight;
                Connectome[oldIndex, post] = 0f;
            }

            Connectome.CommitChanges();
        }

        for (var j = _spikeCount - 1; j >= 0; j--)
        {
            if (_spikes[j] == i)
                _spikes[j] = _spikes[--_spikeCount]; // swap-remove the dead neuron's spike if present
            else if (_spikes[j] == oldIndex)
                _spikes[j] = i; // update the moving neuron's index in the spike list
        }

        NeuronRemoved?.Invoke(i, oldIndex);
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

        Diagnoser.Start();

        // gather synaptic current from last step
        // walks only outbound rows in contrast to every (pre, post) pair for 2D arrays
        // TODO: spike list still holds previous results which means dt bottlenecks propagation speed
        for (var i = 0; i < _spikeCount; i++)
        {
            // avoid foreach in tight loop
            var row = Connectome.From(_spikes[i]);

            for (var j = 0; j < row.Count; j++)
                _current[row.Columns[j]] += row.Values[j];
        }

        Diagnoser.Stop();

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
    }
}