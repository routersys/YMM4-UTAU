using System.Windows;
using System.Windows.Controls;
using UTAU.Notes;
using UTAU.ViewModels;
using UTAU.Views;
using YukkuriMovieMaker.Controls;

namespace UTAU.Tests;

[Collection("Wpf")]
public sealed class NoteEditorDetachTests
{
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

    static UTAUVoicePronounce CreatePronounce(params string[] lyrics)
    {
        var pronounce = new UTAUVoicePronounce();
        foreach (var lyric in lyrics)
            pronounce.Notes.Add(new UTAUNote { Lyric = lyric, Tone = 60, LengthTicks = UTAUNote.DefaultLengthTicks });
        return pronounce;
    }

    static NoteEditor CreateEditor(UTAUVoicePronounce pronounce)
    {
        var editor = new NoteEditor();
        var host = new Border { Width = 200.0, Height = 26.0, Child = editor };
        host.Measure(new Size(200.0, 26.0));
        host.Arrange(new Rect(0.0, 0.0, 200.0, 26.0));
        host.UpdateLayout();
        editor.Pronounce = pronounce;
        return editor;
    }

    static ContentControl PopupHostOf(NoteEditor editor)
        => (ContentControl)((PopupButton)editor.Content).PopupContent!;

    static NoteEditorViewModel ViewModelOf(NoteEditorSurface surface)
        => (NoteEditorViewModel)surface.DataContext;

    [Fact]
    public void DetachingTakesTheSurfaceOutOfThePopupAndIntoTheWindow()
    {
        var state = RunSta(() =>
        {
            var editor = CreateEditor(CreatePronounce("あ", "い"));
            var before = (NoteEditorSurface)PopupHostOf(editor).Content!;
            editor.Detach();
            var moved = ReferenceEquals(editor.DetachedWindow?.Surface, before);
            var emptied = PopupHostOf(editor).Content is null;
            editor.CloseDetached();
            return (moved, emptied);
        });

        Assert.True(state.moved);
        Assert.True(state.emptied);
    }

    [Fact]
    public void ClosingTheWindowPutsTheSameSurfaceBackInThePopup()
    {
        var state = RunSta(() =>
        {
            var editor = CreateEditor(CreatePronounce("あ", "い"));
            var before = PopupHostOf(editor).Content;
            editor.Detach();
            editor.CloseDetached();
            return (
                Same: ReferenceEquals(PopupHostOf(editor).Content, before),
                Gone: editor.DetachedWindow is null);
        });

        Assert.True(state.Same);
        Assert.True(state.Gone);
    }

    [Fact]
    public void ThePopupButtonIsHeldShutWhileTheWindowIsOpen()
    {
        var state = RunSta(() =>
        {
            var editor = CreateEditor(CreatePronounce("あ"));
            var button = (PopupButton)editor.Content;
            editor.Detach();
            var whileOpen = button.IsEnabled;
            editor.CloseDetached();
            return (whileOpen, AfterClose: button.IsEnabled);
        });

        Assert.False(state.whileOpen);
        Assert.True(state.AfterClose);
    }

    [Fact]
    public void DetachingBeginsAnEditAndClosingEndsIt()
    {
        var state = RunSta(() =>
        {
            var editor = CreateEditor(CreatePronounce("あ"));
            var begun = 0;
            var ended = 0;
            editor.BeginEdit += (_, _) => begun++;
            editor.EndEdit += (_, _) => ended++;

            editor.Detach();
            var afterDetach = (Begun: begun, Ended: ended);
            editor.CloseDetached();
            return (afterDetach, After: (Begun: begun, Ended: ended));
        });

        Assert.Equal((1, 0), state.afterDetach);
        Assert.Equal((1, 1), state.After);
    }

    [Fact]
    public void TheDetachedSurfaceEditsTheSamePronounce()
    {
        var pronounce = CreatePronounce("あ", "い", "う");
        var applied = RunSta(() =>
        {
            var editor = CreateEditor(pronounce);
            editor.Detach();
            var viewModel = ViewModelOf(editor.DetachedWindow!.Surface!);
            viewModel.SelectAll();
            var count = viewModel.ApplyLyrics(LyricSplitter.Split("かきく"));
            editor.CloseDetached();
            return count;
        });

        Assert.Equal(3, applied);
        Assert.Equal(["か", "き", "く"], pronounce.Notes.Select(x => x.Lyric));
    }

    [Fact]
    public void TheEditsMadeInTheWindowAreThereWhenItCloses()
    {
        var pronounce = CreatePronounce("あ", "い");
        var inPopup = RunSta(() =>
        {
            var editor = CreateEditor(pronounce);
            editor.Detach();
            var viewModel = ViewModelOf(editor.DetachedWindow!.Surface!);
            viewModel.SelectAll();
            viewModel.OctaveUpCommand.Execute(null);
            editor.CloseDetached();
            return ViewModelOf((NoteEditorSurface)PopupHostOf(editor).Content!)
                .Notes.Select(x => x.Note.Tone).ToArray();
        });

        Assert.Equal([72, 72], inPopup);
        Assert.Equal([72, 72], pronounce.Notes.Select(x => x.Tone));
    }

    [Fact]
    public void ClearingThePronounceClosesTheWindow()
    {
        var state = RunSta(() =>
        {
            var editor = CreateEditor(CreatePronounce("あ"));
            editor.Detach();
            var whileOpen = editor.DetachedWindow is not null;
            editor.Pronounce = null;
            return (whileOpen, After: editor.DetachedWindow is not null, Back: PopupHostOf(editor).Content is NoteEditorSurface);
        });

        Assert.True(state.whileOpen);
        Assert.False(state.After);
        Assert.True(state.Back);
    }

    [Fact]
    public void DetachingTwiceOpensOnlyOneWindow()
    {
        var same = RunSta(() =>
        {
            var editor = CreateEditor(CreatePronounce("あ"));
            editor.Detach();
            var first = editor.DetachedWindow;
            editor.Detach();
            var second = editor.DetachedWindow;
            editor.CloseDetached();
            return ReferenceEquals(first, second);
        });

        Assert.True(same);
    }

    [Fact]
    public void TheCommandAsksTheViewToDetach()
    {
        var pronounce = CreatePronounce("あ");
        var detached = RunSta(() =>
        {
            var editor = CreateEditor(pronounce);
            var viewModel = ViewModelOf((NoteEditorSurface)PopupHostOf(editor).Content!);
            viewModel.DetachCommand.Execute(null);
            var open = editor.DetachedWindow is not null;
            editor.CloseDetached();
            return open;
        });

        Assert.True(detached);
    }

    static MenuItem DetachEntry(NoteEditorSurface surface)
        => surface.RollCanvas.ContextMenu!.Items
            .OfType<MenuItem>()
            .First(x => (string)x.Header == Texts.DetachEditor);

    [Fact]
    public void TheWindowDoesNotOfferToOpenAnotherWindow()
    {
        var state = RunSta(() =>
        {
            var editor = CreateEditor(CreatePronounce("あ"));
            var surface = (NoteEditorSurface)PopupHostOf(editor).Content!;
            var before = DetachEntry(surface).Visibility;
            editor.Detach();
            var whileOpen = DetachEntry(editor.DetachedWindow!.Surface!).Visibility;
            editor.CloseDetached();
            return (before, whileOpen, After: DetachEntry(surface).Visibility);
        });

        Assert.Equal(Visibility.Visible, state.before);
        Assert.Equal(Visibility.Collapsed, state.whileOpen);
        Assert.Equal(Visibility.Visible, state.After);
    }

    [Fact]
    public void TheDetachCommandTurnsItselfOffWhileTheWindowIsOpen()
    {
        var state = RunSta(() =>
        {
            var editor = CreateEditor(CreatePronounce("あ"));
            var viewModel = ViewModelOf((NoteEditorSurface)PopupHostOf(editor).Content!);
            var before = viewModel.DetachCommand.CanExecute(null);
            editor.Detach();
            var whileOpen = (viewModel.DetachCommand.CanExecute(null), viewModel.IsDetached, viewModel.CanDetach);
            editor.CloseDetached();
            return (before, whileOpen, After: (viewModel.DetachCommand.CanExecute(null), viewModel.IsDetached, viewModel.CanDetach));
        });

        Assert.True(state.before);
        Assert.Equal((false, true, false), state.whileOpen);
        Assert.Equal((true, false, true), state.After);
    }

    [Fact]
    public void TheWindowFollowsTheThemeOfTheHost()
    {
        var basedOn = RunSta(() =>
        {
            var window = new NoteEditorWindow(new NoteEditorSurface());
            var result = window.Style?.BasedOn?.TargetType;
            window.Release();
            return result;
        });

        Assert.Equal(typeof(Window), basedOn);
    }

    [Fact]
    public void TheWindowIsTitledLikeTheEditorButton()
    {
        var title = RunSta(() =>
        {
            var window = new NoteEditorWindow(new NoteEditorSurface());
            var result = window.Title;
            window.Release();
            return result;
        });

        Assert.Equal(Texts.EditNotes, title);
    }
}
