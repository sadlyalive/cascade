using System;
using Brain.Data.Collection;

namespace Brain;

public sealed class Network
{
    /// <summary>
    ///     <para>Per-step buffer that caches total synaptic current arriving at each postsynaptic neuron.</para>
    /// </summary>
    private float[] _current;

    /// <summary>
    ///     <para>
    ///         Indices of spiking neurons from the last step. Only the first <see cref="_spikeCount" /> entries are valid.
    ///     </para>
    ///     <para>
    ///         Propagation walks this instead of checking every neuron's <see cref="NeuronPool.Spike" /> flag, so its cost
    ///         depends only on the number of spikes rather than a constant <c>O(n)</c>. <see cref="NeuronPool.Spike" /> is still
    ///         written for outside readers (e.g. rendering), but <c>Step()</c> treats this list as the source of truth.
    ///     </para>
    /// </summary>
    private int[] _spikes;

    private int _spikeCount;

    /// <summary>Creates a network of <paramref name="count" /> unconnected neurons with default parameters.</summary>
    public Network(int count)
    {
        Neurons    = new NeuronPool(count);
        Connectome = new Connectome<float>(count);

        // both sized to the pool's capacity, not its count, and kept that way by AddNeuron
        _current = new float[Neurons.Capacity];
        _spikes  = new int[Neurons.Capacity]; // worst case: every neuron fires on the same step

        for (var i = 0; i < count; i++)
            AddNeuron();
    }

    /// <summary>
    ///     <para>Two-way compressed sparse row matrix for synaptic weights.</para>
    ///     <para>
    ///         Exchanges some algorithmic complexity (and some SIMD and eager caching) for massively less spatial
    ///         complexity in comparison to conventional 2D arrays. <i>Drosophila melanogaster</i>'s brain contains almost 400
    ///         times more synapses than soma on average, yet CSR still saves over 95% memory since their synaptic density is
    ///         still nowhere near <c>soma²</c>.
    ///     </para>
    /// </summary>
    public Connectome<float> Connectome { get; }

    /// <summary>SoA for every neuron.</summary>
    public NeuronPool Neurons { get; }

    public int Count => Neurons.Count;

    public Diagnoser Diagnoser { get; } = new(1024);

    /// <summary>Appends an unconnected neuron with default parameters and returns its index.</summary>
    public int AddNeuron()
    {
        var i = Neurons.Add();

        // adjust metadata arrays if the pool just grew from this addition
        // ReSharper disable once InvertIf
        if (_current.Length < Neurons.Capacity)
        {
            Array.Resize(ref _current, Neurons.Capacity);
            Array.Resize(ref _spikes,  Neurons.Capacity);
        }

        return i;
    }

    /// <summary>
    ///     <para>
    ///         Deletes the neuron at <paramref name="i" /> and every relevant synapse.
    ///     </para>
    ///     <para>
    ///         Performs a swap-remove such that the last neuron moves into <paramref name="i" /> (state, synapses, etc.).
    ///     </para>
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
        // iterating a row while writing removals is safe because removals are buffered and, even if the buffer fills
        // and commits midway, the spans view old arrays
        foreach (var pre in Connectome.To(i).Columns)
            Connectome[pre, i] = 0f;

        foreach (var post in Connectome.From(i).Columns)
            Connectome[i, post] = 0f;

        // commit so reoccupation does not see stale synapses on To/From in the case that the removing neuron and the
        // swapping neuron were connected
        Connectome.CommitChanges();

        // adhere to swap-remove by moving all synapses from the old index to the new vacant one
        var oldIndex = Neurons.RemoveAt(i);

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
                _spikes[j] = _spikes[_spikeCount--]; // swap-remove the dead neuron's spike if present
            else if (_spikes[j] == oldIndex)
                _spikes[j] = i; // update the moving neuron's index in the spike list
        }
    }

    /// <summary>
    ///     <para>Advance the simulation by <paramref name="time" />.</para>
    ///     <para>Accepts a <see cref="TimeSpan" /> for convenience and uses milliseconds internally.</para>
    /// </summary>
    public void Step(TimeSpan time)
    {
        var dt = (float)time.TotalMilliseconds;

        // TODO: change to single-arg Array.Clear(arr) on unity 6.8 CoreCLR
        Array.Clear(_current, 0, _current.Length);

        Diagnoser.Start();

        // gather synaptic current from last step
        // walks ONLY outbound rows of neurons that spiked, in contrast to every (pre, post) pair for 2D arrays
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

        var spike     = Neurons.Spike;
        var threshold = Neurons.Threshold;

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

            // fire if current potential exceeds threshold
            var spiked = potential[i] >= threshold[i];

            spike[i] = spiked;

            if (!spiked) continue;

            potential[i]           = resetPotential[i];
            _spikes[_spikeCount++] = i;
        }
    }
}