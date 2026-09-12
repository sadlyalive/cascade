namespace Brain;

/// <summary>
/// <para>Object-oriented wrapper for a single neuron inside <see cref="NeuronPool"/>.</para>
/// <para>Stores only two fields (reference to the correspondent <see cref="NeuronPool"/> and the neuron's _index), so this struct is extremely cheap. Properties and methods simply read and write to the <see cref="NeuronPool"/>'s SoA.</para>
/// <para>Can become stale if preserved across steps since network modifications could introduce _index mismatches.</para>
/// <para>Properties allow reads only because setters invoke CS1612. Use old-school setter methods for writes.</para>
/// </summary>
public readonly struct Neuron(NeuronPool pool, int index)
{
    private readonly NeuronPool _pool = pool;
    private readonly int _index = index;

    public float Potential => _pool.Potential[_index];
    public float Threshold => _pool.Threshold[_index];
    public float RestPotential => _pool.RestPotential[_index];
    public float ResetPotential => _pool.ResetPotential[_index];
    public float LeakRate => _pool.LeakRate[_index];
    public float InputCurrent => _pool.InputCurrent[_index];
    public bool Spike => _pool.Spike[_index];

    internal void SetPotential(float v) => _pool.Potential[_index] = v;
    public void SetThreshold(float v) => _pool.Threshold[_index] = v;
    public void SetRestPotential(float v) => _pool.RestPotential[_index] = v;
    public void SetResetPotential(float v) => _pool.ResetPotential[_index] = v;
    public void SetLeakRate(float v) => _pool.LeakRate[_index] = v;
    public void SetInputCurrent(float v) => _pool.InputCurrent[_index] = v;
    public void SetSpike(bool v) => _pool.Spike[_index] = v;
}
