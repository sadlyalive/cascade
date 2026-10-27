using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace Brain.Benchmark;

/// <summary>
///     <para>
///         Measures how fast <see cref="Network.Step" /> runs on one thread. Every step propagates then integrates, which
///         this reports separately for more accurate generalizations.
///     </para>
///     <para>
///         Times integration on completely dormant networks, and times propagation relative to its respective dormant
///         network.
///     </para>
/// </summary>
internal static class Program
{
    private const string Green  = "\e[38;2;128;255;128m";
    private const string Gray   = "\e[38;2;128;128;128m";
    private const string Orange = "\e[38;2;255;165;0m";
    private const string Reset  = "\e[0m";

    private const string Usage = """
                                 usage: Benchmark [--neurons N] [--synapses N] [--rate HZ] [--seconds S] [--trials N] [--cpu INDEX] [--seed N]

                                   --neurons, --synapses, --rate    run a custom scenario instead of the default suite
                                   --seconds                        timed stepping per trial (default 1)
                                   --trials                         trials per scenario, of which the median is reported (default 5)
                                   --cpu                            pin to one logical core, Windows and Linux only (default unpinned)
                                   --seed                           wiring seed (default 1)
                                 """;

    // pure integration at three sizes, then one 5M-synapse network at three firing rates, then roughly the network that
    // started lagging in Electrode
    private static readonly Scenario[] Suite =
    [
        new(10_000, 0, 0), // variable integration loads
        new(100_000, 0, 0),
        new(1_000_000, 0, 0),
        new(100_000, 5_000_000, 1), // variable spike rates
        new(100_000, 5_000_000, 10),
        new(100_000, 5_000_000, 100),
        new(150_000, 500_000, 10)
    ];

    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(.5);

    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        double  seconds  = 1;
        int     trials   = 5,    seed    = 1;
        int?    cpu      = null, neurons = null;
        long?   synapses = null;
        double? rate     = null;

        try
        {
            for (var i = 0; i < args.Length; i += 2)
            {
                if (args[i] is "-h" or "--help")
                {
                    Console.WriteLine(Usage);
                    return 0;
                }

                if (i + 1 >= args.Length)
                    throw new ArgumentException($"{args[i]} needs a value");

                var value = args[i + 1];

                switch (args[i])
                {
                    case "--neurons":  neurons  = int.Parse(value); break;
                    case "--synapses": synapses = long.Parse(value); break;
                    case "--rate":     rate     = double.Parse(value); break;
                    case "--seconds":  seconds  = double.Parse(value); break;
                    case "--trials":   trials   = int.Parse(value); break;
                    case "--cpu":      cpu      = int.Parse(value); break;
                    case "--seed":     seed     = int.Parse(value); break;
                    default:           throw new ArgumentException($"unknown option {args[i]}");
                }
            }

            if (cpu is { } index)
                Machine.Pin(index);
        }
        catch (Exception e) when (e is ArgumentException
                                    or FormatException
                                    or OverflowException
                                    or PlatformNotSupportedException)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(Usage);
            return 1;
        }

        try
        {
            Machine.RaisePriority();
        }
        catch (Exception)
        {
            Warn("failed to increase thread priority, expect less accuracy (may require an elevated terminal)");
        }

        var requested = neurons is null && synapses is null && rate is null
                            ? Suite
                            : [new Scenario(neurons ?? 100_000, synapses ?? 0, rate ?? 0)];

        // every propagation result is measured against a baseline of the same size, so run those first
        var scenarios = requested
                        .Select(s => new Scenario(s.Neurons, 0, 0))
                        .Distinct()
                        .Concat(requested.Where(s => !s.IsBaseline))
                        .ToArray();

        PrintHeader(seconds, trials, seed, cpu);

        var baselineMs = new Dictionary<int, double>();

        foreach (var scenario in scenarios)
        {
            var (result, spread, actualSynapses) = Run(scenario, seed, seconds, trials);

            if (scenario.IsBaseline)
                baselineMs[scenario.Neurons] = result.MsPerStep;

            PrintRow(scenario, result, spread, actualSynapses, baselineMs[scenario.Neurons]);
        }

        Console.WriteLine(Green);
        Console.WriteLine("vs. real time    simulated ms per wall-clock ms, i.e., the fastest TimeScale that keeps up");
        Console.WriteLine("M updates/s      neural integrations per second (spikeless baselines)");
        Console.WriteLine("M events/s       spikes per synapse per second (time beyond baselines)");
        Console.WriteLine($"spread          ± half the range between the fastest and slowest trial{Reset}");

        return 0;
    }

    private static (Measurement median, double spread, long synapses) Run(
        Scenario scenario,
        int      seed,
        double   seconds,
        int      trials)
    {
        var network = scenario.Build(seed);

        var  fanOut   = new int[network.Count];
        long synapses = 0;

        for (var i = 0; i < fanOut.Length; i++) synapses += fanOut[i] = network.Connectome.From(i).Count;

        // accelerate collection for garbage from wiring to prevent the collector from affecting an actual trial
        GC.Collect();

        // the JIT first compiles Step quickly and unoptimized, then recompiles it once it proves hot (tiered
        // compilation). warming up keeps that initial slower version out of the results and fills the caches too
        Scenario.Measure(network, fanOut, Warmup);

        var results = new Measurement[trials];

        for (var t = 0; t < trials; t++)
            results[t] = Scenario.Measure(network, fanOut, TimeSpan.FromSeconds(seconds));

        // median ignores a single trial that a background process or a clock change happened to hit
        Array.Sort(results, (a, b) => a.MsPerStep.CompareTo(b.MsPerStep));

        var median = results[trials / 2];
        var spread = (results[^1].MsPerStep - results[0].MsPerStep) / 2 / median.MsPerStep;

        return (median, spread, synapses);
    }

    private static void PrintHeader(double seconds, int trials, int seed, int? cpu)
    {
        Console.WriteLine(
            $"{Green}Brain benchmark: one thread, 1 ms steps, median of {trials} x {seconds} s trials, seed {seed}{Gray}"
        );
        Console.WriteLine($"CPU       {Machine.CpuName()}{(cpu is { } i ? $" (pinned to logical CPU {i})" : "")}");
        Console.WriteLine($"Runtime   {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"OS        {RuntimeInformation.OSDescription}{Reset}");
#if DEBUG
        Warn(
            """
            !! debug build detected !!
            the JIT compiler may optimize less
            rebuild the program with the `-c Release` flag
            """
        );
#endif
        Console.WriteLine();
        Console.WriteLine(
            $"{Green}  neurons     synapses    rate(Hz)   events/step" +
            $"    ms/step  vs. real time   M updates/s   M events/s     spread{Reset}"
        );
        Console.Write(Reset);
    }

    private static void PrintRow(Scenario scenario, Measurement result, double spread, long synapses, double baselineMs)
    {
        // measured rather than requested, so a scenario that accidentally fires more than asked shows it
        var rateHz = result.SpikesPerStep / scenario.Neurons * 1000;

        // ms/step → per second is × 1000, and per second → millions per second is ÷ 1e6
        var updates = scenario.IsBaseline ? $"{scenario.Neurons / result.MsPerStep / 1000,11:F1}" : $"{"-",11}";

        var extraMs = result.MsPerStep - baselineMs;
        var events = result.EventsPerStep > 0 && extraMs > 0
                         ? $"{result.EventsPerStep / extraMs / 1000,10:F1}"
                         : $"{"-",10}";

        Console.WriteLine(
            $"{scenario.Neurons,9:N0}   {synapses,10:N0}    {rateHz,8:F1}   {result.EventsPerStep,11:N0}  " +
            $"{result.MsPerStep,9:F4}    {1 / result.MsPerStep,10:F1}x   {updates}   {events}    {spread * 100,6:±0.0}%"
        );
    }

    private static void Warn(string message)
    {
        PrintLine($"{Orange}{message}");
    }

    private static void PrintLine(string message)
    {
        Console.WriteLine($"{message}{Reset}");
    }
}