using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
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

    const double WheelScrollStep = 64.0;

    DragMode dragMode;
    NoteViewModel? dragTarget;
    Point dragOrigin;
    int dragOriginLengthTicks;
    int dragOriginTone;
    bool isPanning;
    Point panOrigin;
    double panHorizontalOffset;
    double panVerticalOffset;

    public event EventHandler? BeginEdit;

    public event EventHandler? EndEdit;

    public NoteEditor()
    {
        InitializeComponent();
        Loaded += (_, _) => RequestFit();
    }

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
        editor.RequestFit();
    }

    void RequestFit() => Dispatcher.BeginInvoke(UpdateFit, DispatcherPriority.Loaded);

    void UpdateFit() => ViewModel?.FitToViewport(HorizontalScroller.ActualWidth, VerticalScroller.ActualHeight);

    void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateFit();

    NoteEditorViewModel? ViewModel => DataContext as NoteEditorViewModel;

    void PopupButton_BeginEdit(object sender, EventArgs e) => BeginEdit?.Invoke(this, e);

    void PopupButton_EndEdit(object sender, EventArgs e) => EndEdit?.Invoke(this, e);

    void HorizontalScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        HorizontalBar.Maximum = Math.Max(e.ExtentWidth - e.ViewportWidth, 0.0);
        HorizontalBar.ViewportSize = e.ViewportWidth;
        HorizontalBar.LargeChange = e.ViewportWidth;
        HorizontalBar.SmallChange = WheelScrollStep;
        HorizontalBar.Value = e.HorizontalOffset;
    }

    void HorizontalBar_Scroll(object sender, ScrollEventArgs e)
        => HorizontalScroller.ScrollToHorizontalOffset(e.NewValue);

    void RollCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel is not { } viewModel)
            return;

        var factor = e.Delta > 0 ? NoteEditorViewModel.ZoomStep : 1.0 / NoteEditorViewModel.ZoomStep;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                viewModel.ZoomVertically(factor);
            else
                viewModel.ZoomHorizontally(factor);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            HorizontalScroller.ScrollToHorizontalOffset(HorizontalScroller.HorizontalOffset - Math.Sign(e.Delta) * WheelScrollStep);
            e.Handled = true;
            return;
        }

        VerticalScroller.ScrollToVerticalOffset(VerticalScroller.VerticalOffset - Math.Sign(e.Delta) * WheelScrollStep);
        e.Handled = true;
    }

    void RollCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;

        isPanning = true;
        panOrigin = e.GetPosition(RollCanvas);
        panHorizontalOffset = HorizontalScroller.HorizontalOffset;
        panVerticalOffset = VerticalScroller.VerticalOffset;
        RollCanvas.CaptureMouse();
        e.Handled = true;
    }

    void RollCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;

        EndPan();
        e.Handled = true;
    }

    void EndPan()
    {
        if (!isPanning)
            return;

        isPanning = false;
        RollCanvas.ReleaseMouseCapture();
    }

    void Key_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: KeyRowViewModel row } || ViewModel is not { } viewModel)
            return;

        viewModel.SelectToneCommand.Execute(row);
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
        dragOriginLengthTicks = note.LengthTicks;
        dragOriginTone = note.Tone;
        RollCanvas.CaptureMouse();
    }

    void RollCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel is not { } viewModel)
            return;

        var position = e.GetPosition(RollCanvas);
        viewModel.UpdateGuide(position);

        if (isPanning)
        {
            HorizontalScroller.ScrollToHorizontalOffset(panHorizontalOffset - (position.X - panOrigin.X));
            VerticalScroller.ScrollToVerticalOffset(panVerticalOffset - (position.Y - panOrigin.Y));
            return;
        }

        if (dragMode == DragMode.None || dragTarget is null)
            return;

        if (dragMode == DragMode.Tone)
        {
            var delta = (int)Math.Round((dragOrigin.Y - position.Y) / viewModel.SemitoneHeight);
            dragTarget.Tone = Math.Clamp(dragOriginTone + delta, 0, 127);
            return;
        }

        var deltaTicks = viewModel.TicksFromCanvasX(position.X - dragOrigin.X);
        dragTarget.LengthTicks = viewModel.SnapLength(dragOriginLengthTicks + deltaTicks);
    }

    void RollCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndDrag();

    void RollCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        ViewModel?.HideGuide();
        if (e.MiddleButton != MouseButtonState.Pressed)
            EndPan();
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
}
