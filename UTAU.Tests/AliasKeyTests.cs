using System.Diagnostics;
using System.Text;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;

namespace UTAU.Tests;

public sealed class AliasKeyTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    [Theory]
    [InlineData("a か", "a か")]
    [InlineData("あ", "あ")]
    [InlineData("a k", "a k")]
    [InlineData("", "")]
    [InlineData("* か", "* か")]
    [InlineData("a　か", "a か")]
    [InlineData("a  か", "a か")]
    [InlineData("a\tか", "a か")]
    [InlineData(" あ ", "あ")]
    public void KeysCollapseSpacingWithoutTouchingTheRegularForms(string written, string expected)
        => Assert.Equal(expected, VoiceBank.NormalizeKey(written));

    [Fact]
    public void KeysComposeDecomposedKana()
    {
        var decomposed = "が".Normalize(NormalizationForm.FormD);

        Assert.NotEqual("が", decomposed);
        Assert.Equal("が", VoiceBank.NormalizeKey(decomposed));
    }

    [Fact]
    public void AlreadyNormalKeysAreReturnedUnchanged()
    {
        var alias = "a か";

        Assert.Same(alias, VoiceBank.NormalizeKey(alias));
    }

    [Fact]
    public void LoneSurrogatesDoNotThrow()
    {
        var broken = "あ" + (char)0xD800 + "か";

        Assert.Equal(broken, VoiceBank.NormalizeKey(broken));
    }

    [Fact]
    public void DecomposedBankAliasesMatchComposedLyrics()
    {
        var decomposed = "が".Normalize(NormalizationForm.FormD);
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=nfd\r\n");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            "ga.wav=" + decomposed + ",50,80,-500,100,40",
            new UTF8Encoding(false));
        TestVoiceBank.WriteSample(directory, "ga.wav");
        var bank = VoiceBankLoader.Load("nfd", directory);

        Assert.True(bank.Contains("が"));
        Assert.True(bank.Contains(decomposed));
        Assert.Equal(decomposed, bank.Find("が")?.Alias);
    }

    [Fact]
    public void FullWidthSpacingMatchesOnBothSides()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=spacing\r\n");
        TestVoiceBank.WriteText(directory, VoiceBankLoader.OtoFileName, "aka.wav=a　か,50,120,-500,140,50");
        TestVoiceBank.WriteSample(directory, "aka.wav");
        var bank = VoiceBankLoader.Load("spacing", directory);

        Assert.True(bank.Contains("a か"));
        Assert.True(bank.Contains("a　か"));
        Assert.True(bank.Contains("a  か"));
    }

    [Fact]
    public void TypedLyricsResolveDespiteIrregularSpacing()
    {
        var bank = TestVoiceBank.CreateVcvBank(directory);
        var notes = new[]
        {
            new UTAUNote { Lyric = "a　か", LengthTicks = TimeBase.TicksPerQuarterNote, Tone = 60 },
        };

        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);

        Assert.False(units[0].IsUnresolved);
        Assert.Equal("a か", units[0].Entry?.Alias);
    }

    [Fact]
    public void PhonemizingALongScoreStaysFast()
    {
        var bank = TestVoiceBank.CreateVcvAndCvvcBank(directory);
        var notes = new UTAUNote[5000];
        for (var index = 0; index < notes.Length; index++)
            notes[index] = new UTAUNote { Lyric = index % 2 == 0 ? "あ" : "か", LengthTicks = 240, Tone = 60 };
        var tempoMap = TempoMap.Create(notes, TimeBase.Default);

        Phonemizer.Phonemize(bank, tempoMap, null, PhonemizeOptions.Default);

        var watch = Stopwatch.StartNew();
        for (var pass = 0; pass < 5; pass++)
            Phonemizer.Phonemize(bank, tempoMap, null, PhonemizeOptions.Default);
        watch.Stop();

        var perPass = watch.Elapsed.TotalMilliseconds / 5.0;
        Assert.True(perPass < 200.0, $"perPass={perPass:F1}ms");
    }
}
