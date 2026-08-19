using UTAU.Notes;
using UTAU.ViewModels;

namespace UTAU.Tests;

public sealed class CurveDensityTests
{
    const double PointsPerVisiblePixelBudget = 2.5;

    static NoteEditorViewModel CreateViewModel(int noteCount)
    {
        var pronounce = new UTAUVoicePronounce();
        for (var index = 0; index < noteCount; index++)
        {
            pronounce.Notes.Add(new UTAUNote
            {
                Lyric = "あ",
                Tone = 48 + index * 7 % 24,
                LengthTicks = TimeBase.TicksPerQuarterNote,
            });
        }

        var samples = new double[noteCount * TimeBase.TicksPerQuarterNote / ExpressionCurve.IntervalTicks + 2];
        for (var index = 0; index < samples.Length; index++)
            samples[index] = index % 2 == 0 ? 12.0 : -12.0;
        pronounce.FormantCurve.Values = samples;

        var viewModel = new NoteEditorViewModel(pronounce);
        foreach (var note in viewModel.Notes)
        {
            note.Note.PitchPoints.Add(new PitchPoint(0, -300.0));
            note.Note.PitchPoints.Add(new PitchPoint(TimeBase.TicksPerQuarterNote / 2, 300.0));
        }

        viewModel.SelectedExpression = ExpressionItem.All.First(
            x => x.IsCurve && x.CurveExpression == CurveExpression.Formant);
        return viewModel;
    }

    static void ZoomTo(NoteEditorViewModel viewModel, double pixelsPerTick)
    {
        while (viewModel.PixelsPerTick > pixelsPerTick + 1e-9)
            viewModel.ZoomHorizontally(1.0 / NoteEditorViewModel.ZoomStep);
        while (viewModel.PixelsPerTick < pixelsPerTick - 1e-9)
            viewModel.ZoomHorizontally(NoteEditorViewModel.ZoomStep);
    }

    static (int Pitch, int Curve) WorstCounts(int noteCount, double viewportWidth, double pixelsPerTick)
    {
        var viewModel = CreateViewModel(noteCount);
        ZoomTo(viewModel, pixelsPerTick);

        var pitch = 0;
        var curve = 0;
        for (var offset = 0.0; offset < viewModel.CanvasWidth; offset += viewportWidth / 2.0)
        {
            viewModel.SetViewport(offset, viewportWidth);
            pitch = Math.Max(pitch, viewModel.PitchCurve.Count);
            curve = Math.Max(curve, viewModel.ExpressionCurvePoints.Count);
        }
        return (pitch, curve);
    }

    static double VisiblePixels(double viewportWidth)
        => viewportWidth * (1.0 + 2.0 * NoteEditorViewModel.WindowMarginRatio);

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.02)]
    [InlineData(0.05)]
    [InlineData(0.08)]
    [InlineData(0.2)]
    [InlineData(0.6)]
    public void BothCurvesStayThinnedToTheVisiblePixels(double pixelsPerTick)
    {
        const double viewportWidth = 1200.0;
        var counts = WorstCounts(2000, viewportWidth, pixelsPerTick);
        var visible = VisiblePixels(viewportWidth);

        Assert.True(
            counts.Pitch / visible <= PointsPerVisiblePixelBudget,
            $"pitch points={counts.Pitch} density={counts.Pitch / visible:F3}/px");
        Assert.True(
            counts.Curve / visible <= PointsPerVisiblePixelBudget,
            $"curve points={counts.Curve} density={counts.Curve / visible:F3}/px");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.05)]
    [InlineData(0.6)]
    public void ALongerScoreDoesNotEnlargeEitherCurve(double pixelsPerTick)
    {
        var small = WorstCounts(500, 1200.0, pixelsPerTick);
        var large = WorstCounts(8000, 1200.0, pixelsPerTick);

        Assert.True(
            large.Pitch <= small.Pitch + 8,
            $"pitch grew from {small.Pitch} to {large.Pitch} for a 16x longer score");
        Assert.True(
            large.Curve <= small.Curve + 8,
            $"curve grew from {small.Curve} to {large.Curve} for a 16x longer score");
    }

    [Fact]
    public void ThinningTheExpressionCurveKeepsItsExtremes()
    {
        var viewModel = CreateViewModel(200);
        ZoomTo(viewModel, NoteEditorViewModel.MinimumPixelsPerTick);
        viewModel.SetViewport(0.0, 1200.0);

        var tops = viewModel.ExpressionCurvePoints.Select(x => x.Y).ToArray();

        Assert.NotEmpty(tops);
        Assert.True(
            tops.Max() - tops.Min() > NoteEditorViewModel.StripHeight / 4.0,
            $"thinning flattened the curve: span={tops.Max() - tops.Min()}");
    }

    [Fact]
    public void ThinningTheExpressionCurveStillReachesTheEndOfTheWindow()
    {
        var viewModel = CreateViewModel(200);
        ZoomTo(viewModel, NoteEditorViewModel.MinimumPixelsPerTick);
        viewModel.SetViewport(0.0, 1200.0);

        var lefts = viewModel.ExpressionCurvePoints.Select(x => x.X).ToArray();
        var tolerance = ExpressionCurve.IntervalTicks * viewModel.PixelsPerTick * 2.0;

        Assert.NotEmpty(lefts);
        Assert.True(
            lefts.Max() >= viewModel.CanvasWidth - tolerance,
            $"curve stops at {lefts.Max():F1} but the canvas is {viewModel.CanvasWidth:F1}");
    }
}
