using System;
using System.Diagnostics;

namespace Brain.Benchmark;

/// <summary>
///     A reproducible network shape. The same scenario and seed always wire the same network, so two machines running
///     it are measuring identical work.
/// </summary>
/// <param name="Neurons">Neurons integrated every step</param>
/// <param name="Synapses">Synapses spread evenly across neurons, each to a uniformly random target</param>
/// <param name="RateHz">
///     Population firing rate. 10 Hz on 1 ms steps means 1% of neurons spike every step (see <see cref="Build" />)
/// </param>
internal readonly record struct Scenario(int Neurons, long Synapses, double RateHz)
{
    // pushes a neuron from reset (-75 mV) past threshold (-55 mV) within a single 1 ms step, so it spikes every step
    private const float DriveCurrent = 25f;

    // nonzero so every spike still walks its whole row (writing 0 would delete the synapse), but far too small to ever
    // push an undriven neuron to threshold. the measurement counts real spikes either way, so this only keeps the
    // advertised rate honest
    private const float Weight = 1e-4f;

    private static readonly TimeSpan StepSize = TimeSpan.FromMilliseconds(1);

    /// <summary>No synapses and no input, so nothing ever spikes and a step costs pure integration.</summary>
    public bool IsBaseline => Synapses == 0 && RateHz == 0;

    /// <summary>
    ///     <para>Wires the network this scenario describes.</para>
    ///     <para>
    ///         Firing is forced rather than emergent: a random <c>RateHz / 1000</c> fraction of neurons gets enough input
    ///         to spike every step and the rest stay at rest. Real dynamics would make spikes per step depend on weights,
    ///         topology and luck, while this makes it a dial, so cost can be measured against it.
    ///     </para>
    /// </summary>
    public Network Build(int seed)
    {
        var network = new Network(Neurons);

        for (var i = 0; i < Neurons; i++)
            network.AddNeuron();

        // separate generators so the wiring stays identical across rates and only the driven set changes
        var wiring = new Random(seed);
        var drive  = new Random(seed + 1);

        var input    = network.Neurons.InputCurrent;
        var fraction = RateHz / 1000.0;

        for (var i = 0; i < Neurons; i++)
        {
            if (drive.NextDouble() < fraction)
                input[i] = DriveCurrent;
        }

        // presynaptic neurons in ascending order so rows fill front to back, the cheapest order for the CSR to commit.
        // duplicate targets overwrite each other, so the real count can land slightly under Synapses
        for (long s = 0; s < Synapses; s++)
        {
            var pre = (int)(s * Neurons / Synapses);
            network.Connectome[pre, wiring.Next(Neurons)] = Weight;
        }

        network.Connectome.CommitChanges();

        return network;
    }

    /// <summary>
    ///     Steps <paramref name="network" /> until <paramref name="duration" /> elapses in <see cref="Network.Step" />.
    ///     <paramref name="fanOut" /> should count the number of outbound synapses per neuron, i.e., how many events its spike
    ///     would cost.
    /// </summary>
    public static Measurement Measure(Network network, int[] fanOut, TimeSpan duration)
    {
        var  stopwatch = new Stopwatch();
        long steps     = 0, spikes = 0, events = 0;

        // a spike is recorded at the end of one step and delivered at the start of the next, so the first timed step
        // delivers whatever the last untimed one produced
        var (_, pendingEvents) = CountSpikes(network, fanOut);

        // the stopwatch only runs during Step, so counting spikes in between stays off the clock
        while (stopwatch.Elapsed < duration)
        {
            stopwatch.Start();
            network.Step(StepSize);
            stopwatch.Stop();

            steps++;
            events += pendingEvents;

            var (newSpikes, newEvents) = CountSpikes(network, fanOut);

            spikes        += newSpikes;
            pendingEvents =  newEvents;
        }

        return new Measurement(
            stopwatch.Elapsed.TotalMilliseconds / steps,
            (double)spikes / steps,
            (double)events / steps
        );
    }

    private static (long spikes, long events) CountSpikes(Network network, int[] fanOut)
    {
        var lastSpike = network.Neurons.LastSpike;
        var time      = network.Time;

        long spikes = 0, events = 0;

        for (var i = 0; i < lastSpike.Length; i++)
        {
            // safe since Step literally passes Time to LastSpike
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (lastSpike[i] != time)
                continue;

            spikes++;
            events += fanOut[i];
        }

        return (spikes, events);
    }
}

/// <summary>Per-step averages from one timed run.</summary>
/// <param name="EventsPerStep">Synaptic events (a spike propagating to one target) delivered per step</param>
internal readonly record struct Measurement(double MsPerStep, double SpikesPerStep, double EventsPerStep);