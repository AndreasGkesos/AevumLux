using System.Collections.ObjectModel;
using System.Text.Json;
using AevumLux.Core.Models;
using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the Token Validator page.</summary>
public sealed partial class TokenValidatorViewModel : ObservableObject
{
    private readonly ITokenValidationService _tokenValidationService;
    private readonly ISessionHistoryService _sessionHistory;
    private readonly ILogger<TokenValidatorViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    private string _rawToken = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    private string _jwksUri = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    private string _expectedIssuer = string.Empty;

    [ObservableProperty]
    private string _expectedAudience = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isTokenValid;

    [ObservableProperty]
    private string _summary = string.Empty;

    public bool ShowEmptyState => !HasResult && !IsBusy;

    public ObservableCollection<ValidationCheck> Checks { get; } = [];

    public TokenValidatorViewModel(ITokenValidationService tokenValidationService, ISessionHistoryService sessionHistory, ILogger<TokenValidatorViewModel> logger)
    {
        _tokenValidationService = tokenValidationService;
        _sessionHistory = sessionHistory;
        _logger = logger;
    }

    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task ValidateAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        HasResult = false;
        Checks.Clear();
        IsBusy = true;

        var token = RawToken.Trim();
        var jwksUri = JwksUri.Trim();
        var issuer = ExpectedIssuer.Trim();
        var audience = string.IsNullOrWhiteSpace(ExpectedAudience) ? null : ExpectedAudience.Trim();

        _logger.LogInformation("Token validation started. Issuer={Issuer} Audience={Audience}", issuer, audience);

        try
        {
            var result = await _tokenValidationService.ValidateAsync(token, jwksUri, issuer, audience, cancellationToken);

            foreach (var check in result.Checks)
                Checks.Add(check);

            IsTokenValid = result.IsValid;
            Summary = result.Summary;
            HasResult = true;

            _sessionHistory.AddEntry(
                SessionEntryType.TokenValidated,
                $"Validation: {(result.IsValid ? "Passed" : "Failed")} — {issuer}",
                JsonSerializer.Serialize(result.Checks));

            if (result.IsValid)
            {
                _logger.LogInformation("Token validation passed. Issuer={Issuer} Audience={Audience}", issuer, audience);
            }
            else
            {
                var reason = string.Join("; ", result.Checks.Where(c => !c.Passed).Select(c => $"{c.Name}: {c.FailureReason}"));
                _logger.LogWarning("Token validation failed. Issuer={Issuer} Audience={Audience} Reason={Reason}", issuer, audience, reason);
            }
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Could not fetch the JWKS document. Check the URL and your connection.\n\nDetail: {ex.Message}";
            _logger.LogWarning(ex, "Token validation failed: could not fetch JWKS. Issuer={Issuer}", issuer);
        }
        catch (FormatException ex)
        {
            ErrorMessage = $"Invalid token format. Make sure you paste a complete JWT.\n\nDetail: {ex.Message}";
            _logger.LogWarning(ex, "Token validation failed: invalid token format. Issuer={Issuer}", issuer);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Token validation failed unexpectedly. Issuer={Issuer}", issuer);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanValidate() =>
        !string.IsNullOrWhiteSpace(RawToken) &&
        !string.IsNullOrWhiteSpace(JwksUri) &&
        !string.IsNullOrWhiteSpace(ExpectedIssuer) &&
        !IsBusy;
}
