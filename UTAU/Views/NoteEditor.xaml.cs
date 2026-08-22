using System.Windows;
using System.Windows.Controls;
using UTAU.ViewModels;
using YukkuriMovieMaker.Commons;

namespace UTAU.Views;

public partial class NoteEditor : UserControl, IPropertyEditorControl
{
    readonly NoteEditorSurface surface = new();

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

        editor.ViewModel?.Dispose();
        editor.surface.DataContext = editor.Pronounce is null ? null : new NoteEditorViewModel(editor.Pronounce);
        editor.DataContext = editor.surface.DataContext;
        editor.surface.RequestFit();
    }

    NoteEditorViewModel? ViewModel => surface.DataContext as NoteEditorViewModel;

    void PopupButton_BeginEdit(object sender, EventArgs e) => BeginEdit?.Invoke(this, e);

    void PopupButton_EndEdit(object sender, EventArgs e) => EndEdit?.Invoke(this, e);
}
