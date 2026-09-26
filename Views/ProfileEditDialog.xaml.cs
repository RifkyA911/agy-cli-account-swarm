using System.Windows;
using Microsoft.Win32;
using AgyAccountSwarm.ViewModels;

namespace AgyAccountSwarm.Views;

public partial class ProfileEditDialog : Window
{
    private readonly ProfileEditViewModel _viewModel;

    public ProfileEditDialog(ProfileEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RequestClose += (success) =>
        {
            DialogResult = success;
            Close();
        };
    }

    private void BrowseWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Workspace / Project Directory",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.DefaultWorkspace = dialog.FolderName;
        }
    }

    private void BrowseProfileDir_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Custom Isolated Profile Directory",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.CustomProfilePath = dialog.FolderName;
        }
    }
}
