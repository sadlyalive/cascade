using System;

namespace Brain;

public sealed class Network
{
    /// <summary>
    ///     <para>Per-step buffer that caches total synaptic current arriving at each postsynaptic neuron.</para>
    ///     <para>Created only occasionally; would bloat heap and GC if created per step.</para>
    /// </summary>
    private readonly float[] _current;

    /// <summary>
    ///     <para>
    ///         <c>count x count</c> synaptic weight matrix flattened into row-major array for friendlier for CPU caching
    ///         (and eventually SIMD).
    ///     </para>
    ///     <para><c><see cref="_weight" /> [post * Count + pre]</c> is how strongly <c>pre</c> drives <c>post</c>.</para>
    /// </summary>
    private readonly float[] _weight;

    public Network(int count)
    {
        Neurons = new NeuronPool(count);
        _weight = new float[count * count];
        _current = new float[count];

        // TODO: make customizable. currently biologically-inspired defaults
        for (var i = 0; i < count; i++)
        {
            Neurons.RestPotential[i] = -70f; // mV
            Neurons.Potential[i] = -70f; // start at rest
            Neurons.Threshold[i] = -55f; // ~15 mV of headroom to fire
            Neurons.ResetPotential[i] = -75f; // slight undershoot after a spike
            Neurons.LeakRate[i] = 0.1f; // 1/ms
        }
    }

    /// <summary>SoA for every neuron.</summary>
    public NeuronPool Neurons { get; }

    public int Count => Neurons.Count;

    /// <summary>
    ///     Connect neuron <paramref name="pre" /> to <paramref name="post" /> with strength <c>weight</c>.
    /// </summary>
    public void Connect(int pre, int post, float weight)
    {
        _weight[post * Count + pre] = weight;
    }

    /// <summary>
    ///     <para>Advance the simulation by <paramref name="elapse" />.</para>
    ///     <para>Accepts a <see cref="TimeSpan" /> for convenience and uses milliseconds internally.</para>
    /// </summary>
    public void Step(TimeSpan elapse)
    {
        var dt = (float)elapse.TotalMilliseconds;

        // TODO: change to single-arg Array.Clear(arr) on unity 6.8 CoreCLR
        Array.Clear(_current, 0, _current.Length);

        // gather synaptic current from last step
        // TODO: n.Spike still holds previous results which means dt bottlenecks propagation speed
        for (var target = 0; target < Count; target++)
        {
            var row = target * Count;
            var sum = 0f;

            for (var source = 0; source < Count; source++)
                if (Neurons.Spike[source])
                    sum += _weight[row + source];

            _current[target] = sum;

            // TODO:
            // introduce tensor primitives (inner loop is a dot product between the spike vector and one row of
            // the weight matrix, which is the hottest line in the entire library)
        }

        // integrate, leak, and check voltage threshold
        for (var i = 0; i < Count; i++)
        {
            // asymptotically leak towards rest
            var leak = Neurons.LeakRate[i] * (Neurons.RestPotential[i] - Neurons.Potential[i]);

            // integrate all current flowing into this neuron this step
            var current = Neurons.InputCurrent[i] + _current[i];

            // forward Euler integration (value += rate * dt)
            Neurons.Potential[i] += (leak + current) * dt;

            // fire if current potential exceeds threshold
            var spiked = Neurons.Potential[i] >= Neurons.Threshold[i];
            Neurons.Spike[i] = spiked;

            if (spiked)
                Neurons.Potential[i] = Neurons.ResetPotential[i];
        }
    }
}