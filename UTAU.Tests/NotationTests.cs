using System.IO;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;

namespace UTAU.Tests;

public sealed class NotationScannerTests
{
    [Fact]
    public void SplitsMoraeIncludingCombiningKana()
    {
        var tokens = NotationScanner.Scan("きゃしゅと");
        Assert.Equal(["きゃ", "しゅ", "と"], tokens.Select(x => x.Text));
        Assert.All(tokens, x => Assert.Equal(NotationTokenKind.Syllable, x.Kind));
    }

    [Fact]
    public void SokuonIsItsOwnToken()
    {
        var tokens = NotationScanner.Scan("あっか");
        Assert.Equal([NotationTokenKind.Syllable, NotationTokenKind.Sokuon, NotationTokenKind.Syllable], tokens.Select(x => x.Kind));
    }

    [Fact]
    public void LongVowelMarkBecomesAnExtendToken()
        => Assert.Equal(NotationTokenKind.Extend, NotationScanner.Scan("あー")[1].Kind);

    [Theory]
    [InlineData("、")]
    [InlineData("。")]
    [InlineData(" ")]
    [InlineData("?")]
    public void PunctuationBecomesRest(string text)
        => Assert.Equal(NotationTokenKind.Rest, Assert.Single(NotationScanner.Scan(text)).Kind);

    [Fact]
    public void ShortAndLongRestsAreDistinguished()
    {
        Assert.False(NotationScanner.IsLongRest("、"));
        Assert.True(NotationScanner.IsLongRest("。"));
    }

    [Fact]
    public void ParsesToneDirective()
    {
        var token = Assert.Single(NotationScanner.Scan("<!C4>"));
        Assert.Equal(NotationTokenKind.Directive, token.Kind);
        Assert.Equal(60, token.Tone);
        Assert.Null(token.LengthTicks);
    }

    [Fact]
    public void ParsesToneAndTickLength()
    {
        var token = Assert.Single(NotationScanner.Scan("<!A#3:250>"));
        Assert.Equal(58, token.Tone);
        Assert.Equal(250, token.LengthTicks);
    }

    [Fact]
    public void ParsesNoteFraction()
    {
        var token = Assert.Single(NotationScanner.Scan("<!C4:1/4>"));
        Assert.Equal(TimeBase.TicksPerQuarterNote, token.LengthTicks);
    }

    [Fact]
    public void ParsesLengthOnlyDirective()
    {
        var token = Assert.Single(NotationScanner.Scan("<!:300>"));
        Assert.Equal(NotationTokenKind.Directive, token.Kind);
        Assert.Null(token.Tone);
        Assert.Equal(300, token.LengthTicks);
    }

    [Theory]
    [InlineData("<!R:200>")]
    [InlineData("<!r:200>")]
    [InlineData("<!-:200>")]
    public void RestDirectivesAreRecognized(string text)
    {
        var token = Assert.Single(NotationScanner.Scan(text));
        Assert.Equal(NotationTokenKind.Rest, token.Kind);
        Assert.Equal(200, token.LengthTicks);
    }

    [Fact]
    public void UnterminatedDirectiveIsScannedAsPlainCharacters()
        => Assert.DoesNotContain(NotationScanner.Scan("<!C4あ"), x => x.Kind == NotationTokenKind.Directive);

    [Fact]
    public void InvalidToneInsideDirectiveIsScannedAsPlainCharacters()
        => Assert.DoesNotContain(NotationScanner.Scan("<!H9>"), x => x.Kind == NotationTokenKind.Directive);

    [Fact]
    public void InvalidLengthInsideDirectiveIsScannedAsPlainCharacters()
        => Assert.DoesNotContain(NotationScanner.Scan("<!C4:abc>"), x => x.Kind == NotationTokenKind.Directive);

    [Fact]
    public void ZeroAndNegativeLengthsAreRejected()
    {
        Assert.DoesNotContain(NotationScanner.Scan("<!C4:0>"), x => x.Kind == NotationTokenKind.Directive);
        Assert.DoesNotContain(NotationScanner.Scan("<!C4:-5>"), x => x.Kind == NotationTokenKind.Directive);
        Assert.DoesNotContain(NotationScanner.Scan("<!C4:1/0>"), x => x.Kind == NotationTokenKind.Directive);
        Assert.DoesNotContain(NotationScanner.Scan("<!C4:1.5>"), x => x.Kind == NotationTokenKind.Directive);
    }

    [Fact]
    public void DirectiveMarkerAvoidsTheHostControlTagSyntax()
    {
        Assert.Equal('!', NotationScanner.DirectiveMarker);
        Assert.DoesNotContain(char.ToLowerInvariant(NotationScanner.DirectiveMarker), "#sprwcl@");
    }
}

public sealed class NoteSequenceBuilderTests
{
    static NoteBuildOptions Options(int baseTone = 60) => NoteBuildOptions.Create(baseTone);

    [Fact]
    public void EachMoraBecomesANote()
    {
        var notes = NoteSequenceBuilder.Build("あかさ", Options());
        Assert.Equal(["あ", "か", "さ"], notes.Select(x => x.Lyric));
        Assert.All(notes, x => Assert.Equal(60, x.Tone));
        Assert.All(notes, x => Assert.Equal(NoteBuildOptions.BaseSyllableTicks, x.LengthTicks));
    }

    [Fact]
    public void DirectiveAppliesToTheFollowingMoraOnly()
    {
        var notes = NoteSequenceBuilder.Build("<!C4:250>ど<!D4>れみ", Options());
        Assert.Equal(3, notes.Count);
        Assert.Equal(60, notes[0].Tone);
        Assert.Equal(250, notes[0].LengthTicks);
        Assert.Equal(62, notes[1].Tone);
        Assert.Equal(NoteBuildOptions.BaseSyllableTicks, notes[1].LengthTicks);
        Assert.Equal(62, notes[2].Tone);
    }

    [Fact]
    public void FractionLengthBecomesTicks()
    {
        var notes = NoteSequenceBuilder.Build("<!C4:1/4>ど", Options());
        Assert.Equal(TimeBase.TicksPerQuarterNote, Assert.Single(notes).LengthTicks);
    }

    [Fact]
    public void DottedAndTripletFractionsAreExact()
    {
        Assert.Equal(720, Assert.Single(NoteSequenceBuilder.Build("<!C4:3/8>ど", Options())).LengthTicks);
        Assert.Equal(160, Assert.Single(NoteSequenceBuilder.Build("<!C4:1/12>ど", Options())).LengthTicks);
    }

    [Fact]
    public void LongVowelMarkExtendsThePreviousNote()
    {
        var note = Assert.Single(NoteSequenceBuilder.Build("あー", Options()));
        Assert.Equal(NoteBuildOptions.BaseSyllableTicks * 2, note.LengthTicks);
    }

    [Fact]
    public void LongVowelMarkAtTheHeadIsIgnored()
        => Assert.Empty(NoteSequenceBuilder.Build("ー", Options()));

    [Fact]
    public void LongVowelMarkAfterARestIsIgnored()
    {
        var notes = NoteSequenceBuilder.Build("、ー", Options());
        Assert.Equal(NoteBuildOptions.BaseShortRestTicks, Assert.Single(notes).LengthTicks);
    }

    [Fact]
    public void SokuonBecomesAShortNote()
    {
        var notes = NoteSequenceBuilder.Build("あっか", Options());
        Assert.Equal("っ", notes[1].Lyric);
        Assert.Equal(NoteBuildOptions.BaseSokuonTicks, notes[1].LengthTicks);
    }

    [Fact]
    public void ConsecutiveRestsAreMerged()
    {
        var notes = NoteSequenceBuilder.Build("あ、。", Options());
        Assert.Equal(2, notes.Count);
        Assert.True(notes[1].IsRest);
        Assert.Equal(NoteBuildOptions.BaseShortRestTicks + NoteBuildOptions.BaseLongRestTicks, notes[1].LengthTicks);
    }

    [Fact]
    public void PendingLengthIsConsumedByASingleMora()
    {
        var notes = NoteSequenceBuilder.Build("<!:400>あい", Options());
        Assert.Equal(400, notes[0].LengthTicks);
        Assert.Equal(NoteBuildOptions.BaseSyllableTicks, notes[1].LengthTicks);
    }

    [Fact]
    public void RestDirectiveInsertsARestOfTheGivenLength()
    {
        var notes = NoteSequenceBuilder.Build("あ<!R:400>い", Options());
        Assert.Equal(3, notes.Count);
        Assert.True(notes[1].IsRest);
        Assert.Equal(400, notes[1].LengthTicks);
    }

    [Fact]
    public void EmptyTextProducesNoNotes()
        => Assert.Empty(NoteSequenceBuilder.Build(string.Empty, Options()));
}

public sealed class LyricNormalizerTests
{
    [Fact]
    public void ConvertsKatakanaToHiragana()
        => Assert.Equal("あかさ", LyricNormalizer.Normalize("アカサ"));

    [Fact]
    public void PreservesDirectivesVerbatim()
        => Assert.Equal("<!C4:1/4>あ", LyricNormalizer.Normalize("<!C4:1/4>ア"));

    [Fact]
    public void CollapsesWhitespaceRuns()
        => Assert.Equal("あ い", LyricNormalizer.Normalize("  あ \r\n\t い  "));

    [Fact]
    public void KeepsLongVowelMark()
        => Assert.Equal("あー", LyricNormalizer.Normalize("アー"));

    [Fact]
    public void EmptyInputBecomesEmptyString()
    {
        Assert.Equal(string.Empty, LyricNormalizer.Normalize(null));
        Assert.Equal(string.Empty, LyricNormalizer.Normalize("   "));
    }

    [Fact]
    public void UnterminatedDirectiveIsNormalizedLikeText()
        => Assert.Equal("<!c4あ", LyricNormalizer.Normalize("<!c4ア"));
}

public sealed class KanaRomanizationTests
{
    [Theory]
    [InlineData("あ", "a")]
    [InlineData("か", "a")]
    [InlineData("し", "i")]
    [InlineData("つ", "u")]
    [InlineData("きゃ", "a")]
    [InlineData("しゅ", "u")]
    [InlineData("ん", "n")]
    [InlineData("を", "o")]
    [InlineData("ふぉ", "o")]
    public void ExtractsTheVowel(string mora, string expected)
        => Assert.Equal(expected, KanaRomanization.GetVowel(mora));

    [Theory]
    [InlineData("か", "k")]
    [InlineData("し", "sh")]
    [InlineData("つ", "ts")]
    [InlineData("きゃ", "ky")]
    [InlineData("じゃ", "j")]
    [InlineData("ん", "n")]
    public void ExtractsTheConsonant(string mora, string expected)
        => Assert.Equal(expected, KanaRomanization.GetConsonant(mora));

    [Theory]
    [InlineData("あ")]
    [InlineData("い")]
    [InlineData("を")]
    public void VowelOnlyMoraeHaveNoConsonant(string mora)
        => Assert.Null(KanaRomanization.GetConsonant(mora));

    [Fact]
    public void KatakanaIsAcceptedEverywhere()
    {
        Assert.Equal("a", KanaRomanization.GetVowel("カ"));
        Assert.Equal("k", KanaRomanization.GetConsonant("カ"));
        Assert.True(KanaRomanization.TryGetRomaji("キャ", out var romaji));
        Assert.Equal("kya", romaji);
    }

    [Fact]
    public void UnknownMoraeAreReportedAsUnknown()
    {
        Assert.False(KanaRomanization.TryGetRomaji("漢", out _));
        Assert.Null(KanaRomanization.GetVowel("漢"));
        Assert.Null(KanaRomanization.GetConsonant("漢"));
    }

    [Fact]
    public void ScriptConversionRoundTrips()
    {
        Assert.Equal("あゔゖ", KanaRomanization.ToHiragana("アヴヶ"));
        Assert.Equal("アヴヶ", KanaRomanization.ToKatakana("あゔゖ"));
        Assert.Equal("ー", KanaRomanization.ToHiragana("ー"));
    }
}

public sealed class TempoNotationTests
{
    static IReadOnlyList<UTAUNote> Build(string text)
        => NoteSequenceBuilder.Build(text, NoteBuildOptions.Create(60));

    [Fact]
    public void TheTempoDirectiveAppliesToTheNextNote()
    {
        var notes = Build("あ<!T=140>い う");

        Assert.Equal(UTAUNote.FollowScoreValue, notes[0].TempoOverride, 9);
        Assert.Equal(140.0, notes[1].TempoOverride, 9);
        Assert.Equal(UTAUNote.FollowScoreValue, notes[2].TempoOverride, 9);
    }

    [Fact]
    public void TheDirectiveCombinesWithAToneAndALength()
    {
        var notes = Build("<!T=90:1/4><!E4>あ");

        var note = Assert.Single(notes);
        Assert.Equal(90.0, note.TempoOverride, 9);
        Assert.Equal(TimeBase.TicksPerQuarterNote, note.LengthTicks);
        Assert.True(MusicalTone.TryParse("E4", out var expected));
        Assert.Equal(expected.NoteNumber, note.Tone);
    }

    [Fact]
    public void TheDirectiveIsCaseInsensitive()
    {
        Assert.Equal(140.0, Assert.Single(Build("<!t=140>あ")).TempoOverride, 9);
    }

    [Fact]
    public void ARestCarriesTheTempoWithoutMergingIntoThePreviousRest()
    {
        var notes = Build("、<!T=200>、あ");

        Assert.Equal(3, notes.Count);
        Assert.True(notes[0].IsRest);
        Assert.True(notes[1].IsRest);
        Assert.Equal(UTAUNote.FollowScoreValue, notes[0].TempoOverride, 9);
        Assert.Equal(200.0, notes[1].TempoOverride, 9);
    }

    [Fact]
    public void RestsStillMergeWithoutATempo()
    {
        var notes = Build("、、あ");

        Assert.Equal(2, notes.Count);
        Assert.True(notes[0].IsRest);
    }

    [Theory]
    [InlineData("<!T=10>あ")]
    [InlineData("<!T=500>あ")]
    [InlineData("<!T=abc>あ")]
    [InlineData("<!T=>あ")]
    public void OutOfRangeOrMalformedDirectivesAreNotDirectives(string text)
    {
        Assert.All(Build(text), x => Assert.Equal(UTAUNote.FollowScoreValue, x.TempoOverride, 9));
    }

    [Fact]
    public void TheDirectiveChangesTheRenderedTiming()
    {
        var steady = TempoMap.Create([.. Build("あいうえ")], new TimeBase(120.0, 1.0));
        var faster = TempoMap.Create([.. Build("あい<!T=240>うえ")], new TimeBase(120.0, 1.0));

        Assert.Equal(steady.TotalTicks, faster.TotalTicks);
        Assert.True(faster.TotalMilliseconds < steady.TotalMilliseconds);
        Assert.Equal(steady.TotalMilliseconds * 0.75, faster.TotalMilliseconds, 6);
    }

    [Fact]
    public void TheDirectiveSurvivesNormalization()
    {
        var normalized = LyricNormalizer.Normalize(" ア <!T=140> イ ");

        Assert.Contains("<!T=140>", normalized, StringComparison.Ordinal);
    }
}
