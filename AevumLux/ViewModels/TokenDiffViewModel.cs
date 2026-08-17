using System.Collections.ObjectModel;
using System.Text.Json;
using AevumLux.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AevumLux.ViewModels;

/// <summary>ViewModel for the Token Diff page.</summary>
public sealed partial class TokenDiffViewModel : ObservableObject
{
    private readonly IJwtService _jwtService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompareCommand))]
    private string _tokenA = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompareCommand))]
    private string _tokenB = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _hasResult;

    public bool ShowEmptyState => !HasResult;

    public ObservableCollection<DiffRow> HeaderDiff { get; } = [];
    public ObservableCollection<DiffRow> PayloadDiff { get; } = [];

    public TokenDiffViewModel(IJwtService jwtService)
    {
        _jwtService = jwtService;
    }

    [RelayCommand(CanExecute = nameof(CanCompare))]
    private void Compare()
    {
        ErrorMessage = null;
        HasResult = false;
        HeaderDiff.Clear();
        PayloadDiff.Clear();

        try
        {
            var infoA = _jwtService.Decode(TokenA.Trim());
            var infoB = _jwtService.Decode(TokenB.Trim());

            foreach (var row in Diff(infoA.Header, infoB.Header))
                HeaderDiff.Add(row);

            foreach (var row in Diff(infoA.Payload, infoB.Payload))
                PayloadDiff.Add(row);

            HasResult = true;
        }
        catch (FormatException ex)
        {
            ErrorMessage = $"Invalid token format. Make sure both boxes contain a complete JWT.\n\nDetail: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
    }

    private bool CanCompare() => !string.IsNullOrWhiteSpace(TokenA) && !string.IsNullOrWhiteSpace(TokenB);

    private static List<DiffRow> Diff(Dictionary<string, object?> a, Dictionary<string, object?> b)
    {
        var keys = a.Keys.Union(b.Keys).OrderBy(k => k, StringComparer.Ordinal);
        var rows = new List<DiffRow>();

        foreach (var key in keys)
        {
            var hasA = a.TryGetValue(key, out var rawA);
            var hasB = b.TryGetValue(key, out var rawB);
            var valueA = hasA ? FormatValue(rawA) : null;
            var valueB = hasB ? FormatValue(rawB) : null;

            var status = (hasA, hasB) switch
            {
                (true, false) => DiffStatus.Removed,
                (false, true) => DiffStatus.Added,
                _ when valueA != valueB => DiffStatus.Changed,
                _ => DiffStatus.Unchanged
            };

            rows.Add(new DiffRow(key, valueA, valueB, status));
        }

        return rows
            .OrderBy(r => r.Status == DiffStatus.Unchanged ? 1 : 0)
            .ThenBy(r => r.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static string? FormatValue(object? value) => value switch
    {
        null => null,
        JsonElement el => FormatJsonElement(el),
        _ => value.ToString()
    };

    private static string FormatJsonElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? string.Empty,
        JsonValueKind.Number => el.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "(null)",
        JsonValueKind.Array => string.Join(", ", el.EnumerateArray().Select(x => x.GetRawText().Trim('"'))),
        _ => el.GetRawText()
    };
}

/// <summary>Classification of how a claim differs between two tokens.</summary>
public enum DiffStatus
{
    Unchanged,
    Added,
    Removed,
    Changed
}

/// <summary>A single claim's comparison result between two tokens.</summary>
public sealed class DiffRow(string key, string? valueA, string? valueB, DiffStatus status)
{
    public string Key { get; } = key;
    public string ValueA { get; } = valueA ?? "—";
    public string ValueB { get; } = valueB ?? "—";
    public DiffStatus Status { get; } = status;
    public bool IsAdded { get; } = status == DiffStatus.Added;
    public bool IsRemoved { get; } = status == DiffStatus.Removed;
    public bool IsChanged { get; } = status == DiffStatus.Changed;
    public bool IsUnchanged { get; } = status == DiffStatus.Unchanged;
    public double ValueOpacity { get; } = status == DiffStatus.Unchanged ? 0.5 : 1.0;
    public string StatusLabel { get; } = status switch
    {
        DiffStatus.Added => "Added",
        DiffStatus.Removed => "Removed",
        DiffStatus.Changed => "Changed",
        _ => string.Empty
    };
}
