using System.Windows;
using UTAU.ViewModels;

namespace UTAU.Views;

public partial class LyricsBulkEditWindow : Window
{
    internal LyricsBulkEditWindow(LyricsBulkEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        View.Applied += OnApplied;
    }

    internal LyricsBulkEditViewModel ViewModel { get; }

    void OnApplied(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
