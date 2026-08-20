using System.Text;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;

namespace UTAU.Tests;

public sealed class AliasNormalizerTests : IDisposable
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
        => Assert.Equal(expected, AliasNormalizer.Normalize(written));

    [Fact]
    public void KeysComposeDecomposedKana()
    {
        var decomposed = "が".Normalize(NormalizationForm.FormD);

        Assert.NotEqual("が", decomposed);
        Assert.Equal("が", AliasNormalizer.Normalize(decomposed));
    }

    [Fact]
    public void AlreadyNormalKeysAreReturnedUnchanged()
    {
        var alias = "a か";

        Assert.Same(alias, AliasNormalizer.Normalize(alias));
    }

    [Fact]
    public void LoneSurrogatesDoNotThrow()
    {
        var broken = "あ" + (char)0xD800 + "か";

        Assert.Equal(broken, AliasNormalizer.Normalize(broken));
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
    public void IrregularLyricsStillDecomposeIntoMoras()
    {
        var decomposed = "が".Normalize(NormalizationForm.FormD);

        Assert.Equal("が", KanaRomanization.ToMora(decomposed));
        Assert.Equal("a", KanaRomanization.GetVowel(decomposed));
        Assert.Equal("g", KanaRomanization.GetConsonant(decomposed));

        Assert.Equal("か", KanaRomanization.ToMora("a　か"));
        Assert.Equal("a", KanaRomanization.GetVowel("a　か"));
        Assert.Equal("a", KanaRomanization.GetVowel("a  か"));

        Assert.Equal("あ", KanaRomanization.ToMora("あ "));
        Assert.Equal("a", KanaRomanization.GetVowel("あ "));
    }

    [Fact]
    public void IrregularLyricsKeepTheContinuousContext()
    {
        var decomposed = "が".Normalize(NormalizationForm.FormD);
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=nfd\r\n");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            string.Join("\r\n",
            [
                "start.wav=- が,50,80,-500,100,40",
                "aka.wav=a か,50,120,-500,140,50",
            ]),
            new UTF8Encoding(false));
        TestVoiceBank.WriteSample(directory, "start.wav");
        TestVoiceBank.WriteSample(directory, "aka.wav");
        var bank = VoiceBankLoader.Load("nfd", directory);

        var notes = new[]
        {
            new UTAUNote { Lyric = decomposed, LengthTicks = 480, Tone = 60 },
            new UTAUNote { Lyric = "か", LengthTicks = 480, Tone = 60 },
        };
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);

        Assert.Equal(["- が", "a か"], units.Select(x => x.Alias));
    }

    [Fact]
    public void IrregularSpacingDoesNotEarnAnExtraTransition()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=spacing\r\n");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            string.Join("\r\n",
            [
                "a.wav=あ,50,80,-500,100,40",
                "aka.wav=a か,50,120,-500,140,50",
                "ak.wav=a k,50,40,-200,60,20",
            ]));
        foreach (var name in new[] { "a.wav", "aka.wav", "ak.wav" })
            TestVoiceBank.WriteSample(directory, name);
        var bank = VoiceBankLoader.Load("spacing", directory);

        var notes = new[]
        {
            new UTAUNote { Lyric = "あ", LengthTicks = 480, Tone = 60 },
            new UTAUNote { Lyric = "a　か", LengthTicks = 480, Tone = 60 },
        };
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);

        Assert.Equal(["あ", "a か"], units.Select(x => x.Alias));
    }
}
