using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UTAU.Notes;
using UTAU.ViewModels;
using UTAU.Views;
using YukkuriMovieMaker.Controls;

namespace UTAU.Tests;

public sealed class NoteEditorMeasuredLayoutTests
{
    const double PopupWidth = 980.0;
    const double PopupHeight = 480.0;
    const int NoteCount = 40;

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

    static (NoteEditor Editor, NoteEditorViewModel ViewModel, Border Host) Build()
    {
        var editor = new NoteEditor();
        var button = (PopupButton)editor.Content;
        var root = (Grid)button.PopupContent;
        button.PopupContent = null;

        var pronounce = new UTAUVoicePronounce();
        for (var index = 0; index < NoteCount; index++)
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

        var host = new Border
        {
            Width = PopupWidth,
            Height = PopupHeight,
            UseLayoutRounding = true,
            Child = root,
        };
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

    static bool IsUnder(DependencyObject node, DependencyObject ancestor)
    {
        for (var parent = VisualTreeHelper.GetParent(node); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (ReferenceEquals(parent, ancestor))
                return true;
        }
        return false;
    }

    static ItemsControl KeyboardColumn(NoteEditor editor, NoteEditorViewModel viewModel)
        => Descend(editor.VerticalScroller)
            .OfType<ItemsControl>()
            .First(x => ReferenceEquals(x.ItemsSource, viewModel.Keyboard) && !IsUnder(x, editor.RollCanvas));

    static ItemsControl Bound(DependencyObject root, object itemsSource)
        => Descend(root).OfType<ItemsControl>().First(x => ReferenceEquals(x.ItemsSource, itemsSource));

    static double TopOf(FrameworkElement element, UIElement host)
        => element.TranslatePoint(new Point(0.0, 0.0), host).Y;

    static double WorstRowDrift(int verticalZoomSteps)
    {
        var (editor, viewModel, host) = Build();
        for (var step = 0; step < Math.Abs(verticalZoomSteps); step++)
        {
            viewModel.ZoomVertically(verticalZoomSteps > 0
                ? NoteEditorViewModel.ZoomStep
                : 1.0 / NoteEditorViewModel.ZoomStep);
        }
        Pump(host);

        var keys = KeyboardColumn(editor, viewModel);
        var notes = Bound(editor.RollCanvas, viewModel.VisibleNotes);
        var worst = 0.0;
        foreach (var note in viewModel.VisibleNotes)
        {
            var rowIndex = viewModel.MaximumTone - note.Note.Tone;
            if (rowIndex < 0 || rowIndex >= viewModel.Keyboard.Count)
                continue;
            if (keys.ItemContainerGenerator.ContainerFromIndex(rowIndex) is not FrameworkElement row)
                continue;
            if (notes.ItemContainerGenerator.ContainerFromItem(note) is not FrameworkElement container)
                continue;
            worst = Math.Max(worst, Math.Abs(TopOf(container, host) - TopOf(row, host)));
        }
        return worst;
    }

    [Theory]
    [InlineData(-4)]
    [InlineData(-3)]
    [InlineData(-2)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryNoteSitsOnItsKeyboardRow(int verticalZoomSteps)
    {
        Assert.Equal(0.0, RunSta(() => WorstRowDrift(verticalZoomSteps)), 6);
    }

    [Fact]
    public void TheRollCanvasStartsAtTheKeyboardColumnOrigin()
    {
        var offsets = RunSta(() =>
        {
            var (editor, viewModel, host) = Build();
            var keys = KeyboardColumn(editor, viewModel);
            return (TopOf(keys, host), TopOf(editor.RollCanvas, host));
        });

        Assert.Equal(offsets.Item1, offsets.Item2, 6);
    }

    [Fact]
    public void ThePitchHandlesSitOnThePitchCurve()
    {
        var worst = RunSta(() =>
        {
            var (editor, viewModel, host) = Build();
            var note = viewModel.Notes[0];
            viewModel.Select(note);
            note.Note.PitchPoints.Add(new PitchPoint(0, -300.0));
            note.Note.PitchPoints.Add(new PitchPoint(120, 250.0));
            note.Note.PitchPoints.Add(new PitchPoint(240, 0.0));
            Pump(host);

            var handles = Bound(editor.RollCanvas, viewModel.PitchHandles);
            var drift = 0.0;
            for (var index = 0; index < viewModel.PitchHandles.Count; index++)
            {
                var handle = viewModel.PitchHandles[index];
                if (handles.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
                    continue;

                var centre = TopOf(container, host) + NoteEditorViewModel.PitchHandleSize / 2.0;
                var expected = viewModel.ToCanvasPoint(
                    note.StartTicks + handle.Point.Ticks,
                    note.Note.Tone + handle.Point.Cents / 100.0);
                var canvasTop = TopOf(editor.RollCanvas, host);
                drift = Math.Max(drift, Math.Abs(centre - (canvasTop + expected.Y)));
            }
            return drift;
        });

        Assert.Equal(0.0, worst, 6);
    }

    static StackPanel Toolbar(Border host)
        => Descend(host)
            .OfType<StackPanel>()
            .First(x => x.Orientation == Orientation.Horizontal
                && x.Children.OfType<ComboBox>().Any());

    [Fact]
    public void TheToolbarComboBoxTextIsCentredInItsRow()
    {
        var gaps = RunSta(() =>
        {
            var (_, _, host) = Build();
            return Toolbar(host).Children
                .OfType<ComboBox>()
                .Select(combo =>
                {
                    var text = Descend(combo).OfType<TextBlock>().First();
                    var above = TopOf(text, combo);
                    return (Above: above, Below: combo.ActualHeight - above - text.ActualHeight);
                })
                .ToArray();
        });

        Assert.NotEmpty(gaps);
        Assert.All(gaps, x => Assert.True(x.Above > 0.0, $"above={x.Above}"));
        Assert.All(gaps, x => Assert.Equal(x.Above, x.Below, 6));
    }

    [Fact]
    public void TheToolbarControlsAllFillTheToolbarRow()
    {
        var boxes = RunSta(() =>
        {
            var (_, _, host) = Build();
            var toolbar = Toolbar(host);

            return toolbar.Children
                .OfType<Control>()
                .Select(x => (x.GetType().Name, Top: TopOf(x, host), x.ActualHeight))
                .ToArray();
        });

        Assert.NotEmpty(boxes);
        var rowTop = boxes[0].Top;
        var rowHeight = boxes[0].ActualHeight;
        Assert.All(boxes, x => Assert.Equal(rowTop, x.Top, 6));
        Assert.All(boxes, x => Assert.Equal(rowHeight, x.ActualHeight, 6));
    }
}
