namespace UTAU.Notes;

internal sealed class TempoMap
{
    readonly IReadOnlyList<UTAUNote> notes;
    readonly TimeBase baseTimeBase;
    readonly int[] startTicks;
    readonly double[] startMilliseconds;
    readonly double[] lengthMilliseconds;
    readonly double[] millisecondsPerTick;

    TempoMap(
        IReadOnlyList<UTAUNote> notes,
        TimeBase baseTimeBase,
        int[] startTicks,
        double[] startMilliseconds,
        double[] lengthMilliseconds,
        double[] millisecondsPerTick,
        int totalTicks,
        double totalMilliseconds)
    {
        this.notes = notes;
        this.baseTimeBase = baseTimeBase;
        this.startTicks = startTicks;
        this.startMilliseconds = startMilliseconds;
        this.lengthMilliseconds = lengthMilliseconds;
        this.millisecondsPerTick = millisecondsPerTick;
        TotalTicks = totalTicks;
        TotalMilliseconds = totalMilliseconds;
    }

    public static TempoMap Create(IReadOnlyList<UTAUNote> notes, TimeBase baseTimeBase)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var count = notes.Count;
        var startTicks = new int[count];
        var startMilliseconds = new double[count];
        var lengthMilliseconds = new double[count];
        var millisecondsPerTick = new double[count];

        var ticks = 0;
        var milliseconds = 0.0;
        var tempo = baseTimeBase.Tempo;

        for (var index = 0; index < count; index++)
        {
            var note = notes[index];
            if (note.TempoOverride > UTAUNote.FollowScoreValue)
                tempo = note.TempoOverride;

            var timeBase = new TimeBase(tempo, baseTimeBase.Speed);
            startTicks[index] = ticks;
            startMilliseconds[index] = milliseconds;
            lengthMilliseconds[index] = timeBase.ToMilliseconds(note.LengthTicks);
            millisecondsPerTick[index] = timeBase.MillisecondsPerTick;

            milliseconds += lengthMilliseconds[index];
            ticks += note.LengthTicks;
        }

        return new TempoMap(
            notes,
            baseTimeBase,
            startTicks,
            startMilliseconds,
            lengthMilliseconds,
            millisecondsPerTick,
            ticks,
            milliseconds);
    }

    public IReadOnlyList<UTAUNote> Notes => notes;

    public int Count => notes.Count;

    public int TotalTicks { get; }

    public double TotalMilliseconds { get; }

    public double MinimumMillisecondsPerTick
        => millisecondsPerTick.Length == 0 ? baseTimeBase.MillisecondsPerTick : millisecondsPerTick.Min();

    public double StartMilliseconds(int noteIndex)
        => Count == 0 ? 0.0 : startMilliseconds[Math.Clamp(noteIndex, 0, Count - 1)];

    public double LengthMilliseconds(int noteIndex)
        => Count == 0 ? 0.0 : lengthMilliseconds[Math.Clamp(noteIndex, 0, Count - 1)];

    public double ToMilliseconds(double ticks)
    {
        if (Count == 0)
            return ticks * baseTimeBase.MillisecondsPerTick;

        var index = SegmentOfTicks(ticks);
        return startMilliseconds[index] + (ticks - startTicks[index]) * millisecondsPerTick[index];
    }

    public double ToTicks(double milliseconds)
    {
        if (Count == 0)
            return baseTimeBase.MillisecondsPerTick <= 0.0 ? 0.0 : milliseconds / baseTimeBase.MillisecondsPerTick;

        var index = SegmentOfMilliseconds(milliseconds);
        var rate = millisecondsPerTick[index];
        return rate <= 0.0 ? startTicks[index] : startTicks[index] + (milliseconds - startMilliseconds[index]) / rate;
    }

    int SegmentOfTicks(double ticks)
    {
        var low = 0;
        var high = Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (startTicks[middle] <= ticks)
                low = middle;
            else
                high = middle - 1;
        }
        return low;
    }

    int SegmentOfMilliseconds(double milliseconds)
    {
        var low = 0;
        var high = Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (startMilliseconds[middle] <= milliseconds)
                low = middle;
            else
                high = middle - 1;
        }
        return low;
    }
}
