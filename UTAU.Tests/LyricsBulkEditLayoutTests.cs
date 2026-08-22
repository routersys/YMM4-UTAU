using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UTAU.ViewModels;
using UTAU.Views;

namespace UTAU.Tests;

[Collection("Wpf")]
public sealed class LyricsBulkEditLayoutTests
{
    const double RowHeight = 26.0;

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

    sealed record Placed(string Name, double Left, double Top, double Width, double Height)
    {
        public double Right => Left + Width;

        public double Bottom => Top + Height;
    }

    static (Placed[] Buttons, Placed Box, double Width, double Height) Measure(double width, double height)
        => RunSta(() =>
        {
            var view = new LyricsBulkEditView { DataContext = new LyricsBulkEditViewModel("あ い う", 3) };
            var host = new Border { Width = width, Height = height, Child = view };
            for (var pass = 0; pass < 3; pass++)
            {
                host.Measure(new Size(width, height));
                host.Arrange(new Rect(0.0, 0.0, width, height));
                host.UpdateLayout();
            }

            Placed Place(FrameworkElement element, string name)
            {
                var origin = element.TranslatePoint(new Point(0.0, 0.0), host);
                return new Placed(name, origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
            }

            var buttons = Descend(host)
                .OfType<Button>()
                .Select(x => Place(x, (string)x.Content))
                .OrderBy(x => x.Left)
                .ToArray();
            var box = Place((TextBox)view.FindName("LyricBox")!, "LyricBox");
            return (buttons, box, width, height);
        });

    [Fact]
    public void TheButtonsAreNotCutOffAtTheBottom()
    {
        var placed = Measure(560.0, 280.0);

        Assert.NotEmpty(placed.Buttons);
        foreach (var button in placed.Buttons)
            Assert.True(button.Bottom <= placed.Height, $"{button.Name} bottom={button.Bottom} host={placed.Height}");

        Assert.Equal(placed.Height, placed.Buttons.Max(x => x.Bottom), 6);
    }

    [Fact]
    public void TheButtonsFillTheRowWithoutAGapAboveOrBelow()
    {
        var placed = Measure(560.0, 280.0);

        foreach (var button in placed.Buttons)
        {
            Assert.Equal(RowHeight, button.Height, 6);
            Assert.Equal(placed.Height - RowHeight, button.Top, 6);
        }
    }

    [Fact]
    public void TheButtonsSitAgainstEachOtherWithNoGap()
    {
        var placed = Measure(560.0, 280.0);

        for (var index = 1; index < placed.Buttons.Length; index++)
            Assert.Equal(placed.Buttons[index - 1].Right, placed.Buttons[index].Left, 6);
    }

    [Fact]
    public void TheLastButtonReachesTheRightEdge()
    {
        var placed = Measure(560.0, 280.0);

        Assert.Equal(placed.Width, placed.Buttons[^1].Right, 6);
    }

    [Fact]
    public void TheTextBoxFillsEverythingAboveTheButtons()
    {
        var placed = Measure(560.0, 280.0);

        Assert.Equal(0.0, placed.Box.Left, 6);
        Assert.Equal(0.0, placed.Box.Top, 6);
        Assert.Equal(placed.Width, placed.Box.Width, 6);
        Assert.Equal(placed.Height - RowHeight, placed.Box.Height, 6);
    }

    [Fact]
    public void NothingIsCutOffAtTheSmallestAllowedSize()
    {
        var placed = Measure(360.0, 220.0);

        foreach (var button in placed.Buttons)
        {
            Assert.True(button.Bottom <= placed.Height, $"{button.Name} bottom={button.Bottom}");
            Assert.Equal(RowHeight, button.Height, 6);
        }

        Assert.Equal(placed.Width, placed.Buttons[^1].Right, 6);
        Assert.True(placed.Box.Height > 0.0);
    }

    [Fact]
    public void TheHintIsCarriedByTheTextBoxRatherThanTakingUpARow()
    {
        var tip = RunSta(() =>
        {
            var view = new LyricsBulkEditView { DataContext = new LyricsBulkEditViewModel("あ", 1) };
            return ((TextBox)view.FindName("LyricBox")!).ToolTip;
        });

        Assert.Equal(Texts.LyricsBulkEditHint, tip);
    }
}
