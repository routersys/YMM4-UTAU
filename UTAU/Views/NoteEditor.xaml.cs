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
        PitchHandle,
        Expression,
        Select,
    }

    const double WheelScrollStep = 64.0;

    DragMode dragMode;
    NoteViewModel? dragTarget;
    PitchPoint? dragPoint;
    Point dragOrigin;
    bool dragMoved;
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

    internal UTAUVoicePronounce? Pronounce
    {
        get => (UTAUVoicePronounce?)GetValue(PronounceProperty);
        set => SetValue(PronounceProperty, value);
    }

    internal static readonly DependencyProperty PronounceProperty = DependencyProperty.Register(
        nameof(Pronounce),
        typeof(UTAUVoicePronounce),
        typeof(NoteEditor),
        new PropertyMetadata(null, OnPronounceChanged));

    static void OnPronounceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NoteEditor editor)
            return;

        editor.ViewModel?.Dispose();
        editor.DataContext = editor.Pronounce is null ? null : new NoteEditorViewModel(editor.Pronounce);
        editor.RequestFit();
    }

    NoteEditorViewModel? ViewModel => DataContext as NoteEditorViewModel;

    void RequestFit() => Dispatcher.BeginInvoke(UpdateFit, DispatcherPriority.Loaded);

    void UpdateFit() => ViewModel?.FitToViewport(HorizontalScroller.ActualWidth, VerticalScroller.ActualHeight);

    void Scroller_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateFit();

    void PopupButton_BeginEdit(object sender, EventArgs e) => BeginEdit?.Invoke(this, e);

    void PopupButton_EndEdit(object sender, EventArgs e) => EndEdit?.Invoke(this, e);

    void HorizontalScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        HorizontalBar.Maximum = Math.Max(e.ExtentWidth - e.ViewportWidth, 0.0);
        HorizontalBar.ViewportSize = e.ViewportWidth;
        HorizontalBar.LargeChange = e.ViewportWidth;
        HorizontalBar.SmallChange = WheelScrollStep;
        HorizontalBar.Value = e.HorizontalOffset;
        StripScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
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

    void RollCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is not { } viewModel)
            return;

        if (e.ClickCount >= 2)
        {
            if (viewModel.SelectedNote is not { } selected || selected.IsRest)
                return;

            var position = e.GetPosition(RollCanvas);
            var ticks = viewModel.TicksFromCanvasX(position.X) - selected.StartTicks;
            viewModel.AddPitchPointAt(
                Math.Clamp(ticks, 0, selected.Note.LengthTicks),
                viewModel.CentsFromCanvasY(position.Y, selected.Note.Tone));
            e.Handled = true;
            return;
        }

        dragMode = DragMode.Select;
        dragOrigin = e.GetPosition(RollCanvas);
        viewModel.UpdateSelectionBox(dragOrigin, dragOrigin);
        RollCanvas.CaptureMouse();
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

        e.Handled = true;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            viewModel.ToggleSelection(note);
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            viewModel.SelectRange(note);
            return;
        }

        viewModel.MakePrimary(note);
        BeginDrag(note, DragMode.Tone, e);
    }

    void NoteResize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NoteViewModel note } || ViewModel is not { } viewModel)
            return;

        viewModel.MakePrimary(note);
        BeginDrag(note, DragMode.Length, e);
        e.Handled = true;
    }

    void PitchHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PitchHandleViewModel handle } || ViewModel is not { } viewModel)
            return;

        viewModel.SelectedPitchPoint = handle.Point;
        dragMode = DragMode.PitchHandle;
        dragPoint = handle.Point;
        RollCanvas.CaptureMouse();
        e.Handled = true;
    }

    void PitchHandle_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PitchHandleViewModel handle } || ViewModel is not { } viewModel)
            return;

        viewModel.RemovePitchPoint(handle.Point);
        e.Handled = true;
    }

    void BeginDrag(NoteViewModel note, DragMode mode, MouseButtonEventArgs e)
    {
        dragMode = mode;
        dragTarget = note;
        dragOrigin = e.GetPosition(RollCanvas);
        dragMoved = false;
        ViewModel?.BeginTransform();
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

        if (dragMode == DragMode.Select)
        {
            viewModel.UpdateSelectionBox(dragOrigin, position);
            return;
        }

        if (dragMode == DragMode.PitchHandle && dragPoint is not null && viewModel.SelectedNote is { } owner)
        {
            var ticks = viewModel.TicksFromCanvasX(position.X) - owner.StartTicks;
            viewModel.MovePitchPoint(
                dragPoint,
                Math.Clamp(ticks, 0, owner.Note.LengthTicks),
                viewModel.CentsFromCanvasY(position.Y, owner.Note.Tone));
            viewModel.ShowPitchGuide(dragPoint);
            return;
        }

        if (dragMode == DragMode.None || dragTarget is null)
            return;

        if (dragMode == DragMode.Tone)
        {
            var delta = (int)Math.Round((dragOrigin.Y - position.Y) / viewModel.SemitoneHeight);
            dragMoved |= delta != 0;
            viewModel.TransformTones(delta);
            return;
        }

        var deltaTicks = viewModel.TicksFromCanvasX(position.X - dragOrigin.X);
        dragMoved |= deltaTicks != 0;
        viewModel.TransformLengths(deltaTicks);
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
        if (dragMode is DragMode.None or DragMode.Expression)
            return;

        if (dragMode == DragMode.Select)
        {
            dragMode = DragMode.None;
            RollCanvas.ReleaseMouseCapture();
            ViewModel?.CommitSelectionBox(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            return;
        }

        var shouldRelayout = dragMode is DragMode.Tone or DragMode.Length;
        var collapseTarget = dragMode == DragMode.Tone && !dragMoved && ViewModel is { SelectedCount: > 1 }
            ? dragTarget
            : null;
        dragMode = DragMode.None;
        dragTarget = null;
        dragPoint = null;
        dragMoved = false;
        RollCanvas.ReleaseMouseCapture();
        ViewModel?.EndTransform();
        if (collapseTarget is not null)
            ViewModel?.Select(collapseTarget);
        if (shouldRelayout)
            ViewModel?.InvalidateLayout();
    }

    void StripCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null)
            return;

        dragMode = DragMode.Expression;
        StripCanvas.CaptureMouse();
        ApplyExpression(e.GetPosition(StripCanvas));
        e.Handled = true;
    }

    void StripCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (dragMode != DragMode.Expression || e.LeftButton != MouseButtonState.Pressed)
            return;

        ApplyExpression(e.GetPosition(StripCanvas));
    }

    void StripCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndExpressionDrag();

    void StripCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            EndExpressionDrag();
    }

    void EndExpressionDrag()
    {
        if (dragMode != DragMode.Expression)
            return;

        dragMode = DragMode.None;
        StripCanvas.ReleaseMouseCapture();
        ViewModel?.CommitExpression();
    }

    void ApplyExpression(Point position)
    {
        if (ViewModel is not { } viewModel)
            return;

        var ticks = Math.Max(viewModel.TicksFromCanvasX(position.X), 0);
        viewModel.SetExpressionAt(ticks, 1.0 - position.Y / NoteEditorViewModel.StripHeight);
    }
}
