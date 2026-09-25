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
///         so every mutable property returns <c>ref float</c> instead. <c>pool[i].Potential = x</c> automatically
///         dereferences and assigns <c>pool.Potential[i]</c> to <c>x</c>.
///     </para>
/// </summary>
public readonly struct Neuron(NeuronPool pool, int i)
{
    public ref float Potential      => ref pool.Potential[i];
    public ref float Threshold      => ref pool.Threshold[i];
    public ref float RestPotential  => ref pool.RestPotential[i];
    public ref float ResetPotential => ref pool.ResetPotential[i];
    public ref float LeakRate       => ref pool.LeakRate[i];
    public ref float InputCurrent   => ref pool.InputCurrent[i];
    public ref bool  Spike          => ref pool.Spike[i];

    public ref float X => ref pool.X[i];
    public ref float Y => ref pool.Y[i];
    public ref float Z => ref pool.Z[i];

    public void MoveTo(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}