using System;
using System.Collections.Generic;

namespace Brain.Data.Collection;

/// <summary>Storage for every neuron's state in a structure of arrays.</summary>
public sealed partial class NeuronPool : IEnumerable<Neuron>
{
    private float[] _inputCurrent = [];

    private float[] _potential      = [];
    private float[] _restPotential  = [];
    private float[] _resetPotential = [];
    private float[] _leakRate       = [];

    private double[] _lastSpike = [];
    private float[]  _threshold = [];

    private float[] _x = [];
    private float[] _y = [];
    private float[] _z = [];

    /// <summary>Creates an empty pool with preallocation for <paramref name="capacity" /> neurons.</summary>
    internal NeuronPool(int capacity = 0)
    {
        Reallocate(capacity);
    }

    /// <summary>External current injection per step (mV/ms)</summary>
    public Span<float> InputCurrent => _inputCurrent.AsSpan(0, Count);

    /// <summary>Membrane potentials (mV)</summary>
    public Span<float> Potential => _potential.AsSpan(0, Count);

    /// <summary>Resting potentials that neurons leak toward (mV)</summary>
    public Span<float> RestPotential => _restPotential.AsSpan(0, Count);

    /// <summary>Hyperpolarized potentials after spikes (mV)</summary>
    public Span<float> ResetPotential => _resetPotential.AsSpan(0, Count);

    /// <summary>Leakage speed towards rest (1/ms i.e., greater = leakier)</summary>
    public Span<float> LeakRate => _leakRate.AsSpan(0, Count);

    /// <summary>
    ///     <see cref="Network.Time" /> of each neuron's most recent spike (ms), or negative infinity if it never spiked. Uses
    ///     doubles to mitigate floating-point drift.
    /// </summary>
    public Span<double> LastSpike => _lastSpike.AsSpan(0, Count);

    /// <summary>Spike thresholds (mV)</summary>
    public Span<float> Threshold => _threshold.AsSpan(0, Count);

    // soma positions split across axes for SoA and GPU bulk rendering

    /// <summary>Soma X position</summary>
    public Span<float> X => _x.AsSpan(0, Count);

    /// <summary>Soma Y position</summary>
    public Span<float> Y => _y.AsSpan(0, Count);

    /// <summary>Soma Z position</summary>
    public Span<float> Z => _z.AsSpan(0, Count);

    /// <summary>Current number of neurons</summary>
    public int Count { get; private set; }

    public int Capacity => _potential.Length;

    public Neuron this[int i] => new(this, i);

    public IEnumerable<Neuron> this[Range range]
    {
        get
        {
            var (offset, length) = range.GetOffsetAndLength(Count);

            for (var i = 0; i < length; i++)
                yield return this[offset + i];
        }
    }

    /// <summary>Appends <paramref name="count" /> neurons with default parameters and returns the first neuron's index.</summary>
    internal int Add(int count = 1)
    {
        var first = Count;
        count += first;

        if (count > Capacity)
            Reallocate(Math.Max(Math.Max(count, Capacity + (Capacity >> 1)), Capacity + 1));

        for (var i = Count; i < count; i++)
        {
            // explicitly write to every field since positions might hold stale data from deleted neurons
            _inputCurrent[i] = 0f;

            // TODO: make customizable. currently biologically-inspired defaults
            _potential[i]      = -70f; // mV, start at rest
            _restPotential[i]  = -70f; // mV
            _resetPotential[i] = -75f; // mV, slight undershoot after a spike
            _leakRate[i]       = .1f;  // 1/ms

            _lastSpike[i] = double.NegativeInfinity;
            _threshold[i] = -55f; // mV, ~15 mV of headroom to fire

            _x[i] = 0f;
            _y[i] = 0f;
            _z[i] = 0f;
        }

        Count = count;

        return first;
    }

    /// <summary>Remove every neuron in O(1) time with a simple counter reset.</summary>
    /// <param name="free">Whether to deallocate all SoA arrays</param>
    internal void Clear(bool free = false)
    {
        Count = 0;

        if (free)
            Reallocate();
    }

    /// <summary>
    ///     <para>
    ///         Determine what neuron will supersede <paramref name="i" /> after its removal. Allows consumers to prepare for
    ///         removals. <see cref="Network.RemoveNeuron" /> uses this to patch its connectome before internally executing
    ///         <see cref="RemoveAt" />.
    ///     </para>
    ///     <returns>
    ///         The index of the neuron that <see cref="RemoveAt" /> will move into <paramref name="i" />, or -1 if nothing
    ///         moves
    ///     </returns>
    /// </summary>
    public int SuccessorOf(int i)
    {
        if (!Exists(i))
            throw new ArgumentOutOfRangeException(nameof(i), i, $"No neuron at index {i} (count {Count})");

        var last = Count - 1;

        return i == last ? -1 : last;
    }

    /// <summary>
    ///     <para>
    ///         Remove the neuron at <paramref name="i" /> by replacing it with a successor determined by
    ///         <see cref="SuccessorOf" />.
    ///     </para>
    ///     <para>
    ///         Much better than shifting since only one index becomes stale rather than all indices in <c>[i, Count)</c>.
    ///     </para>
    /// </summary>
    /// <returns>The successor's old index, or -1 if nothing moved.</returns>
    internal void RemoveAt(int i)
    {
        var successor = SuccessorOf(i); // also range checks

        Count--;

        if (successor < 0)
            return;

        _inputCurrent[i] = _inputCurrent[successor];

        _potential[i]      = _potential[successor];
        _restPotential[i]  = _restPotential[successor];
        _resetPotential[i] = _resetPotential[successor];
        _leakRate[i]       = _leakRate[successor];

        _lastSpike[i] = _lastSpike[successor];
        _threshold[i] = _threshold[successor];

        _x[i] = _x[successor];
        _y[i] = _y[successor];
        _z[i] = _z[successor];
    }

    public bool Exists(int i)
    {
        // single-comparison range check for 0 <= i < Count (what .NET BCLs do)
        return (uint)i < (uint)Count;
    }

    private void Reallocate(int size = 0)
    {
        Array.Resize(ref _inputCurrent, size);

        Array.Resize(ref _potential,      size);
        Array.Resize(ref _restPotential,  size);
        Array.Resize(ref _resetPotential, size);
        Array.Resize(ref _leakRate,       size);

        Array.Resize(ref _lastSpike, size);
        Array.Resize(ref _threshold, size);

        Array.Resize(ref _x, size);
        Array.Resize(ref _y, size);
        Array.Resize(ref _z, size);
    }
}