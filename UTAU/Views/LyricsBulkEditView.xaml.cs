using System.Windows;
using System.Windows.Controls;

namespace UTAU.Views;

public partial class LyricsBulkEditView : UserControl
{
    public LyricsBulkEditView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LyricBox.Focus();
            LyricBox.SelectAll();
        };
    }

    public event EventHandler? Applied;

    void Apply_Click(object sender, RoutedEventArgs e) => Applied?.Invoke(this, EventArgs.Empty);
}
