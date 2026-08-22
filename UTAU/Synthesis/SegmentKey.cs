using System.Collections.Immutable;
using UTAU.Notes;
using UTAU.Phonemes;

namespace UTAU.Synthesis;

internal readonly record struct VibratoKey(
    double LengthPercent,
    double PeriodMilliseconds,
    double DepthCents,
    double FadeInPercent,
    double FadeOutPercent,
    double PhasePercent,
    double OffsetPercent)
{
    public static VibratoKey From(VibratoSettings vibrato) => new(
        vibrato.LengthPercent,
        vibrato.PeriodMilliseconds,
        vibrato.DepthCents,
        vibrato.FadeInPercent,
        vibrato.FadeOutPercent,
        vibrato.PhasePercent,
        vibrato.OffsetPercent);
}

internal readonly record struct SourceKey(
    string Path,
    long WriteTimeTicks,
    int StartSample,
    int EndSample,
    double RegionStart,
    double RegionEnd,
    double ConsonantEnd);

internal readonly record struct UnitKey(
    SourceKey? Source,
    double AudioStartMilliseconds,
    double RenderLengthMilliseconds,
    double FadeInMilliseconds,
    double FadeOutMilliseconds,
    double SkipMilliseconds,
    double NoteStartMilliseconds,
    double NoteLengthMilliseconds,
    int Tone,
    int LengthTicks,
    double Velocity,
    double Intensity,
    double Modulation,
    VibratoKey Vibrato,
    ImmutableArray<PitchPoint> PitchPoints)
{
    public bool Equals(UnitKey other)
        => Source == other.Source
            && AudioStartMilliseconds.Equals(other.AudioStartMilliseconds)
            && RenderLengthMilliseconds.Equals(other.RenderLengthMilliseconds)
            && FadeInMilliseconds.Equals(other.FadeInMilliseconds)
            && FadeOutMilliseconds.Equals(other.FadeOutMilliseconds)
            && SkipMilliseconds.Equals(other.SkipMilliseconds)
            && NoteStartMilliseconds.Equals(other.NoteStartMilliseconds)
            && NoteLengthMilliseconds.Equals(other.NoteLengthMilliseconds)
            && Tone == other.Tone
            && LengthTicks == other.LengthTicks
            && Velocity.Equals(other.Velocity)
            && Intensity.Equals(other.Intensity)
            && Modulation.Equals(other.Modulation)
            && Vibrato == other.Vibrato
            && PitchPoints.AsSpan().SequenceEqual(other.PitchPoints.AsSpan());

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Source);
        hash.Add(AudioStartMilliseconds);
        hash.Add(RenderLengthMilliseconds);
        hash.Add(FadeInMilliseconds);
        hash.Add(FadeOutMilliseconds);
        hash.Add(SkipMilliseconds);
        hash.Add(NoteStartMilliseconds);
        hash.Add(NoteLengthMilliseconds);
        hash.Add(Tone);
        hash.Add(LengthTicks);
        hash.Add(Velocity);
        hash.Add(Intensity);
        hash.Add(Modulation);
        hash.Add(Vibrato);
        foreach (var point in PitchPoints)
            hash.Add(point);
        return hash.ToHashCode();
    }
}

internal sealed class SegmentKey : IEquatable<SegmentKey>
{
    readonly UnitKey[] units;
    readonly int hash;

    public SegmentKey(
        RenderSettings settings,
        int frameCount,
        int sampleRate,
        double framePeriod,
        UnitKey[] units)
    {
        Settings = settings;
        FrameCount = frameCount;
        SampleRate = sampleRate;
        FramePeriod = framePeriod;
        this.units = units;

        var code = new HashCode();
        code.Add(settings);
        code.Add(frameCount);
        code.Add(sampleRate);
        code.Add(framePeriod);
        foreach (var unit in units)
            code.Add(unit);
        hash = code.ToHashCode();
    }

    public RenderSettings Settings { get; }

    public int FrameCount { get; }

    public int SampleRate { get; }

    public double FramePeriod { get; }

    public bool Equals(SegmentKey? other)
        => other is not null
            && hash == other.hash
            && Settings == other.Settings
            && FrameCount == other.FrameCount
            && SampleRate == other.SampleRate
            && FramePeriod.Equals(other.FramePeriod)
            && units.AsSpan().SequenceEqual(other.units.AsSpan());

    public override bool Equals(object? obj) => Equals(obj as SegmentKey);

    public override int GetHashCode() => hash;
}
