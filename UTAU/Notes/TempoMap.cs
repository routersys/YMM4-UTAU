namespace UTAU.Notes;

internal sealed class TempoMap
{
    readonly TimeBase baseTimeBase;
    readonly int[] startTicks;
    readonly double[] startMilliseconds;
    readonly double[] millisecondsPerTick;
    readonly double[] tempos;

    TempoMap(
        TimeBase baseTimeBase,
        int[] startTicks,
        double[] startMilliseconds,
        double[] millisecondsPerTick,
        double[] tempos,
        int totalTicks,
        double totalMilliseconds)
    {
        this.baseTimeBase = baseTimeBase;
        this.startTicks = startTicks;
        this.startMilliseconds = startMilliseconds;
        this.millisecondsPerTick = millisecondsPerTick;
        this.tempos = tempos;
        TotalTicks = totalTicks;
        TotalMilliseconds = totalMilliseconds;
    }

    public static TempoMap Create(IReadOnlyList<UTAUNote> notes, TimeBase baseTimeBase)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var count = notes.Count;
        var startTicks = new int[count];
        var startMilliseconds = new double[count];
        var millisecondsPerTick = new double[count];
        var tempos = new double[count];

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
            millisecondsPerTick[index] = timeBase.MillisecondsPerTick;
            tempos[index] = tempo;

            milliseconds += timeBase.ToMilliseconds(note.LengthTicks);
            ticks += note.LengthTicks;
        }

        return new TempoMap(baseTimeBase, startTicks, startMilliseconds, millisecondsPerTick, tempos, ticks, milliseconds);
    }

    public int Count => startTicks.Length;

    public int TotalTicks { get; }

    public double TotalMilliseconds { get; }

    public double MinimumMillisecondsPerTick
        => millisecondsPerTick.Length == 0 ? baseTimeBase.MillisecondsPerTick : millisecondsPerTick.Min();

    public TimeBase TimeBaseAt(int noteIndex)
        => Count == 0 ? baseTimeBase : new TimeBase(tempos[Math.Clamp(noteIndex, 0, Count - 1)], baseTimeBase.Speed);

    public double StartMilliseconds(int noteIndex)
        => Count == 0 ? 0.0 : startMilliseconds[Math.Clamp(noteIndex, 0, Count - 1)];

    public double LengthMilliseconds(int noteIndex)
    {
        if (Count == 0)
            return 0.0;

        var index = Math.Clamp(noteIndex, 0, Count - 1);
        var end = index + 1 < Count ? startMilliseconds[index + 1] : TotalMilliseconds;
        return end - startMilliseconds[index];
    }

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
