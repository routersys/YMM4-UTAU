using System.Windows;
using UTAU;
using UTAU.Notes;
using UTAU.ViewModels;

namespace UTAU.Tests;

public sealed class NoteSelectionTests
{
    static NoteEditorViewModel CreateViewModel(params int[] tones)
    {
        var pronounce = new UTAUVoicePronounce();
        foreach (var tone in tones)
            pronounce.Notes.Add(new UTAUNote { Lyric = "あ", Tone = tone, LengthTicks = UTAUNote.DefaultLengthTicks });
        return new NoteEditorViewModel(pronounce);
    }

    static void AssertConsistent(NoteEditorViewModel viewModel)
    {
        foreach (var note in viewModel.Notes)
            Assert.Equal(viewModel.SelectedNotes.Contains(note), note.IsSelected);

        Assert.Equal(viewModel.SelectedNote is null ? 0 : 1, viewModel.Notes.Count(x => x.IsPrimary));
        Assert.Equal(viewModel.SelectedNotes.Count, viewModel.SelectedCount);
        Assert.Equal(viewModel.SelectedNotes.Count, viewModel.SelectedNoteTargets.Count);
        Assert.Equal(viewModel.SelectedNote is not null, viewModel.HasSelection);

        if (viewModel.SelectedNote is not { } primary)
            return;

        Assert.True(primary.IsPrimary);
        Assert.Contains(primary, viewModel.SelectedNotes);
    }

    [Fact]
    public void TheFirstNoteIsSelectedWhenTheEditorOpens()
    {
        var viewModel = CreateViewModel(60, 62, 64);

        Assert.Same(viewModel.Notes[0], viewModel.SelectedNote);
        Assert.Equal(1, viewModel.SelectedCount);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void SelectingReplacesTheWholeSelection()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.SelectAll();

        viewModel.Select(viewModel.Notes[2]);

        Assert.Same(viewModel.Notes[2], viewModel.SelectedNote);
        Assert.Equal(1, viewModel.SelectedCount);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void TogglingAddsAndRemovesOneNoteAtATime()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.Select(viewModel.Notes[0]);

        viewModel.ToggleSelection(viewModel.Notes[2]);
        Assert.Equal(2, viewModel.SelectedCount);
        Assert.Same(viewModel.Notes[2], viewModel.SelectedNote);
        AssertConsistent(viewModel);

        viewModel.ToggleSelection(viewModel.Notes[2]);
        Assert.Equal(1, viewModel.SelectedCount);
        Assert.Same(viewModel.Notes[0], viewModel.SelectedNote);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void TogglingTheSameNoteRepeatedlyAlternatesTheSelection()
    {
        var viewModel = CreateViewModel(60, 62);
        viewModel.Select(viewModel.Notes[0]);

        viewModel.ToggleSelection(viewModel.Notes[1]);
        viewModel.ToggleSelection(viewModel.Notes[1]);
        viewModel.ToggleSelection(viewModel.Notes[1]);

        Assert.Equal(2, viewModel.SelectedCount);
        Assert.Equal(viewModel.SelectedNotes.Count, viewModel.SelectedNotes.Distinct().Count());
        AssertConsistent(viewModel);
    }

    [Fact]
    public void AddingTheSameNotesWithTheBoxDoesNotDuplicateThem()
    {
        var viewModel = CreateViewModel(60, 60, 60);
        viewModel.SelectAll();
        var box = new Rect(0.0, 0.0, viewModel.CanvasWidth, viewModel.CanvasHeight);

        viewModel.SelectInBox(box, true);
        viewModel.SelectInBox(box, true);

        Assert.Equal(viewModel.Notes.Count, viewModel.SelectedCount);
        Assert.Equal(viewModel.SelectedNotes.Count, viewModel.SelectedNotes.Distinct().Count());
        AssertConsistent(viewModel);
    }

    [Fact]
    public void TogglingAwayAnotherNoteLeavesThePrimaryAlone()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.Select(viewModel.Notes[0]);
        viewModel.ToggleSelection(viewModel.Notes[1]);
        viewModel.ToggleSelection(viewModel.Notes[2]);
        viewModel.MakePrimary(viewModel.Notes[0]);

        viewModel.ToggleSelection(viewModel.Notes[2]);

        Assert.Same(viewModel.Notes[0], viewModel.SelectedNote);
        Assert.Equal(2, viewModel.SelectedCount);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void TogglingAwayTheLastNoteLeavesNothingSelected()
    {
        var viewModel = CreateViewModel(60, 62);
        viewModel.Select(viewModel.Notes[0]);

        viewModel.ToggleSelection(viewModel.Notes[0]);

        Assert.Null(viewModel.SelectedNote);
        Assert.Empty(viewModel.SelectedNotes);
        Assert.Empty(viewModel.SelectedNoteTargets);
        Assert.False(viewModel.HasSelection);
        Assert.All(viewModel.Notes, x => Assert.False(x.IsSelected));
        Assert.All(viewModel.Notes, x => Assert.False(x.IsPrimary));
        AssertConsistent(viewModel);
    }

    [Fact]
    public void SelectingARangeWorksInBothDirections()
    {
        var viewModel = CreateViewModel(60, 62, 64, 65);

        viewModel.Select(viewModel.Notes[2]);
        viewModel.SelectRange(viewModel.Notes[0]);
        Assert.Equal(3, viewModel.SelectedCount);
        Assert.Same(viewModel.Notes[0], viewModel.SelectedNote);
        Assert.DoesNotContain(viewModel.Notes[3], viewModel.SelectedNotes);
        AssertConsistent(viewModel);

        viewModel.Select(viewModel.Notes[1]);
        viewModel.SelectRange(viewModel.Notes[3]);
        Assert.Equal(3, viewModel.SelectedCount);
        Assert.Same(viewModel.Notes[3], viewModel.SelectedNote);
        Assert.DoesNotContain(viewModel.Notes[0], viewModel.SelectedNotes);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void SelectingARangeOfOneNoteSelectsOnlyThatNote()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.Select(viewModel.Notes[1]);

        viewModel.SelectRange(viewModel.Notes[1]);

        Assert.Equal(1, viewModel.SelectedCount);
        Assert.Same(viewModel.Notes[1], viewModel.SelectedNote);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void SelectingAllKeepsThePrimaryWhereItWas()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.Select(viewModel.Notes[2]);

        viewModel.SelectAll();

        Assert.Equal(viewModel.Notes.Count, viewModel.SelectedCount);
        Assert.Same(viewModel.Notes[2], viewModel.SelectedNote);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void MakingAnUnselectedNotePrimaryCollapsesTheSelection()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.Select(viewModel.Notes[0]);
        viewModel.ToggleSelection(viewModel.Notes[1]);

        viewModel.MakePrimary(viewModel.Notes[2]);

        Assert.Equal(1, viewModel.SelectedCount);
        Assert.Same(viewModel.Notes[2], viewModel.SelectedNote);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void MakingASelectedNotePrimaryKeepsTheSelection()
    {
        var viewModel = CreateViewModel(60, 62, 64);
        viewModel.SelectAll();

        viewModel.MakePrimary(viewModel.Notes[1]);

        Assert.Equal(3, viewModel.SelectedCount);
        Assert.Same(viewModel.Notes[1], viewModel.SelectedNote);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void TheBoxSelectsEveryNoteItTouches()
    {
        var viewModel = CreateViewModel(60, 60, 60);
        var first = viewModel.Notes[0];
        var second = viewModel.Notes[1];

        viewModel.SelectInBox(new Rect(first.Left, first.Top, second.Left + second.Width - first.Left, first.Height), false);

        Assert.Equal(2, viewModel.SelectedCount);
        Assert.Contains(first, viewModel.SelectedNotes);
        Assert.Contains(second, viewModel.SelectedNotes);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void TheBoxLeavesOutNotesOnOtherRows()
    {
        var viewModel = CreateViewModel(60, 72, 60);
        var row = viewModel.Notes[0];

        viewModel.SelectInBox(new Rect(0.0, row.Top, viewModel.CanvasWidth, row.Height), false);

        Assert.Equal(2, viewModel.SelectedCount);
        Assert.DoesNotContain(viewModel.Notes[1], viewModel.SelectedNotes);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void TheBoxCanAddToTheSelection()
    {
        var viewModel = CreateViewModel(60, 72, 60);
        var row = viewModel.Notes[0];
        viewModel.Select(viewModel.Notes[1]);

        viewModel.SelectInBox(new Rect(0.0, row.Top, viewModel.CanvasWidth, row.Height), true);

        Assert.Equal(3, viewModel.SelectedCount);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void ABoxOverEmptySpaceClearsTheSelection()
    {
        var viewModel = CreateViewModel(60, 62);
        viewModel.SelectAll();

        viewModel.UpdateSelectionBox(new Point(0.0, -200.0), new Point(120.0, -100.0));
        viewModel.CommitSelectionBox(false);

        Assert.Null(viewModel.SelectedNote);
        Assert.Empty(viewModel.SelectedNotes);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void ATinyBoxLeavesTheSelectionAlone()
    {
        var viewModel = CreateViewModel(60, 62);
        viewModel.Select(viewModel.Notes[1]);

        viewModel.UpdateSelectionBox(new Point(400.0, 400.0), new Point(401.0, 401.0));
        viewModel.CommitSelectionBox(false);

        Assert.Same(viewModel.Notes[1], viewModel.SelectedNote);
        Assert.Equal(1, viewModel.SelectedCount);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void TheBoxIsHiddenAfterItIsCommitted()
    {
        var viewModel = CreateViewModel(60, 62);

        viewModel.UpdateSelectionBox(new Point(0.0, 0.0), new Point(40.0, 40.0));
        Assert.True(viewModel.IsSelectionBoxVisible);
        Assert.Equal(40.0, viewModel.SelectionBoxWidth, 9);
        Assert.Equal(40.0, viewModel.SelectionBoxHeight, 9);

        viewModel.CommitSelectionBox(false);

        Assert.False(viewModel.IsSelectionBoxVisible);
    }

    [Fact]
    public void TheBoxIsNormalisedWhenItIsDrawnBackwards()
    {
        var viewModel = CreateViewModel(60, 62);

        viewModel.UpdateSelectionBox(new Point(90.0, 80.0), new Point(30.0, 20.0));

        Assert.Equal(30.0, viewModel.SelectionBoxLeft, 9);
        Assert.Equal(20.0, viewModel.SelectionBoxTop, 9);
        Assert.Equal(60.0, viewModel.SelectionBoxWidth, 9);
        Assert.Equal(60.0, viewModel.SelectionBoxHeight, 9);
    }

    [Fact]
    public void EverySelectedNoteMovesByTheSameNumberOfSemitones()
    {
        var viewModel = CreateViewModel(60, 64, 67);
        viewModel.SelectAll();

        viewModel.BeginTransform();
        viewModel.TransformTones(-3);

        Assert.Equal(57, viewModel.Notes[0].Tone);
        Assert.Equal(61, viewModel.Notes[1].Tone);
        Assert.Equal(64, viewModel.Notes[2].Tone);
    }

    [Fact]
    public void UnselectedNotesAreLeftAloneByATransform()
    {
        var viewModel = CreateViewModel(60, 64, 67);
        viewModel.Select(viewModel.Notes[0]);
        viewModel.ToggleSelection(viewModel.Notes[2]);

        viewModel.BeginTransform();
        viewModel.TransformTones(2);

        Assert.Equal(62, viewModel.Notes[0].Tone);
        Assert.Equal(64, viewModel.Notes[1].Tone);
        Assert.Equal(69, viewModel.Notes[2].Tone);
    }

    [Fact]
    public void TheDownwardShiftStopsAtTheLowestNote()
    {
        var viewModel = CreateViewModel(2, 14);
        viewModel.SelectAll();

        viewModel.BeginTransform();
        viewModel.TransformTones(-10);

        Assert.Equal(0, viewModel.Notes[0].Tone);
        Assert.Equal(12, viewModel.Notes[1].Tone);
    }

    [Fact]
    public void TheUpwardShiftStopsAtTheHighestNote()
    {
        var viewModel = CreateViewModel(115, 125);
        viewModel.SelectAll();

        viewModel.BeginTransform();
        viewModel.TransformTones(10);

        Assert.Equal(117, viewModel.Notes[0].Tone);
        Assert.Equal(127, viewModel.Notes[1].Tone);
    }

    [Fact]
    public void AFullyStretchedSelectionCannotMoveAtAll()
    {
        var viewModel = CreateViewModel(0, 127);
        viewModel.SelectAll();

        viewModel.BeginTransform();
        viewModel.TransformTones(50);
        viewModel.TransformTones(-50);

        Assert.Equal(0, viewModel.Notes[0].Tone);
        Assert.Equal(127, viewModel.Notes[1].Tone);
    }

    [Fact]
    public void RepeatedShiftsAreMeasuredFromTheStartOfTheDrag()
    {
        var viewModel = CreateViewModel(60, 60);
        viewModel.SelectAll();

        viewModel.BeginTransform();
        viewModel.TransformTones(2);
        viewModel.TransformTones(5);
        viewModel.TransformTones(1);

        Assert.All(viewModel.Notes, x => Assert.Equal(61, x.Tone));
    }

    [Fact]
    public void LengthsAreSnappedForEverySelectedNote()
    {
        var viewModel = CreateViewModel(60, 60);
        viewModel.Notes[0].LengthTicks = 240;
        viewModel.Notes[1].LengthTicks = 480;
        viewModel.SelectAll();

        viewModel.BeginTransform();
        viewModel.TransformLengths(130);

        Assert.Equal(360, viewModel.Notes[0].LengthTicks);
        Assert.Equal(600, viewModel.Notes[1].LengthTicks);
    }

    [Fact]
    public void LengthsNeverFallBelowTheMinimum()
    {
        var viewModel = CreateViewModel(60, 60);
        viewModel.SelectAll();

        viewModel.BeginTransform();
        viewModel.TransformLengths(-100000);

        Assert.All(viewModel.Notes, x => Assert.True(x.LengthTicks >= UTAUNote.MinimumLengthTicks));
    }

    [Fact]
    public void TransformsDoNothingWithoutABeginTransform()
    {
        var viewModel = CreateViewModel(60, 60);
        viewModel.SelectAll();

        viewModel.TransformTones(5);
        viewModel.TransformLengths(500);
        Assert.All(viewModel.Notes, x => Assert.Equal(60, x.Tone));

        viewModel.BeginTransform();
        viewModel.EndTransform();
        viewModel.TransformTones(5);
        Assert.All(viewModel.Notes, x => Assert.Equal(60, x.Tone));
    }

    [Fact]
    public void TheLayoutFollowsABatchedTransform()
    {
        var viewModel = CreateViewModel(60, 60);
        viewModel.SelectAll();

        viewModel.BeginTransform();
        viewModel.TransformTones(30);

        Assert.All(viewModel.Notes, x => Assert.Equal(90, x.Tone));
        Assert.InRange(90, viewModel.MinimumTone, viewModel.MaximumTone);
        Assert.Contains(viewModel.Keyboard, x => x.NoteNumber == 90);
    }

    [Fact]
    public void TheLayoutFollowsABatchedLengthChange()
    {
        var viewModel = CreateViewModel(60, 60);
        viewModel.SelectAll();

        viewModel.BeginTransform();
        viewModel.TransformLengths(120);

        var expected = 0;
        foreach (var note in viewModel.Notes)
        {
            Assert.Equal(expected, note.StartTicks);
            expected += note.LengthTicks;
        }
        Assert.Equal(expected, viewModel.TotalTicks);
    }

    [Fact]
    public void RemovingDeletesEverySelectedNote()
    {
        var viewModel = CreateViewModel(60, 61, 62, 63);
        viewModel.Select(viewModel.Notes[1]);
        viewModel.ToggleSelection(viewModel.Notes[2]);

        viewModel.RemoveNoteCommand.Execute(null);

        Assert.Equal(new[] { 60, 63 }, viewModel.Notes.Select(x => x.Tone).ToArray());
        Assert.Contains(viewModel.SelectedNote!, viewModel.Notes);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void RemovingIsRefusedWhenEveryNoteIsSelected()
    {
        var viewModel = CreateViewModel(60, 61);
        viewModel.SelectAll();

        Assert.False(viewModel.RemoveNoteCommand.CanExecute(null));
        viewModel.RemoveNoteCommand.Execute(null);

        Assert.Equal(2, viewModel.Notes.Count);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void ResetPitchClearsEverySelectedNote()
    {
        var viewModel = CreateViewModel(60, 61, 62);
        foreach (var note in viewModel.Notes)
            note.Note.PitchPoints.Add(new PitchPoint(0, 100.0));
        viewModel.Select(viewModel.Notes[0]);
        viewModel.ToggleSelection(viewModel.Notes[1]);

        viewModel.ResetPitchCommand.Execute(null);

        Assert.Empty(viewModel.Notes[0].Note.PitchPoints);
        Assert.Empty(viewModel.Notes[1].Note.PitchPoints);
        Assert.Single(viewModel.Notes[2].Note.PitchPoints);
        Assert.Null(viewModel.SelectedPitchPoint);
    }

    [Fact]
    public void PressingAKeyTransposesTheSelectionAroundThePrimary()
    {
        var viewModel = CreateViewModel(60, 64);
        viewModel.SelectAll();
        viewModel.MakePrimary(viewModel.Notes[0]);

        viewModel.SelectToneCommand.Execute(viewModel.Keyboard.Single(x => x.NoteNumber == 62));

        Assert.Equal(62, viewModel.Notes[0].Tone);
        Assert.Equal(66, viewModel.Notes[1].Tone);
    }

    [Fact]
    public void PressingAKeyIsClampedAtTheEdgeOfTheToneRange()
    {
        var viewModel = CreateViewModel(0, 10);
        viewModel.SelectAll();
        viewModel.MakePrimary(viewModel.Notes[1]);

        viewModel.SelectToneCommand.Execute(viewModel.Keyboard.Single(x => x.NoteNumber == 5));

        Assert.Equal(0, viewModel.Notes[0].Tone);
        Assert.Equal(10, viewModel.Notes[1].Tone);
    }

    [Fact]
    public void TheEditorTargetsFollowTheSelection()
    {
        var viewModel = CreateViewModel(60, 61, 62);

        viewModel.Select(viewModel.Notes[0]);
        Assert.Equal(new[] { viewModel.Notes[0].Note }, viewModel.SelectedNoteTargets.ToArray());

        viewModel.SelectAll();
        Assert.Equal(viewModel.Notes.Select(x => x.Note).ToArray(), viewModel.SelectedNoteTargets.ToArray());
    }

    [Fact]
    public void InsertingARestGoesAfterTheLastSelectedNote()
    {
        var viewModel = CreateViewModel(60, 61, 62);
        viewModel.Select(viewModel.Notes[0]);
        viewModel.ToggleSelection(viewModel.Notes[1]);

        viewModel.InsertRestCommand.Execute(null);

        Assert.Equal(4, viewModel.Notes.Count);
        Assert.True(viewModel.Notes[2].IsRest);
        AssertConsistent(viewModel);
    }

    [Fact]
    public void WideToneRangesDoNotBreakTheLayout()
    {
        var viewModel = CreateViewModel(0, 127);

        Assert.True(viewModel.MinimumTone <= 0);
        Assert.True(viewModel.MaximumTone >= 127);
        Assert.True(viewModel.CanvasHeight > 0.0);
        Assert.Contains(viewModel.Keyboard, x => x.NoteNumber == 0);
        Assert.Contains(viewModel.Keyboard, x => x.NoteNumber == 127);
    }

    [Fact]
    public void TheSelectionSurvivesASequenceOfOperations()
    {
        var viewModel = CreateViewModel(60, 62, 64, 65, 67);

        viewModel.Select(viewModel.Notes[1]);
        AssertConsistent(viewModel);

        viewModel.SelectRange(viewModel.Notes[3]);
        AssertConsistent(viewModel);

        viewModel.ToggleSelection(viewModel.Notes[2]);
        AssertConsistent(viewModel);

        viewModel.ToggleSelection(viewModel.Notes[4]);
        AssertConsistent(viewModel);

        viewModel.SelectAll();
        AssertConsistent(viewModel);

        viewModel.Select(null);
        AssertConsistent(viewModel);

        viewModel.SelectInBox(new Rect(0.0, 0.0, viewModel.CanvasWidth, viewModel.CanvasHeight), false);
        Assert.Equal(viewModel.Notes.Count, viewModel.SelectedCount);
        AssertConsistent(viewModel);
    }
}
