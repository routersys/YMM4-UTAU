using System.Windows.Controls;
using UTAU.Notes;
using UTAU.ViewModels;
using UTAU.Views;
using YukkuriMovieMaker.Controls;

namespace UTAU.Tests;

[Collection("Wpf")]
public sealed class NoteEditorHostTests
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

    static UTAUVoicePronounce CreatePronounce()
    {
        var pronounce = new UTAUVoicePronounce();
        pronounce.Notes.Add(new UTAUNote { Lyric = "あ", Tone = 60 });
        return pronounce;
    }

    [Fact]
    public void ThePopupHoldsTheEditingSurface()
    {
        var hosted = RunSta(() =>
        {
            var editor = new NoteEditor();
            var button = (PopupButton)editor.Content;
            var host = (ContentControl)button.PopupContent!;
            return host.Content;
        });

        Assert.IsType<NoteEditorSurface>(hosted);
    }

    [Fact]
    public void TheSurfaceGetsTheViewModelOfTheGivenPronounce()
    {
        var pronounce = CreatePronounce();
        var context = RunSta(() =>
        {
            var editor = new NoteEditor();
            editor.Pronounce = pronounce;
            var button = (PopupButton)editor.Content;
            var host = (ContentControl)button.PopupContent!;
            return ((NoteEditorSurface)host.Content!).DataContext;
        });

        var viewModel = Assert.IsType<NoteEditorViewModel>(context);
        Assert.Single(viewModel.Notes);
        Assert.Same(pronounce.Notes[0], viewModel.Notes[0].Note);
    }

    [Fact]
    public void ClearingThePronounceTakesTheViewModelAway()
    {
        var pronounce = CreatePronounce();
        var context = RunSta(() =>
        {
            var editor = new NoteEditor();
            editor.Pronounce = pronounce;
            editor.Pronounce = null;
            var button = (PopupButton)editor.Content;
            var host = (ContentControl)button.PopupContent!;
            return ((NoteEditorSurface)host.Content!).DataContext;
        });

        Assert.Null(context);
    }

    [Fact]
    public void TheEditorAttributeBindsThePronounceToTheSurface()
    {
        var pronounce = CreatePronounce();
        var context = RunSta(() =>
        {
            var attribute = new NoteEditorAttribute();
            var control = attribute.Create();
            attribute.SetBindings(control, pronounce, pronounce, typeof(UTAUVoicePronounce).GetProperty(nameof(UTAUVoicePronounce.Notes))!);
            var editor = (NoteEditor)control;
            var button = (PopupButton)editor.Content;
            var host = (ContentControl)button.PopupContent!;
            return ((NoteEditorSurface)host.Content!).DataContext;
        });

        var viewModel = Assert.IsType<NoteEditorViewModel>(context);
        Assert.Same(pronounce.Notes[0], viewModel.Notes[0].Note);
    }
}
