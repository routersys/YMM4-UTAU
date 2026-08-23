using System.Windows.Threading;
using UTAU.Notes;
using UTAU.ViewModels;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Tests;

[Collection("NoteClipboard")]
public sealed class NoteCollectionSyncTests
{
    sealed class Recorder
    {
        readonly List<IUndoRedoCommand> commands = [];
        bool recording = true;

        public Recorder(UTAUVoicePronounce pronounce)
            => pronounce.UndoRedoCommandCreated += (_, e) =>
            {
                if (recording && e.Command is IUndoRedoCommand command)
                    commands.Add(command);
            };

        public int Recorded => commands.Count(x => !x.IsEmpty);

        public void UndoAll()
        {
            recording = false;
            for (var index = commands.Count - 1; index >= 0; index--)
            {
                if (!commands[index].IsEmpty)
                    commands[index].Undo();
            }
        }

        public void RedoAll()
        {
            recording = false;
            foreach (var command in commands)
            {
                if (!command.IsEmpty)
                    command.Redo();
            }
        }
    }

    static (UTAUVoicePronounce Pronounce, NoteEditorViewModel ViewModel) Create(params int[] tones)
    {
        var pronounce = new UTAUVoicePronounce();
        foreach (var tone in tones)
            pronounce.Notes.Add(new UTAUNote { Lyric = "あ", Tone = tone, LengthTicks = UTAUNote.DefaultLengthTicks });
        return (pronounce, new NoteEditorViewModel(pronounce));
    }

    static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            new Action(() => frame.Continue = false),
            DispatcherPriority.SystemIdle);
        Dispatcher.PushFrame(frame);
    }

    static void AssertMirrors(UTAUVoicePronounce pronounce, NoteEditorViewModel viewModel)
    {
        Pump();

        Assert.Equal(pronounce.Notes.Count, viewModel.Notes.Count);
        for (var index = 0; index < pronounce.Notes.Count; index++)
            Assert.Same(pronounce.Notes[index], viewModel.Notes[index].Note);

        foreach (var note in viewModel.Notes)
            Assert.Equal(viewModel.SelectedNotes.Contains(note), note.IsSelected);

        Assert.Equal(viewModel.SelectedNote is null ? 0 : 1, viewModel.Notes.Count(x => x.IsPrimary));
        Assert.Equal(viewModel.SelectedNotes.Count, viewModel.SelectedCount);

        foreach (var selected in viewModel.SelectedNotes)
            Assert.Contains(selected, viewModel.Notes);

        if (viewModel.SelectedNote is { } primary)
        {
            Assert.Contains(primary, viewModel.Notes);
            Assert.True(primary.IsPrimary);
        }

        var position = 0;
        foreach (var note in viewModel.Notes)
        {
            Assert.Equal(position, note.StartTicks);
            position += note.Note.LengthTicks;
        }

        Assert.Equal(position, viewModel.TotalTicks);
    }

    [Fact]
    public void UndoingARestInsertTakesItOutOfTheEditor()
    {
        var (pronounce, viewModel) = Create(60, 62);
        viewModel.Select(viewModel.Notes[0]);

        var recorder = new Recorder(pronounce);
        viewModel.InsertRestCommand.Execute(null);
        Assert.Equal(3, viewModel.Notes.Count);
        recorder.UndoAll();

        Assert.Equal(2, viewModel.Notes.Count);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void UndoingARemovalPutsTheNotesBackInTheEditor()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        viewModel.Select(viewModel.Notes[1]);

        var recorder = new Recorder(pronounce);
        viewModel.RemoveNoteCommand.Execute(null);
        Assert.Equal(2, viewModel.Notes.Count);
        recorder.UndoAll();

        Assert.Equal(3, viewModel.Notes.Count);
        Assert.Equal([60, 62, 64], viewModel.Notes.Select(x => x.Note.Tone));
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void UndoingAPasteTakesTheNotesOutOfTheEditor()
    {
        var (pronounce, viewModel) = Create(60, 62);
        viewModel.SelectAll();
        viewModel.CopyNotesCommand.Execute(null);

        var recorder = new Recorder(pronounce);
        viewModel.PasteNotesCommand.Execute(null);
        Assert.Equal(4, viewModel.Notes.Count);
        recorder.UndoAll();

        Assert.Equal(2, viewModel.Notes.Count);
        Assert.Equal([60, 62], viewModel.Notes.Select(x => x.Note.Tone));
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void RedoingAPasteBringsTheNotesBack()
    {
        var (pronounce, viewModel) = Create(60, 62);
        viewModel.SelectAll();
        viewModel.CopyNotesCommand.Execute(null);

        var recorder = new Recorder(pronounce);
        viewModel.PasteNotesCommand.Execute(null);
        recorder.UndoAll();
        recorder.RedoAll();

        Assert.Equal(4, viewModel.Notes.Count);
        Assert.Equal([60, 62, 60, 62], viewModel.Notes.Select(x => x.Note.Tone));
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void UndoingAPasteDropsTheSelectionItLeftBehind()
    {
        var (pronounce, viewModel) = Create(60, 62);
        viewModel.SelectAll();
        viewModel.CopyNotesCommand.Execute(null);

        var recorder = new Recorder(pronounce);
        viewModel.PasteNotesCommand.Execute(null);
        Assert.Equal(2, viewModel.SelectedCount);
        recorder.UndoAll();

        Assert.Equal(0, viewModel.SelectedCount);
        Assert.Null(viewModel.SelectedNote);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void UndoingARemovalLeavesTheSurvivingSelectionAlone()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        viewModel.Select(viewModel.Notes[0]);

        var recorder = new Recorder(pronounce);
        viewModel.RemoveNoteCommand.Execute(null);
        var kept = viewModel.SelectedNote;
        recorder.UndoAll();

        Assert.Same(kept, viewModel.SelectedNote);
        Assert.Equal(1, viewModel.SelectedCount);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void ANoteAddedFromOutsideShowsUpInTheEditor()
    {
        var (pronounce, viewModel) = Create(60, 62);

        pronounce.Notes.Insert(1, new UTAUNote { Lyric = "か", Tone = 64 });

        Assert.Equal(3, viewModel.Notes.Count);
        Assert.Equal([60, 64, 62], viewModel.Notes.Select(x => x.Note.Tone));
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void ANoteRemovedFromOutsideLeavesTheEditor()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        viewModel.SelectAll();

        pronounce.Notes.RemoveAt(1);

        Assert.Equal(2, viewModel.Notes.Count);
        Assert.Equal(2, viewModel.SelectedCount);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void ClearingTheScoreFromOutsideEmptiesTheEditor()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        viewModel.SelectAll();

        pronounce.Notes.Clear();

        Assert.Empty(viewModel.Notes);
        Assert.Equal(0, viewModel.SelectedCount);
        Assert.Null(viewModel.SelectedNote);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void TheEditorKeepsTheViewModelOfANoteThatStays()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        var kept = viewModel.Notes[2];

        pronounce.Notes.RemoveAt(0);

        Assert.Same(kept, viewModel.Notes[1]);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void MovingANoteFromOutsideReordersTheEditor()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        var moved = viewModel.Notes[0];

        pronounce.Notes.Move(0, 2);

        Assert.Equal([62, 64, 60], viewModel.Notes.Select(x => x.Note.Tone));
        Assert.Same(moved, viewModel.Notes[2]);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void TheEditorStopsListeningOnceDisposed()
    {
        var (pronounce, viewModel) = Create(60, 62);
        viewModel.Dispose();

        pronounce.Notes.Add(new UTAUNote { Lyric = "か", Tone = 64 });

        Assert.Empty(viewModel.Notes);
    }

    [Fact]
    public void ADroppedNoteStopsDrivingTheEditor()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        var dropped = pronounce.Notes[1];
        pronounce.Notes.RemoveAt(1);

        dropped.Tone = 80;

        Assert.Equal([60, 64], viewModel.Notes.Select(x => x.Note.Tone));
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void TheNoteListMirrorsAtOnceAndThePositionsFollowOnTheDispatcher()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);

        pronounce.Notes.RemoveAt(0);

        Assert.Equal(2, viewModel.Notes.Count);
        Assert.Equal([62, 64], viewModel.Notes.Select(x => x.Note.Tone));
        Assert.Equal([UTAUNote.DefaultLengthTicks, UTAUNote.DefaultLengthTicks * 2], viewModel.Notes.Select(x => x.StartTicks));

        Pump();

        Assert.Equal([0, UTAUNote.DefaultLengthTicks], viewModel.Notes.Select(x => x.StartTicks));
        Assert.Equal(UTAUNote.DefaultLengthTicks * 2, viewModel.TotalTicks);
    }

    [Fact]
    public void ABurstOfChangesCostsOneLayoutPass()
    {
        var (pronounce, viewModel) = Create(60, 62, 64, 65, 67);
        Pump();
        var passes = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NoteEditorViewModel.TotalTicks))
                passes++;
        };

        for (var index = 0; index < 4; index++)
            pronounce.Notes.RemoveAt(0);
        Pump();

        Assert.Equal(1, passes);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void ScrollingCatchesUpAPendingLayout()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);

        pronounce.Notes.RemoveAt(0);
        viewModel.SetViewport(0.0, 400.0);

        Assert.Equal([0, UTAUNote.DefaultLengthTicks], viewModel.Notes.Select(x => x.StartTicks));
        Assert.Equal(UTAUNote.DefaultLengthTicks * 2, viewModel.TotalTicks);
    }

    [Fact]
    public void OurOwnEditsLeaveNothingPendingForTheDispatcher()
    {
        var (pronounce, viewModel) = Create(60, 62);
        viewModel.Select(viewModel.Notes[0]);

        viewModel.SnapDivision = new NoteDivision(16);
        viewModel.InsertRestCommand.Execute(null);

        Assert.Equal(3, viewModel.Notes.Count);
        Assert.Equal([240, 120, 240], viewModel.Notes.Select(x => x.Note.LengthTicks));
        Assert.Equal([0, 240, 360], viewModel.Notes.Select(x => x.StartTicks));
        Assert.Equal(600, viewModel.TotalTicks);
    }

    [Fact]
    public void RemovingManySelectedNotesResynchronisesOnlyOnce()
    {
        var (pronounce, viewModel) = Create(60, 62, 64, 65, 67, 69);
        var rebuilds = 0;
        viewModel.Notes.CollectionChanged += (_, _) => rebuilds++;
        viewModel.Select(viewModel.Notes[1]);
        viewModel.ToggleSelection(viewModel.Notes[2]);
        viewModel.ToggleSelection(viewModel.Notes[3]);

        viewModel.RemoveNoteCommand.Execute(null);

        Assert.Equal(3, viewModel.Notes.Count);
        Assert.Equal(1 + 3, rebuilds);
        AssertMirrors(pronounce, viewModel);
    }
}
