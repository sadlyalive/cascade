using System;
using System.Diagnostics;

namespace Brain;

public class Diagnoser(int trials)
{
    private readonly Stopwatch _stopwatch = new();

    private int  _trial;
    private long _dt;
    public  int  TrialsPerDiagnosis { get; } = trials;

    public event EventHandler<decimal> OnFinish;

    public void Start()
    {
        _stopwatch.Start();
    }

    public void Stop()
    {
        _dt += _stopwatch.ElapsedTicks;
        _stopwatch.Reset();

        if (++_trial < TrialsPerDiagnosis)
            return;

        OnFinish?.Invoke(this, (decimal)_dt * 1000 / Stopwatch.Frequency);
        _trial = 0;
        _dt    = 0;
    }
}