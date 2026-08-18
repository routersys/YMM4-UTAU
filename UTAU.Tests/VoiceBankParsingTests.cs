using System.IO;
using System.Text;
using UTAU.Models;

namespace UTAU.Tests;

public sealed class VoiceBankTextReaderTests
{
    static readonly Encoding ShiftJis = VoiceBankTextReader.ShiftJis;

    [Fact]
    public void DetectsShiftJisWhenBytesAreNotValidUtf8()
    {
        var bytes = ShiftJis.GetBytes("あいうえお");
        Assert.Equal("あいうえお", VoiceBankTextReader.Decode(bytes));
    }

    [Fact]
    public void DetectsUtf8WithoutPreamble()
    {
        var bytes = new UTF8Encoding(false).GetBytes("あいうえお");
        Assert.Equal("あいうえお", VoiceBankTextReader.Decode(bytes));
    }

    [Fact]
    public void StripsUtf8Preamble()
    {
        var bytes = new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes("name=試験")).ToArray();
        Assert.Equal("name=試験", VoiceBankTextReader.Decode(bytes));
    }

    [Fact]
    public void DecodesUtf16WithPreamble()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("かな")).ToArray();
        Assert.Equal("かな", VoiceBankTextReader.Decode(bytes));
    }

    [Fact]
    public void AsciiIsDecodedIdenticallyByBothEncodings()
    {
        var bytes = "oto.ini"u8.ToArray();
        Assert.Equal("oto.ini", VoiceBankTextReader.Decode(bytes));
        Assert.Equal("oto.ini", VoiceBankTextReader.Decode(bytes, ShiftJis));
    }

    [Fact]
    public void ForcedEncodingOverridesDetection()
    {
        var bytes = new UTF8Encoding(false).GetBytes("あ");
        Assert.NotEqual("あ", VoiceBankTextReader.Decode(bytes, ShiftJis));
    }

    [Theory]
    [InlineData("a\r\nb\r\nc", 3)]
    [InlineData("a\nb\nc", 3)]
    [InlineData("a\rb\rc", 3)]
    [InlineData("a\r\nb\nc\r", 3)]
    [InlineData("a\r\n\r\nb", 3)]
    [InlineData("", 0)]
    [InlineData("\r\n", 1)]
    public void SplitsLinesOnEveryLineBreakStyle(string text, int expected)
        => Assert.Equal(expected, VoiceBankTextReader.ReadLines(text).Count());

    [Fact]
    public void KeepsEmptyLinesInOrder()
        => Assert.Equal(["a", "", "b"], VoiceBankTextReader.ReadLines("a\r\n\r\nb").ToArray());

    [Fact]
    public void ResolvesDeclaredEncodingByName()
    {
        Assert.Equal(932, VoiceBankTextReader.ResolveDeclaredEncoding("shift_jis")?.CodePage);
        Assert.Equal(65001, VoiceBankTextReader.ResolveDeclaredEncoding("utf-8")?.CodePage);
        Assert.Null(VoiceBankTextReader.ResolveDeclaredEncoding("not-an-encoding"));
        Assert.Null(VoiceBankTextReader.ResolveDeclaredEncoding(null));
    }

    [Fact]
    public void DeclaredUtf8DoesNotEmitPreamble()
        => Assert.Empty(VoiceBankTextReader.ResolveDeclaredEncoding("utf-8")!.GetPreamble());
}

public sealed class OtoIniParserTests
{
    const string Directory = @"C:\bank";

    [Fact]
    public void ParsesEveryField()
    {
        var entry = OtoIniParser.ParseLine(Directory, "_あ.wav=あ,100,200,-300,150,50");
        Assert.NotNull(entry);
        Assert.Equal("_あ.wav", entry.SampleFileName);
        Assert.Equal("あ", entry.Alias);
        Assert.Equal(100.0, entry.Offset);
        Assert.Equal(200.0, entry.Consonant);
        Assert.Equal(-300.0, entry.Cutoff);
        Assert.Equal(150.0, entry.Preutterance);
        Assert.Equal(50.0, entry.Overlap);
    }

    [Fact]
    public void EmptyAliasFallsBackToFileNameWithoutExtension()
    {
        var entry = OtoIniParser.ParseLine(Directory, "_あ.wav=,0,0,0,0,0");
        Assert.Equal("_あ", entry?.Alias);
    }

    [Fact]
    public void MissingTrailingFieldsBecomeZero()
    {
        var entry = OtoIniParser.ParseLine(Directory, "a.wav=あ,10,20");
        Assert.NotNull(entry);
        Assert.Equal(10.0, entry.Offset);
        Assert.Equal(20.0, entry.Consonant);
        Assert.Equal(0.0, entry.Cutoff);
        Assert.Equal(0.0, entry.Preutterance);
        Assert.Equal(0.0, entry.Overlap);
    }

    [Fact]
    public void AliasMayContainCommas()
    {
        var entry = OtoIniParser.ParseLine(Directory, "a.wav=a,b,1,2,3,4,5");
        Assert.NotNull(entry);
        Assert.Equal("a,b", entry.Alias);
        Assert.Equal(1.0, entry.Offset);
        Assert.Equal(5.0, entry.Overlap);
    }

    [Fact]
    public void AcceptsDecimalAndSignedValues()
    {
        var entry = OtoIniParser.ParseLine(Directory, "a.wav=a,1.5,-2.25,+3.75,0.001,-0.5");
        Assert.NotNull(entry);
        Assert.Equal(1.5, entry.Offset);
        Assert.Equal(-2.25, entry.Consonant);
        Assert.Equal(3.75, entry.Cutoff);
        Assert.Equal(0.001, entry.Preutterance);
        Assert.Equal(-0.5, entry.Overlap);
    }

    [Fact]
    public void NormalizesFullWidthDigits()
    {
        var entry = OtoIniParser.ParseLine(Directory, "a.wav=a,１２３,0,0,0,0");
        Assert.Equal(123.0, entry?.Offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no separator")]
    [InlineData("=あ,0,0,0,0,0")]
    public void RejectsLinesWithoutUsableFileName(string line)
        => Assert.Null(OtoIniParser.ParseLine(Directory, line));

    [Fact]
    public void RejectsFileNamesContainingPathSeparators()
        => Assert.Null(OtoIniParser.ParseLine(Directory, @"sub\a.wav=a,0,0,0,0,0"));

    [Fact]
    public void NonNumericValuesBecomeZeroInsteadOfThrowing()
    {
        var entry = OtoIniParser.ParseLine(Directory, "a.wav=a,abc,,NaN,Infinity,0");
        Assert.NotNull(entry);
        Assert.Equal(0.0, entry.Offset);
        Assert.Equal(0.0, entry.Consonant);
        Assert.Equal(0.0, entry.Cutoff);
        Assert.Equal(0.0, entry.Preutterance);
    }

    [Fact]
    public void ParsesMultipleLinesAndSkipsInvalidOnes()
    {
        var entries = OtoIniParser.Parse(Directory, "a.wav=a,0,0,0,0,0\r\n\r\ngarbage\r\nb.wav=b,0,0,0,0,0\n");
        Assert.Equal(2, entries.Count);
        Assert.Equal(["a", "b"], entries.Select(x => x.Alias));
    }
}

public sealed class OtoEntryTests
{
    static OtoEntry Create(double offset, double cutoff)
        => new(@"C:\bank", "a.wav", "a", offset, 0.0, cutoff, 0.0, 0.0);

    [Fact]
    public void PositiveCutoffIsMeasuredFromTheEndOfTheFile()
        => Assert.Equal(700.0, Create(100.0, 300.0).GetEndMilliseconds(1000.0));

    [Fact]
    public void NegativeCutoffIsALengthFromTheOffset()
        => Assert.Equal(400.0, Create(100.0, -300.0).GetEndMilliseconds(1000.0));

    [Fact]
    public void ZeroCutoffReachesTheEndOfTheFile()
        => Assert.Equal(1000.0, Create(100.0, 0.0).GetEndMilliseconds(1000.0));

    [Fact]
    public void LengthNeverGoesNegative()
        => Assert.Equal(0.0, Create(900.0, 300.0).GetLengthMilliseconds(1000.0));
}

public sealed class MusicalToneTests
{
    [Theory]
    [InlineData("C4", 60)]
    [InlineData("c4", 60)]
    [InlineData("A4", 69)]
    [InlineData("C#4", 61)]
    [InlineData("Db4", 61)]
    [InlineData("B3", 59)]
    [InlineData("C-1", 0)]
    [InlineData("G9", 127)]
    public void ParsesNoteNames(string text, int expected)
    {
        Assert.True(MusicalTone.TryParse(text, out var tone));
        Assert.Equal(expected, tone.NoteNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("H4")]
    [InlineData("C")]
    [InlineData("C#")]
    [InlineData("Cx4")]
    [InlineData("C10")]
    [InlineData("C-2")]
    public void RejectsInvalidNoteNames(string text)
        => Assert.False(MusicalTone.TryParse(text, out _));

    [Fact]
    public void NameRoundTripsForEveryNoteNumber()
    {
        for (var noteNumber = 0; noteNumber <= 127; noteNumber++)
        {
            var name = new MusicalTone(noteNumber).Name;
            Assert.True(MusicalTone.TryParse(name, out var parsed), name);
            Assert.Equal(noteNumber, parsed.NoteNumber);
        }
    }

    [Fact]
    public void FrequencyMatchesEqualTemperament()
    {
        Assert.Equal(440.0, new MusicalTone(69).Frequency, 9);
        Assert.Equal(261.62556530059862, new MusicalTone(60).Frequency, 9);
        Assert.Equal(880.0, new MusicalTone(81).Frequency, 9);
    }

    [Fact]
    public void FractionalNoteNumbersFollowTheSameLaw()
        => Assert.Equal(new MusicalTone(70).Frequency, MusicalTone.FrequencyOf(70.0), 9);

    [Fact]
    public void OneHundredCentsEqualsOneSemitone()
        => Assert.Equal(new MusicalTone(61).Frequency, MusicalTone.FrequencyOf(60 + 100.0 / 100.0), 9);
}

public sealed class ToneRangeTests
{
    [Fact]
    public void ParsesRange()
    {
        Assert.True(ToneRange.TryParse("C1-B7", out var range));
        Assert.Equal(24, range.Low);
        Assert.Equal(107, range.High);
    }

    [Fact]
    public void ParsesSingleTone()
    {
        Assert.True(ToneRange.TryParse("C4", out var range));
        Assert.Equal(60, range.Low);
        Assert.Equal(60, range.High);
    }

    [Fact]
    public void ParsesNegativeOctaveAsSingleTone()
    {
        Assert.True(ToneRange.TryParse("C-1", out var range));
        Assert.Equal(0, range.Low);
        Assert.Equal(0, range.High);
    }

    [Fact]
    public void ParsesRangeStartingAtNegativeOctave()
    {
        Assert.True(ToneRange.TryParse("C-1-C0", out var range));
        Assert.Equal(0, range.Low);
        Assert.Equal(12, range.High);
    }

    [Fact]
    public void ReversedRangeIsNormalized()
    {
        Assert.True(ToneRange.TryParse("B7-C1", out var range));
        Assert.Equal(24, range.Low);
        Assert.Equal(107, range.High);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("C1-")]
    [InlineData("-B7")]
    [InlineData("X1-Y2")]
    public void RejectsInvalidRanges(string text)
        => Assert.False(ToneRange.TryParse(text, out _));

    [Fact]
    public void ContainsIsInclusive()
    {
        ToneRange.TryParse("C4-C5", out var range);
        Assert.True(range.Contains(60));
        Assert.True(range.Contains(72));
        Assert.False(range.Contains(59));
        Assert.False(range.Contains(73));
    }
}

public sealed class PrefixMapTests
{
    [Fact]
    public void ParsesTabSeparatedEntries()
    {
        var map = PrefixMap.Parse("C4\t\t_C4\r\nC5\t\t_C5\r\n");
        Assert.Equal(2, map.Count);
        Assert.Equal((string.Empty, "_C4"), map.Resolve(60));
        Assert.Equal((string.Empty, "_C5"), map.Resolve(72));
    }

    [Fact]
    public void SecondFieldIsThePrefix()
        => Assert.Equal(("↑", string.Empty), PrefixMap.Parse("C4\t↑").Resolve(60));

    [Fact]
    public void FallsBackToWhitespaceSplittingWhenNoTabIsPresent()
        => Assert.Equal(("↑", string.Empty), PrefixMap.Parse("C4 ↑").Resolve(60));

    [Fact]
    public void UnknownToneResolvesToEmptyAffixes()
        => Assert.Equal((string.Empty, string.Empty), PrefixMap.Parse("C4\t\t_C4").Resolve(61));

    [Fact]
    public void InvalidToneLinesAreSkipped()
        => Assert.Equal(0, PrefixMap.Parse("H9\t\tx\r\n\r\n   ").Count);

    [Fact]
    public void LaterEntriesReplaceEarlierOnesForTheSameTone()
        => Assert.Equal((string.Empty, "second"), PrefixMap.Parse("C4\t\tfirst\r\nC4\t\tsecond").Resolve(60));

    [Fact]
    public void EnumerationIsOrderedByTone()
    {
        var tones = PrefixMap.Parse("C5\t\ta\r\nC4\t\tb").Enumerate().Select(x => x.Tone.NoteNumber).ToArray();
        Assert.Equal([60, 72], tones);
    }
}
