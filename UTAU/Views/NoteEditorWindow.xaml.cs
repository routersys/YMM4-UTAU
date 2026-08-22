using System.Windows;

namespace UTAU.Views;

public partial class NoteEditorWindow : Window
{
    internal NoteEditorWindow(NoteEditorSurface surface)
    {
        InitializeComponent();
        SurfaceHost.Content = surface;
    }

    internal NoteEditorSurface? Surface => SurfaceHost.Content as NoteEditorSurface;

    internal NoteEditorSurface? Release()
    {
        var surface = SurfaceHost.Content as NoteEditorSurface;
        SurfaceHost.Content = null;
        return surface;
    }
}
