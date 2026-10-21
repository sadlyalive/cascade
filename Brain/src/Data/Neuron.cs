using System;
using Brain.Data.Collection;

namespace Brain.Data;

/// <summary>
///     <para>Object-oriented wrapper for a single neuron inside <see cref="NeuronPool" />.</para>
///     <para>
///         Stores only two fields (reference to the correspondent <see cref="NeuronPool" /> and the neuron's index), so
///         this struct is extremely cheap. Properties simply read and write to the <see cref="NeuronPool" />'s SoA.
///     </para>
///     <para>Can become stale if preserved across steps since network modifications could introduce index mismatches.</para>
///     <para>
///         CS1612 prohibits writing to a transient struct's fields even if the field is a property with a custom setter,
///         so every mutable property returns <c>ref float</c> instead. <c>pool[i]._potential = x</c> automatically
///         dereferences and assigns <c>pool._potential[i]</c> to <c>x</c>.
///     </para>
/// </summary>
public readonly struct Neuron(NeuronPool pool, int i) : IEquatable<Neuron>
{
    public int Id { get; } = i;

    public ref float Potential      => ref pool.Potential[Id];
    public ref float RestPotential  => ref pool.RestPotential[Id];
    public ref float ResetPotential => ref pool.ResetPotential[Id];
    public ref float LeakRate       => ref pool.LeakRate[Id];

    public ref float InputCurrent => ref pool.InputCurrent[Id];

    public ref double LastSpike => ref pool.LastSpike[Id];
    public ref float  Threshold => ref pool.Threshold[Id];

    public ref float X => ref pool.X[Id];
    public ref float Y => ref pool.Y[Id];
    public ref float Z => ref pool.Z[Id];

    public void MoveTo(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static implicit operator int(Neuron n)
    {
        return n.Id;
    }

    public static bool operator ==(Neuron l, Neuron r)
    {
        return l.Equals(r);
    }

    public static bool operator !=(Neuron l, Neuron r)
    {
        return !l.Equals(r);
    }

    public bool Equals(Neuron other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is Neuron other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }
}