namespace Brain;

/// <summary>
/// <para>Storage for every neuron's state, laid out as a structure of arrays. Every array represents a neural property where each index (0 .. <see cref="Count"/> - 1) is a unique neuron.</para>
///
/// Simulation loop addresses one property across all neurons at a time so the CPU streams them efficiently and (TODO) can process 8+ at once with SIMD instructions.
/// With an array of structs those same values would be scattered 20-40 bytes apart.
/// </summary>
public sealed class NeuronPool(int count)
{
    /// <summary>Fixed number of neurons</summary>
    public int Count { get; } = count;

    // --- Per-neuron state. Every array below has length == Count. ---
    // Potentials are in millivolts (mV). Currents are in mV/ms: we've folded the
    // membrane resistance into the weight/current scale to keep the toy simple, so
    // the numbers are biologically flavoured but not calibrated to real units.

    /// <summary>Membrane potentials (mV)</summary>
    public readonly float[] Potential = new float[count];

    /// <summary>Spike thresholds (mV)</summary>
    public readonly float[] Threshold = new float[count];

    /// <summary>Resting potentials that neurons leak toward (mV)</summary>
    public readonly float[] RestPotential = new float[count];

    /// <summary>Hyperpolarized potentials after spikes (mV)</summary>
    public readonly float[] ResetPotential = new float[count];

    /// <summary>Leakage speed towards rest (1/ms i.e., greater = leakier)</summary>
    public readonly float[] LeakRate = new float[count];

    /// <summary>External current injection per step (mV/ms)</summary>
    public readonly float[] InputCurrent = new float[count];

    /// <summary>True on the exact step of a spike. Resets next step.</summary>
    public readonly bool[] Spike = new bool[count];

    /// <summary>Create a transient struct of the neuron at <paramref name="i"/> for object-oriented wrapping.</summary>
    public Neuron this[int i] => new(this, i);
}
