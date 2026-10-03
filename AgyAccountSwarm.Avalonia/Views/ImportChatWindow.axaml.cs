using Avalonia.Controls;
using Avalonia.Interactivity;
using AgyAccountSwarm.ViewModels;

namespace AgyAccountSwarm.Avalonia.Views;

public partial class ImportChatWindow : Window
{
    public ImportChatWindow()
    {
        InitializeComponent();
    }

    public ImportChatWindow(ImportChatViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += (success, msg) =>
        {
            Close(success);
        };
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
