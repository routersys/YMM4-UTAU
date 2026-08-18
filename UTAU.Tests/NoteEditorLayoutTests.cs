using System.Collections.ObjectModel;
using System.IO;
using UTAU.Notes;
using UTAU;
using UTAU.ViewModels;

namespace UTAU.Tests;

public sealed class NoteEditorLayoutTests
{
    static UTAUVoicePronounce CreatePronounce(ObservableCollection<UTAUNote> notes)
    {
        var pronounce = new UTAUVoicePronounce();
        foreach (var note in notes)
            pronounce.Notes.Add(note);
        return pronounce;
    }

    static ObservableCollection<UTAUNote> CreateNotes(int count, int lengthTicks = UTAUNote.DefaultLengthTicks)
    {
        var notes = new ObservableCollection<UTAUNote>();
        for (var i = 0; i < count; i++)
            notes.Add(new UTAUNote { Lyric = "あ", LengthTicks = lengthTicks, Tone = 60 });
        return notes;
    }

    [Fact]
    public void ShortPhrasesFillTheViewportWidth()
    {
        var viewModel = new NoteEditorViewModel(CreatePronounce(CreateNotes(8)));
        Assert.True(viewModel.FitToViewport(800.0, 400.0));
        Assert.InRange(viewModel.CanvasWidth, 800.0 * 0.9, 800.0);
    }

    [Fact]
    public void LongPhrasesAreNotZoomedBelowTheLowerBound()
    {
        var viewModel = new NoteEditorViewModel(CreatePronounce(CreateNotes(400)));
        viewModel.FitToViewport(800.0, 400.0);
        Assert.Equal(NoteEditorViewModel.MinimumPixelsPerTick, viewModel.PixelsPerTick, 12);
    }

    [Fact]
    public void VeryShortPhrasesAreNotZoomedAboveTheUpperBound()
    {
        var viewModel = new NoteEditorViewModel(CreatePronounce(CreateNotes(1, 60)));
        viewModel.FitToViewport(2000.0, 400.0);
        Assert.Equal(NoteEditorViewModel.MaximumPixelsPerTick, viewModel.PixelsPerTick, 12);
    }

    [Fact]
    public void TheToneRangeFillsTheViewportHeight()
    {
        var viewModel = new NoteEditorViewModel(CreatePronounce(CreateNotes(4)));
        viewModel.FitToViewport(800.0, 400.0);
        Assert.InRange(viewModel.CanvasHeight, 400.0 * 0.9, 400.0);
    }

    [Fact]
    public void WideToneRangesAreNotZoomedBelowTheLowerBound()
    {
        var notes = CreateNotes(2);
        notes[0].Tone = 24;
        notes[1].Tone = 96;
        var viewModel = new NoteEditorViewModel(CreatePronounce(notes));
        viewModel.FitToViewport(800.0, 200.0);
        Assert.Equal(NoteEditorViewModel.MinimumSemitoneHeight, viewModel.SemitoneHeight, 12);
    }

    [Fact]
    public void FittingIsSkippedWhenTheViewportBarelyChanges()
    {
        var viewModel = new NoteEditorViewModel(CreatePronounce(CreateNotes(8)));
        Assert.True(viewModel.FitToViewport(800.0, 400.0));
        Assert.False(viewModel.FitToViewport(801.0, 400.0));
        Assert.True(viewModel.FitToViewport(900.0, 400.0));
    }

    [Fact]
    public void ManualZoomStopsAutomaticFitting()
    {
        var viewModel = new NoteEditorViewModel(CreatePronounce(CreateNotes(8)));
        viewModel.FitToViewport(800.0, 400.0);
        viewModel.ZoomHorizontally(NoteEditorViewModel.ZoomStep);
        var zoomed = viewModel.PixelsPerTick;

        Assert.False(viewModel.FitToViewport(1200.0, 400.0));
        Assert.Equal(zoomed, viewModel.PixelsPerTick, 12);
    }

    [Fact]
    public void FittingResumesAfterItIsRequestedAgain()
    {
        var viewModel = new NoteEditorViewModel(CreatePronounce(CreateNotes(8)));
        viewModel.FitToViewport(800.0, 400.0);
        viewModel.ZoomHorizontally(NoteEditorViewModel.ZoomStep);
        viewModel.EnableAutoFit();

        Assert.True(viewModel.FitToViewport(800.0, 400.0));
        Assert.InRange(viewModel.CanvasWidth, 800.0 * 0.9, 800.0);
    }

    [Fact]
    public void EmptyNoteListsDoNotDivideByZero()
    {
        var viewModel = new NoteEditorViewModel(new UTAUVoicePronounce());
        viewModel.FitToViewport(800.0, 400.0);

        Assert.True(double.IsFinite(viewModel.PixelsPerTick));
        Assert.True(double.IsFinite(viewModel.SemitoneHeight));
        Assert.True(viewModel.CanvasWidth > 0.0);
        Assert.True(viewModel.CanvasHeight > 0.0);
    }

    [Fact]
    public void ZeroSizedViewportsAreIgnored()
    {
        var viewModel = new NoteEditorViewModel(CreatePronounce(CreateNotes(8)));
        var before = viewModel.PixelsPerTick;

        Assert.False(viewModel.FitToViewport(0.0, 400.0));
        Assert.False(viewModel.FitToViewport(800.0, 0.0));
        Assert.Equal(before, viewModel.PixelsPerTick, 12);
    }

    [Fact]
    public void NotesLineUpWithTheKeyboardRows()
    {
        var notes = CreateNotes(3);
        notes[0].Tone = 60;
        notes[1].Tone = 64;
        notes[2].Tone = 67;
        var viewModel = new NoteEditorViewModel(CreatePronounce(notes));
        viewModel.FitToViewport(800.0, 400.0);

        foreach (var note in viewModel.Notes)
        {
            var row = viewModel.Keyboard.Single(x => x.NoteNumber == note.Note.Tone);
            var rowTop = viewModel.Keyboard.TakeWhile(x => x.NoteNumber != note.Note.Tone).Sum(x => x.Height);

            Assert.Equal(rowTop, note.Top, 9);
            Assert.Equal(viewModel.SemitoneHeight, row.Height, 9);
        }
    }

    [Fact]
    public void NotesAreLaidOutBackToBackInTicks()
    {
        var viewModel = new NoteEditorViewModel(CreatePronounce(CreateNotes(4)));
        viewModel.FitToViewport(800.0, 400.0);

        var expected = 0;
        foreach (var note in viewModel.Notes)
        {
            Assert.Equal(expected, note.StartTicks);
            Assert.Equal(expected * viewModel.PixelsPerTick, note.Left, 9);
            expected += note.Note.LengthTicks;
        }
        Assert.Equal(expected, viewModel.TotalTicks);
    }
}
