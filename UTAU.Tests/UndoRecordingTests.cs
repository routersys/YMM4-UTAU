using UTAU.Notes;
using UTAU.ViewModels;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Tests;

public sealed class UndoRecordingTests
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

    [Fact]
    public void ResettingThePitchRecordsEveryRemovedPoint()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60, 62);
        var note = viewModel.Notes[0].Note;
        note.PitchPoints.Add(new PitchPoint(0, -200.0));
        note.PitchPoints.Add(new PitchPoint(120, 300.0, PitchPointShape.Linear));
        viewModel.Select(viewModel.Notes[0]);

        var recorder = new Recorder(pronounce);
        viewModel.ResetPitchCommand.Execute(null);

        Assert.Empty(note.PitchPoints);
        Assert.Equal(2, recorder.Recorded);
    }

    [Fact]
    public void UndoingAPitchResetBringsThePointsBackInOrder()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60, 62);
        var note = viewModel.Notes[0].Note;
        note.PitchPoints.Add(new PitchPoint(0, -200.0));
        note.PitchPoints.Add(new PitchPoint(120, 300.0, PitchPointShape.Linear));
        note.PitchPoints.Add(new PitchPoint(240, 0.0, PitchPointShape.RCurve));
        viewModel.Select(viewModel.Notes[0]);

        var recorder = new Recorder(pronounce);
        viewModel.ResetPitchCommand.Execute(null);
        recorder.UndoAll();

        Assert.Equal(3, note.PitchPoints.Count);
        Assert.Equal([0, 120, 240], note.PitchPoints.Select(x => x.Ticks));
        Assert.Equal([-200.0, 300.0, 0.0], note.PitchPoints.Select(x => x.Cents));
        Assert.Equal(
            [PitchPointShape.SCurve, PitchPointShape.Linear, PitchPointShape.RCurve],
            note.PitchPoints.Select(x => x.Shape));
    }

    [Fact]
    public void ResettingThePitchOfEverySelectedNoteIsRecorded()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60, 62, 64);
        foreach (var note in viewModel.Notes)
        {
            note.Note.PitchPoints.Add(new PitchPoint(0, 100.0));
            note.Note.PitchPoints.Add(new PitchPoint(120, -100.0));
        }

        viewModel.SelectAll();
        var recorder = new Recorder(pronounce);
        viewModel.ResetPitchCommand.Execute(null);
        recorder.UndoAll();

        Assert.All(viewModel.Notes, x => Assert.Equal(2, x.Note.PitchPoints.Count));
    }

    [Fact]
    public void RemovingASinglePitchPointStaysRecorded()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, 60);
        var note = viewModel.Notes[0].Note;
        note.PitchPoints.Add(new PitchPoint(0, 50.0));
        viewModel.Select(viewModel.Notes[0]);

        var recorder = new Recorder(pronounce);
        viewModel.RemovePitchPoint(note.PitchPoints[0]);

        Assert.Equal(1, recorder.Recorded);
        recorder.UndoAll();
        Assert.Single(note.PitchPoints);
    }
}
