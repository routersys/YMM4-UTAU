using System.Collections.ObjectModel;
using System.IO;
using UTAU.Notes;
using UTAU.ViewModels;

namespace UTAU.Tests;

public sealed class PitchCurveTests
{
    static NoteEditorViewModel CreateViewModel()
    {
        var notes = new ObservableCollection<UTAUNote>
        {
            new() { Lyric = "あ", Tone = 60, LengthTicks = 480 },
        };
        var viewModel = new NoteEditorViewModel(notes);
        viewModel.FitToViewport(800.0, 400.0);
        return viewModel;
    }

    [Fact]
    public void AddingAControlPointRedrawsTheCurve()
    {
        var viewModel = CreateViewModel();
        var before = viewModel.PitchCurve;

        viewModel.SelectedNote!.Note.PitchPoints.Add(new PitchPoint(0.0, -200.0));

        Assert.NotSame(before, viewModel.PitchCurve);
    }

    [Fact]
    public void EditingAControlPointRedrawsTheCurve()
    {
        var viewModel = CreateViewModel();
        var point = new PitchPoint(0.0, 0.0);
        viewModel.SelectedNote!.Note.PitchPoints.Add(point);
        var before = viewModel.PitchCurve;

        point.Cents = -300.0;

        Assert.NotSame(before, viewModel.PitchCurve);
    }

    [Fact]
    public void ChangingTheCurveShapeRedrawsTheCurve()
    {
        var viewModel = CreateViewModel();
        var note = viewModel.SelectedNote!.Note;
        note.PitchPoints.Add(new PitchPoint(0.0, -200.0, PitchPointShape.SCurve));
        note.PitchPoints.Add(new PitchPoint(200.0, 0.0, PitchPointShape.SCurve));
        var before = viewModel.PitchCurve;

        note.PitchPoints[0].Shape = PitchPointShape.Linear;

        Assert.NotSame(before, viewModel.PitchCurve);
    }

    [Fact]
    public void RemovingAControlPointRedrawsTheCurve()
    {
        var viewModel = CreateViewModel();
        var point = new PitchPoint(0.0, -200.0);
        viewModel.SelectedNote!.Note.PitchPoints.Add(point);
        var before = viewModel.PitchCurve;

        viewModel.SelectedNote.Note.PitchPoints.Remove(point);

        Assert.NotSame(before, viewModel.PitchCurve);
    }

    [Fact]
    public void PointsOfAnUnselectedNoteAreNoLongerObserved()
    {
        var notes = new ObservableCollection<UTAUNote>
        {
            new() { Lyric = "あ", Tone = 60, LengthTicks = 480 },
            new() { Lyric = "か", Tone = 62, LengthTicks = 480 },
        };
        var viewModel = new NoteEditorViewModel(notes);
        var first = viewModel.Notes[0];
        var point = new PitchPoint(0.0, 0.0);
        first.Note.PitchPoints.Add(point);

        viewModel.SelectedNote = viewModel.Notes[1];
        var before = viewModel.PitchCurve;
        point.Cents = -400.0;

        Assert.Same(before, viewModel.PitchCurve);
    }

    [Fact]
    public void TheCurveFollowsTheSelectedNoteAfterSwitching()
    {
        var notes = new ObservableCollection<UTAUNote>
        {
            new() { Lyric = "あ", Tone = 60, LengthTicks = 480 },
            new() { Lyric = "か", Tone = 62, LengthTicks = 480 },
        };
        var viewModel = new NoteEditorViewModel(notes);
        viewModel.SelectedNote = viewModel.Notes[1];
        var before = viewModel.PitchCurve;

        viewModel.Notes[1].Note.PitchPoints.Add(new PitchPoint(0.0, 250.0));

        Assert.NotSame(before, viewModel.PitchCurve);
    }

    [Fact]
    public void DisposingStopsObserving()
    {
        var viewModel = CreateViewModel();
        var point = new PitchPoint(0.0, 0.0);
        viewModel.SelectedNote!.Note.PitchPoints.Add(point);
        var note = viewModel.SelectedNote.Note;

        viewModel.Dispose();
        var before = viewModel.PitchCurve;
        point.Cents = -500.0;
        note.PitchPoints.Add(new PitchPoint(100.0, 0.0));

        Assert.Same(before, viewModel.PitchCurve);
    }

    [Fact]
    public void SelectingAPointExposesItForEditing()
    {
        var viewModel = CreateViewModel();
        Assert.False(viewModel.HasPitchPoint);

        var point = new PitchPoint(0.0, 0.0);
        viewModel.SelectedNote!.Note.PitchPoints.Add(point);
        viewModel.SelectedPitchPoint = point;

        Assert.True(viewModel.HasPitchPoint);
        Assert.Same(point, viewModel.SelectedPitchPoint);
    }

    [Fact]
    public void SwitchingNotesClearsTheSelectedPoint()
    {
        var notes = new ObservableCollection<UTAUNote>
        {
            new() { Lyric = "あ", Tone = 60, LengthTicks = 480 },
            new() { Lyric = "か", Tone = 62, LengthTicks = 480 },
        };
        var viewModel = new NoteEditorViewModel(notes);
        var point = new PitchPoint(0.0, 0.0);
        viewModel.Notes[0].Note.PitchPoints.Add(point);
        viewModel.SelectedPitchPoint = point;

        viewModel.SelectedNote = viewModel.Notes[1];

        Assert.Null(viewModel.SelectedPitchPoint);
        Assert.False(viewModel.HasPitchPoint);
    }

    [Fact]
    public void AddingAPointSelectsIt()
    {
        var viewModel = CreateViewModel();
        viewModel.AddPitchPointCommand.Execute(null);

        Assert.True(viewModel.HasPitchPoint);
        Assert.Same(Assert.Single(viewModel.SelectedNote!.Note.PitchPoints), viewModel.SelectedPitchPoint);
    }

    [Fact]
    public void ResettingClearsThePointsAndTheSelection()
    {
        var viewModel = CreateViewModel();
        viewModel.AddPitchPointCommand.Execute(null);
        viewModel.ResetPitchCommand.Execute(null);

        Assert.Empty(viewModel.SelectedNote!.Note.PitchPoints);
        Assert.False(viewModel.HasPitchPoint);
    }

    [Fact]
    public void EverySelectableCurveShapeIsOffered()
    {
        var offered = NoteEditorViewModel.PitchShapes.Select(x => x.Value).ToArray();
        Assert.Equal(Enum.GetValues<PitchPointShape>(), offered);
        Assert.All(NoteEditorViewModel.PitchShapes, x => Assert.False(string.IsNullOrEmpty(x.Name)));
    }
}
