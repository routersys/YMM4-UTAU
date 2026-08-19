using UTAU.Notes;
using UTAU.ViewModels;

namespace UTAU.Tests;

public sealed class DragAllocationTests
{
    const int NoteCount = 800;

    static NoteEditorViewModel CreateSelectedViewModel()
    {
        var pronounce = new UTAUVoicePronounce();
        for (var index = 0; index < NoteCount; index++)
        {
            pronounce.Notes.Add(new UTAUNote
            {
                Lyric = "あ",
                Tone = 48 + index * 5 % 30,
                LengthTicks = TimeBase.TicksPerQuarterNote / 2,
            });
        }

        var viewModel = new NoteEditorViewModel(pronounce);
        viewModel.SetViewport(0.0, 900.0);
        viewModel.SelectAll();
        return viewModel;
    }

    static long BytesPerCall(int iterations, Action action)
    {
        action();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < iterations; index++)
            action();
        return (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
    }

    [Fact]
    public void RepeatingTheSameToneOffsetCostsNothing()
    {
        var viewModel = CreateSelectedViewModel();
        viewModel.BeginTransform();
        viewModel.TransformTones(3);

        var bytes = BytesPerCall(200, () => viewModel.TransformTones(3));

        Assert.True(bytes < 1024, $"{bytes} bytes per repeated call");
    }

    [Fact]
    public void RepeatingTheSameLengthOffsetCostsNothing()
    {
        var viewModel = CreateSelectedViewModel();
        viewModel.BeginTransform();
        viewModel.TransformLengths(120);

        var bytes = BytesPerCall(200, () => viewModel.TransformLengths(120));

        Assert.True(bytes < 1024, $"{bytes} bytes per repeated call");
    }

    [Fact]
    public void AChangedToneOffsetStillMovesEverySelectedNote()
    {
        var viewModel = CreateSelectedViewModel();
        var originals = viewModel.Notes.Select(x => x.Note.Tone).ToArray();

        viewModel.BeginTransform();
        viewModel.TransformTones(2);
        viewModel.TransformTones(2);
        viewModel.TransformTones(5);

        Assert.Equal(originals.Select(x => x + 5), viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void ReturningToTheStartingOffsetRestoresEveryTone()
    {
        var viewModel = CreateSelectedViewModel();
        var originals = viewModel.Notes.Select(x => x.Note.Tone).ToArray();

        viewModel.BeginTransform();
        viewModel.TransformTones(4);
        viewModel.TransformTones(-3);
        viewModel.TransformTones(0);

        Assert.Equal(originals, viewModel.Notes.Select(x => x.Note.Tone));
    }

    [Fact]
    public void AChangedLengthOffsetStillResizesEverySelectedNote()
    {
        var viewModel = CreateSelectedViewModel();
        var originals = viewModel.Notes.Select(x => x.Note.LengthTicks).ToArray();

        viewModel.BeginTransform();
        viewModel.TransformLengths(0);
        viewModel.TransformLengths(240);

        Assert.Equal(
            originals.Select(x => viewModel.SnapLength(x + 240)),
            viewModel.Notes.Select(x => x.Note.LengthTicks));
    }

    [Fact]
    public void ASecondDragStartsFromTheNewOrigin()
    {
        var viewModel = CreateSelectedViewModel();
        var originals = viewModel.Notes.Select(x => x.Note.Tone).ToArray();

        viewModel.BeginTransform();
        viewModel.TransformTones(3);
        viewModel.EndTransform();

        viewModel.BeginTransform();
        viewModel.TransformTones(3);
        viewModel.EndTransform();

        Assert.Equal(originals.Select(x => x + 6), viewModel.Notes.Select(x => x.Note.Tone));
    }
}
