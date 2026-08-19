using System.IO;
using UTAU;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;
using UTAU.Synthesis;
using UTAU.ViewModels;
using WorldNet;

namespace UTAU.Tests;

public sealed class ExpressionCurveTests
{
    [Fact]
    public void AnUntouchedCurveEvaluatesToZero()
    {
        var curve = new ExpressionCurve();
        Assert.True(curve.IsEmpty);
        Assert.Equal(0.0, curve.Evaluate(0.0));
        Assert.Equal(0.0, curve.Evaluate(100000.0));
    }

    [Fact]
    public void ValuesAreInterpolatedBetweenSamples()
    {
        var curve = new ExpressionCurve { Values = [0.0, 10.0] };
        Assert.Equal(0.0, curve.Evaluate(0.0), 9);
        Assert.Equal(5.0, curve.Evaluate(ExpressionCurve.IntervalTicks / 2.0), 9);
        Assert.Equal(10.0, curve.Evaluate(ExpressionCurve.IntervalTicks), 9);
    }

    [Fact]
    public void SamplesOutsideTheCurveHoldTheEdgeValue()
    {
        var curve = new ExpressionCurve { Values = [3.0, 7.0] };
        Assert.Equal(3.0, curve.Evaluate(-1000.0), 9);
        Assert.Equal(7.0, curve.Evaluate(1000000.0), 9);
    }

    [Fact]
    public void AWorkingCopyCoversTheRequestedLength()
    {
        var curve = new ExpressionCurve();
        var working = curve.CreateWorkingCopy(TimeBase.TicksPerWholeNote);
        Assert.True(working.Length > ExpressionCurve.ToIndex(TimeBase.TicksPerWholeNote));
    }

    [Fact]
    public void AWorkingCopyKeepsTheExistingValues()
    {
        var curve = new ExpressionCurve { Values = [1.0, 2.0, 3.0] };
        var working = curve.CreateWorkingCopy(TimeBase.TicksPerWholeNote);
        Assert.Equal(1.0, working[0]);
        Assert.Equal(2.0, working[1]);
        Assert.Equal(3.0, working[2]);
    }

    [Fact]
    public void CommittingAnAllZeroCurveClearsIt()
    {
        var curve = new ExpressionCurve { Values = [1.0, 2.0] };
        curve.Commit(new double[8]);
        Assert.True(curve.IsEmpty);
    }

    [Fact]
    public void CommittingKeepsANonZeroCurve()
    {
        var curve = new ExpressionCurve();
        curve.Commit([0.0, 4.0, 0.0]);
        Assert.False(curve.IsEmpty);
        Assert.Equal(4.0, curve.Evaluate(ExpressionCurve.IntervalTicks), 9);
    }

    [Fact]
    public void CurveChangesNotifyTheHost()
    {
        var pronounce = new UTAUVoicePronounce();
        var raised = 0;
        pronounce.UndoRedoCommandCreated += (_, _) => raised++;

        pronounce.FormantCurve.Commit([0.0, 5.0]);
        Assert.True(raised > 0);

        var afterFormant = raised;
        pronounce.BreathinessCurve.Commit([0.0, 5.0]);
        Assert.True(raised > afterFormant);
    }
}

public sealed class RenderCurvesTests
{
    [Fact]
    public void AnEmptyCurveContributesNothing()
    {
        Assert.False(RenderCurves.Empty.HasFormant);
        Assert.False(RenderCurves.Empty.HasBreathiness);
        Assert.Equal(0.0, RenderCurves.Empty.Formant(123.0));
        Assert.Equal(0.0, RenderCurves.Empty.Breathiness(123.0));
    }

    [Fact]
    public void ValuesAreInterpolatedOverTime()
    {
        var curves = new RenderCurves([0.0, 12.0], [], 10.0);
        Assert.True(curves.HasFormant);
        Assert.Equal(0.0, curves.Formant(0.0), 9);
        Assert.Equal(6.0, curves.Formant(5.0), 9);
        Assert.Equal(12.0, curves.Formant(10.0), 9);
    }

    [Fact]
    public void TimeOutsideTheCurveHoldsTheEdgeValue()
    {
        var curves = new RenderCurves([2.0, 8.0], [], 10.0);
        Assert.Equal(2.0, curves.Formant(-50.0), 9);
        Assert.Equal(8.0, curves.Formant(5000.0), 9);
    }

    [Fact]
    public void EachCurveIsIndependent()
    {
        var curves = new RenderCurves([1.0, 1.0], [-4.0, -4.0], 10.0);
        Assert.Equal(1.0, curves.Formant(5.0), 9);
        Assert.Equal(-4.0, curves.Breathiness(5.0), 9);
    }
}

public sealed class ExpressionItemTests
{
    [Fact]
    public void EveryNoteExpressionAndCurveExpressionIsOffered()
    {
        Assert.Equal(
            Enum.GetValues<NoteExpression>(),
            ExpressionItem.All.Where(x => !x.IsCurve).Select(x => x.NoteExpression));
        Assert.Equal(
            Enum.GetValues<CurveExpression>(),
            ExpressionItem.All.Where(x => x.IsCurve).Select(x => x.CurveExpression));
    }

    [Fact]
    public void EveryItemHasAUsableRangeAndName()
    {
        Assert.All(ExpressionItem.All, x =>
        {
            Assert.False(string.IsNullOrEmpty(x.Name));
            Assert.True(x.Range > 0.0);
        });
    }

    [Fact]
    public void RatiosRoundTripThroughValues()
    {
        foreach (var item in ExpressionItem.All)
        {
            Assert.Equal(0.0, item.ToRatio(item.Minimum), 9);
            Assert.Equal(1.0, item.ToRatio(item.Maximum), 9);
            Assert.Equal(item.Minimum, item.FromRatio(0.0), 9);
            Assert.Equal(item.Maximum, item.FromRatio(1.0), 9);
            Assert.Equal(0.5, item.ToRatio(item.FromRatio(0.5)), 9);
        }
    }

    [Fact]
    public void RatiosAndValuesAreClamped()
    {
        var item = ExpressionItem.All[0];
        Assert.Equal(item.Minimum, item.FromRatio(-5.0), 9);
        Assert.Equal(item.Maximum, item.FromRatio(5.0), 9);
        Assert.Equal(0.0, item.ToRatio(item.Minimum - 100.0), 9);
        Assert.Equal(1.0, item.ToRatio(item.Maximum + 100.0), 9);
    }

    [Fact]
    public void TheBaselineSitsAtZeroForSignedExpressions()
    {
        var modulation = ExpressionItem.All.Single(x => !x.IsCurve && x.NoteExpression == NoteExpression.Modulation);
        Assert.Equal(0.5, modulation.Baseline, 9);
    }
}

public sealed class ExpressionEditingTests
{
    static UTAUVoicePronounce CreatePronounce(int noteCount = 4)
    {
        var pronounce = new UTAUVoicePronounce();
        for (var i = 0; i < noteCount; i++)
            pronounce.Notes.Add(new UTAUNote { Lyric = "あ", Tone = 60, LengthTicks = 480 });
        return pronounce;
    }

    static NoteEditorViewModel CreateViewModel(UTAUVoicePronounce pronounce)
    {
        var viewModel = new NoteEditorViewModel(pronounce);
        viewModel.FitToViewport(800.0, 400.0);
        return viewModel;
    }

    static ExpressionItem Find(NoteExpression expression)
        => ExpressionItem.All.Single(x => !x.IsCurve && x.NoteExpression == expression);

    static ExpressionItem Find(CurveExpression expression)
        => ExpressionItem.All.Single(x => x.IsCurve && x.CurveExpression == expression);

    [Fact]
    public void PaintingABarChangesOnlyTheNoteUnderTheCursor()
    {
        var pronounce = CreatePronounce();
        var viewModel = CreateViewModel(pronounce);
        viewModel.SelectedExpression = Find(NoteExpression.Intensity);

        viewModel.SetExpressionAt(600, 0.25);

        Assert.Equal(100.0, pronounce.Notes[0].Intensity, 9);
        Assert.Equal(50.0, pronounce.Notes[1].Intensity, 9);
        Assert.Equal(100.0, pronounce.Notes[2].Intensity, 9);
    }

    [Fact]
    public void EveryNoteExpressionCanBePainted()
    {
        var pronounce = CreatePronounce();
        var viewModel = CreateViewModel(pronounce);

        viewModel.SelectedExpression = Find(NoteExpression.Velocity);
        viewModel.SetExpressionAt(0, 1.0);
        Assert.Equal(200.0, pronounce.Notes[0].Velocity, 9);

        viewModel.SelectedExpression = Find(NoteExpression.Intensity);
        viewModel.SetExpressionAt(0, 0.0);
        Assert.Equal(0.0, pronounce.Notes[0].Intensity, 9);

        viewModel.SelectedExpression = Find(NoteExpression.Modulation);
        viewModel.SetExpressionAt(0, 1.0);
        Assert.Equal(200.0, pronounce.Notes[0].Modulation, 9);
    }

    [Fact]
    public void RestsAreNotPainted()
    {
        var pronounce = new UTAUVoicePronounce();
        pronounce.Notes.Add(new UTAUNote { Lyric = UTAUNote.RestLyric, LengthTicks = 480 });
        var viewModel = CreateViewModel(pronounce);
        viewModel.SelectedExpression = Find(NoteExpression.Intensity);

        viewModel.SetExpressionAt(100, 0.0);

        Assert.Equal(100.0, pronounce.Notes[0].Intensity, 9);
        Assert.Empty(viewModel.ExpressionBars);
    }

    [Fact]
    public void BarsAreDrawnForEveryVoicedNote()
    {
        var pronounce = CreatePronounce();
        var viewModel = CreateViewModel(pronounce);
        viewModel.SelectedExpression = Find(NoteExpression.Intensity);

        Assert.Equal(pronounce.Notes.Count, viewModel.ExpressionBars.Count);
        Assert.All(viewModel.ExpressionBars, x => Assert.True(x.Height >= 1.0));
    }

    [Fact]
    public void ACurveIsOnlyStoredWhenItIsCommitted()
    {
        var pronounce = CreatePronounce();
        var viewModel = CreateViewModel(pronounce);
        viewModel.SelectedExpression = Find(CurveExpression.Formant);

        viewModel.SetExpressionAt(240, 1.0);
        Assert.True(pronounce.FormantCurve.IsEmpty);

        viewModel.CommitExpression();
        Assert.False(pronounce.FormantCurve.IsEmpty);
        Assert.Equal(12.0, pronounce.FormantCurve.Evaluate(240), 6);
    }

    [Fact]
    public void EachCurveExpressionIsStoredSeparately()
    {
        var pronounce = CreatePronounce();
        var viewModel = CreateViewModel(pronounce);

        viewModel.SelectedExpression = Find(CurveExpression.Formant);
        viewModel.SetExpressionAt(240, 1.0);
        viewModel.CommitExpression();

        viewModel.SelectedExpression = Find(CurveExpression.Breathiness);
        viewModel.SetExpressionAt(240, 0.0);
        viewModel.CommitExpression();

        Assert.Equal(12.0, pronounce.FormantCurve.Evaluate(240), 6);
        Assert.Equal(-100.0, pronounce.BreathinessCurve.Evaluate(240), 6);
    }

    [Fact]
    public void SwitchingExpressionsDiscardsAnUncommittedStroke()
    {
        var pronounce = CreatePronounce();
        var viewModel = CreateViewModel(pronounce);

        viewModel.SelectedExpression = Find(CurveExpression.Formant);
        viewModel.SetExpressionAt(240, 1.0);
        viewModel.SelectedExpression = Find(CurveExpression.Breathiness);
        viewModel.CommitExpression();

        Assert.True(pronounce.FormantCurve.IsEmpty);
        Assert.True(pronounce.BreathinessCurve.IsEmpty);
    }

    [Fact]
    public void ResettingRestoresTheNoteDefaults()
    {
        var pronounce = CreatePronounce();
        var viewModel = CreateViewModel(pronounce);
        viewModel.SelectedExpression = Find(NoteExpression.Intensity);
        viewModel.SetExpressionAt(0, 0.0);

        viewModel.ResetExpressionCommand.Execute(null);

        Assert.All(pronounce.Notes, x => Assert.Equal(100.0, x.Intensity, 9));
    }

    [Fact]
    public void ResettingClearsTheCurve()
    {
        var pronounce = CreatePronounce();
        var viewModel = CreateViewModel(pronounce);
        viewModel.SelectedExpression = Find(CurveExpression.Formant);
        viewModel.SetExpressionAt(240, 1.0);
        viewModel.CommitExpression();

        viewModel.ResetExpressionCommand.Execute(null);

        Assert.True(pronounce.FormantCurve.IsEmpty);
    }

    [Fact]
    public void CurveModeDrawsALineAndNoBars()
    {
        var pronounce = CreatePronounce();
        var viewModel = CreateViewModel(pronounce);
        viewModel.SelectedExpression = Find(CurveExpression.Formant);

        Assert.True(viewModel.IsCurveExpression);
        Assert.Empty(viewModel.ExpressionBars);
        Assert.NotEmpty(viewModel.ExpressionCurvePoints);
    }
}

public sealed class PitchHandleTests
{
    static NoteEditorViewModel CreateViewModel(out UTAUVoicePronounce pronounce)
    {
        pronounce = new UTAUVoicePronounce();
        pronounce.Notes.Add(new UTAUNote { Lyric = "あ", Tone = 60, LengthTicks = 480 });
        var viewModel = new NoteEditorViewModel(pronounce);
        viewModel.FitToViewport(800.0, 400.0);
        return viewModel;
    }

    [Fact]
    public void HandlesAppearForEveryControlPointOfTheSelectedNote()
    {
        var viewModel = CreateViewModel(out var pronounce);
        Assert.Empty(viewModel.PitchHandles);

        pronounce.Notes[0].PitchPoints.Add(new PitchPoint(0, 0.0));
        pronounce.Notes[0].PitchPoints.Add(new PitchPoint(240, -100.0));

        Assert.Equal(2, viewModel.PitchHandles.Count);
    }

    [Fact]
    public void AHandleSitsOnTheCurveItRepresents()
    {
        var viewModel = CreateViewModel(out var pronounce);
        pronounce.Notes[0].PitchPoints.Add(new PitchPoint(240, -100.0));

        var handle = Assert.Single(viewModel.PitchHandles);
        var expected = viewModel.ToCanvasPoint(240, 60 - 1.0);

        Assert.Equal(expected.X - NoteEditorViewModel.PitchHandleSize / 2.0, handle.Left, 6);
        Assert.Equal(expected.Y - NoteEditorViewModel.PitchHandleSize / 2.0, handle.Top, 6);
    }

    [Fact]
    public void ControlPointsAreInsertedInTimeOrder()
    {
        var viewModel = CreateViewModel(out var pronounce);
        viewModel.AddPitchPointAt(240, 0.0);
        viewModel.AddPitchPointAt(60, -50.0);
        viewModel.AddPitchPointAt(400, 50.0);

        Assert.Equal([60, 240, 400], pronounce.Notes[0].PitchPoints.Select(x => x.Ticks));
    }

    [Fact]
    public void MovingAHandleUpdatesThePoint()
    {
        var viewModel = CreateViewModel(out var pronounce);
        viewModel.AddPitchPointAt(0, 0.0);
        var point = pronounce.Notes[0].PitchPoints[0];

        viewModel.MovePitchPoint(point, 300, -250.0);

        Assert.Equal(300, point.Ticks);
        Assert.Equal(-250.0, point.Cents, 9);
    }

    [Fact]
    public void RemovingAHandleRemovesThePoint()
    {
        var viewModel = CreateViewModel(out var pronounce);
        viewModel.AddPitchPointAt(0, 0.0);
        var point = pronounce.Notes[0].PitchPoints[0];

        viewModel.RemovePitchPoint(point);

        Assert.Empty(pronounce.Notes[0].PitchPoints);
        Assert.Null(viewModel.SelectedPitchPoint);
    }

    [Fact]
    public void CentsAreDerivedFromTheVerticalPosition()
    {
        var viewModel = CreateViewModel(out _);
        var oneSemitoneUp = viewModel.ToCanvasPoint(0, 61).Y;

        Assert.Equal(100.0, viewModel.CentsFromCanvasY(oneSemitoneUp, 60), 6);
        Assert.Equal(0.0, viewModel.CentsFromCanvasY(viewModel.ToCanvasPoint(0, 60).Y, 60), 6);
    }

    [Fact]
    public void RestsCarryNoHandles()
    {
        var pronounce = new UTAUVoicePronounce();
        pronounce.Notes.Add(new UTAUNote { Lyric = UTAUNote.RestLyric, LengthTicks = 480 });
        var viewModel = new NoteEditorViewModel(pronounce);

        viewModel.AddPitchPointAt(100, 50.0);

        Assert.Empty(viewModel.PitchHandles);
        Assert.Empty(pronounce.Notes[0].PitchPoints);
    }
}

public sealed class CurveRenderingTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();
    readonly AnalysisCache cache = new(AnalysisCache.DefaultBudgetBytes);

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    double[] Render(VoiceBank bank, RenderCurves curves)
    {
        var notes = NoteSequenceBuilder.Build("<!C4:1/4>あ", NoteBuildOptions.Create(60));
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);
        using var arena = new WorldArena();
        var settings = RenderSettings.Default with { Estimator = F0Estimator.Dio };
        return new UtauRenderer(settings, curves, cache).Render(units, arena).Samples;
    }

    [Fact]
    public void AnEmptyCurveRendersTheSameAsNoCurve()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        Assert.Equal(Render(bank, RenderCurves.Empty), Render(bank, new RenderCurves([], [], 5.0)));
    }

    [Fact]
    public void AFormantCurveChangesTheOutput()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var plain = Render(bank, RenderCurves.Empty);
        var shifted = Render(bank, new RenderCurves([6.0, 6.0], [], 5.0));

        Assert.Equal(plain.Length, shifted.Length);
        Assert.NotEqual(plain, shifted);
        Assert.All(shifted, x => Assert.True(double.IsFinite(x)));
    }

    [Fact]
    public void ABreathinessCurveChangesTheOutput()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var plain = Render(bank, RenderCurves.Empty);
        var breathy = Render(bank, new RenderCurves([], [80.0, 80.0], 5.0));

        Assert.Equal(plain.Length, breathy.Length);
        Assert.NotEqual(plain, breathy);
        Assert.All(breathy, x => Assert.True(double.IsFinite(x)));
    }

    [Fact]
    public void ACurveThatVariesOverTimeStaysStable()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var ramp = Enumerable.Range(0, 200).Select(x => -12.0 + 24.0 * x / 199.0).ToArray();
        var samples = Render(bank, new RenderCurves(ramp, [], 5.0));

        Assert.All(samples, x => Assert.True(double.IsFinite(x)));
        Assert.True(samples.Max(Math.Abs) > 0.001);
    }
}

public sealed class PitchPointValueTests
{
    static NoteEditorViewModel CreateViewModel(out UTAUVoicePronounce pronounce)
    {
        pronounce = new UTAUVoicePronounce();
        pronounce.Notes.Add(new UTAUNote { Lyric = "あ", Tone = 60, LengthTicks = 480 });
        var viewModel = new NoteEditorViewModel(pronounce);
        viewModel.FitToViewport(800.0, 400.0);
        return viewModel;
    }

    [Fact]
    public void TheSelectedPointExposesItsExactValues()
    {
        var viewModel = CreateViewModel(out var pronounce);
        viewModel.AddPitchPointAt(240, -125.0);

        var point = Assert.Single(pronounce.Notes[0].PitchPoints);
        Assert.Same(point, viewModel.SelectedPitchPoint);
        Assert.Equal(240, viewModel.SelectedPitchPoint!.Ticks);
        Assert.Equal(-125.0, viewModel.SelectedPitchPoint.Cents, 9);
    }

    [Fact]
    public void TypedValuesAreAppliedAndRedrawTheCurve()
    {
        var viewModel = CreateViewModel(out var pronounce);
        viewModel.AddPitchPointAt(0, 0.0);
        var point = pronounce.Notes[0].PitchPoints[0];
        var before = viewModel.PitchCurve;

        point.Ticks = 360;
        point.Cents = 275.0;

        Assert.Equal(360, point.Ticks);
        Assert.Equal(275.0, point.Cents, 9);
        Assert.NotSame(before, viewModel.PitchCurve);
    }

    [Fact]
    public void TypedValuesAreClampedToTheDeclaredRange()
    {
        var point = new PitchPoint();
        point.Ticks = PitchPoint.MinimumTicks - 1000;
        point.Cents = PitchPoint.MaximumCents + 1000.0;

        Assert.Equal(PitchPoint.MinimumTicks, point.Ticks);
        Assert.Equal(PitchPoint.MaximumCents, point.Cents, 9);
    }

    [Fact]
    public void DraggingReportsTheValuesThroughTheGuide()
    {
        var viewModel = CreateViewModel(out var pronounce);
        viewModel.AddPitchPointAt(120, -50.0);
        var point = pronounce.Notes[0].PitchPoints[0];

        viewModel.ShowPitchGuide(point);

        Assert.True(viewModel.IsGuideVisible);
        Assert.Contains("120", viewModel.GuideText);
        Assert.Contains("-50", viewModel.GuideText);
    }

    [Fact]
    public void TheHandlePositionAgreesWithTheTypedValues()
    {
        var viewModel = CreateViewModel(out var pronounce);
        viewModel.AddPitchPointAt(0, 0.0);
        var point = pronounce.Notes[0].PitchPoints[0];

        point.Ticks = 240;
        point.Cents = 100.0;

        var handle = Assert.Single(viewModel.PitchHandles);
        var expected = viewModel.ToCanvasPoint(240, 61.0);
        Assert.Equal(expected.X - NoteEditorViewModel.PitchHandleSize / 2.0, handle.Left, 6);
        Assert.Equal(expected.Y - NoteEditorViewModel.PitchHandleSize / 2.0, handle.Top, 6);
    }
}
