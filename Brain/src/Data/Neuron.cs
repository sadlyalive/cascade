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
public readonly struct Neuron : IEquatable<Neuron>
{
    private readonly int        _id;
    private readonly NeuronPool _pool;

    internal Neuron(NeuronPool pool, int id)
    {
        _pool = pool;
        _id   = id;
    }

    public ref float Potential      => ref _pool.Potential[_id];
    public ref float RestPotential  => ref _pool.RestPotential[_id];
    public ref float ResetPotential => ref _pool.ResetPotential[_id];
    public ref float LeakRate       => ref _pool.LeakRate[_id];

    public ref float InputCurrent => ref _pool.InputCurrent[_id];

    public ref double LastSpike => ref _pool.LastSpike[_id];
    public ref float  Threshold => ref _pool.Threshold[_id];

    public ref float X => ref _pool.X[_id];
    public ref float Y => ref _pool.Y[_id];
    public ref float Z => ref _pool.Z[_id];

    public void MoveTo(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static implicit operator int(Neuron n)
    {
        return n._id;
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
        return _id == other._id;
    }

    public override bool Equals(object? obj)
    {
        return obj is Neuron other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _id;
    }
}