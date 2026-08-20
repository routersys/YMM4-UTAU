using System.Text;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;

namespace UTAU.Tests;

public sealed class PhonemeEquivalenceTests
{
    static readonly string[] Vowels = ["-", "", "a", "i", "u", "e", "o", "n"];

    static IEnumerable<string> Lyrics()
    {
        foreach (var kana in ReferenceKana.Keys())
        {
            yield return kana;
            yield return ReferenceKana.ToKatakana(kana);
            yield return ReferenceKana.Table()[kana];
            foreach (var vowel in Vowels)
            {
                if (vowel.Length == 0)
                    continue;
                yield return vowel + " " + kana;
            }
        }

        foreach (var romaji in ReferenceKana.AliasKeys())
            yield return romaji;

        yield return "";
        yield return " ";
        yield return "   ";
        yield return "\t";
        yield return "　";
        yield return "a　か";
        yield return "a  か";
        yield return "あ ";
        yield return " あ";
        yield return "- あ";
        yield return "* か";
        yield return "R";
        yield return "-";
        yield return "漢字";
        yield return "a k";
        yield return "ん";
        yield return "っ";
        yield return "ー";
        yield return "が".Normalize(NormalizationForm.FormD);
        yield return ("あ" + "が".Normalize(NormalizationForm.FormD));
        yield return "a " + "ぱ".Normalize(NormalizationForm.FormD);
        yield return new string('ア', 200);
        yield return new string('ア', 200) + "か";
        yield return "a " + new string('カ', 150);
        yield return "あ" + (char)0xD800 + "か";
    }

    [Fact]
    public void CandidateListsMatchThePreSpanImplementation()
    {
        var mismatches = new List<string>();
        var cases = 0;

        foreach (var lyric in Lyrics())
        {
            if (lyric.Trim().Length == 0)
                continue;

            foreach (var previousVowel in Vowels)
            {
                cases++;
                var expected = ReferenceAliasResolver.EnumerateCandidates(lyric, previousVowel).ToArray();
                var actual = AliasResolver.EnumerateCandidates(lyric, previousVowel).ToArray();

                if (expected.SequenceEqual(actual, StringComparer.Ordinal))
                    continue;

                mismatches.Add($"[{lyric}|{previousVowel}] expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
            }
        }

        Assert.True(cases > 3000, $"cases={cases}");
        Assert.True(mismatches.Count == 0, $"count={mismatches.Count} " + string.Join(" ;; ", mismatches));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("　")]
    public void BlankLyricsProduceNoCandidates(string lyric)
    {
        foreach (var previousVowel in Vowels)
            Assert.Empty(AliasResolver.EnumerateCandidates(lyric, previousVowel));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("　")]
    [InlineData("   ")]
    public void ABlankLyricNoLongerBorrowsAnUnrelatedAlias(string lyric)
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=blank\r\n");
            TestVoiceBank.WriteText(directory, VoiceBankLoader.OtoFileName, "silence.wav=-,50,80,-500,100,40");
            TestVoiceBank.WriteSample(directory, "silence.wav");
            var bank = VoiceBankLoader.Load("blank", directory);

            var borrowed = ReferenceAliasResolver
                .EnumerateCandidates(lyric, KanaRomanization.StartVowel)
                .Any(x => bank.Resolve(x, 60, null) is not null);
            Assert.True(borrowed);

            var notes = new[] { new UTAUNote { Lyric = lyric, LengthTicks = 480, Tone = 60 } };
            var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);

            Assert.False(notes[0].IsRest);
            Assert.True(units[0].IsUnresolved);
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }

    [Fact]
    public void MoraDecompositionMatchesThePreSpanImplementation()
    {
        var mismatches = new List<string>();

        foreach (var lyric in Lyrics())
        {
            if (ReferenceKana.ToMora(lyric) != KanaRomanization.ToMora(lyric))
                mismatches.Add($"mora [{lyric}]");
            if (ReferenceKana.GetVowel(lyric) != KanaRomanization.GetVowel(lyric))
                mismatches.Add($"vowel [{lyric}]");
            if (ReferenceKana.GetConsonant(lyric) != KanaRomanization.GetConsonant(lyric))
                mismatches.Add($"consonant [{lyric}]");
            if (ReferenceKana.ToHiragana(lyric) != KanaRomanization.ToHiragana(lyric))
                mismatches.Add($"hiragana [{lyric}]");
            if (ReferenceKana.ToKatakana(lyric) != KanaRomanization.ToKatakana(lyric))
                mismatches.Add($"katakana [{lyric}]");

            var referenceFound = ReferenceKana.TryGetRomaji(lyric, out var referenceRomaji);
            var found = KanaRomanization.TryGetRomaji(lyric, out var romaji);
            if (referenceFound != found || (found && referenceRomaji != romaji))
                mismatches.Add($"romaji [{lyric}]");
        }

        Assert.Empty(mismatches);
    }

    [Fact]
    public void SpanAndStringEntryPointsAgree()
    {
        var mismatches = new List<string>();

        Span<char> buffer = stackalloc char[KanaRomanization.StackTextLength];
        foreach (var lyric in Lyrics())
        {
            var spanMora = KanaRomanization.ToMora(lyric.AsSpan(), buffer).ToString();
            if (spanMora != KanaRomanization.ToMora(lyric))
                mismatches.Add($"mora [{lyric}]");

            var spanVowel = KanaRomanization.GetVowel(lyric.AsSpan(), buffer);
            var stringVowel = KanaRomanization.GetVowel(lyric);
            if ((stringVowel is null) != spanVowel.IsEmpty || (stringVowel is not null && !spanVowel.SequenceEqual(stringVowel)))
                mismatches.Add($"vowel [{lyric}]");

            var spanConsonant = KanaRomanization.GetConsonant(lyric.AsSpan(), buffer);
            var stringConsonant = KanaRomanization.GetConsonant(lyric);
            if ((stringConsonant is null) != spanConsonant.IsEmpty || (stringConsonant is not null && !spanConsonant.SequenceEqual(stringConsonant)))
                mismatches.Add($"consonant [{lyric}]");
        }

        Assert.Empty(mismatches);
    }
}
