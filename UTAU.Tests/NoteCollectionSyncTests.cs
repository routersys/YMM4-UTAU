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

    static void AssertSameShapeAsAFreshEditor(UTAUVoicePronounce pronounce, NoteEditorViewModel viewModel)
    {
        Pump();
        using var fresh = new NoteEditorViewModel(pronounce);

        Assert.Equal(fresh.Notes.Select(x => x.Note), viewModel.Notes.Select(x => x.Note));
        Assert.Equal(fresh.Notes.Select(x => x.StartTicks), viewModel.Notes.Select(x => x.StartTicks));
        Assert.Equal(fresh.VisibleNotes.Select(x => x.Note), viewModel.VisibleNotes.Select(x => x.Note));
        Assert.Equal(fresh.TotalTicks, viewModel.TotalTicks);
        Assert.Equal(fresh.MinimumTone, viewModel.MinimumTone);
        Assert.Equal(fresh.MaximumTone, viewModel.MaximumTone);
        Assert.Equal(fresh.CanvasWidth, viewModel.CanvasWidth);
        Assert.Equal(fresh.CanvasHeight, viewModel.CanvasHeight);
        Assert.Equal(fresh.Keyboard.Count, viewModel.Keyboard.Count);
        Assert.Equal(fresh.TimeGridLines.Count, viewModel.TimeGridLines.Count);
        Assert.Equal(fresh.PitchCurve.Count, viewModel.PitchCurve.Count);
        Assert.Equal(fresh.ExpressionBars.Count, viewModel.ExpressionBars.Count);
    }

    [Fact]
    public void AnyRunOfChangesLeavesTheEditorWhereAFreshOneWouldBe()
    {
        var random = new Random(20260823);
        for (var trial = 0; trial < 200; trial++)
        {
            var (pronounce, viewModel) = Create(60, 62, 64, 65);
            var steps = random.Next(1, 9);
            for (var step = 0; step < steps; step++)
            {
                switch (random.Next(6))
                {
                    case 0:
                        pronounce.Notes.Insert(
                            random.Next(pronounce.Notes.Count + 1),
                            new UTAUNote { Lyric = "か", Tone = 50 + random.Next(30), LengthTicks = 60 + random.Next(400) });
                        break;
                    case 1 when pronounce.Notes.Count > 1:
                        pronounce.Notes.RemoveAt(random.Next(pronounce.Notes.Count));
                        break;
                    case 2 when pronounce.Notes.Count > 1:
                        pronounce.Notes.Move(random.Next(pronounce.Notes.Count), random.Next(pronounce.Notes.Count));
                        break;
                    case 3:
                        pronounce.Notes[random.Next(pronounce.Notes.Count)].LengthTicks = 60 + random.Next(400);
                        break;
                    case 4:
                        viewModel.Select(viewModel.Notes[random.Next(viewModel.Notes.Count)]);
                        viewModel.InsertRestCommand.Execute(null);
                        break;
                    default:
                        if (viewModel.Notes.Count > 1)
                        {
                            viewModel.Select(viewModel.Notes[random.Next(viewModel.Notes.Count)]);
                            viewModel.RemoveNoteCommand.Execute(null);
                        }

                        break;
                }
            }

            AssertSameShapeAsAFreshEditor(pronounce, viewModel);
            AssertMirrors(pronounce, viewModel);
            viewModel.Dispose();
        }
    }

    [Fact]
    public void TheLayoutCatchesUpBeforeAnythingDrawsTheEditor()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        Pump();
        var order = new List<string>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NoteEditorViewModel.TotalTicks))
                order.Add("layout");
        };

        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => order.Add("draw")), DispatcherPriority.Render);
        pronounce.Notes.RemoveAt(0);
        Pump();

        Assert.Equal(["layout", "draw"], order);
    }

    [Fact]
    public void PastingIntoAnEditorThatWasEmptiedFromOutsideWorks()
    {
        var (pronounce, viewModel) = Create(60, 62);
        viewModel.SelectAll();
        viewModel.CopyNotesCommand.Execute(null);

        pronounce.Notes.Clear();
        viewModel.PasteNotesCommand.Execute(null);

        Assert.Equal([60, 62], viewModel.Notes.Select(x => x.Note.Tone));
        Assert.Equal(2, viewModel.SelectedCount);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void ADroppedNoteNeverStaysInTheDrawnList()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        Pump();
        var dropped = viewModel.Notes[1];
        Assert.Contains(dropped, viewModel.VisibleNotes);

        pronounce.Notes.RemoveAt(1);

        Assert.DoesNotContain(dropped, viewModel.VisibleNotes);
        Assert.All(viewModel.VisibleNotes, x => Assert.Contains(x, viewModel.Notes));
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void AChangeFromAnotherThreadIsBroughtBackToTheEditorThread()
    {
        var (pronounce, viewModel) = Create(60, 62);
        Pump();

        var thread = new Thread(() => pronounce.Notes.Add(new UTAUNote { Lyric = "か", Tone = 64 }));
        thread.Start();
        thread.Join();

        Assert.Equal(2, viewModel.Notes.Count);

        Pump();

        Assert.Equal(3, viewModel.Notes.Count);
        AssertMirrors(pronounce, viewModel);
    }

    [Fact]
    public void AChangeThatArrivesAfterDisposalIsIgnored()
    {
        var (pronounce, viewModel) = Create(60, 62);
        var thread = new Thread(() => pronounce.Notes.Add(new UTAUNote { Lyric = "か", Tone = 64 }));
        thread.Start();
        thread.Join();

        viewModel.Dispose();
        Pump();

        Assert.Empty(viewModel.Notes);
        Assert.Equal(3, pronounce.Notes.Count);
    }

    [Fact]
    public void DisposingTwiceIsHarmless()
    {
        var (pronounce, viewModel) = Create(60, 62);

        viewModel.Dispose();
        viewModel.Dispose();

        Assert.Empty(viewModel.Notes);
        Assert.Equal(2, pronounce.Notes.Count);
    }

    [Fact]
    public void ARedundantChangeCostsNoLayoutPass()
    {
        var (pronounce, viewModel) = Create(60, 62, 64);
        Pump();
        var passes = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NoteEditorViewModel.TotalTicks))
                passes++;
        };

        pronounce.Notes.Move(1, 1);
        Pump();

        Assert.Equal(0, passes);
        AssertMirrors(pronounce, viewModel);
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
