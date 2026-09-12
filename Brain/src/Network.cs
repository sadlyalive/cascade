using System;

namespace Brain;

public sealed class Network
{
    /// <summary>SoA for every neuron.</summary>
    public NeuronPool Neurons { get; }

    /// <summary>
    /// <para><c>count x count</c> synaptic weight matrix flattened into row-major array for friendlier for CPU caching (and eventually SIMD).</para>
    /// <para><c><paramref name="Weight"/>[post * Count + pre]</c> is how strongly <c>pre</c> drives <c>post</c>.</para>
    /// </summary>
    public readonly float[] Weight;

    /// <summary>
    /// <para>Per-step buffer that caches total synaptic current arriving at each postsynaptic neuron.</para>
    /// <para>Created only occasionally; would bloat heap and GC if created per step.</para>
    /// </summary>
    private readonly float[] _current;

    public int Count => Neurons.Count;

    public Network(int count)
    {
        Neurons = new NeuronPool(count);
        Weight = new float[count * count];
        _current = new float[count];

        // TODO: make customizable. currently biologically-inspired defaults
        for (int i = 0; i < count; i++)
        {
            Neurons.RestPotential[i] = -70f; // mV
            Neurons.Potential[i] = -70f; // start at rest
            Neurons.Threshold[i] = -55f; // ~15 mV of headroom to fire
            Neurons.ResetPotential[i] = -75f; // slight undershoot after a spike
            Neurons.LeakRate[i] = 0.1f; // 1/ms
        }
    }

    /// <summary>
    /// Connect neuron <paramref name="pre"/> to <paramref name="post"/> with strength <c>weight</c>.
    /// </summary>
    public void Connect(int pre, int post, float weight)
        => Weight[post * Count + pre] = weight;

    /// <summary>
    /// <para>Randomly wire the network.</para>
    /// <para>Every ordered pair <c>(pre, post)</c> such that <c>pre != post</c> has a <paramref name="connectChance"/>% chance of receiving a uniformly random weight along <c>[<paramref name="minWeight"/>, <paramref name="maxWeight"/>)</c>.</para>
    /// </summary>
    public void WireRandom(float connectChance, float minWeight, float maxWeight, int? seed = null)
    {
        // newer Random.Shared/rng.NextSingle() do not exist in .NET-Standard-2.1 which unity Mono/IL2CPP runtime uses,
        // so library needs to stick to the older usings instead
        Random rng = seed is null ? new Random() : new Random(seed.Value);

        for (int post = 0; post < Count; post++)
        {
            int row = post * Count;

            for (int pre = 0; pre < Count; pre++)
            {
                if (pre == post)
                    continue;

                if ((float)rng.NextDouble() < connectChance)
                    Weight[row + pre] = minWeight + (float)rng.NextDouble() * (maxWeight - minWeight);
            }
        }
    }

    /// <summary>
    /// Advance the simulation by <paramref name="dt"/> milliseconds.
    /// </summary>
    public void Step(float dt)
    {
        NeuronPool n = Neurons;
        int count = Count;

        // single-arg Array.Clear(arr) is also post 2.1 only
        Array.Clear(_current, 0, _current.Length);

        // gather synaptic current from last step. n.Spike still holds previous results which means dt bottlenecks
        // propagation speed (TODO?)
        for (int target = 0; target < count; target++)
        {
            int row = target * count;
            float sum = 0f;

            for (int source = 0; source < count; source++)
            {
                if (n.Spike[source])
                    sum += Weight[row + source];
            }

            _current[target] = sum;

            // TODO: introduce tensor primitives (inner loop is a dot product between the spike vector and one row of
            // the weight matrix, which is the hottest line in the entire library)
        }

        // integrate, leak, and check voltage threshold
        for (int i = 0; i < count; i++)
        {
            // asymptotically leak towards rest
            float leak = n.LeakRate[i] * (n.RestPotential[i] - n.Potential[i]);

            // integrate all current flowing into this neuron this step
            float current = n.InputCurrent[i] + _current[i];

            // forward Euler integration (value += rate * dt)
            n.Potential[i] += (leak + current) * dt;

            // fire if current potential exceeds threshold
            bool spiked = n.Potential[i] >= n.Threshold[i];
            n.Spike[i] = spiked;

            if (spiked)
                n.Potential[i] = n.ResetPotential[i];
        }
    }
}
