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

public sealed class ScaleUpdateTests
{
    static NoteEditorViewModel CreateViewModel(int noteCount, bool withVibrato)
    {
        var pronounce = new UTAUVoicePronounce();
        for (var index = 0; index < noteCount; index++)
        {
            var note = new UTAUNote { Lyric = "あ", Tone = 60 + index % 7, LengthTicks = 480 };
            if (withVibrato)
            {
                note.Vibrato.LengthPercent = 80.0;
                note.Vibrato.DepthCents = 200.0;
                note.Vibrato.FadeInPercent = 0.0;
                note.Vibrato.FadeOutPercent = 0.0;
            }
            pronounce.Notes.Add(note);
        }
        return new NoteEditorViewModel(pronounce);
    }

    static (double Lowest, double Highest) ToneRange(NoteEditorViewModel viewModel)
    {
        var lowest = double.MaxValue;
        var highest = double.MinValue;
        foreach (var point in viewModel.PitchCurve)
        {
            var tone = viewModel.MaximumTone + 0.5 - point.Y / viewModel.SemitoneHeight;
            lowest = Math.Min(lowest, tone);
            highest = Math.Max(highest, tone);
        }
        return (lowest, highest);
    }

    [Fact]
    public void ScalingReusesTheKeyboardAndGridObjects()
    {
        var viewModel = CreateViewModel(60, false);
        var key = viewModel.Keyboard[0];
        var line = viewModel.TimeGridLines[0];
        var bar = viewModel.ExpressionBars[0];
        var keyCount = viewModel.Keyboard.Count;
        var lineCount = viewModel.TimeGridLines.Count;

        viewModel.ZoomHorizontally(NoteEditorViewModel.ZoomStep);
        viewModel.ZoomVertically(NoteEditorViewModel.ZoomStep);

        Assert.Same(key, viewModel.Keyboard[0]);
        Assert.Same(line, viewModel.TimeGridLines[0]);
        Assert.Same(bar, viewModel.ExpressionBars[0]);
        Assert.Equal(keyCount, viewModel.Keyboard.Count);
        Assert.Equal(lineCount, viewModel.TimeGridLines.Count);
    }

    [Fact]
    public void ScalingUpdatesTheGeometryOfTheReusedObjects()
    {
        var viewModel = CreateViewModel(60, false);
        var line = viewModel.TimeGridLines[0];
        var left = line.Left;
        var height = viewModel.Keyboard[0].Height;

        viewModel.ZoomHorizontally(NoteEditorViewModel.ZoomStep);
        Assert.Equal(left * NoteEditorViewModel.ZoomStep, line.Left, 6);

        viewModel.ZoomVertically(NoteEditorViewModel.ZoomStep);
        Assert.Equal(height * NoteEditorViewModel.ZoomStep, viewModel.Keyboard[0].Height, 6);
    }

    [Fact]
    public void ScalingDoesNotMoveNotesInTicks()
    {
        var viewModel = CreateViewModel(40, false);
        var before = viewModel.Notes.Select(x => x.StartTicks).ToArray();

        viewModel.ZoomHorizontally(1.0 / NoteEditorViewModel.ZoomStep);
        viewModel.ZoomVertically(NoteEditorViewModel.ZoomStep);

        Assert.Equal(before, viewModel.Notes.Select(x => x.StartTicks).ToArray());
    }

    [Fact]
    public void TheCurveKeepsEverySampleWhileTheyAreAtLeastAPixelApart()
    {
        var viewModel = CreateViewModel(40, true);
        while (viewModel.PixelsPerTick < NoteEditorViewModel.MaximumPixelsPerTick)
            viewModel.ZoomHorizontally(NoteEditorViewModel.ZoomStep);

        var expected = viewModel.Notes.Sum(x => x.LengthTicks / NoteEditorViewModel.PitchCurveIntervalTicks + 1);

        Assert.Equal(expected, viewModel.PitchCurve.Count);
    }

    [Fact]
    public void TheCurveIsThinnedOnceSamplesFallBelowAPixel()
    {
        var viewModel = CreateViewModel(40, true);
        var dense = viewModel.PitchCurve.Count;

        while (viewModel.PixelsPerTick > NoteEditorViewModel.MinimumPixelsPerTick)
            viewModel.ZoomHorizontally(1.0 / NoteEditorViewModel.ZoomStep);

        Assert.True(viewModel.PitchCurve.Count * 3 < dense, $"dense={dense} thinned={viewModel.PitchCurve.Count}");
    }

    [Fact]
    public void TheThinnedCurveKeepsTheVibratoEnvelope()
    {
        var viewModel = CreateViewModel(40, true);
        var dense = ToneRange(viewModel);

        while (viewModel.PixelsPerTick > NoteEditorViewModel.MinimumPixelsPerTick)
            viewModel.ZoomHorizontally(1.0 / NoteEditorViewModel.ZoomStep);
        var thinned = ToneRange(viewModel);

        Assert.Equal(dense.Lowest, thinned.Lowest, 9);
        Assert.Equal(dense.Highest, thinned.Highest, 9);
    }

    [Fact]
    public void EditingTheScoreStillRebuildsTheRows()
    {
        var viewModel = CreateViewModel(10, false);
        var lineCount = viewModel.TimeGridLines.Count;
        var barCount = viewModel.ExpressionBars.Count;

        viewModel.Notes[0].LengthTicks = 1920;

        Assert.True(viewModel.TimeGridLines.Count > lineCount);
        Assert.Equal(barCount, viewModel.ExpressionBars.Count);
    }
}
