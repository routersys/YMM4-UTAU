using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UTAU.Notes;
using UTAU.ViewModels;
using UTAU.Views;
using YukkuriMovieMaker.Controls;

namespace UTAU.Tests;

[Collection("Wpf")]
public sealed class ScrollingTextTests
{
    const string Long = "697 個のノートを読み込みました  全 88 フレーズ  前後の休符 1200 ティックを除きました";
    const string Short = "12 個のノート";

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

    static (bool Scrolling, double Hidden, double Width) Measure(string text, double available)
    {
        var control = new ScrollingText { Text = text, FontSize = 12.0 };
        var host = new Border { Width = available, Height = 26.0, Child = control };
        host.Measure(new Size(available, 26.0));
        host.Arrange(new Rect(0.0, 0.0, available, 26.0));
        host.UpdateLayout();
        return (control.IsScrolling, control.HiddenWidth, control.ActualWidth);
    }

    [Fact]
    public void ShortTextDoesNotScroll()
    {
        var result = RunSta(() => Measure(Short, 400.0));

        Assert.False(result.Scrolling);
        Assert.Equal(0.0, result.Hidden);
    }

    [Fact]
    public void OverflowingTextScrolls()
    {
        var result = RunSta(() => Measure(Long, 200.0));

        Assert.True(result.Scrolling);
        Assert.True(result.Hidden > 0.0, $"hidden={result.Hidden}");
        Assert.Equal(200.0, result.Width, 1);
    }

    static double NaturalWidth(string text)
    {
        var control = new ScrollingText { Text = text, FontSize = 12.0 };
        control.Measure(new Size(double.PositiveInfinity, 26.0));
        return control.DesiredSize.Width;
    }

    [Fact]
    public void TextThatFitsDoesNotScroll()
    {
        var result = RunSta(() =>
        {
            var natural = NaturalWidth(Long);
            return (natural, Measure(Long, natural + 8.0));
        });

        Assert.True(result.natural > 0.0);
        Assert.False(result.Item2.Scrolling);
        Assert.Equal(0.0, result.Item2.Hidden);
    }

    [Fact]
    public void TheToolbarUsesScrollingTextForBothMessages()
    {
        var found = RunSta(() =>
        {
            var editor = new NoteEditor();
            var button = (PopupButton)editor.Content;
            var root = (Grid)button.PopupContent!;
            button.PopupContent = null;

            var pronounce = new UTAUVoicePronounce { ImportMessage = Long, RenderMessage = Long };
            pronounce.Notes.Add(new UTAUNote { Lyric = "あ" });
            using var viewModel = new NoteEditorViewModel(pronounce);
            root.DataContext = viewModel;

            var host = new Border { Width = 980.0, Height = 480.0, Child = root };
            host.Measure(new Size(980.0, 480.0));
            host.Arrange(new Rect(0.0, 0.0, 980.0, 480.0));
            host.UpdateLayout();

            return Descend(host)
                .OfType<DockPanel>()
                .SelectMany(x => x.Children.OfType<ScrollingText>())
                .Select(x => x.Text)
                .ToArray();
        });

        Assert.Equal(2, found.Length);
        Assert.All(found, x => Assert.Equal(Long, x));
    }
}
