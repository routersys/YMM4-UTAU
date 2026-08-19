using System.Windows;
using System.Windows.Controls;

namespace UTAU.Tests;

public sealed class CanvasAlignmentTests
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

    static double TopOffset(FrameworkElement child)
    {
        var host = new Grid();
        host.Children.Add(child);
        host.Measure(new Size(400.0, 400.0));
        host.Arrange(new Rect(0.0, 0.0, 400.0, 400.0));
        host.UpdateLayout();
        return child.TranslatePoint(new Point(0.0, 0.0), host).Y;
    }

    [Fact]
    public void AnExplicitHeightTurnsStretchIntoCentering()
    {
        var offset = RunSta(() => TopOffset(new Canvas { Width = 200.0, Height = 300.0 }));

        Assert.Equal(50.0, offset, 6);
    }

    [Fact]
    public void PinningToTheTopRemovesTheOffset()
    {
        var offset = RunSta(() => TopOffset(new Canvas
        {
            Width = 200.0,
            Height = 300.0,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
        }));

        Assert.Equal(0.0, offset, 6);
    }

    [Fact]
    public void AStackedColumnAlwaysStartsAtTheTop()
    {
        var offset = RunSta(() =>
        {
            var panel = new StackPanel();
            for (var index = 0; index < 10; index++)
                panel.Children.Add(new Border { Height = 30.0 });
            return TopOffset(panel);
        });

        Assert.Equal(0.0, offset, 6);
    }

    [Fact]
    public void ThePinnedCanvasAndTheStackedColumnShareTheSameOrigin()
    {
        var offsets = RunSta(() =>
        {
            var panel = new StackPanel();
            for (var index = 0; index < 10; index++)
                panel.Children.Add(new Border { Height = 30.0 });

            var canvas = new Canvas
            {
                Width = 200.0,
                Height = 300.0,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var marker = new Border { Height = 30.0, Width = 50.0 };
            Canvas.SetTop(marker, 90.0);
            canvas.Children.Add(marker);

            var host = new Grid();
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42.0) });
            host.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(panel, 0);
            Grid.SetColumn(canvas, 1);
            host.Children.Add(panel);
            host.Children.Add(canvas);
            host.Measure(new Size(400.0, 400.0));
            host.Arrange(new Rect(0.0, 0.0, 400.0, 400.0));
            host.UpdateLayout();

            var rowTop = panel.Children[3].TranslatePoint(new Point(0.0, 0.0), host).Y;
            var noteTop = marker.TranslatePoint(new Point(0.0, 0.0), host).Y;
            return (rowTop, noteTop);
        });

        Assert.Equal(offsets.rowTop, offsets.noteTop, 6);
    }
}
