using System.IO;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;
using UTAU.Synthesis;

namespace UTAU.Tests;

public sealed class VoiceBankResolutionTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    VoiceBank Load(string oto, string? prefixMap = null, string? yaml = null)
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.OtoFileName, oto);
        if (prefixMap is not null)
            TestVoiceBank.WriteText(directory, VoiceBankLoader.PrefixMapFileName, prefixMap);
        if (yaml is not null)
            TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterYamlFileName, yaml, System.Text.Encoding.UTF8);
        return VoiceBankLoader.Load("id", directory);
    }

    [Fact]
    public void PlainAliasIsResolved()
    {
        var bank = Load("a.wav=あ,0,0,0,0,0");
        Assert.Equal("あ", bank.Resolve("あ", 60, null)?.Alias);
    }

    [Fact]
    public void PrefixMapSuffixIsTriedBeforeThePlainAlias()
    {
        var bank = Load("a.wav=あ,0,0,0,0,0\r\nhigh.wav=あ_C5,0,0,0,0,0", "C5\t\t_C5\r\n");
        Assert.Equal("あ_C5", bank.Resolve("あ", 72, null)?.Alias);
        Assert.Equal("あ", bank.Resolve("あ", 60, null)?.Alias);
    }

    [Fact]
    public void PrefixMapFallsBackWhenTheSuffixedAliasIsAbsent()
    {
        var bank = Load("a.wav=あ,0,0,0,0,0", "C5\t\t_C5\r\n");
        Assert.Equal("あ", bank.Resolve("あ", 72, null)?.Alias);
    }

    [Fact]
    public void SubBankAffixesAreAppliedForTheMatchingColourAndTone()
    {
        var bank = Load(
            "a.wav=あ,0,0,0,0,0\r\npower.wav=あ強,0,0,0,0,0",
            null,
            """
            subbanks:
              - color: Power
                suffix: 強
                tone_ranges:
                  - C4-B4
            """);

        Assert.Equal("あ強", bank.Resolve("あ", 60, "Power")?.Alias);
        Assert.Equal("あ", bank.Resolve("あ", 72, "Power")?.Alias);
        Assert.Equal("あ", bank.Resolve("あ", 60, null)?.Alias);
        Assert.Equal(["Power"], bank.Colors);
    }

    [Fact]
    public void FirstOtoEntryWinsForDuplicateAliases()
    {
        var bank = Load("first.wav=あ,1,0,0,0,0\r\nsecond.wav=あ,2,0,0,0,0");
        Assert.Equal("first.wav", bank.Resolve("あ", 60, null)?.SampleFileName);
        Assert.Equal(1, bank.AliasCount);
    }

    [Fact]
    public void SubDirectoriesContributeTheirOwnOtoEntries()
    {
        Directory.CreateDirectory(Path.Combine(directory, "sub"));
        TestVoiceBank.WriteText(Path.Combine(directory, "sub"), VoiceBankLoader.OtoFileName, "b.wav=い,0,0,0,0,0");
        var bank = Load("a.wav=あ,0,0,0,0,0");

        Assert.Equal(2, bank.OtoSets.Count);
        Assert.Equal(Path.Combine(directory, "sub", "b.wav"), bank.Resolve("い", 60, null)?.SamplePath);
    }

    [Fact]
    public void UnknownAliasResolvesToNull()
        => Assert.Null(Load("a.wav=あ,0,0,0,0,0").Resolve("ぱ", 60, null));

    [Fact]
    public void NameFallsBackToTheDirectoryNameWithoutCharacterFile()
        => Assert.Equal(Path.GetFileName(directory), Load("a.wav=あ,0,0,0,0,0").Name);
}

public sealed class AliasResolverTests
{
    [Fact]
    public void CandidateOrderPrefersTheContinuousForm()
    {
        var candidates = AliasResolver.EnumerateCandidates("か", "a").ToArray();
        Assert.Equal("a か", candidates[0]);
        Assert.Contains("か", candidates);
        Assert.Contains("a ka", candidates);
        Assert.Contains("ka", candidates);
        Assert.True(Array.IndexOf(candidates, "a か") < Array.IndexOf(candidates, "か"));
    }

    [Fact]
    public void CandidatesAreDistinct()
    {
        var candidates = AliasResolver.EnumerateCandidates("か", "a").ToArray();
        Assert.Equal(candidates.Length, candidates.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void BothScriptsAreOffered()
    {
        var candidates = AliasResolver.EnumerateCandidates("か", string.Empty).ToArray();
        Assert.Contains("か", candidates);
        Assert.Contains("カ", candidates);
    }

    [Fact]
    public void UnknownMoraeStillOfferTheLiteralAlias()
        => Assert.Contains("漢", AliasResolver.EnumerateCandidates("漢", "-"));

    [Fact]
    public void RomajiLyricsAlsoOfferTheKanaForms()
    {
        var candidates = AliasResolver.EnumerateCandidates("ka", "a").ToArray();
        Assert.Equal("a ka", candidates[0]);
        Assert.Contains("a か", candidates);
        Assert.Contains("か", candidates);
        Assert.Contains("カ", candidates);
    }

    [Fact]
    public void ContinuousLyricsAlsoOfferTheSingleForms()
    {
        var candidates = AliasResolver.EnumerateCandidates("a あ", "-").ToArray();
        Assert.Contains("a あ", candidates);
        Assert.Contains("- あ", candidates);
        Assert.Contains("あ", candidates);
        Assert.Contains("a", candidates);
    }

    [Fact]
    public void TheWildcardFormFollowsARealPreviousVowel()
    {
        var following = AliasResolver.EnumerateCandidates("か", "a").ToArray();
        Assert.Contains("* か", following);
        Assert.True(Array.IndexOf(following, "* か") > Array.IndexOf(following, "a か"));
        Assert.True(Array.IndexOf(following, "* か") < Array.IndexOf(following, "か"));

        Assert.DoesNotContain("* か", AliasResolver.EnumerateCandidates("か", "-").ToArray());
        Assert.DoesNotContain("* か", AliasResolver.EnumerateCandidates("か", string.Empty).ToArray());
    }

    [Fact]
    public void AddedCandidatesStayDistinct()
    {
        var candidates = AliasResolver.EnumerateCandidates("a カ", "i").ToArray();
        Assert.Equal(candidates.Length, candidates.Distinct(StringComparer.Ordinal).Count());
    }
}

public sealed class PhonemizerTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    static readonly TimeBase Base = TimeBase.Default;

    static IReadOnlyList<PhonemeUnit> Phonemize(VoiceBank bank, IReadOnlyList<UTAUNote> notes)
        => Phonemizer.Phonemize(bank, TempoMap.Create(notes, Base), null, PhonemizeOptions.Default);

    static IReadOnlyList<UTAUNote> Notes(string text)
        => NoteSequenceBuilder.Build(text, NoteBuildOptions.Create(60));

    [Fact]
    public void SingleKanaBankResolvesEveryMora()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var units = Phonemize(bank, Notes("あかさ"));

        Assert.Equal(["あ", "か", "さ"], units.Select(x => x.Alias));
        Assert.All(units, x => Assert.False(x.IsUnresolved));
    }

    [Fact]
    public void ContinuousBankUsesThePreviousVowel()
    {
        var bank = TestVoiceBank.CreateVcvBank(directory);
        var units = Phonemize(bank, Notes("あか"));

        Assert.Equal(["- あ", "a か"], units.Select(x => x.Alias));
    }

    [Fact]
    public void RestResetsTheContinuousContext()
    {
        var bank = TestVoiceBank.CreateVcvBank(directory);
        var units = Phonemize(bank, Notes("あ、あ"));

        Assert.Equal(["- あ", UTAUNote.RestLyric, "- あ"], units.Select(x => x.Alias));
    }

    [Fact]
    public void TransitionUnitIsInsertedForCvvcBanks()
    {
        var bank = TestVoiceBank.CreateCvvcBank(directory);
        var units = Phonemize(bank, Notes("あか"));

        Assert.Equal(["あ", "a k", "か", "a -"], units.Select(x => x.Alias));
    }

    [Fact]
    public void TransitionUnitTakesTimeFromTheEndOfThePreviousNote()
    {
        var bank = TestVoiceBank.CreateCvvcBank(directory);
        var units = Phonemize(bank, Notes("あか"));

        Assert.Equal(units[0].EndMilliseconds, units[1].StartMilliseconds, 9);
        Assert.Equal(units[2].StartMilliseconds, units[1].EndMilliseconds, 9);
        Assert.True(units[1].LengthMilliseconds > 0.0);
    }

    [Fact]
    public void EndingUnitIsAppendedAfterTheLastNote()
    {
        var bank = TestVoiceBank.CreateCvvcBank(directory);
        var units = Phonemize(bank, Notes("あ"));

        Assert.Equal(["あ", "a -"], units.Select(x => x.Alias));
        Assert.Equal(units[0].EndMilliseconds, units[1].StartMilliseconds, 9);
    }

    [Fact]
    public void NoEndingUnitIsAppendedWhenTheTextEndsWithARest()
    {
        var bank = TestVoiceBank.CreateCvvcBank(directory);
        var units = Phonemize(bank, Notes("あ。"));

        Assert.DoesNotContain("a -", units.Select(x => x.Alias));
    }

    static IReadOnlyList<UTAUNote> Lyrics(params string[] lyrics)
        => [.. lyrics.Select(x => new UTAUNote { Lyric = x, LengthTicks = TimeBase.TicksPerQuarterNote, Tone = 60 })];

    [Fact]
    public void SingleKanaBankAcceptsRomajiLyrics()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var units = Phonemize(bank, Lyrics("a", "ka", "sa"));

        Assert.Equal(["あ", "か", "さ"], units.Select(x => x.Alias));
        Assert.All(units, x => Assert.False(x.IsUnresolved));
    }

    [Fact]
    public void SingleKanaBankAcceptsContinuousLyrics()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var units = Phonemize(bank, Lyrics("- あ", "a か"));

        Assert.Equal(["あ", "か"], units.Select(x => x.Alias));
        Assert.All(units, x => Assert.False(x.IsUnresolved));
    }

    [Fact]
    public void ContinuousBankAcceptsRomajiLyrics()
    {
        var bank = TestVoiceBank.CreateVcvBank(directory);
        var units = Phonemize(bank, Lyrics("a", "ka"));

        Assert.Equal(["- あ", "a か"], units.Select(x => x.Alias));
    }

    [Fact]
    public void ContinuousLyricsFollowTheActualPreviousVowel()
    {
        var bank = TestVoiceBank.CreateVcvBank(directory);
        var units = Phonemize(bank, Lyrics("i あ", "e か"));

        Assert.Equal(["- あ", "a か"], units.Select(x => x.Alias));
    }

    [Fact]
    public void ContinuousAliasesDoNotTakeAnExtraTransition()
    {
        var bank = TestVoiceBank.CreateVcvAndCvvcBank(directory);
        var units = Phonemize(bank, Lyrics("- あ", "a か"));

        Assert.Equal(["- あ", "a か", "a -"], units.Select(x => x.Alias));
    }

    [Fact]
    public void SingleLyricsPreferTheContinuousAliasInTheSameBank()
    {
        var bank = TestVoiceBank.CreateVcvAndCvvcBank(directory);
        var units = Phonemize(bank, Lyrics("あ", "か"));

        Assert.Equal(["- あ", "a か", "a -"], units.Select(x => x.Alias));
    }

    [Fact]
    public void VoicedConsonantNotesLeaveTheFollowingNotePlain()
    {
        var bank = TestVoiceBank.CreateCvvcBank(directory);
        var units = Phonemize(bank, Lyrics("か", "a k", "あ"));

        Assert.Equal(["か", "a k", "あ", "a -"], units.Select(x => x.Alias));
    }

    [Fact]
    public void SuppressedAutoVcvKeepsTheWrittenLyric()
    {
        var bank = TestVoiceBank.CreateVcvAndCvvcBank(directory);
        var plain = Phonemize(bank, Lyrics("あ", "か"));
        Assert.Equal(["- あ", "a か", "a -"], plain.Select(x => x.Alias));

        var notes = Lyrics("あ", "か");
        notes[1].SuppressAutoVcv = true;
        Assert.Equal(["- あ", "a k", "か", "a -"], Phonemize(bank, notes).Select(x => x.Alias));
    }

    [Fact]
    public void IgnoredPrefixMapFallsBackToTheUnmappedAlias()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=map\r\n");
        TestVoiceBank.WriteText(directory, VoiceBankLoader.PrefixMapFileName, "C4\t\t_C4\r\n");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            "a.wav=あ,50,80,-500,100,40\r\nam.wav=あ_C4,50,80,-500,100,40");
        TestVoiceBank.WriteSample(directory, "a.wav");
        TestVoiceBank.WriteSample(directory, "am.wav");
        var bank = VoiceBankLoader.Load("map", directory);

        var mapped = Lyrics("あ");
        Assert.Equal("あ_C4", Phonemize(bank, mapped)[0].Entry?.Alias);

        var ignored = Lyrics("あ");
        ignored[0].IgnorePrefixMap = true;
        Assert.Equal("あ", Phonemize(bank, ignored)[0].Entry?.Alias);
    }

    [Fact]
    public void TheWildcardAliasIsUsedWhenThePreviousVowelHasNoEntry()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=wild\r\n");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.OtoFileName,
            "s.wav=- あ,50,80,-500,100,40\r\nw.wav=* か,50,120,-500,140,50");
        TestVoiceBank.WriteSample(directory, "s.wav");
        TestVoiceBank.WriteSample(directory, "w.wav");
        var bank = VoiceBankLoader.Load("wild", directory);

        Assert.Equal(["- あ", "* か"], Phonemize(bank, Lyrics("あ", "か")).Select(x => x.Alias));
    }

    [Fact]
    public void UnresolvedLyricsAreReportedWithoutAnEntry()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var units = Phonemize(bank, Notes("あぱ"));

        Assert.Contains(units, x => x.IsUnresolved && x.Alias == "ぱ");
    }

    [Fact]
    public void RestsAreSilentButNotUnresolved()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var rest = Assert.Single(Phonemize(bank, Notes("、")));

        Assert.True(rest.IsSilent);
        Assert.False(rest.IsUnresolved);
    }

    [Fact]
    public void ScoreTimeIsContiguousAcrossNotes()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var notes = Notes("あかさ");
        var units = Phonemize(bank, notes);

        var position = 0.0;
        for (var i = 0; i < notes.Count; i++)
        {
            Assert.Equal(position, units[i].StartMilliseconds, 9);
            position += Base.ToMilliseconds(notes[i].LengthTicks);
        }
    }
}

public sealed class UnitTimingBuilderTests
{
    static PhonemeUnit CreateUnit(double start, double length, double preutterance, double overlap, string alias = "a")
    {
        var note = new UTAUNote { Lyric = alias };
        var entry = new OtoEntry(@"C:\bank", "a.wav", alias, 0.0, 0.0, 0.0, preutterance, overlap);
        return new PhonemeUnit(note, entry, alias, start, length, start, length, 60);
    }

    [Fact]
    public void AudioStartsEarlierByThePreutterance()
    {
        var timing = Assert.Single(UnitTimingBuilder.Build([CreateUnit(100.0, 200.0, 40.0, 20.0)]));
        Assert.Equal(60.0, timing.AudioStartMilliseconds, 9);
    }

    [Fact]
    public void RenderLengthReachesIntoTheOverlapOfTheNextUnit()
    {
        var timings = UnitTimingBuilder.Build([CreateUnit(0.0, 200.0, 0.0, 0.0), CreateUnit(200.0, 200.0, 40.0, 20.0)]);
        Assert.Equal(180.0, timings[0].AudioEndMilliseconds, 9);
        Assert.Equal(160.0, timings[1].AudioStartMilliseconds, 9);
        Assert.Equal(timings[1].AudioStartMilliseconds + 20.0, timings[0].AudioEndMilliseconds, 9);
    }

    [Fact]
    public void CrossfadeWeightsSumToOneThroughTheOverlap()
    {
        var timings = UnitTimingBuilder.Build([CreateUnit(0.0, 200.0, 0.0, 0.0), CreateUnit(200.0, 200.0, 40.0, 20.0)]);
        var start = timings[1].AudioStartMilliseconds;

        for (var offset = 0.0; offset <= 20.0; offset += 1.0)
        {
            var absolute = start + offset;
            var sum = timings[0].GetWeight(absolute - timings[0].AudioStartMilliseconds)
                + timings[1].GetWeight(absolute - timings[1].AudioStartMilliseconds);
            Assert.Equal(1.0, sum, 9);
        }
    }

    [Fact]
    public void PreutteranceIsScaledDownWhenThePreviousNoteIsTooShort()
    {
        var timings = UnitTimingBuilder.Build([CreateUnit(0.0, 20.0, 0.0, 0.0), CreateUnit(20.0, 200.0, 80.0, 40.0)]);
        Assert.Equal(-20.0, timings[1].AudioStartMilliseconds, 9);
        Assert.Equal(20.0, timings[1].FadeInMilliseconds, 9);
    }

    [Fact]
    public void SilentUnitsAreNotRendered()
    {
        var rest = new PhonemeUnit(new UTAUNote { Lyric = UTAUNote.RestLyric }, null, "R", 0.0, 100.0, 0.0, 100.0, 60);
        var timings = UnitTimingBuilder.Build([rest, CreateUnit(100.0, 200.0, 40.0, 20.0)]);
        Assert.Single(timings);
    }

    [Fact]
    public void ARestBeforeAUnitDisablesTheCrossfade()
    {
        var rest = new PhonemeUnit(new UTAUNote { Lyric = UTAUNote.RestLyric }, null, "R", 0.0, 100.0, 0.0, 100.0, 60);
        var timing = Assert.Single(UnitTimingBuilder.Build([rest, CreateUnit(100.0, 200.0, 40.0, 20.0)]));
        Assert.Equal(UTAUNote.DefaultFadeInMilliseconds, timing.FadeInMilliseconds, 9);
    }

    [Fact]
    public void FadesNeverExceedTheRenderLength()
    {
        var unit = CreateUnit(0.0, 10.0, 0.0, 0.0);
        unit.Note.FadeInMilliseconds = 500.0;
        unit.Note.FadeOutMilliseconds = 500.0;
        var timing = Assert.Single(UnitTimingBuilder.Build([unit]));

        Assert.True(timing.FadeInMilliseconds + timing.FadeOutMilliseconds <= timing.RenderLengthMilliseconds + 1e-9);
    }

    [Fact]
    public void WeightIsZeroOutsideTheRenderedSpan()
    {
        var timing = Assert.Single(UnitTimingBuilder.Build([CreateUnit(0.0, 200.0, 0.0, 0.0)]));
        Assert.Equal(0.0, timing.GetWeight(-1.0));
        Assert.Equal(0.0, timing.GetWeight(timing.RenderLengthMilliseconds + 1.0));
    }

    [Fact]
    public void OverriddenPreutteranceAndOverlapAreUsed()
    {
        var unit = CreateUnit(100.0, 200.0, 40.0, 20.0);
        unit.Note.PreutteranceOverride = 10.0;
        unit.Note.OverlapOverride = 5.0;
        var timing = Assert.Single(UnitTimingBuilder.Build([unit]));

        Assert.Equal(90.0, timing.AudioStartMilliseconds, 9);
    }

    [Fact]
    public void ZeroOverridesFallBackToTheOtoValues()
    {
        var unit = CreateUnit(100.0, 200.0, 40.0, 20.0);
        unit.Note.PreutteranceOverride = UTAUNote.FollowOtoValue;
        unit.Note.OverlapOverride = UTAUNote.FollowOtoValue;

        Assert.Equal(40.0, unit.Preutterance, 9);
        Assert.Equal(20.0, unit.Overlap, 9);
        Assert.Equal(60.0, Assert.Single(UnitTimingBuilder.Build([unit])).AudioStartMilliseconds, 9);
    }

    [Fact]
    public void EmptyInputProducesNoTimings()
        => Assert.Empty(UnitTimingBuilder.Build([]));
}
