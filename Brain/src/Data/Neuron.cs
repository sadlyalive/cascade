using System;
using System.Numerics;
using Brain.Data.Collection.Pool;

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

    /// <summary>
    ///     Create a new neuron in <paramref name="network" />.
    /// </summary>
    /// <remarks>
    ///     Actual parameters for this neuron and every other neuron live in a structure of arrays in
    ///     <see cref="Network.Neurons" />. This constructor simply wraps the neuron at the new index returned by
    ///     <see cref="Network.Append" />.
    /// </remarks>
    public Neuron(Network network) : this(network.Neurons, network.Append())
    {
    }

    internal Neuron(NeuronPool pool, int id)
    {
        _pool = pool;
        _id   = id;
    }

    /// <summary>
    ///     Current membrane potential (mV)
    /// </summary>
    public ref float Potential => ref _pool.Potential[_id];

    /// <summary>
    ///     The potential to which the neuron leaks towards (mV)
    /// </summary>
    public ref float RestPotential => ref _pool.RestPotential[_id];

    /// <summary>
    ///     The potential that the neuron hyperpolarizes to after a spike (mV)
    /// </summary>
    public ref float ResetPotential => ref _pool.ResetPotential[_id];

    /// <summary>
    ///     The speed at which the neuron's potential restabilizes (1/ms)
    /// </summary>
    public ref float LeakRate => ref _pool.LeakRate[_id];

    /// <summary>
    ///     External current injection (mV/ms)
    /// </summary>
    public ref float InputCurrent => ref _pool.InputCurrent[_id];

    /// <summary>
    ///     <see cref="Network.Time" /> at which the neuron's last spike occurred, or negative infinity if yet to spike (ms)
    /// </summary>
    public ref double LastSpike => ref _pool.LastSpike[_id];

    /// <summary>
    ///     Membrane potential at which a spike occurs (mV)
    /// </summary>
    public ref float Threshold => ref _pool.Threshold[_id];

    /// <summary>
    ///     The position of the neuron's soma on the X axis
    /// </summary>
    public ref float X => ref _pool.X[_id];

    /// <summary>
    ///     The position of the neuron's soma on the Y axis
    /// </summary>
    public ref float Y => ref _pool.Y[_id];

    /// <summary>
    ///     The position of the neuron's soma on the Z axis
    /// </summary>
    public ref float Z => ref _pool.Z[_id];

    public Vector3 Position
    {
        get => new(X, Y, Z);
        set => MoveTo(value.X, value.Y, value.Z);
    }

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