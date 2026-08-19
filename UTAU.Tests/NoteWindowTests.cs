using System.Windows;
using UTAU;
using UTAU.Notes;
using UTAU.ViewModels;

namespace UTAU.Tests;

public sealed class NoteWindowTests
{
    const double Viewport = 900.0;

    static NoteEditorViewModel CreateViewModel(int noteCount)
    {
        var pronounce = new UTAUVoicePronounce();
        for (var index = 0; index < noteCount; index++)
        {
            var note = new UTAUNote { Lyric = "あ", Tone = 60 + index % 5, LengthTicks = 480 };
            note.PitchPoints.Add(new PitchPoint(-40, -200.0));
            note.PitchPoints.Add(new PitchPoint(40, 0.0));
            pronounce.Notes.Add(note);
        }
        return new NoteEditorViewModel(pronounce);
    }

    static long Allocated(Action action, int iterations)
    {
        action();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < iterations; index++)
            action();
        return (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
    }

    [Fact]
    public void WithoutAViewportEveryNoteIsShown()
    {
        var viewModel = CreateViewModel(200);

        Assert.Equal(viewModel.Notes.Count, viewModel.VisibleNotes.Count);
    }

    [Fact]
    public void TheWindowLimitsWhatIsShown()
    {
        var viewModel = CreateViewModel(400);
        viewModel.SetViewport(0.0, Viewport);

        Assert.True(viewModel.VisibleNotes.Count < viewModel.Notes.Count / 4);
        Assert.NotEmpty(viewModel.VisibleNotes);
        Assert.Same(viewModel.Notes[0], viewModel.VisibleNotes[0]);
    }

    [Fact]
    public void TheWindowReachesBeyondTheViewportOnBothSides()
    {
        var viewModel = CreateViewModel(400);
        viewModel.SetViewport(Viewport * 4.0, Viewport);

        var first = viewModel.VisibleNotes[0];
        var last = viewModel.VisibleNotes[^1];

        Assert.True(first.Left < Viewport * 4.0);
        Assert.True(last.Left + last.Width > Viewport * 5.0);
    }

    [Fact]
    public void EverythingInTheViewportIsShown()
    {
        var viewModel = CreateViewModel(400);
        viewModel.SetViewport(Viewport * 3.0, Viewport);

        foreach (var note in viewModel.Notes)
        {
            var inside = note.Left + note.Width >= Viewport * 3.0 && note.Left <= Viewport * 4.0;
            if (inside)
                Assert.Contains(note, viewModel.VisibleNotes);
        }
    }

    [Fact]
    public void ScrollingSlidesTheWindowWithoutRebuildingTheOverlap()
    {
        var viewModel = CreateViewModel(400);
        viewModel.SetViewport(Viewport * 3.0, Viewport);
        var before = viewModel.VisibleNotes.ToArray();

        viewModel.SetViewport(Viewport * 3.0 + 40.0, Viewport);
        var after = viewModel.VisibleNotes.ToArray();

        var shared = before.Intersect(after).ToArray();
        Assert.NotEmpty(shared);
        foreach (var note in shared)
            Assert.Same(note, after.First(x => ReferenceEquals(x, note)));
        Assert.True(after.Length > 0);
    }

    [Fact]
    public void ScrollingStaysCheap()
    {
        var viewModel = CreateViewModel(800);
        viewModel.SetViewport(0.0, Viewport);

        var offset = 0.0;
        var cost = Allocated(
            () =>
            {
                offset += 20.0;
                viewModel.SetViewport(offset, Viewport);
            },
            200);

        Assert.True(cost < 40_000, $"cost={cost}");
    }

    [Fact]
    public void TheCurveIsClippedToTheWindow()
    {
        var viewModel = CreateViewModel(800);
        var whole = viewModel.PitchCurve.Count;
        viewModel.SetViewport(Viewport * 5.0, Viewport);

        Assert.True(viewModel.PitchCurve.Count * 8 < whole, $"whole={whole} windowed={viewModel.PitchCurve.Count}");
        Assert.NotEmpty(viewModel.PitchCurve);
    }

    [Fact]
    public void TheGridAndBarsAreClippedToTheWindow()
    {
        var viewModel = CreateViewModel(800);
        var lines = viewModel.TimeGridLines.Count;
        var bars = viewModel.ExpressionBars.Count;

        viewModel.SetViewport(Viewport * 5.0, Viewport);

        Assert.True(viewModel.TimeGridLines.Count < lines / 4);
        Assert.True(viewModel.ExpressionBars.Count < bars / 4);
    }

    [Theory]
    [InlineData(NoteEditorViewModel.MinimumPixelsPerTick)]
    [InlineData(NoteEditorViewModel.DefaultPixelsPerTick)]
    [InlineData(NoteEditorViewModel.MaximumPixelsPerTick)]
    public void TheCurveNeverGrowsPastTheLargeObjectThreshold(double pixelsPerTick)
    {
        var viewModel = CreateViewModel(2000);
        while (viewModel.PixelsPerTick > pixelsPerTick)
            viewModel.ZoomHorizontally(1.0 / NoteEditorViewModel.ZoomStep);
        while (viewModel.PixelsPerTick < pixelsPerTick)
            viewModel.ZoomHorizontally(NoteEditorViewModel.ZoomStep);

        viewModel.SetViewport(viewModel.CanvasWidth / 2.0, Viewport);

        var bytes = viewModel.PitchCurve.Count * 16;
        Assert.True(bytes < 85_000, $"points={viewModel.PitchCurve.Count} bytes={bytes}");
    }

    [Fact]
    public void SelectionStillReachesNotesOutsideTheWindow()
    {
        var viewModel = CreateViewModel(400);
        viewModel.SetViewport(0.0, Viewport);

        viewModel.SelectAll();
        Assert.Equal(viewModel.Notes.Count, viewModel.SelectedCount);

        viewModel.SelectInBox(new Rect(0.0, 0.0, viewModel.CanvasWidth, viewModel.CanvasHeight), false);
        Assert.Equal(viewModel.Notes.Count, viewModel.SelectedCount);
    }

    [Fact]
    public void ZoomingMovesTheWindow()
    {
        var viewModel = CreateViewModel(400);
        viewModel.SetViewport(0.0, Viewport);
        var before = viewModel.VisibleNotes.Count;

        viewModel.ZoomHorizontally(1.0 / NoteEditorViewModel.ZoomStep);

        Assert.True(viewModel.VisibleNotes.Count > before);
    }

    [Fact]
    public void EditingALengthKeepsTheWindowConsistent()
    {
        var viewModel = CreateViewModel(400);
        viewModel.SetViewport(Viewport * 2.0, Viewport);

        viewModel.Notes[0].LengthTicks = 1920;

        Assert.NotEmpty(viewModel.VisibleNotes);
        foreach (var note in viewModel.VisibleNotes)
            Assert.Contains(note, viewModel.Notes);

        var expected = viewModel.Notes
            .Where(x => x.EndTicks >= viewModel.TicksFromCanvasX(Viewport * 2.0) && x.StartTicks <= viewModel.TicksFromCanvasX(Viewport * 3.0))
            .ToArray();
        foreach (var note in expected)
            Assert.Contains(note, viewModel.VisibleNotes);
    }

    [Fact]
    public void RemovingNotesKeepsTheWindowConsistent()
    {
        var viewModel = CreateViewModel(200);
        viewModel.SetViewport(0.0, Viewport);

        viewModel.Select(viewModel.Notes[0]);
        viewModel.RemoveNoteCommand.Execute(null);

        Assert.NotEmpty(viewModel.VisibleNotes);
        foreach (var note in viewModel.VisibleNotes)
            Assert.Contains(note, viewModel.Notes);
        Assert.Same(viewModel.Notes[0], viewModel.VisibleNotes[0]);
    }
}
