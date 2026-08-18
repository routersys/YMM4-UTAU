using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UTAU.Notes;
using UTAU.ViewModels;
using YukkuriMovieMaker.Commons;

namespace UTAU.Views;

public partial class NoteEditor : UserControl, IPropertyEditorControl
{
    enum DragMode
    {
        None,
        Tone,
        Length,
    }

    const double MinimumNoteLengthMilliseconds = 10.0;
    const double LengthSnapMilliseconds = 10.0;
    const double WheelScrollStep = 48.0;

    DragMode dragMode;
    NoteViewModel? dragTarget;
    Point dragOrigin;
    double dragOriginLength;
    int dragOriginTone;

    public event EventHandler? BeginEdit;

    public event EventHandler? EndEdit;

    public NoteEditor() => InitializeComponent();

    internal ObservableCollection<UTAUNote>? Notes
    {
        get => (ObservableCollection<UTAUNote>?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    internal static readonly DependencyProperty NotesProperty = DependencyProperty.Register(
        nameof(Notes),
        typeof(ObservableCollection<UTAUNote>),
        typeof(NoteEditor),
        new PropertyMetadata(null, OnNotesChanged));

    static void OnNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NoteEditor editor)
            return;

        editor.ViewModel?.Dispose();
        editor.DataContext = editor.Notes is null ? null : new NoteEditorViewModel(editor.Notes);
    }

    NoteEditorViewModel? ViewModel => DataContext as NoteEditorViewModel;

    void PopupButton_BeginEdit(object sender, EventArgs e) => BeginEdit?.Invoke(this, e);

    void PopupButton_EndEdit(object sender, EventArgs e) => EndEdit?.Invoke(this, e);

    void RollScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
        => KeyboardScroller.ScrollToVerticalOffset(e.VerticalOffset);

    void RollScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel is not { } viewModel)
            return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            viewModel.Zoom(e.Delta > 0 ? NoteEditorViewModel.ZoomStep : 1.0 / NoteEditorViewModel.ZoomStep);
            e.Handled = true;
            return;
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            return;

        RollScroller.ScrollToHorizontalOffset(RollScroller.HorizontalOffset - Math.Sign(e.Delta) * WheelScrollStep);
        e.Handled = true;
    }

    void Note_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NoteViewModel note } || ViewModel is not { } viewModel)
            return;

        viewModel.SelectedNote = note;
        BeginDrag(note, DragMode.Tone, e);
        e.Handled = true;
    }

    void NoteResize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NoteViewModel note } || ViewModel is not { } viewModel)
            return;

        viewModel.SelectedNote = note;
        BeginDrag(note, DragMode.Length, e);
        e.Handled = true;
    }

    void BeginDrag(NoteViewModel note, DragMode mode, MouseButtonEventArgs e)
    {
        dragMode = mode;
        dragTarget = note;
        dragOrigin = e.GetPosition(RollCanvas);
        dragOriginLength = note.LengthMilliseconds;
        dragOriginTone = note.Tone;
        RollCanvas.CaptureMouse();
    }

    void RollCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (dragMode == DragMode.None || dragTarget is null || ViewModel is not { } viewModel)
            return;

        var position = e.GetPosition(RollCanvas);
        if (dragMode == DragMode.Tone)
        {
            var delta = (int)Math.Round((dragOrigin.Y - position.Y) / viewModel.SemitoneHeight);
            dragTarget.Tone = Math.Clamp(dragOriginTone + delta, 0, 127);
        }
        else
        {
            var delta = viewModel.MillisecondsFromCanvasX(position.X - dragOrigin.X);
            var length = Math.Max(dragOriginLength + delta, MinimumNoteLengthMilliseconds);
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                length = Math.Max(Math.Round(length / LengthSnapMilliseconds) * LengthSnapMilliseconds, MinimumNoteLengthMilliseconds);
            dragTarget.LengthMilliseconds = length;
        }
    }

    void RollCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndDrag();

    void RollCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            EndDrag();
    }

    void EndDrag()
    {
        if (dragMode == DragMode.None)
            return;

        dragMode = DragMode.None;
        dragTarget = null;
        RollCanvas.ReleaseMouseCapture();
        ViewModel?.InvalidateLayout();
    }

    void PitchGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        => Dispatcher.BeginInvoke(() => ViewModel?.UpdatePitchCurve());
}
