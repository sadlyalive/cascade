using System;
using Brain.Data.Collection;
using Brain.Data.Collection.CSR;

namespace Brain;

public sealed class Network
{
    /// <summary>
    ///     <para>Per-step buffer that caches total synaptic current arriving at each postsynaptic neuron.</para>
    ///     <para>Created only occasionally; would bloat heap and GC if created per step.</para>
    /// </summary>
    private readonly float[] _current;

    public Network(int count)
    {
        Neurons = new NeuronPool(count);

        Connectome = new Connectome<float>(count);
        _current   = new float[count];

        // TODO: make customizable. currently biologically-inspired defaults
        foreach (var n in Neurons)
        {
            n.RestPotential  = -70f; // mV
            n.Potential      = -70f; // start at rest
            n.Threshold      = -55f; // ~15 mV of headroom to fire
            n.ResetPotential = -75f; // slight undershoot after a spike
            n.LeakRate       = 0.1f; // 1/ms
        }
    }

    /// <summary>
    ///     <para>Two-way compressed sparse row matrix for synaptic weights.</para>
    ///     <para>
    ///         Exchanges some algorithmic complexity (and some SIMD and eager caching) for massively less spatial
    ///         complexity in comparison to conventional 2D arrays. <i>Drosophila melanogaster</i>'s brain contains almost 400
    ///         times more synapses than soma on average, yet CSR still saves over 95% memory since their synaptic density is
    ///         still nowhere near <c>soma²</c>.
    ///     </para>
    /// </summary>
    public Connectome<float> Connectome { get; }

    /// <summary>SoA for every neuron.</summary>
    public NeuronPool Neurons { get; }

    public int Count => Neurons.Count;

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
        // walks ONLY outbound rows for spiking neurons in contrast to every  (pre, post) pair for 2D arrays
        // TODO: n.Spike still holds previous results which means dt bottlenecks propagation speed
        for (var pre = 0; pre < Count; pre++)
        {
            if (!Neurons.Spike[pre])
                continue;

            // avoid foreach enumerator since it slightly hurts performance
            var outbound = Connectome.From(pre);

            for (var i = 0; i < outbound.Count; i++)
                _current[outbound.Columns[i]] += outbound.Values[i];
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