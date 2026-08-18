using System.Windows;
using System.Windows.Data;
using UTAU.ViewModels;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Views.Converters;

namespace UTAU.Views;

internal sealed class ToneComboBoxAttribute : PropertyEditorAttribute2
{
    public const int MinimumTone = 24;
    public const int MaximumTone = 96;

    public override FrameworkElement Create() => new CommonComboBox();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        if (control is not CommonComboBox comboBox)
            return;

        comboBox.ItemsSource = ComboBoxItems.CreateTones(MinimumTone, MaximumTone);
        comboBox.DisplayMemberPath = nameof(ToneItem.Name);
        comboBox.SelectedValuePath = nameof(ToneItem.Value);
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
