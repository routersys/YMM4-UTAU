using UTAU.Notes;

namespace UTAU.Tests;

public sealed class TempoMapTests
{
    static UTAUNote Note(int lengthTicks = 480, double tempoOverride = UTAUNote.FollowScoreValue)
        => new() { Lyric = "あ", LengthTicks = lengthTicks, TempoOverride = tempoOverride };

    static TempoMap Create(TimeBase timeBase, params UTAUNote[] notes) => TempoMap.Create(notes, timeBase);

    [Fact]
    public void AScoreWithoutOverridesMatchesThePlainTimeBase()
    {
        var timeBase = new TimeBase(144.0, 1.0);
        var map = Create(timeBase, Note(), Note(240), Note(120));

        Assert.Equal(840, map.TotalTicks);
        Assert.Equal(timeBase.ToMilliseconds(840), map.TotalMilliseconds, 9);
        for (var ticks = 0; ticks <= 840; ticks += 30)
            Assert.Equal(timeBase.ToMilliseconds(ticks), map.ToMilliseconds(ticks), 9);
    }

    [Fact]
    public void AnOverrideChangesTheLengthFromThatNoteOnward()
    {
        var map = Create(new TimeBase(120.0, 1.0), Note(), Note(480, 240.0), Note());

        Assert.Equal(500.0, map.LengthMilliseconds(0), 6);
        Assert.Equal(250.0, map.LengthMilliseconds(1), 6);
        Assert.Equal(250.0, map.LengthMilliseconds(2), 6);
        Assert.Equal(1000.0, map.TotalMilliseconds, 6);
    }

    [Fact]
    public void EveryNoteStartsWhereThePreviousOneEnds()
    {
        var map = Create(new TimeBase(120.0, 1.0), Note(), Note(240, 90.0), Note(120), Note(480, 200.0));

        var expected = 0.0;
        for (var index = 0; index < map.Count; index++)
        {
            Assert.Equal(expected, map.StartMilliseconds(index), 6);
            expected += map.LengthMilliseconds(index);
        }
        Assert.Equal(expected, map.TotalMilliseconds, 6);
    }

    [Fact]
    public void ZeroMeansTheNoteInheritsTheCurrentTempo()
    {
        var map = Create(new TimeBase(120.0, 1.0), Note(480, 240.0), Note(), Note(480, UTAUNote.FollowScoreValue));

        Assert.Equal(250.0, map.LengthMilliseconds(0), 6);
        Assert.Equal(250.0, map.LengthMilliseconds(1), 6);
        Assert.Equal(250.0, map.LengthMilliseconds(2), 6);
    }

    [Fact]
    public void TheFirstNoteCanOverrideTheScoreTempo()
    {
        var map = Create(new TimeBase(120.0, 1.0), Note(480, 60.0), Note());

        Assert.Equal(1000.0, map.LengthMilliseconds(0), 6);
        Assert.Equal(1000.0, map.LengthMilliseconds(1), 6);
    }

    [Fact]
    public void TheMapIsMonotonic()
    {
        var map = Create(new TimeBase(120.0, 1.0), Note(), Note(240, 400.0), Note(120, 20.0), Note());

        var previous = double.NegativeInfinity;
        for (var ticks = -480; ticks <= map.TotalTicks + 480; ticks += 15)
        {
            var milliseconds = map.ToMilliseconds(ticks);
            Assert.True(milliseconds > previous, $"ticks={ticks}");
            previous = milliseconds;
        }
    }

    [Fact]
    public void TicksAndMillisecondsRoundTrip()
    {
        var map = Create(new TimeBase(133.0, 1.0), Note(), Note(240, 90.0), Note(360, 210.0), Note(120));

        for (var ticks = 0; ticks <= map.TotalTicks; ticks += 7)
            Assert.Equal(ticks, map.ToTicks(map.ToMilliseconds(ticks)), 6);
    }

    [Fact]
    public void PositionsOutsideTheScoreExtrapolate()
    {
        var map = Create(new TimeBase(120.0, 1.0), Note(480, 240.0));

        Assert.Equal(-250.0, map.ToMilliseconds(-480), 6);
        Assert.Equal(500.0, map.ToMilliseconds(960), 6);
    }

    [Fact]
    public void AnEmptyScoreFallsBackToTheBaseTimeBase()
    {
        var timeBase = new TimeBase(150.0, 1.0);
        var map = TempoMap.Create([], timeBase);

        Assert.Equal(0, map.Count);
        Assert.Equal(0, map.TotalTicks);
        Assert.Equal(0.0, map.TotalMilliseconds, 9);
        Assert.Equal(timeBase.ToMilliseconds(480), map.ToMilliseconds(480), 9);
        Assert.Equal(480.0, map.ToTicks(timeBase.ToMilliseconds(480)), 6);
        Assert.Equal(0.0, map.LengthMilliseconds(0), 9);
    }

    [Fact]
    public void SpeedScalesEveryTempo()
    {
        var single = Create(new TimeBase(120.0, 1.0), Note(), Note(480, 240.0));
        var doubled = Create(new TimeBase(120.0, 2.0), Note(), Note(480, 240.0));

        Assert.Equal(single.TotalMilliseconds / 2.0, doubled.TotalMilliseconds, 6);
    }

    [Fact]
    public void TheSmallestStepComesFromTheFastestTempo()
    {
        var map = Create(new TimeBase(120.0, 1.0), Note(), Note(480, 240.0), Note(480, 60.0));

        Assert.Equal(new TimeBase(240.0, 1.0).MillisecondsPerTick, map.MinimumMillisecondsPerTick, 12);
    }

    [Fact]
    public void OutOfRangeOverridesAreClampedByTheNote()
    {
        Assert.Equal(UTAUNote.FollowScoreValue, Note(480, 0.0).TempoOverride, 9);
        Assert.Equal(UTAUNote.FollowScoreValue, Note(480, -30.0).TempoOverride, 9);
        Assert.Equal(TimeBase.MinimumTempo, Note(480, 5.0).TempoOverride, 9);
        Assert.Equal(TimeBase.MaximumTempo, Note(480, 5000.0).TempoOverride, 9);
    }

    [Fact]
    public void CloningKeepsTheOverride()
    {
        var note = Note(480, 175.0);

        Assert.Equal(175.0, note.Clone().TempoOverride, 9);
    }
}

public sealed class ExpressionCurveResamplerTests
{
    static UTAUNote Note(int lengthTicks = 480, double tempoOverride = UTAUNote.FollowScoreValue)
        => new() { Lyric = "あ", LengthTicks = lengthTicks, TempoOverride = tempoOverride };

    static ExpressionCurve Ramp(int count)
    {
        var values = new double[count];
        for (var index = 0; index < count; index++)
            values[index] = index;
        return new ExpressionCurve { Values = values };
    }

    [Fact]
    public void AConstantTempoReproducesTheOriginalSamples()
    {
        var timeBase = new TimeBase(120.0, 1.0);
        var map = TempoMap.Create([Note(), Note()], timeBase);
        var curve = Ramp(map.TotalTicks / ExpressionCurve.IntervalTicks + 1);

        var resampled = ExpressionCurveResampler.Resample(curve, new ExpressionCurve(), map);

        Assert.Equal(timeBase.ToMilliseconds(ExpressionCurve.IntervalTicks), resampled.IntervalMilliseconds, 9);
        for (var index = 0; index < curve.Values.Length; index++)
            Assert.Equal(curve.Values[index], resampled.Formant[index], 6);
    }

    [Fact]
    public void TheCurveStaysAlignedWithTheScoreAcrossATempoChange()
    {
        var map = TempoMap.Create([Note(), Note(480, 240.0)], new TimeBase(120.0, 1.0));
        var curve = Ramp(map.TotalTicks / ExpressionCurve.IntervalTicks + 1);

        var resampled = ExpressionCurveResampler.Resample(curve, new ExpressionCurve(), map);

        for (var ticks = 0; ticks <= map.TotalTicks; ticks += ExpressionCurve.IntervalTicks)
        {
            var position = map.ToMilliseconds(ticks) / resampled.IntervalMilliseconds;
            var index = (int)Math.Round(position);
            Assert.Equal(position, index, 6);
            Assert.Equal(curve.Evaluate(ticks), resampled.Formant[index], 6);
        }
    }

    [Fact]
    public void TheSampleGridCoversTheWholeScore()
    {
        var map = TempoMap.Create([Note(), Note(480, 60.0)], new TimeBase(120.0, 1.0));
        var resampled = ExpressionCurveResampler.Resample(Ramp(4), new ExpressionCurve(), map);

        Assert.True((resampled.Formant.Length - 1) * resampled.IntervalMilliseconds >= map.TotalMilliseconds);
    }

    [Fact]
    public void EmptyCurvesStayEmpty()
    {
        var map = TempoMap.Create([Note()], new TimeBase(120.0, 1.0));
        var resampled = ExpressionCurveResampler.Resample(new ExpressionCurve(), new ExpressionCurve(), map);

        Assert.Empty(resampled.Formant);
        Assert.Empty(resampled.Breathiness);
        Assert.True(resampled.IntervalMilliseconds > 0.0);
    }
}
