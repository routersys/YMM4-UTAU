using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    const string LongWarning = "原音設定に ぱ, ぴ, ぷ, ぺ, ぽ, ざ, じ, ず ... が見つかりません。";
    const string LongImport = "44個のノートを取り込みました  前後の休符を480ティック除きました";

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

    static IEnumerable<DependencyObject> Descend(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descend(child))
                yield return descendant;
        }
    }

    static Border Build(NoteEditorViewModel viewModel)
    {
        var editor = new NoteEditor();
        var button = (PopupButton)editor.Content;
        var root = (Grid)button.PopupContent!;
        button.PopupContent = null;
        root.DataContext = viewModel;

        var host = new Border { Width = PopupWidth, Height = PopupHeight, Child = root };
        host.Measure(new Size(PopupWidth, PopupHeight));
        host.Arrange(new Rect(0.0, 0.0, PopupWidth, PopupHeight));
        host.UpdateLayout();
        return host;
    }

    static TextBlock WarningBlock(Border host)
        => Descend(host)
            .OfType<DockPanel>()
            .SelectMany(x => x.Children.OfType<TextBlock>())
            .First(x => DockPanel.GetDock(x) == Dock.Right);

    [Fact]
    public void TheWarningReachesTheToolbarFromTheSynthesisThread()
    {
        var shown = RunSta(() =>
        {
            var pronounce = new UTAUVoicePronounce();
            pronounce.Notes.Add(new UTAUNote { Lyric = "あ" });
            using var viewModel = new NoteEditorViewModel(pronounce);
            var host = Build(viewModel);

            Task.Run(() => pronounce.RenderMessage = Warning).GetAwaiter().GetResult();
            viewModel.InvalidateMessages();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

            return WarningBlock(host).Text;
        });

        Assert.Equal(Warning, shown);
    }

    [Fact]
    public void TheWarningStaysInsideTheToolbarWhenEveryMessageIsLong()
    {
        var right = RunSta(() =>
        {
            var pronounce = new UTAUVoicePronounce
            {
                ImportMessage = LongImport,
                RenderMessage = LongWarning,
            };
            pronounce.Notes.Add(new UTAUNote { Lyric = "あ" });
            using var viewModel = new NoteEditorViewModel(pronounce);
            var host = Build(viewModel);

            var warning = WarningBlock(host);
            return warning.TranslatePoint(new Point(0.0, 0.0), host).X + warning.ActualWidth;
        });

        Assert.True(right <= PopupWidth, $"right={right}");
    }
}
