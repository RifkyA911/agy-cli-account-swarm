using System.Windows;
using AgyAccountSwarm.ViewModels;

namespace AgyAccountSwarm.Views;

public partial class ImportChatDialog : Window
{
    private readonly ImportChatViewModel _viewModel;

    public ImportChatDialog(ImportChatViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RequestClose += (success, message) =>
        {
            DialogResult = success;
            Close();
        };
    }
}
