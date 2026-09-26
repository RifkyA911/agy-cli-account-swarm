using System.Threading.Tasks;
using System.Windows;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.ViewModels;

namespace AgyAccountSwarm.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.ShowEditDialogRequested += OnShowEditDialogAsync;
        _viewModel.ConfirmDeleteRequested += OnConfirmDeleteAsync;

        Loaded += async (s, e) => await _viewModel.InitializeAsync();
    }

    private Task<AccountProfile?> OnShowEditDialogAsync(AccountProfile? profileToEdit)
    {
        var editVm = profileToEdit != null
            ? new ProfileEditViewModel(profileToEdit)
            : new ProfileEditViewModel();

        var dialog = new ProfileEditDialog(editVm)
        {
            Owner = this
        };

        var result = dialog.ShowDialog();
        if (result == true)
        {
            return Task.FromResult<AccountProfile?>(editVm.ResultProfile);
        }

        return Task.FromResult<AccountProfile?>(null);
    }

    private Task<bool> OnConfirmDeleteAsync(string title, string message)
    {
        var result = MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
