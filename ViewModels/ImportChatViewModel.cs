using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgyAccountSwarm.ViewModels;

public partial class ImportChatViewModel : ObservableObject
{
    private readonly IConversationTransferService _transferService;
    private readonly IAudioService _audioService;

    public AccountProfile TargetProfile { get; }
    public ObservableCollection<AccountProfile> SourceProfiles { get; } = [];

    [ObservableProperty]
    private AccountProfile? _selectedSourceProfile;

    public ObservableCollection<TransferableConversation> Conversations { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isTransferring;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _filterQuery = string.Empty;

    public event Action<bool, string>? RequestClose;

    public ImportChatViewModel(
        AccountProfile targetProfile,
        IEnumerable<AccountProfile> allProfiles,
        IConversationTransferService transferService,
        IAudioService audioService)
    {
        TargetProfile = targetProfile;
        _transferService = transferService;
        _audioService = audioService;

        foreach (var p in allProfiles.Where(p => !string.Equals(p.Id, targetProfile.Id, StringComparison.OrdinalIgnoreCase)))
        {
            SourceProfiles.Add(p);
        }

        SelectedSourceProfile = SourceProfiles.FirstOrDefault();
    }

    async partial void OnSelectedSourceProfileChanged(AccountProfile? value)
    {
        await LoadConversationsAsync();
    }

    public async Task LoadConversationsAsync()
    {
        if (SelectedSourceProfile == null)
        {
            Conversations.Clear();
            return;
        }

        IsLoading = true;
        StatusMessage = $"Scanning conversations in '{SelectedSourceProfile.Name}'...";
        try
        {
            var list = await _transferService.GetConversationsAsync(SelectedSourceProfile);
            Conversations.Clear();
            foreach (var conv in list)
            {
                Conversations.Add(conv);
            }
            StatusMessage = list.Count > 0
                ? $"Found {list.Count} conversation(s) available to transfer."
                : "No conversations found in selected profile.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed scanning: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void SelectAll()
    {
        _audioService.PlayClick();
        foreach (var c in Conversations)
        {
            c.IsSelected = true;
        }
    }

    [RelayCommand]
    public void DeselectAll()
    {
        _audioService.PlayClick();
        foreach (var c in Conversations)
        {
            c.IsSelected = false;
        }
    }

    [RelayCommand]
    public async Task ImportAsync()
    {
        if (SelectedSourceProfile == null)
        {
            StatusMessage = "Please select a source profile.";
            return;
        }

        var selected = Conversations.Where(c => c.IsSelected).Select(c => c.ConversationId).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Please select at least one conversation to transfer.";
            return;
        }

        IsTransferring = true;
        StatusMessage = $"Transferring {selected.Count} conversation(s)...";
        try
        {
            var result = await _transferService.TransferConversationsAsync(
                SelectedSourceProfile,
                TargetProfile,
                selected,
                overwrite: true);

            if (result.Success)
            {
                _audioService.PlaySuccess();
                RequestClose?.Invoke(true, result.Message);
            }
            else
            {
                StatusMessage = $"Transfer failed: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsTransferring = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        _audioService.PlayClick();
        RequestClose?.Invoke(false, "Transfer cancelled.");
    }
}
