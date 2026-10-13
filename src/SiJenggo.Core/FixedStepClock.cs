namespace SiJenggo.Core;

/// <summary>
/// Converts variable render frame times into a whole number of fixed simulation ticks
/// and exposes the leftover fraction for render interpolation.
/// </summary>
public sealed class FixedStepClock
{
    public const int TicksPerSecond = 60;
    public const float Step = 1f / TicksPerSecond;

    private double _accumulator;

    /// <summary>Upper bound on catch-up ticks per frame, so a hitch never spirals.</summary>
    public int MaxTicksPerFrame { get; set; } = 5;

    /// <summary>0..1 position between the last two simulated states.</summary>
    public float Alpha => (float)(_accumulator / Step);

    public int Advance(double deltaSeconds)
    {
        _accumulator += Math.Clamp(deltaSeconds, 0, 0.25);
        // Small epsilon: 1/60 s in double must count as one tick of the float step.
        int ticks = (int)((_accumulator + 1e-7) / Step);
        if (ticks > MaxTicksPerFrame)
        {
            ticks = MaxTicksPerFrame;
            _accumulator = 0;
        }
        else
        {
            _accumulator = Math.Max(0, _accumulator - ticks * (double)Step);
        }

        return ticks;
    }

    public void Reset() => _accumulator = 0;
}
