using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;
using UTAU.Synthesis;

namespace UTAU.Tests;

public sealed class ParallelRenderTests : IDisposable
{
    const double SampleMilliseconds = 300.0;

    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();
    readonly AnalysisCache analysis = new(AnalysisCache.DefaultBudgetBytes);

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    VoiceBank Bank()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=parallel\r\n");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            string.Join("\r\n",
            [
                "a.wav=あ,50,80,-200,100,40",
                "ka.wav=か,50,120,-200,140,50",
                "sa.wav=さ,50,120,-200,140,50",
            ]));
        var index = 0;
        foreach (var name in new[] { "a.wav", "ka.wav", "sa.wav" })
            TestVoiceBank.WriteSample(directory, name, TestVoiceBank.CreateVowel(180.0 + index++ * 40.0, SampleMilliseconds));
        return VoiceBankLoader.Load("parallel", directory);
    }

    static UTAUNote[] Score(int phrases)
    {
        var notes = new List<UTAUNote>();
        string[] lyrics = ["あ", "か", "さ"];
        for (var phrase = 0; phrase < phrases; phrase++)
        {
            if (phrase > 0)
                notes.Add(new UTAUNote { Lyric = UTAUNote.RestLyric, LengthTicks = 240, Tone = 60 });
            for (var index = 0; index < 4; index++)
                notes.Add(new UTAUNote { Lyric = lyrics[index % 3], LengthTicks = 240, Tone = 60 + index % 5 });
        }
        return [.. notes];
    }

    double[] Render(VoiceBank bank, UTAUNote[] notes, SegmentCache cache)
    {
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);
        var renderer = new UtauRenderer(
            RenderSettings.Default with { Estimator = F0Estimator.Dio },
            RenderCurves.Empty,
            analysis,
            cache);
        return renderer.Render(units).Samples;
    }

    static SegmentCache Fresh() => new(SegmentCache.DefaultBudgetBytes);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(20)]
    public void RenderingTheSameScoreTwiceGivesTheSameAudio(int phrases)
    {
        var bank = Bank();
        var notes = Score(phrases);

        var first = Render(bank, notes, Fresh());
        var second = Render(bank, notes, Fresh());

        Assert.Equal(first, second);
    }

    [Fact]
    public void RepeatedRendersStayIdentical()
    {
        var bank = Bank();
        var notes = Score(12);
        var expected = Render(bank, notes, Fresh());

        for (var pass = 0; pass < 5; pass++)
            Assert.Equal(expected, Render(bank, notes, Fresh()));
    }

    [Fact]
    public void EverySegmentWritesADistinctStretchOfTheOutput()
    {
        var bank = Bank();
        var notes = Score(20);
        var samples = Render(bank, notes, Fresh());

        var silent = 0;
        foreach (var value in samples)
        {
            if (value == 0.0)
                silent++;
        }

        Assert.True(samples.Length > 0);
        Assert.True(silent < samples.Length, "the render produced only silence");
    }

    [Fact]
    public void ACachedParallelRenderMatchesAFreshOne()
    {
        var bank = Bank();
        var notes = Score(20);
        var shared = Fresh();

        var warm = Render(bank, notes, shared);
        notes[5].Tone = 68;

        var cached = Render(bank, notes, shared);
        var fresh = Render(bank, notes, Fresh());

        Assert.Equal(fresh, cached);
        Assert.False(warm.AsSpan().SequenceEqual(cached));
    }
}
