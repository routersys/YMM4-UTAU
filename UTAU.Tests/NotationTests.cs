using System.IO;
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
        Assert.Null(token.LengthMilliseconds);
    }

    [Fact]
    public void ParsesToneAndMillisecondLength()
    {
        var token = Assert.Single(NotationScanner.Scan("<!A#3:250>"));
        Assert.Equal(58, token.Tone);
        Assert.Equal(250.0, token.LengthMilliseconds);
    }

    [Fact]
    public void ParsesNoteFraction()
    {
        var token = Assert.Single(NotationScanner.Scan("<!C4:1/4>"));
        Assert.Equal(0.25, token.LengthWholeNotes);
        Assert.Null(token.LengthMilliseconds);
    }

    [Fact]
    public void ParsesLengthOnlyDirective()
    {
        var token = Assert.Single(NotationScanner.Scan("<!:300>"));
        Assert.Equal(NotationTokenKind.Directive, token.Kind);
        Assert.Null(token.Tone);
        Assert.Equal(300.0, token.LengthMilliseconds);
    }

    [Theory]
    [InlineData("<!R:200>")]
    [InlineData("<!r:200>")]
    [InlineData("<!-:200>")]
    public void RestDirectivesAreRecognized(string text)
    {
        var token = Assert.Single(NotationScanner.Scan(text));
        Assert.Equal(NotationTokenKind.Rest, token.Kind);
        Assert.Equal(200.0, token.LengthMilliseconds);
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
    static NoteBuildOptions Options(double speed = 1.0, double tempo = 120.0, int baseTone = 60)
        => NoteBuildOptions.Create(baseTone, speed, tempo);

    [Fact]
    public void EachMoraBecomesANote()
    {
        var notes = NoteSequenceBuilder.Build("あかさ", Options());
        Assert.Equal(["あ", "か", "さ"], notes.Select(x => x.Lyric));
        Assert.All(notes, x => Assert.Equal(60, x.Tone));
        Assert.All(notes, x => Assert.Equal(NoteBuildOptions.BaseSyllableMilliseconds, x.LengthMilliseconds));
    }

    [Fact]
    public void DirectiveAppliesToTheFollowingMoraOnly()
    {
        var notes = NoteSequenceBuilder.Build("<!C4:250>ど<!D4>れみ", Options());
        Assert.Equal(3, notes.Count);
        Assert.Equal(60, notes[0].Tone);
        Assert.Equal(250.0, notes[0].LengthMilliseconds);
        Assert.Equal(62, notes[1].Tone);
        Assert.Equal(NoteBuildOptions.BaseSyllableMilliseconds, notes[1].LengthMilliseconds);
        Assert.Equal(62, notes[2].Tone);
    }

    [Fact]
    public void FractionLengthUsesTheTempo()
    {
        var notes = NoteSequenceBuilder.Build("<!C4:1/4>ど", Options(tempo: 120.0));
        Assert.Equal(500.0, Assert.Single(notes).LengthMilliseconds, 9);
    }

    [Fact]
    public void SpeedScalesTheDefaultLengths()
    {
        var notes = NoteSequenceBuilder.Build("あ", Options(speed: 2.0));
        Assert.Equal(NoteBuildOptions.BaseSyllableMilliseconds / 2.0, Assert.Single(notes).LengthMilliseconds);
    }

    [Fact]
    public void LongVowelMarkExtendsThePreviousNote()
    {
        var note = Assert.Single(NoteSequenceBuilder.Build("あー", Options()));
        Assert.Equal(NoteBuildOptions.BaseSyllableMilliseconds * 2.0, note.LengthMilliseconds);
    }

    [Fact]
    public void LongVowelMarkAtTheHeadIsIgnored()
        => Assert.Empty(NoteSequenceBuilder.Build("ー", Options()));

    [Fact]
    public void LongVowelMarkAfterARestIsIgnored()
    {
        var notes = NoteSequenceBuilder.Build("、ー", Options());
        Assert.Equal(NoteBuildOptions.BaseShortRestMilliseconds, Assert.Single(notes).LengthMilliseconds);
    }

    [Fact]
    public void SokuonBecomesAShortNote()
    {
        var notes = NoteSequenceBuilder.Build("あっか", Options());
        Assert.Equal("っ", notes[1].Lyric);
        Assert.Equal(NoteBuildOptions.BaseSokuonMilliseconds, notes[1].LengthMilliseconds);
    }

    [Fact]
    public void ConsecutiveRestsAreMerged()
    {
        var notes = NoteSequenceBuilder.Build("あ、。", Options());
        Assert.Equal(2, notes.Count);
        Assert.True(notes[1].IsRest);
        Assert.Equal(NoteBuildOptions.BaseShortRestMilliseconds + NoteBuildOptions.BaseLongRestMilliseconds, notes[1].LengthMilliseconds);
    }

    [Fact]
    public void PendingLengthIsConsumedByASingleMora()
    {
        var notes = NoteSequenceBuilder.Build("<!:400>あい", Options());
        Assert.Equal(400.0, notes[0].LengthMilliseconds);
        Assert.Equal(NoteBuildOptions.BaseSyllableMilliseconds, notes[1].LengthMilliseconds);
    }

    [Fact]
    public void RestDirectiveInsertsARestOfTheGivenLength()
    {
        var notes = NoteSequenceBuilder.Build("あ<!R:400>い", Options());
        Assert.Equal(3, notes.Count);
        Assert.True(notes[1].IsRest);
        Assert.Equal(400.0, notes[1].LengthMilliseconds);
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
