namespace UTAU.Notes;

internal readonly record struct TimeBase(double Tempo, double Speed)
{
    public const int TicksPerQuarterNote = 480;
    public const int TicksPerWholeNote = TicksPerQuarterNote * 4;
    public const double DefaultTempo = 120.0;
    public const double MinimumTempo = 20.0;
    public const double MaximumTempo = 400.0;
    public const double MinimumSpeed = 0.2;
    public const double MaximumSpeed = 3.0;

    public static TimeBase Default => new(DefaultTempo, 1.0);

    public double EffectiveTempo => Math.Clamp(Tempo, MinimumTempo, MaximumTempo) * Math.Clamp(Speed, MinimumSpeed, MaximumSpeed);

    public double MillisecondsPerTick => 60000.0 / (EffectiveTempo * TicksPerQuarterNote);

    public double ToMilliseconds(double ticks) => ticks * MillisecondsPerTick;

    public int ToTicks(double milliseconds) => (int)Math.Round(milliseconds / MillisecondsPerTick, MidpointRounding.AwayFromZero);

    public static int FromWholeNotes(double wholeNotes)
        => (int)Math.Round(wholeNotes * TicksPerWholeNote, MidpointRounding.AwayFromZero);
}

internal readonly record struct NoteDivision(int Denominator)
{
    public static NoteDivision Free => new(0);

    public bool IsFree => Denominator <= 0;

    public int Ticks => IsFree ? 1 : Math.Max(TimeBase.TicksPerWholeNote / Denominator, 1);

    public string Name => IsFree ? Texts.SnapFree : $"1/{Denominator}";

    public int Snap(int ticks)
    {
        var step = Ticks;
        return step <= 1 ? ticks : (int)Math.Round(ticks / (double)step, MidpointRounding.AwayFromZero) * step;
    }

    public static IReadOnlyList<NoteDivision> All { get; } =
    [
        new(4),
        new(8),
        new(12),
        new(16),
        new(24),
        new(32),
        Free,
    ];
}
