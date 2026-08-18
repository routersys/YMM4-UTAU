using System.Reflection;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace UTAU.Views;

internal sealed class NoteEditorAttribute : PropertyEditorAttribute
{
    public override FrameworkElement Create() => new NoteEditor();

    public override void SetBindings(FrameworkElement control, object item, object propertyOwner, PropertyInfo propertyInfo)
    {
        if (control is not NoteEditor editor)
            return;
        if (propertyOwner is not UTAUVoicePronounce pronounce)
            return;

        editor.Pronounce = pronounce;
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is NoteEditor editor)
            editor.Pronounce = null;
    }
}
