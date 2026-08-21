using System.Collections.ObjectModel;
using AevumLux.Core.Models;
using AevumLux.Core.Repositories.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the Provider Manager page.</summary>
public sealed partial class ProviderManagerViewModel : ObservableObject
{
    private readonly IProviderRepository _providerRepository;
    private readonly ILogger<ProviderManagerViewModel> _logger;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading;

    public bool ShowEmptyState => !IsLoading && Providers.Count == 0;

    public ObservableCollection<OidcProvider> Providers { get; } = [];

    // Editor form state
    [ObservableProperty]
    private string? _editingId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _editorName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _editorIssuerUrl = string.Empty;

    [ObservableProperty]
    private string _editorJwksUri = string.Empty;

    public bool IsEditing => EditingId is not null;

    public ProviderManagerViewModel(IProviderRepository providerRepository, ILogger<ProviderManagerViewModel> logger)
    {
        _providerRepository = providerRepository;
        _logger = logger;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var providers = await _providerRepository.GetAllAsync();
            Providers.Clear();
            // Scenario providers are seeded for Flow Simulator's picker only — they don't
            // belong in Provider Manager's regular list.
            foreach (var provider in providers.Where(p => p.Source == ProviderSource.Manual))
                Providers.Add(provider);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load saved providers.\n\nDetail: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void StartAdd() => ResetForm();

    private void ResetForm()
    {
        EditingId = null;
        EditorName = string.Empty;
        EditorIssuerUrl = string.Empty;
        EditorJwksUri = string.Empty;
        OnPropertyChanged(nameof(IsEditing));
    }

    [RelayCommand]
    private void StartEdit(OidcProvider provider)
    {
        EditingId = provider.Id;
        EditorName = provider.Name;
        EditorIssuerUrl = provider.IssuerUrl;
        EditorJwksUri = provider.JwksUri ?? string.Empty;
        OnPropertyChanged(nameof(IsEditing));
    }

    [RelayCommand]
    private void CancelEdit() => ResetForm();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        var isUpdate = EditingId is not null;

        try
        {
            var existing = EditingId is not null
                ? await _providerRepository.GetByIdAsync(EditingId)
                : null;

            var provider = existing ?? new OidcProvider();
            provider.Name = EditorName.Trim();
            provider.IssuerUrl = EditorIssuerUrl.Trim();
            provider.JwksUri = string.IsNullOrWhiteSpace(EditorJwksUri) ? null : EditorJwksUri.Trim();
            provider.UpdatedAt = DateTime.UtcNow;

            await _providerRepository.UpsertAsync(provider);
            await LoadAsync();

            ResetForm();

            _logger.LogInformation(
                "Provider {Action}. Name={Name} IssuerUrl={IssuerUrl}",
                isUpdate ? "updated" : "saved",
                provider.Name,
                provider.IssuerUrl);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not save provider.\n\nDetail: {ex.Message}";
            _logger.LogError(ex, "Provider save failed. Name={Name}", EditorName);
        }
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(EditorName) && !string.IsNullOrWhiteSpace(EditorIssuerUrl);

    [RelayCommand]
    private async Task DeleteAsync(OidcProvider provider)
    {
        ErrorMessage = null;

        try
        {
            await _providerRepository.DeleteAsync(provider.Id);
            Providers.Remove(provider);
            ResetForm();

            _logger.LogInformation("Provider deleted. Name={Name} IssuerUrl={IssuerUrl}", provider.Name, provider.IssuerUrl);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not delete provider.\n\nDetail: {ex.Message}";
            _logger.LogError(ex, "Provider delete failed. Name={Name}", provider.Name);
        }
    }
}
