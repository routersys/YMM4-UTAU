using System.Windows;
using System.Windows.Controls;

namespace UTAU.Tests;

public sealed class ToolbarHeightTests
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

    static (double Top, double Height) Place(FrameworkElement element)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var button = new Button { Width = RowHeight, Height = RowHeight };
        panel.Children.Add(button);
        panel.Children.Add(element);

        var host = new Grid();
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
        host.RowDefinitions.Add(new RowDefinition());
        host.Children.Add(panel);
        host.Measure(new Size(900.0, 400.0));
        host.Arrange(new Rect(0.0, 0.0, 900.0, 400.0));
        host.UpdateLayout();

        return (element.TranslatePoint(new Point(0.0, 0.0), host).Y, element.ActualHeight);
    }

    [Fact]
    public void ACenteredComboBoxLeavesAGapAboveAndBelow()
    {
        var placed = RunSta(() => Place(new ComboBox { Width = 88.0, VerticalAlignment = VerticalAlignment.Center }));

        Assert.True(placed.Height < RowHeight, $"height={placed.Height}");
        Assert.True(placed.Top > 0.0, $"top={placed.Top}");
    }

    [Fact]
    public void AnExplicitHeightFillsTheToolbarRow()
    {
        var placed = RunSta(() => Place(new ComboBox { Width = 88.0, Height = RowHeight }));

        Assert.Equal(RowHeight, placed.Height, 6);
        Assert.Equal(0.0, placed.Top, 6);
    }

    [Fact]
    public void TheComboBoxAndTheButtonShareTheSameBox()
    {
        var boxes = RunSta(() =>
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var button = new Button { Width = RowHeight, Height = RowHeight };
            var combo = new ComboBox { Width = 88.0, Height = RowHeight };
            panel.Children.Add(button);
            panel.Children.Add(combo);

            var host = new Grid();
            host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
            host.RowDefinitions.Add(new RowDefinition());
            host.Children.Add(panel);
            host.Measure(new Size(900.0, 400.0));
            host.Arrange(new Rect(0.0, 0.0, 900.0, 400.0));
            host.UpdateLayout();

            return (
                ButtonTop: button.TranslatePoint(new Point(0.0, 0.0), host).Y,
                ButtonHeight: button.ActualHeight,
                ComboTop: combo.TranslatePoint(new Point(0.0, 0.0), host).Y,
                ComboHeight: combo.ActualHeight);
        });

        Assert.Equal(boxes.ButtonTop, boxes.ComboTop, 6);
        Assert.Equal(boxes.ButtonHeight, boxes.ComboHeight, 6);
    }
}
