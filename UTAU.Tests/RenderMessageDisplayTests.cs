using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UTAU.Notes;
using UTAU.ViewModels;
using UTAU.Views;
using YukkuriMovieMaker.Controls;

namespace UTAU.Tests;

[Collection("Wpf")]
public sealed class RenderMessageDisplayTests
{
    const double PopupWidth = 980.0;
    const double PopupHeight = 480.0;
    const string Warning = "見つからない歌詞";

    [Fact]
    public void TheWarningReachesTheToolbarFromTheSynthesisThread()
    {
        Exception? error = null;
        string? shown = null;

        var thread = new Thread(() =>
        {
            try
            {
                var editor = new NoteEditor();
                var button = (PopupButton)editor.Content;
                var root = (Grid)button.PopupContent!;
                button.PopupContent = null;

                var pronounce = new UTAUVoicePronounce();
                pronounce.Notes.Add(new UTAUNote { Lyric = "あ" });
                using var viewModel = new NoteEditorViewModel(pronounce);
                root.DataContext = viewModel;

                var host = new Border { Width = PopupWidth, Height = PopupHeight, Child = root };
                host.Measure(new Size(PopupWidth, PopupHeight));
                host.Arrange(new Rect(0.0, 0.0, PopupWidth, PopupHeight));
                host.UpdateLayout();

                Task.Run(() => pronounce.RenderMessage = Warning).GetAwaiter().GetResult();
                viewModel.InvalidateMessages();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

                shown = ((StackPanel)root.Children[0]).Children.OfType<TextBlock>().Last().Text;
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Equal(Warning, shown);
    }
}
