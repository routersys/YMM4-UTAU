using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UTAU.Notes;
using UTAU.ViewModels;
using UTAU.Views;

namespace UTAU.Tests;

[Collection("Wpf")]
public sealed class NoteContextMenuTests
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

    static (NoteEditorSurface Surface, NoteEditorViewModel ViewModel) Build(params int[] tones)
    {
        var pronounce = new UTAUVoicePronounce();
        foreach (var tone in tones)
            pronounce.Notes.Add(new UTAUNote { Lyric = "あ", Tone = tone, LengthTicks = UTAUNote.DefaultLengthTicks });

        var viewModel = new NoteEditorViewModel(pronounce);
        var surface = new NoteEditorSurface { DataContext = viewModel };
        var host = new Border { Width = PopupWidth, Height = PopupHeight, Child = surface };
        for (var pass = 0; pass < 3; pass++)
        {
            host.Measure(new Size(PopupWidth, PopupHeight));
            host.Arrange(new Rect(0.0, 0.0, PopupWidth, PopupHeight));
            host.UpdateLayout();
        }

        return (surface, viewModel);
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

    static FrameworkElement NoteBody(NoteEditorSurface surface, NoteViewModel note)
        => Descend(surface.RollCanvas)
            .OfType<Grid>()
            .First(x => ReferenceEquals(x.DataContext, note));

    [Fact]
    public void TheRollCanvasCarriesAContextMenu()
    {
        var headers = RunSta(() =>
        {
            var (surface, _) = Build(60, 62);
            return surface.RollCanvas.ContextMenu!.Items
                .OfType<MenuItem>()
                .Select(x => (string)x.Header)
                .ToArray();
        });

        Assert.Equal(
            [
                Texts.NoteContextCopy,
                Texts.NoteContextPaste,
                Texts.SelectAllNotes,
                Texts.OctaveUp,
                Texts.OctaveDown,
                Texts.QuantizeLength,
                Texts.ResetGroup,
                Texts.EditLyrics,
                Texts.DetachEditor,
            ],
            headers);
    }

    [Fact]
    public void TheResetGroupHoldsTheFourResets()
    {
        var headers = RunSta(() =>
        {
            var (surface, _) = Build(60, 62);
            var group = surface.RollCanvas.ContextMenu!.Items.OfType<MenuItem>()
                .First(x => (string)x.Header == Texts.ResetGroup);
            return group.Items.OfType<MenuItem>().Select(x => (string)x.Header).ToArray();
        });

        Assert.Equal(
            [Texts.ResetPitch, Texts.ResetVibrato, Texts.ResetTiming, Texts.ResetNote],
            headers);
    }

    [Fact]
    public void EveryMenuEntryIsBoundToACommand()
    {
        var unbound = RunSta(() =>
        {
            var (surface, _) = Build(60, 62);
            var items = surface.RollCanvas.ContextMenu!.Items.OfType<MenuItem>().ToList();
            var leaves = items.Where(x => x.Items.Count == 0)
                .Concat(items.SelectMany(x => x.Items.OfType<MenuItem>()))
                .ToList();
            return leaves.Where(x => x.Command is null).Select(x => (string)x.Header).ToArray();
        });

        Assert.Empty(unbound);
    }

    [Fact]
    public void TheMenuCommandsAreTheOnesOfTheEditor()
    {
        var same = RunSta(() =>
        {
            var (surface, viewModel) = Build(60, 62);
            var items = surface.RollCanvas.ContextMenu!.Items.OfType<MenuItem>().ToList();
            return ReferenceEquals(items[0].Command, viewModel.CopyNotesCommand)
                && ReferenceEquals(items[3].Command, viewModel.OctaveUpCommand)
                && ReferenceEquals(items[4].Command, viewModel.OctaveDownCommand)
                && ReferenceEquals(items[5].Command, viewModel.QuantizeLengthCommand)
                && ReferenceEquals(items[6].Items.OfType<MenuItem>().Last().Command, viewModel.ResetNoteCommand)
                && ReferenceEquals(items[7].Command, viewModel.EditLyricsCommand)
                && ReferenceEquals(items[8].Command, viewModel.DetachCommand);
        });

        Assert.True(same);
    }

    [Fact]
    public void RightClickingAnUnselectedNoteSelectsIt()
    {
        var selected = RunSta(() =>
        {
            var (surface, viewModel) = Build(60, 62, 64);
            viewModel.Select(viewModel.Notes[0]);
            var body = NoteBody(surface, viewModel.Notes[2]);
            body.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right)
            {
                RoutedEvent = UIElement.MouseRightButtonDownEvent,
            });
            return (viewModel.SelectedCount, IsThird: ReferenceEquals(viewModel.SelectedNote, viewModel.Notes[2]));
        });

        Assert.Equal(1, selected.SelectedCount);
        Assert.True(selected.IsThird);
    }

    [Fact]
    public void RightClickingInsideTheSelectionKeepsIt()
    {
        var count = RunSta(() =>
        {
            var (surface, viewModel) = Build(60, 62, 64);
            viewModel.SelectAll();
            var body = NoteBody(surface, viewModel.Notes[1]);
            body.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right)
            {
                RoutedEvent = UIElement.MouseRightButtonDownEvent,
            });
            return viewModel.SelectedCount;
        });

        Assert.Equal(3, count);
    }
}
