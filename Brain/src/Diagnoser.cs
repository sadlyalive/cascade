using System;
using System.Diagnostics;

namespace Brain;

public class Diagnoser(TimeSpan interval)
{
    private readonly Stopwatch _stopwatch = new();
    private          TimeSpan  _dt;
    private          ulong     _trials;

    // EventHandler<TSender, TEventArgs> only exists in .NET 10, so netstandard2.1 (unity's build) can't see it
    public event Action<Diagnoser, DiagnoserReport>? IntervalElapsed;

    public void Start()
    {
        _stopwatch.Start();
    }

    public void Stop(uint trials = 0)
    {
        _dt     += _stopwatch.Elapsed;
        _trials += trials;
        _stopwatch.Reset();

        if (_dt < interval || _trials < 1)
            return;

        IntervalElapsed?.Invoke(this, new DiagnoserReport(_dt, _trials));

        _dt     = TimeSpan.Zero;
        _trials = 0;
    }
}

public readonly record struct DiagnoserReport(TimeSpan DeltaTime, ulong Trials)
{
    public TimeSpan TimePerTrial => DeltaTime / Trials;
}