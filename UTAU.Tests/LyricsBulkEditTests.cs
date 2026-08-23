using System.Windows;
using System.Windows.Controls;
using UTAU.Notes;
using UTAU.ViewModels;
using UTAU.Views;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Tests;

[Collection("Wpf")]
public sealed class LyricsBulkEditTests
{
    sealed class Recorder
    {
        readonly List<IUndoRedoCommand> commands = [];

        public Recorder(UTAUVoicePronounce pronounce)
            => pronounce.UndoRedoCommandCreated += (_, e) =>
            {
                if (e.Command is IUndoRedoCommand command)
                    commands.Add(command);
            };

        public int Recorded => commands.Count(x => !x.IsEmpty);

        public void UndoAll()
        {
            for (var index = commands.Count - 1; index >= 0; index--)
            {
                if (!commands[index].IsEmpty)
                    commands[index].Undo();
            }
        }
    }

    static NoteEditorViewModel CreateViewModel(UTAUVoicePronounce pronounce, params string[] lyrics)
    {
        foreach (var lyric in lyrics)
            pronounce.Notes.Add(new UTAUNote { Lyric = lyric, Tone = 60, LengthTicks = UTAUNote.DefaultLengthTicks });
        return new NoteEditorViewModel(pronounce);
    }

    static NoteEditorViewModel CreateViewModel(params string[] lyrics)
        => CreateViewModel(new UTAUVoicePronounce(), lyrics);

    static T RunSta<T>(Func<T> action)
    {
        var result = default(T)!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            throw new InvalidOperationException(error.Message, error);
        return result;
    }

    [Fact]
    public void TheSelectedLyricsComeOutInNoteOrder()
    {
        var viewModel = CreateViewModel("あ", "り", "が", "と");
        viewModel.Select(viewModel.Notes[3]);
        viewModel.ToggleSelection(viewModel.Notes[1]);

        Assert.Equal("り と", viewModel.ReadSelectedLyrics());
    }

    [Fact]
    public void ALyricThatWouldNotSurviveSplittingComesOutQuoted()
    {
        var viewModel = CreateViewModel("あい", "R");
        viewModel.SelectAll();

        Assert.Equal("\"あい\" R", viewModel.ReadSelectedLyrics());
    }

    [Fact]
    public void ApplyingReplacesTheLyricsOfTheSelectionInOrder()
    {
        var viewModel = CreateViewModel("あ", "あ", "あ");
        viewModel.SelectAll();

        var applied = viewModel.ApplyLyrics(LyricSplitter.Split("かきく"));

        Assert.Equal(3, applied);
        Assert.Equal(["か", "き", "く"], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void ApplyingFollowsScoreOrderNotTheOrderTheNotesWereClicked()
    {
        var viewModel = CreateViewModel("あ", "い", "う");
        viewModel.Select(viewModel.Notes[2]);
        viewModel.ToggleSelection(viewModel.Notes[0]);

        viewModel.ApplyLyrics(LyricSplitter.Split("かき"));

        Assert.Equal(["か", "い", "き"], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void ReadingAndApplyingAgreeOnTheOrderOfAScatteredSelection()
    {
        var viewModel = CreateViewModel("あ", "い", "う", "え");
        viewModel.Select(viewModel.Notes[3]);
        viewModel.ToggleSelection(viewModel.Notes[0]);
        viewModel.ToggleSelection(viewModel.Notes[2]);

        viewModel.ApplyLyrics(LyricSplitter.Split(viewModel.ReadSelectedLyrics()));

        Assert.Equal(["あ", "い", "う", "え"], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void ApplyingLeavesTheNotesOutsideTheSelectionAlone()
    {
        var viewModel = CreateViewModel("あ", "い", "う");
        viewModel.Select(viewModel.Notes[1]);

        viewModel.ApplyLyrics(LyricSplitter.Split("か"));

        Assert.Equal(["あ", "か", "う"], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void ExtraLyricsBeyondTheSelectionAreDropped()
    {
        var viewModel = CreateViewModel("あ", "い");
        viewModel.SelectAll();

        var applied = viewModel.ApplyLyrics(LyricSplitter.Split("かきくけこ"));

        Assert.Equal(2, applied);
        Assert.Equal(["か", "き"], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void NotesBeyondTheLyricsKeepWhatTheyHad()
    {
        var viewModel = CreateViewModel("あ", "い", "う", "え");
        viewModel.SelectAll();

        var applied = viewModel.ApplyLyrics(LyricSplitter.Split("かき"));

        Assert.Equal(2, applied);
        Assert.Equal(["か", "き", "う", "え"], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void ApplyingNothingChangesNothing()
    {
        var viewModel = CreateViewModel("あ", "い");
        viewModel.SelectAll();

        var applied = viewModel.ApplyLyrics(LyricSplitter.Split("   "));

        Assert.Equal(0, applied);
        Assert.Equal(["あ", "い"], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void AQuotedRunBecomesOneLyric()
    {
        var viewModel = CreateViewModel("あ", "い");
        viewModel.SelectAll();

        viewModel.ApplyLyrics(LyricSplitter.Split("\"a あ\" \"\""));

        Assert.Equal(["a あ", ""], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void TheLyricsSurviveAReadAndAWriteRoundTrip()
    {
        var viewModel = CreateViewModel("あ", "きゃ", "two words", "", "R", "-", "中");
        viewModel.SelectAll();
        var text = viewModel.ReadSelectedLyrics();

        viewModel.ApplyLyrics(LyricSplitter.Split(text));

        Assert.Equal(["あ", "きゃ", "two words", "", "R", "-", "中"], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void UndoingBringsEveryLyricBack()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, "あ", "い", "う");
        viewModel.SelectAll();

        var recorder = new Recorder(pronounce);
        viewModel.ApplyLyrics(LyricSplitter.Split("かきく"));
        Assert.Equal(3, recorder.Recorded);
        recorder.UndoAll();

        Assert.Equal(["あ", "い", "う"], viewModel.Notes.Select(x => x.Note.Lyric));
    }

    [Fact]
    public void AnUnchangedLyricIsNotRecorded()
    {
        var pronounce = new UTAUVoicePronounce();
        var viewModel = CreateViewModel(pronounce, "あ", "い", "う");
        viewModel.SelectAll();

        var recorder = new Recorder(pronounce);
        viewModel.ApplyLyrics(LyricSplitter.Split("あ い く"));

        Assert.Equal(1, recorder.Recorded);
    }

    [Fact]
    public void EditingTheLyricsNeedsASelection()
    {
        var viewModel = CreateViewModel("あ");
        viewModel.Select(null);

        Assert.False(viewModel.EditLyricsCommand.CanExecute(null));
    }

    [Fact]
    public void TheCommandAsksTheViewToOpenTheWindow()
    {
        var viewModel = CreateViewModel("あ", "い");
        viewModel.SelectAll();
        var asked = 0;
        viewModel.LyricsEditRequested += (_, _) => asked++;

        viewModel.EditLyricsCommand.Execute(null);

        Assert.Equal(1, asked);
    }

    [Fact]
    public void TheDialogCountsTheLyricsAgainstTheNotes()
    {
        var dialog = new LyricsBulkEditViewModel("あ い う", 3);

        Assert.Equal(3, dialog.LyricCount);
        Assert.Equal(3, dialog.NoteCount);
        Assert.False(dialog.HasMismatch);
        Assert.Equal(string.Format(Texts.LyricCountFormat, 3, 3), dialog.CountText);
    }

    [Fact]
    public void TheDialogReportsAMismatchAsTheTextChanges()
    {
        var dialog = new LyricsBulkEditViewModel("あ い う", 3);

        dialog.Text = "かき";

        Assert.Equal(2, dialog.LyricCount);
        Assert.True(dialog.HasMismatch);
        Assert.Equal(string.Format(Texts.LyricCountFormat, 2, 3), dialog.CountText);
    }

    [Fact]
    public void TheDialogPutsTheTextBackWhenReverted()
    {
        var dialog = new LyricsBulkEditViewModel("あ い う", 3);
        dialog.Text = "かき";

        Assert.True(dialog.ResetCommand.CanExecute(null));
        dialog.ResetCommand.Execute(null);

        Assert.Equal("あ い う", dialog.Text);
        Assert.False(dialog.ResetCommand.CanExecute(null));
    }

    [Fact]
    public void TheDialogRaisesItsCountAsTheTextChanges()
    {
        var dialog = new LyricsBulkEditViewModel("あ", 1);
        var raised = new List<string>();
        dialog.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

        dialog.Text = "あ い";

        Assert.Contains(nameof(LyricsBulkEditViewModel.Text), raised);
        Assert.Contains(nameof(LyricsBulkEditViewModel.LyricCount), raised);
        Assert.Contains(nameof(LyricsBulkEditViewModel.CountText), raised);
        Assert.Contains(nameof(LyricsBulkEditViewModel.HasMismatch), raised);
    }

    [Fact]
    public void TheWindowShowsTheViewAndTakesItsViewModel()
    {
        var hosted = RunSta(() =>
        {
            var dialog = new LyricsBulkEditViewModel("あ い", 2);
            var window = new LyricsBulkEditWindow(dialog);
            return (window.Content, ReferenceEquals(window.DataContext, dialog), window.Title);
        });

        Assert.IsType<LyricsBulkEditView>(hosted.Content);
        Assert.True(hosted.Item2);
        Assert.Equal(Texts.EditLyrics, hosted.Title);
    }

    [Fact]
    public void TheWindowFollowsTheThemeOfTheHost()
    {
        var basedOn = RunSta(() =>
        {
            var window = new LyricsBulkEditWindow(new LyricsBulkEditViewModel("あ", 1));
            return window.Style?.BasedOn?.TargetType;
        });

        Assert.Equal(typeof(Window), basedOn);
    }

    [Fact]
    public void TheApplyButtonTellsTheWindowToClose()
    {
        var applied = RunSta(() =>
        {
            var view = new LyricsBulkEditView { DataContext = new LyricsBulkEditViewModel("あ", 1) };
            var count = 0;
            view.Applied += (_, _) => count++;
            var button = (Button)view.FindName("ApplyButton")!;
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            return count;
        });

        Assert.Equal(1, applied);
    }
}
