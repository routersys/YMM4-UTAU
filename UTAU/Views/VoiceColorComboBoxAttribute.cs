using System.Windows;
using System.Windows.Data;
using UTAU.ViewModels;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Views.Converters;

namespace UTAU.Views;

internal sealed class VoiceColorComboBoxAttribute : PropertyEditorAttribute2, IPropertyEditorForVoiceParameterAttribute
{
    VoiceDescription? voiceDescription;

    public VoiceDescription? VoiceDescription
    {
        get => voiceDescription;
        set => Set(ref voiceDescription, value);
    }

    public override FrameworkElement Create() => new CommonComboBox();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not CommonComboBox comboBox)
            return;
        if (VoiceDescription?.Speaker is not UTAUVoiceSpeaker speaker)
            return;

        comboBox.ItemsSource = ComboBoxItems.CreateVoiceColors(speaker.Colors);
        comboBox.DisplayMemberPath = nameof(VoiceColorItem.Name);
        comboBox.SelectedValuePath = nameof(VoiceColorItem.Value);
        comboBox.SetBinding(CommonComboBox.ValueProperty, ItemPropertiesBinding.Create2(itemProperties));
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is not CommonComboBox comboBox)
            return;
        BindingOperations.ClearBinding(comboBox, CommonComboBox.ValueProperty);
        comboBox.ItemsSource = null;
        comboBox.DisplayMemberPath = null;
        comboBox.SelectedValuePath = null;
    }
}
