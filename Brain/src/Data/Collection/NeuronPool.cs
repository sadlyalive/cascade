using System;
using System.Collections;
using System.Collections.Generic;

namespace Brain.Data.Collection;

/// <summary>
///     <para>
///         Storage for every neuron's state, laid out as a structure of arrays. Every array represents a neural property
///         where each index (0 ... <see cref="Count" /> - 1) is a unique neuron.
///     </para>
///     Simulation loop addresses one property across all neurons at a time so the CPU streams them efficiently and can
///     process 8+ at once with SIMD instructions.
///     With an array of structs those same values would be scattered 20-40 bytes apart.
/// </summary>
public sealed partial class NeuronPool(int count) : IEnumerable<Neuron>
{
    private const string BoxingAllocationError = "IEnumerable cast forces boxing allocation.";

    /// <summary>External current injection per step (mV/ms)</summary>
    public readonly float[] InputCurrent = new float[count];

    /// <summary>Leakage speed towards rest (1/ms i.e., greater = leakier)</summary>
    public readonly float[] LeakRate = new float[count];

    // neural properties
    // TODO: membrane resistance?

    /// <summary>Membrane potentials (mV)</summary>
    public readonly float[] Potential = new float[count];

    /// <summary>Hyperpolarized potentials after spikes (mV)</summary>
    public readonly float[] ResetPotential = new float[count];

    /// <summary>Resting potentials that neurons leak toward (mV)</summary>
    public readonly float[] RestPotential = new float[count];

    /// <summary>True on the exact step of a spike. Resets next step.</summary>
    public readonly bool[] Spike = new bool[count];

    /// <summary>Spike thresholds (mV)</summary>
    public readonly float[] Threshold = new float[count];

    // soma positions split across axes for SoA and GPU bulk rendering

    /// <summary>Soma X position</summary>
    public readonly float[] X = new float[count];

    /// <summary>Soma Y position</summary>
    public readonly float[] Y = new float[count];

    /// <summary>Soma Z position</summary>
    public readonly float[] Z = new float[count];

    /// <summary>Fixed number of neurons</summary>
    public int Count { get; } = count;

    /// <summary>Create a transient struct of the neuron at <paramref name="i" /> for object-oriented wrapping.</summary>
    public Neuron this[int i] => new(this, i);

    [Obsolete(BoxingAllocationError, true)]
    IEnumerator<Neuron> IEnumerable<Neuron>.GetEnumerator()
    {
        return GetEnumerator();
    }

    [Obsolete(BoxingAllocationError, true)]
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }
}