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
    internal NeuronPool(int capacity)
    {
        Reallocate(Math.Max(capacity, 1));
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

    /// <summary>Create a transient struct of the neuron at <paramref name="i" /> for object-oriented wrapping.</summary>
    public Neuron this[int i] => new(this, i);

    /// <summary>Appends a neuron with default parameters and returns its index.</summary>
    internal int Add()
    {
        if (Count >= Capacity)
            Reallocate(Math.Max(Capacity + (Capacity >> 1), Capacity + 1));

        var i = Count++;

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

        return i;
    }

    /// <summary>Remove every neuron in O(1) time with a simple counter reset. Arrays remain allocated.</summary>
    internal void Clear()
    {
        Count = 0;
    }

    /// <summary>
    ///     <para>Removes the neuron at <paramref name="i" /> by moving the last neuron into the removing slot.</para>
    ///     <para>
    ///         Much better than shifting since shifted indices would become stale in other places (most notably the
    ///         connectome). The caller must still tidy the connectome for the former last neuron's new index (see
    ///         <see cref="Network" />).
    ///     </para>
    /// </summary>
    /// <returns>The old index of the neuron that moved into <paramref name="i" />, or -1 if <paramref name="i" /> was last.</returns>
    internal int RemoveAt(int i)
    {
        // single-comparison range check for 0 <= i < Count (see Network.RemoveNeuron)
        if ((uint)i >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(i), i, $"No neuron at index {i} (count {Count})");

        var last = Count - 1;

        Count--;

        if (i == last)
            return -1;

        _inputCurrent[i] = _inputCurrent[last];

        _potential[i]      = _potential[last];
        _restPotential[i]  = _restPotential[last];
        _resetPotential[i] = _resetPotential[last];
        _leakRate[i]       = _leakRate[last];

        _lastSpike[i] = _lastSpike[last];
        _threshold[i] = _threshold[last];

        _x[i] = _x[last];
        _y[i] = _y[last];
        _z[i] = _z[last];

        return last;
    }

    private void Reallocate(int size)
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