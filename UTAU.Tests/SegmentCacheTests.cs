using System.Reflection;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;
using UTAU.Synthesis;

namespace UTAU.Tests;

public sealed class SegmentCacheTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();
    readonly AnalysisCache analysis = new(AnalysisCache.DefaultBudgetBytes);
    readonly SegmentCache segments = new(SegmentCache.DefaultBudgetBytes);

    const double SampleMilliseconds = 300.0;

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    VoiceBank Bank()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=cache\r\n");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            string.Join("\r\n",
            [
                "a.wav=あ,50,80,-500,100,40",
                "ka.wav=か,50,120,-500,140,50",
                "sa.wav=さ,50,120,-500,140,50",
            ]));
        foreach (var name in new[] { "a.wav", "ka.wav", "sa.wav" })
            TestVoiceBank.WriteSample(directory, name, TestVoiceBank.CreateVowel(durationMilliseconds: SampleMilliseconds));
        return VoiceBankLoader.Load("cache", directory);
    }

    static UTAUNote[] Score() =>
    [
        new() { Lyric = "あ", LengthTicks = 240, Tone = 60 },
        new() { Lyric = "か", LengthTicks = 240, Tone = 62 },
        new() { Lyric = "さ", LengthTicks = 240, Tone = 64 },
    ];

    static UTAUNote[] SplitScore() =>
    [
        new() { Lyric = "あ", LengthTicks = 240, Tone = 60 },
        new() { Lyric = UTAUNote.RestLyric, LengthTicks = 240, Tone = 60 },
        new() { Lyric = "か", LengthTicks = 240, Tone = 62 },
    ];

    double[] Render(VoiceBank bank, UTAUNote[] notes, RenderSettings? settings = null)
    {
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);
        var renderer = new UtauRenderer(
            settings ?? RenderSettings.Default with { Estimator = F0Estimator.Dio },
            RenderCurves.Empty,
            analysis,
            segments);
        return renderer.Render(units).Samples;
    }

    [Fact]
    public void RenderingTwiceReusesEverySegment()
    {
        var bank = Bank();
        var notes = Score();

        var first = Render(bank, notes);
        var stored = segments.Count;
        var second = Render(bank, notes);

        Assert.True(stored > 0);
        Assert.Equal(stored, segments.Count);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ACachedRenderMatchesAnUncachedOne()
    {
        var bank = Bank();
        var notes = Score();

        var cached = Render(bank, notes);
        var fresh = new UtauRenderer(
            RenderSettings.Default with { Estimator = F0Estimator.Dio },
            RenderCurves.Empty,
            analysis,
            new SegmentCache(SegmentCache.DefaultBudgetBytes));
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);

        Assert.Equal(fresh.Render(units).Samples, cached);
    }

    static UTAUNote[] LongScore(int phrases)
    {
        var notes = new List<UTAUNote>();
        for (var phrase = 0; phrase < phrases; phrase++)
        {
            if (phrase > 0)
                notes.Add(new UTAUNote { Lyric = UTAUNote.RestLyric, LengthTicks = 240, Tone = 60 });
            notes.Add(new UTAUNote { Lyric = "あ", LengthTicks = 240, Tone = 60 });
            notes.Add(new UTAUNote { Lyric = "か", LengthTicks = 240, Tone = 62 });
        }
        return [.. notes];
    }

    [Fact]
    public void ShiftingTheWholeScoreReusesEveryLaterSegment()
    {
        var bank = Bank();
        var notes = LongScore(8);

        Render(bank, notes);
        var before = segments.Count;

        notes[0].PreutteranceOverride = 200.0;
        Render(bank, notes);

        Assert.Equal(before + 1, segments.Count);
    }

    [Fact]
    public void AnEditThatMovesLaterNotesRebuildsFewerThanEverySegment()
    {
        var bank = Bank();
        var notes = LongScore(8);

        Render(bank, notes);
        var before = segments.Count;

        notes[0].LengthTicks = 480;
        Render(bank, notes);

        Assert.True(segments.Count - before < before, $"rebuilt={segments.Count - before} of {before}");
    }

    [Fact]
    public void EditingOneNoteLeavesTheOtherSegmentCached()
    {
        var bank = Bank();
        var notes = SplitScore();

        Render(bank, notes);
        var before = segments.Count;

        notes[2].Tone = 65;
        Render(bank, notes);

        Assert.Equal(before + 1, segments.Count);
    }

    public static TheoryData<string> MutableProperties() =>
    [
        nameof(UTAUNote.Lyric),
        nameof(UTAUNote.Tone),
        nameof(UTAUNote.LengthTicks),
        nameof(UTAUNote.Velocity),
        nameof(UTAUNote.Intensity),
        nameof(UTAUNote.Modulation),
        nameof(UTAUNote.StartPointMilliseconds),
        nameof(UTAUNote.PreutteranceOverride),
        nameof(UTAUNote.OverlapOverride),
        nameof(UTAUNote.FadeInMilliseconds),
        nameof(UTAUNote.FadeOutMilliseconds),
        nameof(UTAUNote.Vibrato),
        nameof(UTAUNote.PitchPoints),
        nameof(UTAUNote.IgnorePrefixMap),
        nameof(UTAUNote.SuppressAutoVcv),
    ];

    static void Mutate(UTAUNote note, string property)
    {
        switch (property)
        {
            case nameof(UTAUNote.Lyric): note.Lyric = "さ"; break;
            case nameof(UTAUNote.Tone): note.Tone = 67; break;
            case nameof(UTAUNote.LengthTicks): note.LengthTicks = 360; break;
            case nameof(UTAUNote.Velocity): note.Velocity = 150.0; break;
            case nameof(UTAUNote.Intensity): note.Intensity = 50.0; break;
            case nameof(UTAUNote.Modulation): note.Modulation = 80.0; break;
            case nameof(UTAUNote.StartPointMilliseconds): note.StartPointMilliseconds = 20.0; break;
            case nameof(UTAUNote.PreutteranceOverride): note.PreutteranceOverride = 90.0; break;
            case nameof(UTAUNote.OverlapOverride): note.OverlapOverride = 60.0; break;
            case nameof(UTAUNote.FadeInMilliseconds): note.FadeInMilliseconds = 70.0; break;
            case nameof(UTAUNote.FadeOutMilliseconds): note.FadeOutMilliseconds = 80.0; break;
            case nameof(UTAUNote.Vibrato):
                note.Vibrato.LengthPercent = 80.0;
                note.Vibrato.DepthCents = 120.0;
                break;
            case nameof(UTAUNote.PitchPoints):
                note.PitchPoints.Add(new PitchPoint(0, -400.0));
                note.PitchPoints.Add(new PitchPoint(120, 400.0));
                break;
            case nameof(UTAUNote.IgnorePrefixMap): note.IgnorePrefixMap = true; break;
            case nameof(UTAUNote.SuppressAutoVcv): note.SuppressAutoVcv = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(property), property, null);
        }
    }

    double[] RenderFresh(VoiceBank bank, UTAUNote[] notes, RenderSettings? settings = null)
    {
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);
        var renderer = new UtauRenderer(
            settings ?? RenderSettings.Default with { Estimator = F0Estimator.Dio },
            RenderCurves.Empty,
            analysis,
            new SegmentCache(SegmentCache.DefaultBudgetBytes));
        return renderer.Render(units).Samples;
    }

    [Theory]
    [MemberData(nameof(MutableProperties))]
    public void TheCacheAgreesWithAFreshRenderAfterEveryEdit(string property)
    {
        var bank = Bank();
        var notes = Score();

        Render(bank, notes);
        Mutate(notes[1], property);

        var cached = Render(bank, notes);
        var fresh = RenderFresh(bank, notes);

        Assert.Equal(fresh, cached);
    }

    [Theory]
    [MemberData(nameof(MutableProperties))]
    public void TheCacheAgreesWithAFreshRenderAfterEditingEveryNote(string property)
    {
        var bank = Bank();
        var notes = Score();

        Render(bank, notes);
        foreach (var note in notes)
            Mutate(note, property);

        Assert.Equal(RenderFresh(bank, notes), Render(bank, notes));
    }

    [Theory]
    [InlineData(nameof(RenderSettings.Volume))]
    [InlineData(nameof(RenderSettings.FormantSemitones))]
    [InlineData(nameof(RenderSettings.Breathiness))]
    [InlineData(nameof(RenderSettings.Brightness))]
    [InlineData(nameof(RenderSettings.StretchMode))]
    [InlineData(nameof(RenderSettings.Estimator))]
    public void ChangingARenderSettingChangesTheRenderedAudio(string property)
    {
        var bank = Bank();
        var notes = Score();
        var baseline = RenderSettings.Default with { Estimator = F0Estimator.Dio };
        var changed = property switch
        {
            nameof(RenderSettings.Volume) => baseline with { Volume = 40.0 },
            nameof(RenderSettings.FormantSemitones) => baseline with { FormantSemitones = 6.0 },
            nameof(RenderSettings.Breathiness) => baseline with { Breathiness = 60.0 },
            nameof(RenderSettings.Brightness) => baseline with { Brightness = 6.0 },
            nameof(RenderSettings.StretchMode) => baseline with { StretchMode = StretchMode.Stretch },
            _ => baseline with { Estimator = F0Estimator.Harvest },
        };

        var before = Render(bank, notes, baseline);
        var after = Render(bank, notes, changed);

        Assert.False(before.AsSpan().SequenceEqual(after), property);
        Assert.Equal(RenderFresh(bank, notes, changed), after);
    }

    [Theory]
    [InlineData(nameof(UTAUNote.Tone))]
    [InlineData(nameof(UTAUNote.Intensity))]
    [InlineData(nameof(UTAUNote.LengthTicks))]
    [InlineData(nameof(UTAUNote.PitchPoints))]
    [InlineData(nameof(UTAUNote.Vibrato))]
    public void AnEditThatReachesTheAudioIsHeard(string property)
    {
        var bank = Bank();
        var notes = Score();

        var before = Render(bank, notes);
        Mutate(notes[1], property);
        var after = Render(bank, notes);

        Assert.False(before.AsSpan().SequenceEqual(after), property);
    }

    [Fact]
    public void EveryNotePropertyThatReachesSynthesisIsExercised()
    {
        var covered = MutableProperties().Select(x => x.Data).ToHashSet(StringComparer.Ordinal);
        string[] derived =
        [
            nameof(UTAUNote.IsRest),
            nameof(UTAUNote.MusicalTone),
            nameof(UTAUNote.TempoOverride),
            "HasErrors",
        ];

        var declared = typeof(UTAUNote)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(declared.Except(covered).Except(derived));
    }
}
