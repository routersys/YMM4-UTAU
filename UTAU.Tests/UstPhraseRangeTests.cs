using System.IO;
using System.Text;
using UTAU;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;

namespace UTAU.Tests;

public sealed class UstPhraseRangeTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    static readonly string[][] Phrases =
    [
        ["あ", "か", "あ"],
        ["か", "あ"],
        ["あ", "あ", "か", "あ"],
        ["か"],
    ];

    static string Document(bool leadingRest, bool trailingRest)
    {
        var builder = new StringBuilder();
        builder.Append("[#VERSION]\r\nUST Version1.2\r\n[#SETTING]\r\nTempo=120.00\r\n");
        var index = 0;

        void Append(string lyric, int length)
        {
            builder.Append($"[#{index:D4}]\r\n");
            builder.Append($"Length={length}\r\nLyric={lyric}\r\nNoteNum=60\r\n");
            index++;
        }

        if (leadingRest)
            Append("R", 480);

        for (var phrase = 0; phrase < Phrases.Length; phrase++)
        {
            if (phrase > 0)
                Append("R", 240);
            foreach (var lyric in Phrases[phrase])
                Append(lyric, 480);
        }

        if (trailingRest)
            Append("R", 960);

        builder.Append("[#TRACKEND]\r\n");
        return builder.ToString();
    }

    static UstImportResult Import(UstPhraseRange range, bool leadingRest = true, bool trailingRest = true)
        => UstImporter.Import(UstParser.Parse(Document(leadingRest, trailingRest)), range);

    static string[] Lyrics(UstImportResult result) => [.. result.Notes.Select(x => x.Lyric)];

    [Fact]
    public void TheWholeFileIsTakenWhenNoRangeIsGiven()
    {
        var result = Import(UstPhraseRange.All);

        Assert.Equal(Phrases.Length, result.TotalPhrases);
        Assert.Equal(["あ", "か", "あ", "R", "か", "あ", "R", "あ", "あ", "か", "あ", "R", "か"], Lyrics(result));
        Assert.Equal(480, result.StartTicks);
    }

    [Theory]
    [InlineData(1, 1, new[] { "あ", "か", "あ" }, 480)]
    [InlineData(2, 1, new[] { "か", "あ" }, 2160)]
    [InlineData(3, 1, new[] { "あ", "あ", "か", "あ" }, 3360)]
    [InlineData(4, 1, new[] { "か" }, 5520)]
    public void ASinglePhraseKeepsOnlyItsOwnNotes(int start, int count, string[] expected, int startTicks)
    {
        var result = Import(new UstPhraseRange(start, count));

        Assert.Equal(expected, Lyrics(result));
        Assert.Equal(Phrases.Length, result.TotalPhrases);
        Assert.Equal(startTicks, result.StartTicks);
    }

    [Fact]
    public void AdjacentPhrasesKeepTheRestBetweenThem()
    {
        var result = Import(new UstPhraseRange(1, 2));

        Assert.Equal(["あ", "か", "あ", "R", "か", "あ"], Lyrics(result));
        Assert.Equal(480, result.StartTicks);
    }

    [Fact]
    public void ACountThatRunsPastTheEndStopsAtTheLastPhrase()
    {
        var result = Import(new UstPhraseRange(3, 99));

        Assert.Equal(["あ", "あ", "か", "あ", "R", "か"], Lyrics(result));
    }

    [Fact]
    public void AStartPastTheEndKeepsNothing()
    {
        var result = Import(new UstPhraseRange(99, 1));

        Assert.Empty(result.Notes);
        Assert.Equal(Phrases.Length, result.TotalPhrases);
    }

    [Fact]
    public void EveryPhraseTakenSeparatelyRebuildsTheWholeScore()
    {
        var whole = Lyrics(Import(UstPhraseRange.All)).Where(x => x != UTAUNote.RestLyric).ToArray();

        var pieces = new List<string>();
        for (var phrase = 1; phrase <= Phrases.Length; phrase++)
            pieces.AddRange(Lyrics(Import(new UstPhraseRange(phrase, 1))));

        Assert.Equal(whole, pieces);
    }

    [Fact]
    public void EachPhraseStartsWhereTheWholeScorePlacesIt()
    {
        var offsets = new List<int>();
        for (var phrase = 1; phrase <= Phrases.Length; phrase++)
            offsets.Add(Import(new UstPhraseRange(phrase, 1)).StartTicks);

        var expected = new List<int>();
        var position = 480;
        for (var phrase = 0; phrase < Phrases.Length; phrase++)
        {
            expected.Add(position);
            position += Phrases[phrase].Length * 480 + 240;
        }

        Assert.Equal(expected, offsets);
    }

    [Fact]
    public void TheParameterCarriesTheRangeIntoTheImport()
    {
        var path = Path.Combine(directory, "range.ust");
        File.WriteAllBytes(path, VoiceBankTextReader.ShiftJis.GetBytes(Document(true, true)));

        var whole = UTAUVoicePronounce.FromUst(path, new UTAUVoiceParameter());
        var second = UTAUVoicePronounce.FromUst(path, new UTAUVoiceParameter { UstPhraseStart = 2, UstPhraseCount = 1 });

        Assert.Equal(13, whole.Notes.Count);
        Assert.Equal(["か", "あ"], second.Notes.Select(x => x.Lyric));
        Assert.Equal(UstPhraseRange.All, whole.ImportedRange);
        Assert.Equal(new UstPhraseRange(2, 1), second.ImportedRange);
        Assert.Contains(string.Format(Texts.UstPhraseTotalFormat, Phrases.Length), second.ImportMessage);
        Assert.Contains(string.Format(Texts.UstPhraseOffsetFormat, 2160), second.ImportMessage);
    }

    [Fact]
    public void ChangingTheRangeRebuildsTheScore()
    {
        var path = Path.Combine(directory, "rebuild.ust");
        File.WriteAllBytes(path, VoiceBankTextReader.ShiftJis.GetBytes(Document(true, true)));

        var first = UTAUVoicePronounce.FromUst(path, new UTAUVoiceParameter { UstPhraseStart = 1, UstPhraseCount = 1 });
        var changed = new UTAUVoiceParameter { UstPhraseStart = 3, UstPhraseCount = 1 };

        Assert.NotEqual(changed.UstRange, first.ImportedRange);

        var second = UTAUVoicePronounce.FromUst(path, changed);

        Assert.Equal(["あ", "あ", "か", "あ"], second.Notes.Select(x => x.Lyric));
    }

    [Fact]
    public void TheDefaultParameterTakesTheWholeFile()
    {
        var parameter = new UTAUVoiceParameter();

        Assert.Equal(1, parameter.UstPhraseStart);
        Assert.Equal(0, parameter.UstPhraseCount);
        Assert.True(parameter.UstRange.CoversEverything);
    }

    VoiceBank Bank()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=split\r\n");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            string.Join("\r\n",
            [
                "a.wav=あ,50,80,-500,100,40",
                "ka.wav=か,50,120,-500,140,50",
                "start.wav=- あ,50,80,-500,100,40",
                "aka.wav=a か,50,120,-500,140,50",
                "ak.wav=a k,50,40,-200,60,20",
                "aend.wav=a -,50,40,-200,60,20",
            ]));
        foreach (var name in new[] { "a.wav", "ka.wav", "start.wav", "aka.wav", "ak.wav", "aend.wav" })
            TestVoiceBank.WriteSample(directory, name);
        return VoiceBankLoader.Load("split", directory);
    }

    static string[] Aliases(VoiceBank bank, IReadOnlyList<UTAUNote> notes)
        => [.. Phonemizer
            .Phonemize(bank, TempoMap.Create([.. notes], TimeBase.Default), null, PhonemizeOptions.Default)
            .Select(x => x.Alias)];

    [Fact]
    public void APhraseSoundsTheSameAloneAsItDoesInTheWholeScore()
    {
        var bank = Bank();
        var whole = Aliases(bank, Import(UstPhraseRange.All).Notes);

        var cursor = 0;
        for (var phrase = 1; phrase <= Phrases.Length; phrase++)
        {
            var piece = Aliases(bank, Import(new UstPhraseRange(phrase, 1)).Notes);
            var body = piece[^1] == "a -" ? piece[..^1] : piece;

            Assert.Equal(body, whole.Skip(cursor).Take(body.Length).ToArray());
            cursor += body.Length;

            while (cursor < whole.Length && whole[cursor] == UTAUNote.RestLyric)
                cursor++;
        }
    }
}
