using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UTAU.ViewModels;

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

public sealed class KeyboardRowAlignmentTests
{
    const int RowCount = 60;

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

    static DataTemplate RowTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(FrameworkElement.HeightProperty, new Binding(nameof(KeyRowViewModel.Height)));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0.0, 0.0, 1.0, 1.0));
        return new DataTemplate { VisualTree = border };
    }

    static double WorstDrift(double semitoneHeight, bool roundingInsideTheEditor)
    {
        var rows = new List<KeyRowViewModel>();
        for (var index = 0; index < RowCount; index++)
            rows.Add(new KeyRowViewModel { Height = semitoneHeight, Name = "C4", NoteNumber = 100 - index, RollWidth = 1200.0 });

        var keys = new ItemsControl { ItemsSource = rows, ItemTemplate = RowTemplate() };
        var canvas = new Canvas
        {
            Width = 1200.0,
            Height = semitoneHeight * RowCount,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var markers = new List<Border>();
        for (var index = 0; index < RowCount; index++)
        {
            var marker = new Border { Height = Math.Max(semitoneHeight - 1.0, 1.0), Width = 20.0 };
            Canvas.SetTop(marker, index * semitoneHeight);
            canvas.Children.Add(marker);
            markers.Add(marker);
        }

        var editor = new Grid { UseLayoutRounding = roundingInsideTheEditor };
        editor.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42.0) });
        editor.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(keys, 0);
        Grid.SetColumn(canvas, 1);
        editor.Children.Add(keys);
        editor.Children.Add(canvas);

        var host = new Grid { Width = 900.0, Height = 400.0, UseLayoutRounding = true };
        host.Children.Add(editor);
        host.Measure(new Size(900.0, 400.0));
        host.Arrange(new Rect(0.0, 0.0, 900.0, 400.0));
        host.UpdateLayout();

        var worst = 0.0;
        for (var index = 0; index < RowCount; index++)
        {
            var row = (FrameworkElement)keys.ItemContainerGenerator.ContainerFromIndex(index);
            var keyTop = row.TranslatePoint(new Point(0.0, 0.0), host).Y;
            var noteTop = markers[index].TranslatePoint(new Point(0.0, 0.0), host).Y;
            worst = Math.Max(worst, Math.Abs(noteTop - keyTop));
        }
        return worst;
    }

    [Theory]
    [InlineData(10.24)]
    [InlineData(12.25)]
    [InlineData(8.192)]
    [InlineData(31.25)]
    public void RoundingInsideTheEditorMakesTheRowsDriftAwayFromTheNotes(double semitoneHeight)
    {
        Assert.True(RunSta(() => WorstDrift(semitoneHeight, true)) > 1.0);
    }

    [Theory]
    [InlineData(16.0)]
    [InlineData(10.24)]
    [InlineData(12.25)]
    [InlineData(8.192)]
    [InlineData(31.25)]
    public void TurningRoundingOffKeepsTheRowsOnTheNotes(double semitoneHeight)
    {
        Assert.Equal(0.0, RunSta(() => WorstDrift(semitoneHeight, false)), 6);
    }
}
