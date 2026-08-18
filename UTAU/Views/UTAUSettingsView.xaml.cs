using System.Windows.Controls;
using UTAU.ViewModels;

namespace UTAU.Views;

public partial class UTAUSettingsView : UserControl
{
    public UTAUSettingsView()
    {
        InitializeComponent();
        DataContext = new UTAUSettingsViewModel();
    }
}
