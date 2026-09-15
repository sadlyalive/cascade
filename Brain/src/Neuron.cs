namespace Brain;

/// <summary>
/// <para>Object-oriented wrapper for a single neuron inside <see cref="NeuronPool"/>.</para>
/// <para>Stores only two fields (reference to the correspondent <see cref="NeuronPool"/> and the neuron's _i), so this struct is extremely cheap. Properties simply read and write to the <see cref="NeuronPool"/>'s SoA.</para>
/// <para>Can become stale if preserved across steps since network modifications could introduce index mismatches.</para>
/// <para>CS1612 prohibits writing to a transient struct's fields even if the field is a property with a custom setter, so every mutable property returns <c>ref float</c> instead. <c>pool[i].Potential = x</c> automatically dereferences and assigns <c>pool.Potential[i]</c> to <c>x</c>.</para>
/// </summary>
public readonly struct Neuron(NeuronPool _pool, int _i)
{
    public ref float Potential => ref _pool.Potential[_i];
    public ref float Threshold => ref _pool.Threshold[_i];
    public ref float RestPotential => ref _pool.RestPotential[_i];
    public ref float ResetPotential => ref _pool.ResetPotential[_i];
    public ref float LeakRate => ref _pool.LeakRate[_i];
    public ref float InputCurrent => ref _pool.InputCurrent[_i];
    public ref bool Spike => ref _pool.Spike[_i];

    public ref float X => ref _pool.X[_i];
    public ref float Y => ref _pool.Y[_i];
    public ref float Z => ref _pool.Z[_i];

    public readonly void MoveTo(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
