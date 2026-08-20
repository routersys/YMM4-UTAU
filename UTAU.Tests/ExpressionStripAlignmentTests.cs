using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UTAU.Notes;
using UTAU.ViewModels;
using UTAU.Views;
using YukkuriMovieMaker.Controls;

namespace UTAU.Tests;

public sealed class ExpressionStripAlignmentTests
{
    const double PopupWidth = 980.0;
    const double PopupHeight = 480.0;

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

    static (NoteEditor Editor, NoteEditorViewModel ViewModel, Border Host) Build(int noteCount)
    {
        var editor = new NoteEditor();
        var button = (PopupButton)editor.Content;
        var root = (Grid)button.PopupContent!;
        button.PopupContent = null;

        var pronounce = new UTAUVoicePronounce();
        for (var index = 0; index < noteCount; index++)
        {
            pronounce.Notes.Add(new UTAUNote
            {
                Lyric = "あ",
                Tone = 48 + index * 5 % 30,
                LengthTicks = TimeBase.TicksPerQuarterNote / 2,
            });
        }

        var viewModel = new NoteEditorViewModel(pronounce);
        root.DataContext = viewModel;

        var host = new Border { Width = PopupWidth, Height = PopupHeight, Child = root };
        Pump(host);
        return (editor, viewModel, host);
    }

    static void Pump(FrameworkElement host)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            host.Measure(new Size(PopupWidth, PopupHeight));
            host.Arrange(new Rect(0.0, 0.0, PopupWidth, PopupHeight));
            host.UpdateLayout();
        }
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

    static ItemsControl Bound(DependencyObject root, object itemsSource)
        => Descend(root).OfType<ItemsControl>().First(x => ReferenceEquals(x.ItemsSource, itemsSource));

    static double LeftOf(FrameworkElement element, UIElement host)
        => element.TranslatePoint(new Point(0.0, 0.0), host).X;

    [Fact]
    public void TheStripAndTheRollShareTheSameHorizontalViewport()
    {
        var widths = RunSta(() =>
        {
            var (editor, _, _) = Build(40);
            return (editor.HorizontalScroller.ViewportWidth, editor.StripScroller.ViewportWidth);
        });

        Assert.True(widths.Item1 > 0.0, $"roll viewport={widths.Item1}");
        Assert.Equal(widths.Item1, widths.Item2, 6);
    }

    [Fact]
    public void TheVerticalScrollBarIsVisibleSoTheViewportsCouldDiffer()
    {
        var visibility = RunSta(() =>
        {
            var (editor, _, _) = Build(40);
            return editor.VerticalScroller.ComputedVerticalScrollBarVisibility;
        });

        Assert.Equal(Visibility.Visible, visibility);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(40.0)]
    [InlineData(100.0)]
    [InlineData(100000.0)]
    public void TheExpressionBarStaysUnderItsNoteAtEveryScrollOffset(double offset)
    {
        var drift = RunSta(() =>
        {
            var (editor, viewModel, host) = Build(40);
            editor.HorizontalScroller.ScrollToHorizontalOffset(offset);
            Pump(host);

            var rollItems = Bound(editor.RollCanvas, viewModel.VisibleNotes);
            var stripItems = Bound(editor.StripCanvas, viewModel.ExpressionBars);
            var worst = 0.0;
            foreach (var bar in viewModel.ExpressionBars)
            {
                var noteContainer = rollItems.ItemContainerGenerator.ContainerFromItem(bar.Note) as FrameworkElement;
                var barContainer = stripItems.ItemContainerGenerator.ContainerFromItem(bar) as FrameworkElement;
                if (noteContainer is null || barContainer is null)
                    continue;
                worst = Math.Max(worst, Math.Abs(LeftOf(barContainer, host) - LeftOf(noteContainer, host)));
            }
            return worst;
        });

        Assert.Equal(0.0, drift, 6);
    }

    [Fact]
    public void ScrollingToTheEndKeepsBothCanvasesOnTheSameOffset()
    {
        var offsets = RunSta(() =>
        {
            var (editor, _, host) = Build(40);
            editor.HorizontalScroller.ScrollToHorizontalOffset(double.MaxValue);
            Pump(host);
            return (editor.HorizontalScroller.HorizontalOffset, editor.StripScroller.HorizontalOffset);
        });

        Assert.True(offsets.Item1 > 0.0, $"roll offset={offsets.Item1}");
        Assert.Equal(offsets.Item1, offsets.Item2, 6);
    }
}
