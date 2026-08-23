using UTAU;
using UTAU.Notes;
using UTAU.ViewModels;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Tests;

[Collection("NoteClipboard")]
public sealed class NoteBatchEditTests
{
    sealed class Recorder
    {
        readonly List<IUndoRedoCommand> commands = [];

        public Recorder(UTAUVoicePronounce pronounce)
            => pronounce.UndoRedoCommandCreated += (_, e) =>
            {
                if (e.Command is IUndoRedoCommand command)
                    commands.Add(command);
            };

        public int Recorded => commands.Count(x => !x.IsEmpty);

        public void UndoAll()
        {
            for (var index = commands.Count - 1; index >= 0; index--)
            {
                if (!commands[index].IsEmpty)
                    commands[index].Undo();
            }
        }
    }

    static NoteEditorViewModel CreateViewModel(UTAUVoicePronounce pronounce, params int[] tones)
    {
        foreach (var tone in tones)
            pronounce.Notes.Add(new UTAUNote { Lyric = "あ", Tone = tone, LengthTicks = UTAUNote.DefaultLengthTicks });
        return new NoteEditorViewModel(pronounce);
    }

    static NoteEditorViewModel CreateViewModel(params int[] tones)
        => CreateViewModel(new UTAUVoicePronounce(), tones);

    static void SelectAllOf(NoteEditorViewModel viewModel) => viewModel.SelectAll();

    [Fact]
    public void RaisingAnOctaveAddsTwelveSemitonesToEverySelectedNote()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        SelectAllOf(viewModel);

        viewModel.OctaveUpCommand.Execute(null);

        Assert.Equal([72, 74, 76], viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void LoweringAnOctaveSubtractsTwelveSemitonesFromEverySelectedNote()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        SelectAllOf(viewModel);

        viewModel.OctaveDownCommand.Execute(null);

        Assert.Equal([48, 50, 52], viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void OnlyTheSelectedNotesMove()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.Select(viewModel.Notes[1]);

        viewModel.OctaveUpCommand.Execute(null);

        Assert.Equal([60, 74, 64], viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void TheWholeShiftShrinksSoThatTheIntervalsSurvive()
    {
        var viewModel = CreateViewModel(100, 118);
        SelectAllOf(viewModel);

        viewModel.OctaveUpCommand.Execute(null);

        Assert.Equal([109, 127], viewModel.Notes.Select(x => x.Note.Tone));
        Assert.Equal(18, viewModel.Notes[1].Note.Tone - viewModel.Notes[0].Note.Tone);
    }

    [Fact]
    public void ANoteAlreadyAtTheTopOfTheRangeStopsTheWholeSelection()
    {
        var viewModel = CreateViewModel(60, 127);
        SelectAllOf(viewModel);

        viewModel.OctaveUpCommand.Execute(null);

        Assert.Equal([60, 127], viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void UndoingAnOctaveBringsEveryToneBack()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60, 62, 64);
        SelectAllOf(viewModel);

        var recorder = new Recorder(pronounce);
        viewModel.OctaveUpCommand.Execute(null);
        Assert.Equal(3, recorder.Recorded);
        recorder.UndoAll();

        Assert.Equal([60, 62, 64], viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void QuantizingRoundsEveryLengthToTheSnapDivision()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.SnapDivision = new NoteDivision(16);
        viewModel.Notes[0].Note.LengthTicks = 130;
        viewModel.Notes[1].Note.LengthTicks = 180;
        viewModel.Notes[2].Note.LengthTicks = 470;
        SelectAllOf(viewModel);

        viewModel.QuantizeLengthCommand.Execute(null);

        Assert.Equal([120, 240, 480], viewModel.Notes.Select(x => x.Note.LengthTicks));
    }

    [Fact]
    public void QuantizingNeverProducesANoteShorterThanTheMinimum()
    {
        var viewModel = CreateViewModel(60);
        viewModel.SnapDivision = new NoteDivision(16);
        viewModel.Notes[0].Note.LengthTicks = 40;
        SelectAllOf(viewModel);

        viewModel.QuantizeLengthCommand.Execute(null);

        Assert.Equal(UTAUNote.MinimumLengthTicks, viewModel.Notes[0].Note.LengthTicks);
    }

    [Fact]
    public void QuantizingDoesNothingWhileTheSnapIsFree()
    {
        var viewModel = CreateViewModel(60);
        viewModel.SnapDivision = NoteDivision.Free;
        viewModel.Notes[0].Note.LengthTicks = 130;
        SelectAllOf(viewModel);

        Assert.False(viewModel.QuantizeLengthCommand.CanExecute(null));
        viewModel.QuantizeLengthCommand.Execute(null);

        Assert.Equal(130, viewModel.Notes[0].Note.LengthTicks);
    }

    [Fact]
    public void TheFreeSnapLeavesEveryValidLengthWhereItIs()
    {
        var viewModel = CreateViewModel(60);
        viewModel.SnapDivision = NoteDivision.Free;

        foreach (var length in new[]
        {
            UTAUNote.MinimumLengthTicks, 16, 47, 130, 479, 480, 481, 1919, UTAUNote.MaximumLengthTicks,
        })
        {
            Assert.Equal(length, viewModel.SnapLength(length));
        }
    }

    [Fact]
    public void UndoingAQuantizeBringsEveryLengthBack()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60, 62);
        viewModel.SnapDivision = new NoteDivision(16);
        viewModel.Notes[0].Note.LengthTicks = 130;
        viewModel.Notes[1].Note.LengthTicks = 180;
        SelectAllOf(viewModel);

        var recorder = new Recorder(pronounce);
        viewModel.QuantizeLengthCommand.Execute(null);
        recorder.UndoAll();

        Assert.Equal([130, 180], viewModel.Notes.Select(x => x.Note.LengthTicks));
    }

    [Fact]
    public void ResettingTheVibratoRestoresEveryField()
    {
        var viewModel = CreateViewModel(60, 62);
        var vibrato = viewModel.Notes[0].Note.Vibrato;
        vibrato.LengthPercent = 60.0;
        vibrato.PeriodMilliseconds = 300.0;
        vibrato.DepthCents = 90.0;
        vibrato.FadeInPercent = 40.0;
        vibrato.FadeOutPercent = 50.0;
        vibrato.PhasePercent = -30.0;
        vibrato.OffsetPercent = 20.0;
        SelectAllOf(viewModel);

        viewModel.ResetVibratoCommand.Execute(null);

        Assert.Equal(0.0, vibrato.LengthPercent);
        Assert.Equal(175.0, vibrato.PeriodMilliseconds);
        Assert.Equal(25.0, vibrato.DepthCents);
        Assert.Equal(20.0, vibrato.FadeInPercent);
        Assert.Equal(20.0, vibrato.FadeOutPercent);
        Assert.Equal(0.0, vibrato.PhasePercent);
        Assert.Equal(0.0, vibrato.OffsetPercent);
        Assert.False(vibrato.IsEnabled);
    }

    [Fact]
    public void UndoingAVibratoResetBringsEveryFieldBack()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60);
        var vibrato = viewModel.Notes[0].Note.Vibrato;
        vibrato.LengthPercent = 60.0;
        vibrato.DepthCents = 90.0;
        SelectAllOf(viewModel);

        var recorder = new Recorder(pronounce);
        viewModel.ResetVibratoCommand.Execute(null);
        recorder.UndoAll();

        Assert.Equal(60.0, vibrato.LengthPercent);
        Assert.Equal(90.0, vibrato.DepthCents);
    }

    [Fact]
    public void ResettingTheTimingRestoresTheOverridesAndTheFades()
    {
        var viewModel = CreateViewModel(60);
        var note = viewModel.Notes[0].Note;
        note.PreutteranceOverride = 120.0;
        note.OverlapOverride = 60.0;
        note.StartPointMilliseconds = 30.0;
        note.FadeInMilliseconds = 90.0;
        note.FadeOutMilliseconds = 90.0;
        SelectAllOf(viewModel);

        viewModel.ResetTimingCommand.Execute(null);

        Assert.Equal(UTAUNote.FollowOtoValue, note.PreutteranceOverride);
        Assert.Equal(UTAUNote.FollowOtoValue, note.OverlapOverride);
        Assert.Equal(UTAUNote.DefaultStartPointMilliseconds, note.StartPointMilliseconds);
        Assert.Equal(UTAUNote.DefaultFadeInMilliseconds, note.FadeInMilliseconds);
        Assert.Equal(UTAUNote.DefaultFadeOutMilliseconds, note.FadeOutMilliseconds);
    }

    [Fact]
    public void ResettingTheTimingLeavesTheLyricAndTheToneAndTheLength()
    {
        var viewModel = CreateViewModel(64);
        var note = viewModel.Notes[0].Note;
        note.LengthTicks = 321;
        note.PreutteranceOverride = 120.0;
        SelectAllOf(viewModel);

        viewModel.ResetTimingCommand.Execute(null);

        Assert.Equal("あ", note.Lyric);
        Assert.Equal(64, note.Tone);
        Assert.Equal(321, note.LengthTicks);
    }

    [Fact]
    public void ResettingANoteClearsEveryParameterAndEveryPitchPoint()
    {
        var viewModel = CreateViewModel(64);
        var note = viewModel.Notes[0].Note;
        note.LengthTicks = 321;
        note.TempoOverride = 150.0;
        note.Velocity = 40.0;
        note.Intensity = 160.0;
        note.Modulation = -80.0;
        note.PreutteranceOverride = 120.0;
        note.Vibrato.LengthPercent = 70.0;
        note.PitchPoints.Add(new PitchPoint(0, -200.0));
        note.PitchPoints.Add(new PitchPoint(120, 300.0, PitchPointShape.Linear));
        SelectAllOf(viewModel);

        viewModel.ResetNoteCommand.Execute(null);

        Assert.Equal(UTAUNote.FollowScoreValue, note.TempoOverride);
        Assert.Equal(UTAUNote.DefaultVelocity, note.Velocity);
        Assert.Equal(UTAUNote.DefaultIntensity, note.Intensity);
        Assert.Equal(UTAUNote.DefaultModulation, note.Modulation);
        Assert.Equal(UTAUNote.FollowOtoValue, note.PreutteranceOverride);
        Assert.Equal(0.0, note.Vibrato.LengthPercent);
        Assert.Empty(note.PitchPoints);
        Assert.Equal("あ", note.Lyric);
        Assert.Equal(64, note.Tone);
        Assert.Equal(321, note.LengthTicks);
    }

    [Fact]
    public void UndoingANoteResetBringsThePitchPointsAndTheParametersBack()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60);
        var note = viewModel.Notes[0].Note;
        note.Velocity = 40.0;
        note.PitchPoints.Add(new PitchPoint(0, -200.0));
        note.PitchPoints.Add(new PitchPoint(120, 300.0, PitchPointShape.Linear));
        SelectAllOf(viewModel);

        var recorder = new Recorder(pronounce);
        viewModel.ResetNoteCommand.Execute(null);
        recorder.UndoAll();

        Assert.Equal(40.0, note.Velocity);
        Assert.Equal(2, note.PitchPoints.Count);
        Assert.Equal([0, 120], note.PitchPoints.Select(x => x.Ticks));
        Assert.Equal([-200.0, 300.0], note.PitchPoints.Select(x => x.Cents));
    }

    [Fact]
    public void PastingPutsTheCopiedNotesAfterTheSelection()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60, 62, 64);
        viewModel.Select(viewModel.Notes[0]);

        viewModel.CopyNotesCommand.Execute(null);
        viewModel.PasteNotesCommand.Execute(null);

        Assert.Equal([60, 60, 62, 64], viewModel.Notes.Select(x => x.Note.Tone));
        Assert.Equal([60, 60, 62, 64], pronounce.Notes.Select(x => x.Tone));
    }

    [Fact]
    public void CopyingTakesTheNotesInScoreOrderNotInTheOrderTheyWereClicked()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.Select(viewModel.Notes[2]);
        viewModel.ToggleSelection(viewModel.Notes[0]);

        viewModel.CopyNotesCommand.Execute(null);
        viewModel.PasteNotesCommand.Execute(null);

        Assert.Equal([60, 62, 64, 60, 64], viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void PastingKeepsTheOrderOfTheCopiedNotes()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.SelectAll();

        viewModel.CopyNotesCommand.Execute(null);
        viewModel.PasteNotesCommand.Execute(null);

        Assert.Equal([60, 62, 64, 60, 62, 64], viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void PastingSelectsWhatItPastedAndNothingElse()
    {
        var viewModel = CreateViewModel(60, 62);
        viewModel.Select(viewModel.Notes[0]);

        viewModel.CopyNotesCommand.Execute(null);
        viewModel.PasteNotesCommand.Execute(null);

        Assert.Equal(1, viewModel.SelectedCount);
        Assert.Same(viewModel.Notes[1], viewModel.SelectedNote);
        Assert.True(viewModel.Notes[1].IsSelected);
        Assert.False(viewModel.Notes[0].IsSelected);
    }

    [Fact]
    public void ACopiedNoteCarriesItsParametersAndItsPitchPoints()
    {
        var viewModel = CreateViewModel(60, 62);
        var note = viewModel.Notes[0].Note;
        note.Lyric = "か";
        note.LengthTicks = 321;
        note.Velocity = 40.0;
        note.Vibrato.LengthPercent = 70.0;
        note.PitchPoints.Add(new PitchPoint(60, -150.0, PitchPointShape.Linear));
        viewModel.Select(viewModel.Notes[0]);

        viewModel.CopyNotesCommand.Execute(null);
        viewModel.PasteNotesCommand.Execute(null);

        var pasted = viewModel.Notes[1].Note;
        Assert.NotSame(note, pasted);
        Assert.Equal("か", pasted.Lyric);
        Assert.Equal(321, pasted.LengthTicks);
        Assert.Equal(40.0, pasted.Velocity);
        Assert.Equal(70.0, pasted.Vibrato.LengthPercent);
        Assert.Equal([60], pasted.PitchPoints.Select(x => x.Ticks));
        Assert.Equal([-150.0], pasted.PitchPoints.Select(x => x.Cents));
        Assert.Equal([PitchPointShape.Linear], pasted.PitchPoints.Select(x => x.Shape));
    }

    [Fact]
    public void EditingAPastedNoteLeavesTheOriginalAlone()
    {
        var viewModel = CreateViewModel(60, 62);
        viewModel.Select(viewModel.Notes[0]);
        viewModel.CopyNotesCommand.Execute(null);
        viewModel.PasteNotesCommand.Execute(null);

        viewModel.Notes[1].Note.Lyric = "き";

        Assert.Equal("あ", viewModel.Notes[0].Note.Lyric);
    }

    [Fact]
    public void PastingTwiceMakesTwoIndependentNotes()
    {
        var viewModel = CreateViewModel(60);
        viewModel.Select(viewModel.Notes[0]);
        viewModel.CopyNotesCommand.Execute(null);

        viewModel.PasteNotesCommand.Execute(null);
        viewModel.PasteNotesCommand.Execute(null);

        Assert.Equal(3, viewModel.Notes.Count);
        Assert.NotSame(viewModel.Notes[1].Note, viewModel.Notes[2].Note);
    }

    [Fact]
    public void UndoingAPasteTakesTheNotesOutOfTheScore()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60, 62);
        viewModel.SelectAll();
        viewModel.CopyNotesCommand.Execute(null);

        var recorder = new Recorder(pronounce);
        viewModel.PasteNotesCommand.Execute(null);
        Assert.Equal(4, pronounce.Notes.Count);
        recorder.UndoAll();

        Assert.Equal(2, pronounce.Notes.Count);
        Assert.Equal([60, 62], pronounce.Notes.Select(x => x.Tone));
    }

    [Fact]
    public void CopyingNothingLeavesTheClipboardAlone()
    {
        var viewModel = CreateViewModel(60);
        viewModel.Select(viewModel.Notes[0]);
        viewModel.CopyNotesCommand.Execute(null);
        viewModel.Select(null);

        viewModel.CopyNotesCommand.Execute(null);
        viewModel.PasteNotesCommand.Execute(null);

        Assert.Equal(2, viewModel.Notes.Count);
    }

    [Fact]
    public void PastingWithNothingSelectedAppendsToTheEnd()
    {
        var viewModel = CreateViewModel(60, 62);
        viewModel.Select(viewModel.Notes[0]);
        viewModel.CopyNotesCommand.Execute(null);
        viewModel.Select(null);

        viewModel.PasteNotesCommand.Execute(null);

        Assert.Equal([60, 62, 60], viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void EveryBatchOperationNeedsASelection()
    {
        var viewModel = CreateViewModel(60);
        viewModel.Select(null);

        Assert.False(viewModel.OctaveUpCommand.CanExecute(null));
        Assert.False(viewModel.OctaveDownCommand.CanExecute(null));
        Assert.False(viewModel.QuantizeLengthCommand.CanExecute(null));
        Assert.False(viewModel.ResetVibratoCommand.CanExecute(null));
        Assert.False(viewModel.ResetTimingCommand.CanExecute(null));
        Assert.False(viewModel.ResetNoteCommand.CanExecute(null));
        Assert.False(viewModel.CopyNotesCommand.CanExecute(null));
    }

    [Fact]
    public void ThePositionsFollowTheLengthsAfterAQuantize()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.SnapDivision = new NoteDivision(16);
        viewModel.Notes[0].Note.LengthTicks = 130;
        viewModel.Notes[1].Note.LengthTicks = 180;
        SelectAllOf(viewModel);

        viewModel.QuantizeLengthCommand.Execute(null);

        Assert.Equal([0, 120, 360], viewModel.Notes.Select(x => x.StartTicks));
    }
}
