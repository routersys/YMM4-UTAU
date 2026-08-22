using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using UTAU.ViewModels;
using YukkuriMovieMaker.Commons;

namespace UTAU.Views;

public partial class NoteEditor : UserControl, IPropertyEditorControl
{
    readonly NoteEditorSurface surface = new();
    NoteEditorWindow? detached;
    NoteEditorViewModel? attached;

    public event EventHandler? BeginEdit;

    public event EventHandler? EndEdit;

    public NoteEditor()
    {
        InitializeComponent();
        SurfaceHost.Content = surface;
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

        editor.CloseDetached();
        editor.Attach(null);
        editor.ViewModel?.Dispose();
        editor.surface.DataContext = editor.Pronounce is null ? null : new NoteEditorViewModel(editor.Pronounce);
        editor.DataContext = editor.surface.DataContext;
        editor.Attach(editor.surface.DataContext as NoteEditorViewModel);
        editor.surface.RequestFit();
    }

    NoteEditorViewModel? ViewModel => surface.DataContext as NoteEditorViewModel;

    internal NoteEditorWindow? DetachedWindow => detached;

    void Attach(NoteEditorViewModel? viewModel)
    {
        if (attached is not null)
            attached.DetachRequested -= OnDetachRequested;

        attached = viewModel;

        if (attached is not null)
            attached.DetachRequested += OnDetachRequested;
    }

    void OnDetachRequested(object? sender, EventArgs e) => Detach();

    internal void Detach()
    {
        if (detached is not null)
        {
            detached.Activate();
            return;
        }

        ClosePopup();
        SurfaceHost.Content = null;
        PopupHost.IsEnabled = false;
        if (ViewModel is { } viewModel)
            viewModel.IsDetached = true;

        detached = new NoteEditorWindow(surface);
        if (OwnerWindow() is { } owner)
            detached.Owner = owner;
        detached.Closed += OnDetachedClosed;
        BeginEdit?.Invoke(this, EventArgs.Empty);
        detached.Show();
        surface.RequestFit();
    }

    internal void CloseDetached() => detached?.Close();

    void OnDetachedClosed(object? sender, EventArgs e)
    {
        if (detached is null)
            return;

        detached.Closed -= OnDetachedClosed;
        detached.Release();
        detached = null;
        SurfaceHost.Content = surface;
        PopupHost.IsEnabled = true;
        if (ViewModel is { } viewModel)
            viewModel.IsDetached = false;
        EndEdit?.Invoke(this, EventArgs.Empty);
    }

    void ClosePopup()
    {
        if (PopupHost.Template?.FindName("PART_Popup", PopupHost) is Popup popup)
            popup.IsOpen = false;
    }

    static Window? OwnerWindow()
        => Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive)
            ?? Application.Current?.MainWindow;

    void PopupButton_BeginEdit(object sender, EventArgs e) => BeginEdit?.Invoke(this, e);

    void PopupButton_EndEdit(object sender, EventArgs e) => EndEdit?.Invoke(this, e);
}
