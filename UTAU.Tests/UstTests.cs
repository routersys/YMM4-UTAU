using System.Globalization;
using System.IO;
using System.Text;
using UTAU;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Synthesis;
using UTAU.ViewModels;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Tests;

public sealed class UstTests
{
    const string ExactTempo = "Tempo=125.00";

    static string Document(string setting, params string[][] notes)
    {
        var builder = new StringBuilder();
        builder.Append("[#VERSION]\r\nUST Version1.2\r\n[#SETTING]\r\n").Append(setting).Append("\r\n");
        for (var index = 0; index < notes.Length; index++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"[#{index:D4}]\r\n");
            foreach (var line in notes[index])
                builder.Append(line).Append("\r\n");
        }
        builder.Append("[#TRACKEND]\r\n");
        return builder.ToString();
    }

    static UstImportResult Import(string content) => UstImporter.Import(UstParser.Parse(content));

    static UstImportResult Import(byte[] bytes) => UstImporter.Import(UstParser.Parse(bytes));

    static string[] Sung(params string[] lines) => ["Length=480", "Lyric=a", "NoteNum=60", .. lines];

    [Fact]
    public void ShiftJisIsTheDefaultEncoding()
    {
        var text = Document("Tempo=120.00", ["Length=480", "Lyric=あ", "NoteNum=60"]);
        var result = Import(VoiceBankTextReader.ShiftJis.GetBytes(text));

        Assert.Equal("あ", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void TheDeclaredCharsetWinsOverDetection()
    {
        var text = "[#VERSION]\r\nUST Version1.2\r\nCharset=UTF-8\r\n[#SETTING]\r\nTempo=120.00\r\n"
            + "[#0000]\r\nLength=480\r\nLyric=あ\r\nNoteNum=60\r\n[#TRACKEND]\r\n";
        var result = Import(new UTF8Encoding(false).GetBytes(text));

        Assert.Equal("あ", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void AByteOrderMarkIsStripped()
    {
        var text = Document("Tempo=120.00", ["Length=480", "Lyric=あ", "NoteNum=60"]);
        var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
        var result = Import(bytes);

        Assert.Equal("あ", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void AUtf16FileIsDecoded()
    {
        var text = Document("Tempo=120.00", ["Length=480", "Lyric=あ", "NoteNum=60"]);
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
        var result = Import(bytes);

        Assert.Equal("あ", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void TheCharsetScanStopsBeforeTheFirstNote()
    {
        var text = Document("Tempo=120.00", ["Length=480", "Lyric=あ", "NoteNum=60", "Charset=UTF-8"]);
        var result = Import(VoiceBankTextReader.ShiftJis.GetBytes(text));

        Assert.Equal("あ", Assert.Single(result.Notes).Lyric);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void EveryLineEndingIsAccepted(string lineEnding)
    {
        var text = Document("Tempo=120.00", Sung(), Sung("Lyric=i")).Replace("\r\n", lineEnding);
        var result = Import(text);

        Assert.Equal(2, result.Notes.Count);
        Assert.Equal("a", result.Notes[0].Lyric);
        Assert.Equal("i", result.Notes[1].Lyric);
    }

    [Fact]
    public void AFileWithoutATrackEndIsStillRead()
    {
        var result = Import("[#SETTING]\r\nTempo=120.00\r\n[#0000]\r\nLength=480\r\nLyric=a\r\nNoteNum=60\r\n");

        Assert.Single(result.Notes);
    }

    [Fact]
    public void LinesBeforeTheFirstSectionAreIgnored()
    {
        var result = Import("garbage\r\nLyric=zzz\r\n" + Document("Tempo=120.00", Sung()));

        Assert.Equal("a", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void UnknownSectionsAreIgnored()
    {
        var result = Import(Document("Tempo=120.00", Sung()) + "[#WHATEVER]\r\nLength=480\r\nLyric=x\r\nNoteNum=60\r\n");

        Assert.Equal("a", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void ThePluginNeighbourSectionsAreRead()
    {
        var text = "[#SETTING]\r\nTempo=120.00\r\n"
            + "[#PREV]\r\nLength=480\r\nLyric=a\r\nNoteNum=60\r\n"
            + "[#0000]\r\nLength=480\r\nLyric=i\r\nNoteNum=62\r\n"
            + "[#NEXT]\r\nLength=480\r\nLyric=u\r\nNoteNum=64\r\n";
        var result = Import(text);

        Assert.Equal(["a", "i", "u"], result.Notes.Select(x => x.Lyric).ToArray());
    }

    [Fact]
    public void DeletedSectionsAreSkipped()
    {
        var text = "[#SETTING]\r\nTempo=120.00\r\n"
            + "[#0000]\r\nLength=480\r\nLyric=a\r\nNoteNum=60\r\n"
            + "[#DELETE]\r\nLength=480\r\nLyric=x\r\nNoteNum=60\r\n";
        var result = Import(text);

        Assert.Equal("a", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void TheVersionComesFromTheBareLineOrTheSetting()
    {
        Assert.Equal("UST Version1.2", UstParser.Parse(Document("Tempo=120.00", Sung())).Version);
        Assert.Equal("1.19", UstParser.Parse("[#SETTING]\r\nUstVersion=1.19\r\n").Version);
        Assert.Null(UstParser.Parse("[#SETTING]\r\nTempo=120.00\r\n").Version);
    }

    [Fact]
    public void AnEmptyDocumentYieldsNoNotes()
    {
        Assert.Empty(Import(string.Empty).Notes);
        Assert.Empty(Import("[#VERSION]\r\nUST Version1.2\r\n[#TRACKEND]\r\n").Notes);
    }

    [Fact]
    public void TheLastValueWinsForARepeatedKey()
    {
        var result = Import(Document("Tempo=120.00", ["Length=480", "Lyric=a", "Lyric=i", "NoteNum=60"]));

        Assert.Equal("i", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void ValuesKeepEverythingAfterTheFirstEquals()
    {
        var result = Import(Document("Tempo=120.00", ["Length=480", "Lyric=a=b", "NoteNum=60"]));

        Assert.Equal("a=b", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void LyricsKeepTheirInnerSpaces()
    {
        var result = Import(Document("Tempo=120.00", ["Length=480", "Lyric=- あ", "NoteNum=60"]));

        Assert.Equal("- あ", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void NotesFollowEachOtherInTicks()
    {
        var result = Import(Document("Tempo=120.00", Sung(), Sung("Length=240"), Sung("Length=120")));

        Assert.Equal([480, 240, 120], result.Notes.Select(x => x.LengthTicks).ToArray());
    }

    [Theory]
    [InlineData("R")]
    [InlineData("r")]
    public void RestLyricsBecomeRests(string lyric)
    {
        var result = Import(Document("Tempo=120.00", Sung(), [$"Length=240", $"Lyric={lyric}", "NoteNum=60"], Sung()));

        Assert.True(result.Notes[1].IsRest);
        Assert.Equal(UTAUNote.RestLyric, result.Notes[1].Lyric);
    }

    [Theory]
    [InlineData("?あ", "あ")]
    [InlineData("!あ", "あ")]
    [InlineData("?!あ", "あ")]
    public void TheLyricMarkersAreStripped(string written, string expected)
    {
        var result = Import(Document("Tempo=120.00", ["Length=480", $"Lyric={written}", "NoteNum=60"]));

        Assert.Equal(expected, Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void AMissingNoteNumberFallsBackToMiddleC()
    {
        var result = Import(Document("Tempo=120.00", ["Length=480", "Lyric=a"]));

        Assert.Equal(MusicalTone.MiddleC.NoteNumber, Assert.Single(result.Notes).Tone);
    }

    [Fact]
    public void NotesWithoutAUsableLengthAreSkipped()
    {
        var result = Import(Document(
            "Tempo=120.00",
            ["Lyric=a", "NoteNum=60"],
            ["Length=0", "Lyric=i", "NoteNum=60"],
            ["Length=-480", "Lyric=u", "NoteNum=60"],
            Sung("Lyric=e")));

        Assert.Equal("e", Assert.Single(result.Notes).Lyric);
    }

    [Fact]
    public void ValuesOutsideTheModelRangeAreClamped()
    {
        var result = Import(Document("Tempo=120.00", Sung(
            "Velocity=220",
            "Intensity=-40",
            "Modulation=900",
            "NoteNum=999",
            "PreUtterance=99999")));

        var note = Assert.Single(result.Notes);
        Assert.Equal(200.0, note.Velocity, 9);
        Assert.Equal(0.0, note.Intensity, 9);
        Assert.Equal(200.0, note.Modulation, 9);
        Assert.Equal(127, note.Tone);
        Assert.Equal(5000.0, note.PreutteranceOverride, 9);
    }

    [Fact]
    public void ABlankPreUtteranceFollowsTheOriginalSetting()
    {
        var result = Import(Document("Tempo=120.00", Sung("PreUtterance=", "VoiceOverlap=")));

        var note = Assert.Single(result.Notes);
        Assert.Equal(UTAUNote.FollowOtoValue, note.PreutteranceOverride, 9);
        Assert.Equal(UTAUNote.FollowOtoValue, note.OverlapOverride, 9);
    }

    [Fact]
    public void LeadingAndTrailingRestsAreTrimmedAndReported()
    {
        var result = Import(Document(
            "Tempo=120.00",
            ["Length=960", "Lyric=R", "NoteNum=60"],
            Sung(),
            ["Length=240", "Lyric=R", "NoteNum=60"]));

        Assert.Equal("a", Assert.Single(result.Notes).Lyric);
        Assert.Equal(1200, result.TrimmedRestTicks);
    }

    [Fact]
    public void RestsBetweenNotesAreKept()
    {
        var result = Import(Document(
            "Tempo=120.00",
            Sung(),
            ["Length=240", "Lyric=R", "NoteNum=60"],
            Sung("Lyric=i")));

        Assert.Equal(3, result.Notes.Count);
        Assert.True(result.Notes[1].IsRest);
        Assert.Equal(0, result.TrimmedRestTicks);
    }

    [Fact]
    public void AFileOfOnlyRestsYieldsNoNotes()
    {
        var result = Import(Document(
            "Tempo=120.00",
            ["Length=480", "Lyric=R", "NoteNum=60"],
            ["Length=480", "Lyric=R", "NoteNum=60"]));

        Assert.Empty(result.Notes);
        Assert.Equal(960, result.TrimmedRestTicks);
    }

    [Fact]
    public void DeltaAndDurationInsertTheImpliedRest()
    {
        var text = "[#SETTING]\r\nTempo=120.00\r\n"
            + "[#0000]\r\nDelta=0\r\nDuration=480\r\nLength=960\r\nLyric=a\r\nNoteNum=60\r\n"
            + "[#0001]\r\nDelta=960\r\nDuration=480\r\nLength=480\r\nLyric=i\r\nNoteNum=62\r\n";
        var result = Import(text);

        Assert.Equal(3, result.Notes.Count);
        Assert.Equal("a", result.Notes[0].Lyric);
        Assert.True(result.Notes[1].IsRest);
        Assert.Equal(480, result.Notes[1].LengthTicks);
        Assert.Equal("i", result.Notes[2].Lyric);
    }

    [Fact]
    public void FlushDeltaAndDurationBehaveLikeAPlainSequence()
    {
        var text = "[#SETTING]\r\nTempo=120.00\r\n"
            + "[#0000]\r\nDelta=0\r\nDuration=480\r\nLength=480\r\nLyric=a\r\nNoteNum=60\r\n"
            + "[#0001]\r\nDelta=480\r\nDuration=240\r\nLength=240\r\nLyric=i\r\nNoteNum=62\r\n";
        var result = Import(text);

        Assert.Equal([480, 240], result.Notes.Select(x => x.LengthTicks).ToArray());
    }

    [Fact]
    public void OverlappingPositionsDoNotProduceNegativeRests()
    {
        var text = "[#SETTING]\r\nTempo=120.00\r\n"
            + "[#0000]\r\nDelta=0\r\nDuration=480\r\nLength=480\r\nLyric=a\r\nNoteNum=60\r\n"
            + "[#0001]\r\nDelta=-240\r\nDuration=480\r\nLength=480\r\nLyric=i\r\nNoteNum=62\r\n";
        var result = Import(text);

        Assert.Equal(["a", "i"], result.Notes.Select(x => x.Lyric).ToArray());
        Assert.All(result.Notes, x => Assert.True(x.LengthTicks >= UTAUNote.MinimumLengthTicks));
    }

    [Fact]
    public void ALongGapIsSplitIntoSeveralRests()
    {
        var text = "[#SETTING]\r\nTempo=120.00\r\n"
            + "[#0000]\r\nDelta=0\r\nDuration=480\r\nLength=480\r\nLyric=a\r\nNoteNum=60\r\n"
            + "[#0001]\r\nDelta=80000\r\nDuration=480\r\nLength=480\r\nLyric=i\r\nNoteNum=62\r\n";
        var result = Import(text);

        var rests = result.Notes.Where(x => x.IsRest).ToArray();
        Assert.True(rests.Length > 1);
        Assert.Equal(79520, rests.Sum(x => x.LengthTicks));
    }

    [Fact]
    public void TheSettingTempoIsUsed()
    {
        Assert.Equal(144.0, Import(Document("Tempo=144.00", Sung())).Tempo, 9);
    }

    [Fact]
    public void ADefectiveSettingTempoFallsBackToTheFirstNoteTempo()
    {
        var result = Import(Document("Tempo=500000.00", Sung(), Sung("Tempo=127.00"), Sung()));

        Assert.Equal(127.0, result.Tempo, 9);
        Assert.Equal(0, result.TempoChangeCount);
    }

    [Fact]
    public void AMissingTempoFallsBackToTheDefault()
    {
        Assert.Equal(TimeBase.DefaultTempo, Import(Document("ProjectName=x", Sung())).Tempo, 9);
        Assert.Equal(TimeBase.DefaultTempo, Import(Document("Tempo=0", Sung())).Tempo, 9);
        Assert.Equal(TimeBase.DefaultTempo, Import(Document("Tempo=abc", Sung())).Tempo, 9);
    }

    [Fact]
    public void TempoChangesAreCountedAndNotApplied()
    {
        var result = Import(Document("Tempo=120.00", Sung(), Sung("Tempo=150.00"), Sung("Tempo=150.00"), Sung("Tempo=90.00")));

        Assert.Equal(120.0, result.Tempo, 9);
        Assert.Equal(2, result.TempoChangeCount);
    }

    [Fact]
    public void PitchBendPointsUseMillisecondsAndTenCentUnits()
    {
        var result = Import(Document(ExactTempo, Sung("PBS=-50;20", "PBW=100,150", "PBY=-10,0")));

        var points = Assert.Single(result.Notes).PitchPoints;
        Assert.Equal(3, points.Count);
        Assert.Equal(-50, points[0].Ticks);
        Assert.Equal(200.0, points[0].Cents, 9);
        Assert.Equal(50, points[1].Ticks);
        Assert.Equal(-100.0, points[1].Cents, 9);
        Assert.Equal(200, points[2].Ticks);
        Assert.Equal(0.0, points[2].Cents, 9);
    }

    [Theory]
    [InlineData("PBS=-50;20")]
    [InlineData("PBS=-50,20")]
    public void TheStartAcceptsBothSeparators(string pbs)
    {
        var result = Import(Document(ExactTempo, Sung(pbs, "PBW=100")));

        var points = Assert.Single(result.Notes).PitchPoints;
        Assert.Equal(-50, points[0].Ticks);
        Assert.Equal(200.0, points[0].Cents, 9);
    }

    [Fact]
    public void AStartWithoutAHeightIsRead()
    {
        var result = Import(Document(ExactTempo, Sung("PBS=-30", "PBW=100")));

        var points = Assert.Single(result.Notes).PitchPoints;
        Assert.Equal(-30, points[0].Ticks);
        Assert.Equal(0.0, points[0].Cents, 9);
    }

    [Fact]
    public void AStartWithoutAWidthProducesNoPitchPoints()
    {
        var result = Import(Document(ExactTempo, Sung("PBS=-30;20", "PBY=10")));

        Assert.Empty(Assert.Single(result.Notes).PitchPoints);
    }

    [Fact]
    public void MissingHeightsArePaddedWithZero()
    {
        var result = Import(Document(ExactTempo, Sung("PBS=0;0", "PBW=50,50,50", "PBY=10")));

        var points = Assert.Single(result.Notes).PitchPoints;
        Assert.Equal(4, points.Count);
        Assert.Equal(100.0, points[1].Cents, 9);
        Assert.Equal(0.0, points[2].Cents, 9);
        Assert.Equal(0.0, points[3].Cents, 9);
    }

    [Fact]
    public void TheShapesMapToTheEditorCurves()
    {
        var result = Import(Document(ExactTempo, Sung("PBS=0;0", "PBW=50,50,50", "PBM=,s,r,j")));

        var points = Assert.Single(result.Notes).PitchPoints;
        Assert.Equal(PitchPointShape.SCurve, points[0].Shape);
        Assert.Equal(PitchPointShape.Linear, points[1].Shape);
        Assert.Equal(PitchPointShape.RCurve, points[2].Shape);
        Assert.Equal(PitchPointShape.JCurve, points[3].Shape);
    }

    [Fact]
    public void ShapesBeyondThePointCountAreIgnored()
    {
        var result = Import(Document(ExactTempo, Sung("PBS=0;0", "PBW=50", "PBM=r,j,s,r,j")));

        var points = Assert.Single(result.Notes).PitchPoints;
        Assert.Equal(2, points.Count);
        Assert.Equal(PitchPointShape.RCurve, points[0].Shape);
        Assert.Equal(PitchPointShape.JCurve, points[1].Shape);
    }

    [Fact]
    public void PitchPointsNeverMoveBackwards()
    {
        var result = Import(Document(ExactTempo, Sung("PBS=-40;0", "PBW=100,-500,60")));

        var points = Assert.Single(result.Notes).PitchPoints;
        for (var index = 1; index < points.Count; index++)
            Assert.True(points[index].Ticks >= points[index - 1].Ticks);
    }

    [Fact]
    public void VibratoFieldsMapInOrder()
    {
        var result = Import(Document("Tempo=120.00", Sung("VBR=65,180,40,15,25,-30,10,0")));

        var vibrato = Assert.Single(result.Notes).Vibrato;
        Assert.Equal(65.0, vibrato.LengthPercent, 9);
        Assert.Equal(180.0, vibrato.PeriodMilliseconds, 9);
        Assert.Equal(40.0, vibrato.DepthCents, 9);
        Assert.Equal(15.0, vibrato.FadeInPercent, 9);
        Assert.Equal(25.0, vibrato.FadeOutPercent, 9);
        Assert.Equal(-30.0, vibrato.PhasePercent, 9);
        Assert.Equal(10.0, vibrato.OffsetPercent, 9);
        Assert.True(vibrato.IsEnabled);
    }

    [Fact]
    public void AZeroLengthVibratoLeavesTheDefaults()
    {
        var result = Import(Document("Tempo=120.00", Sung("VBR=0,0,0,0,0,0,0,0")));

        var vibrato = Assert.Single(result.Notes).Vibrato;
        Assert.Equal(0.0, vibrato.LengthPercent, 9);
        Assert.Equal(175.0, vibrato.PeriodMilliseconds, 9);
        Assert.Equal(25.0, vibrato.DepthCents, 9);
        Assert.False(vibrato.IsEnabled);
    }

    [Fact]
    public void TheEnvelopeSetsTheFades()
    {
        var result = Import(Document("Tempo=120.00", Sung("Envelope=0,5,35,0,100,100,0")));

        var note = Assert.Single(result.Notes);
        Assert.Equal(UTAUNote.DefaultFadeInMilliseconds, note.FadeInMilliseconds, 9);
        Assert.Equal(UTAUNote.DefaultFadeOutMilliseconds, note.FadeOutMilliseconds, 9);
    }

    [Fact]
    public void TheEnvelopeCountsTheLeadingSilence()
    {
        var result = Import(Document("Tempo=120.00", Sung("Envelope=8,5,15,0,100,100,0")));

        var note = Assert.Single(result.Notes);
        Assert.Equal(13.0, note.FadeInMilliseconds, 9);
        Assert.Equal(15.0, note.FadeOutMilliseconds, 9);
    }

    [Fact]
    public void TheExtendedEnvelopeIsAccepted()
    {
        var result = Import(Document("Tempo=120.00", Sung("Envelope=0,10,22,0,73,53,0,%,0,102,89")));

        var note = Assert.Single(result.Notes);
        Assert.Equal(10.0, note.FadeInMilliseconds, 9);
        Assert.Equal(22.0, note.FadeOutMilliseconds, 9);
    }

    [Fact]
    public void ShortEnvelopesAreIgnored()
    {
        var result = Import(Document("Tempo=120.00", Sung("Envelope=1,2,3")));

        var note = Assert.Single(result.Notes);
        Assert.Equal(UTAUNote.DefaultFadeInMilliseconds, note.FadeInMilliseconds, 9);
        Assert.Equal(UTAUNote.DefaultFadeOutMilliseconds, note.FadeOutMilliseconds, 9);
    }

    [Fact]
    public void NumbersAreReadWithTheInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var result = Import(Document("Tempo=143.50", Sung("Velocity=150.5", "PBS=-50.5;20.5", "PBW=100.25")));

            Assert.Equal(143.5, result.Tempo, 9);
            var note = Assert.Single(result.Notes);
            Assert.Equal(150.5, note.Velocity, 9);
            Assert.Equal(205.0, note.PitchPoints[0].Cents, 9);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void GarbageNumbersFallBackWithoutThrowing()
    {
        var result = Import(Document("Tempo=120.00", Sung(
            "Velocity=abc",
            "Intensity=",
            "NoteNum=x9",
            "PBS=nope;also",
            "PBW=1,,2",
            "PBY=,,",
            "VBR=a,b,c",
            "Envelope=q,w,e,r,t,y,u")));

        var note = Assert.Single(result.Notes);
        Assert.Equal(100.0, note.Velocity, 9);
        Assert.Equal(100.0, note.Intensity, 9);
        Assert.Equal(MusicalTone.MiddleC.NoteNumber, note.Tone);
        Assert.Equal(4, note.PitchPoints.Count);
        Assert.All(note.PitchPoints, x => Assert.Equal(0.0, x.Cents, 9));
    }

    [Fact]
    public void HugeNumbersDoNotOverflow()
    {
        var result = Import(Document("Tempo=120.00", [
            "Length=99999999999999999999",
            "Lyric=a",
            "NoteNum=60",
            "PBS=-99999999999;99999",
            "PBW=99999999999",
        ]));

        var note = Assert.Single(result.Notes);
        Assert.Equal(UTAUNote.MaximumLengthTicks, note.LengthTicks);
        Assert.All(note.PitchPoints, x => Assert.InRange(x.Ticks, PitchPoint.MinimumTicks, PitchPoint.MaximumTicks));
        Assert.All(note.PitchPoints, x => Assert.InRange(x.Cents, PitchPoint.MinimumCents, PitchPoint.MaximumCents));
    }

    [Fact]
    public void TheMisspelledModulationIsAccepted()
    {
        var result = Import(Document("Tempo=120.00", Sung("Moduration=45")));

        Assert.Equal(45.0, Assert.Single(result.Notes).Modulation, 9);
    }

    [Fact]
    public void TheCorrectModulationSpellingWins()
    {
        var result = Import(Document("Tempo=120.00", Sung("Modulation=10", "Moduration=90")));

        Assert.Equal(10.0, Assert.Single(result.Notes).Modulation, 9);
    }

    [Fact]
    public void LegacyPitchIsCountedButNotImported()
    {
        var result = Import(Document("Tempo=120.00", Sung("PBType=5", "PBStart=-40", "PitchBend=0,1,2,3")));

        Assert.Empty(Assert.Single(result.Notes).PitchPoints);
        Assert.Equal(1, result.LegacyPitchNoteCount);
    }

    [Fact]
    public void LegacyPitchIsNotCountedWhenTheModernPitchExists()
    {
        var result = Import(Document(ExactTempo, Sung("PBS=0;0", "PBW=100", "PBType=5", "Pitches=0,1,2")));

        Assert.Equal(2, Assert.Single(result.Notes).PitchPoints.Count);
        Assert.Equal(0, result.LegacyPitchNoteCount);
    }

    [Fact]
    public void TheImportedPitchIsEvaluatedInsideTheNote()
    {
        var result = Import(Document(ExactTempo, Sung("PBS=-100;20", "PBW=200", "PBM=s")));

        var note = Assert.Single(result.Notes);
        Assert.Equal(100.0, note.EvaluatePortamentoCents(0.0), 6);
        Assert.Equal(0.0, note.EvaluatePortamentoCents(100.0), 6);
        Assert.Equal(0.0, note.EvaluatePortamentoCents(480.0), 6);
    }
}


public sealed class UstSourceTests
{
    [Theory]
    [InlineData(@"C:\songs\a.ust")]
    [InlineData(@"C:\ボイス\歌.ust")]
    [InlineData(@"C:\songs\a.UST")]
    [InlineData(@"  C:\songs\a.ust  ")]
    [InlineData(@"""C:\songs\a.ust""")]
    [InlineData(@"relative\a.ust")]
    public void UstPathsAreRecognised(string text)
    {
        Assert.True(UstSource.TryGetPath(text, out var path));
        Assert.EndsWith(".ust", path, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(path, path.Trim());
        Assert.DoesNotContain('"', path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("あいうえお")]
    [InlineData("a.ustx")]
    [InlineData("song.wav")]
    [InlineData(".ust")]
    [InlineData("<!C4:480>あ")]
    [InlineData("C:\\songs\\a.ust\r\nあ")]
    [InlineData("あ\nC:\\songs\\a.ust")]
    public void OtherTextIsNotAPath(string text)
    {
        Assert.False(UstSource.TryGetPath(text, out var path));
        Assert.Equal(string.Empty, path);
    }
}

public sealed class UstPronounceTests
{
    const string Sample = "[#VERSION]\r\nUST Version1.2\r\n[#SETTING]\r\nTempo=144.00\r\n"
        + "[#0000]\r\nLength=480\r\nLyric=か\r\nNoteNum=62\r\n"
        + "[#0001]\r\nLength=240\r\nLyric=き\r\nNoteNum=64\r\n"
        + "[#TRACKEND]\r\n";

    static string Write(string directory, string content)
    {
        var path = Path.Combine(directory, "sample.ust");
        File.WriteAllBytes(path, VoiceBankTextReader.ShiftJis.GetBytes(content));
        return path;
    }

    [Fact]
    public void TheNotesAndTempoComeFromTheFile()
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            var path = Write(directory, Sample);
            var pronounce = UTAUVoicePronounce.FromUst(path, new UTAUVoiceParameter { Speed = 1.5 });

            Assert.Equal(["か", "き"], pronounce.Notes.Select(x => x.Lyric).ToArray());
            Assert.Equal([480, 240], pronounce.Notes.Select(x => x.LengthTicks).ToArray());
            Assert.Equal(144.0, pronounce.Tempo, 9);
            Assert.Equal(1.5, pronounce.Speed, 9);
            Assert.Equal(path, pronounce.SourceText);
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }

    [Fact]
    public void AnUnreadableFileIsReported()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-file.ust");
        var error = Assert.Throws<InvalidOperationException>(
            () => UTAUVoicePronounce.FromUst(missing, new UTAUVoiceParameter()));

        Assert.Equal(Texts.UstImportFailed, error.Message);
    }

    [Fact]
    public void AFileWithoutNotesIsReported()
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            var path = Write(directory, "[#SETTING]\r\nTempo=120.00\r\n[#TRACKEND]\r\n");
            var error = Assert.Throws<InvalidOperationException>(
                () => UTAUVoicePronounce.FromUst(path, new UTAUVoiceParameter()));

            Assert.Equal(Texts.UstImportEmpty, error.Message);
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }

    [Fact]
    public void TheNoticeCountsTheNotes()
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            var pronounce = UTAUVoicePronounce.FromUst(Write(directory, Sample), new UTAUVoiceParameter());

            Assert.Equal(string.Format(Texts.UstImportedFormat, 2), pronounce.ImportMessage);
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }

    [Fact]
    public void TheNoticeListsWhatCouldNotBeImported()
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            var content = "[#SETTING]\r\nTempo=120.00\r\n"
                + "[#0000]\r\nLength=480\r\nLyric=R\r\nNoteNum=60\r\n"
                + "[#0001]\r\nLength=480\r\nLyric=か\r\nNoteNum=62\r\nPBType=5\r\nPitchBend=0,1,2\r\n"
                + "[#0002]\r\nLength=480\r\nLyric=き\r\nNoteNum=64\r\nTempo=150.00\r\n"
                + "[#TRACKEND]\r\n";
            var pronounce = UTAUVoicePronounce.FromUst(Write(directory, content), new UTAUVoiceParameter());

            Assert.Contains(Texts.UstTempoChangeIgnored, pronounce.ImportMessage);
            Assert.Contains(Texts.UstLegacyPitchIgnored, pronounce.ImportMessage);
            Assert.Contains(string.Format(Texts.UstRestTrimmedFormat, 480), pronounce.ImportMessage);
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ThePronunciationKeepsTheUstPathVerbatim()
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            var speaker = new UTAUVoiceSpeaker(TestVoiceBank.CreateSingleKanaBank(directory));
            var path = @"C:\ボイス\歌.ust";

            Assert.Equal(path, await speaker.ConvertKanjiToYomiAsync(path, new UTAUVoiceParameter()));
            Assert.Equal(path, await speaker.ConvertKanjiToYomiAsync($"  \"{path}\"  ", new UTAUVoiceParameter()));
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task PlainTextIsStillNormalised()
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            var speaker = new UTAUVoiceSpeaker(TestVoiceBank.CreateSingleKanaBank(directory));

            Assert.Equal("あいうえお", await speaker.ConvertKanjiToYomiAsync("アイウエオ", new UTAUVoiceParameter()));
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }
}

public sealed class UstRenderSourceTests
{
    static string WriteUst(string directory, double tempo)
    {
        var content = "[#SETTING]\r\n"
            + string.Format(CultureInfo.InvariantCulture, "Tempo={0:F2}\r\n", tempo)
            + "[#0000]\r\nLength=480\r\nLyric=あ\r\nNoteNum=60\r\n[#TRACKEND]\r\n";
        var path = Path.Combine(directory, string.Format(CultureInfo.InvariantCulture, "song{0:F0}.ust", tempo));
        File.WriteAllBytes(path, VoiceBankTextReader.ShiftJis.GetBytes(content));
        return path;
    }

    static async Task<double> RenderAsync(UTAUVoiceSpeaker speaker, string directory, string text, double parameterTempo)
    {
        var output = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".wav");
        var parameter = new UTAUVoiceParameter { Tempo = parameterTempo };
        await speaker.CreateVoiceAsync(text, null, parameter, output);
        return WaveIo.Read(output).DurationMilliseconds;
    }

    [Fact]
    public async Task TheFileTempoDrivesTheRenderOfAUstSource()
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            var speaker = new UTAUVoiceSpeaker(TestVoiceBank.CreateSingleKanaBank(directory));
            var path = WriteUst(directory, 240.0);

            var slow = await RenderAsync(speaker, directory, path, 60.0);
            var fast = await RenderAsync(speaker, directory, path, 400.0);

            Assert.Equal(slow, fast, 3);
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task TheParameterTempoStillDrivesTheRenderOfPlainText()
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            var speaker = new UTAUVoiceSpeaker(TestVoiceBank.CreateSingleKanaBank(directory));

            var slow = await RenderAsync(speaker, directory, "あ", 60.0);
            var fast = await RenderAsync(speaker, directory, "あ", 240.0);

            Assert.True(slow > fast * 1.5, $"slow={slow} fast={fast}");
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task TwoTemposInTheFileGiveDifferentLengths()
    {
        var directory = TestVoiceBank.CreateTemporaryDirectory();
        try
        {
            var speaker = new UTAUVoiceSpeaker(TestVoiceBank.CreateSingleKanaBank(directory));

            var slow = await RenderAsync(speaker, directory, WriteUst(directory, 60.0), 120.0);
            var fast = await RenderAsync(speaker, directory, WriteUst(directory, 240.0), 120.0);

            Assert.True(slow > fast * 1.5, $"slow={slow} fast={fast}");
        }
        finally
        {
            TestVoiceBank.DeleteDirectory(directory);
        }
    }
}
