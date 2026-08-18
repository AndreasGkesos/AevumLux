namespace AevumLux.Core.Models;

/// <summary>Represents a single step in an OIDC flow simulation timeline.</summary>
public sealed class FlowStep
{
    /// <summary>Gets or sets the step number (1-based) within the flow.</summary>
    public int StepNumber { get; set; }

    /// <summary>Gets or sets the short title for this step.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the plain English explanation of what this step does and why.</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>Gets or sets the current execution status of this step.</summary>
    public FlowStepStatus Status { get; set; } = FlowStepStatus.Pending;

    /// <summary>Gets or sets the HTTP request details for this step, if applicable.</summary>
    public HttpRequestDetail? Request { get; set; }

    /// <summary>Gets or sets the HTTP response details for this step, if applicable.</summary>
    public HttpResponseDetail? Response { get; set; }

    /// <summary>Gets or sets the plain English explanation of the response or error.</summary>
    public string? ResponseExplanation { get; set; }

    /// <summary>Gets or sets the error detail if this step failed.</summary>
    public FlowError? Error { get; set; }

    /// <summary>
    /// When set, this step demonstrates a real but discouraged/deprecated pattern (Implicit,
    /// ROPC). Explains where the anti-pattern manifests in this exact step, what the concrete
    /// risk is, and why modern IdPs/OAuth 2.1 replaced it — shown in the UI alongside the
    /// step's real request/response so the deprecation isn't just asserted, it's visible.
    /// </summary>
    public string? DeprecationWarning { get; set; }

    /// <summary>Gets or sets the UTC time this step started executing.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Gets or sets the UTC time this step completed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// The equivalent curl command for this step's request, for copy-pasting outside the app.
    /// Contains any real secret/credential that was part of the request — never share as-is.
    /// </summary>
    public string? CurlCommand => Request is null ? null : BuildCurlCommand(Request);

    private static string BuildCurlCommand(HttpRequestDetail request)
    {
        var parts = new List<string> { "curl", "-X", request.Method, $"\"{request.Url}\"" };

        foreach (var header in request.Headers)
            parts.Add($"-H \"{header.Key}: {header.Value}\"");

        if (!string.IsNullOrEmpty(request.Body) && request.Method == "POST")
            parts.Add($"-d '{request.Body}'");

        return string.Join(" \\\n  ", parts);
    }
}

/// <summary>Represents the execution status of a flow step.</summary>
public enum FlowStepStatus
{
    Pending,
    InProgress,
    Success,
    Failed,
    Skipped
}

/// <summary>Captures the details of an outgoing HTTP request for display.</summary>
public sealed class HttpRequestDetail
{
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = [];
    public string? Body { get; set; }

    /// <summary>
    /// Per-parameter breakdown of this request's query-string/body fields — name, actual value,
    /// and a short explanation of what it is and who set it. Only populated for display when
    /// Flow Simulator's "Show flow explanations" setting is on; empty otherwise.
    /// </summary>
    public List<ParameterExplanation> Parameters { get; set; } = [];
}

/// <summary>Captures the details of an HTTP response for display.</summary>
public sealed class HttpResponseDetail
{
    public int StatusCode { get; set; }
    public string ReasonPhrase { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = [];
    public string? Body { get; set; }

    /// <summary>
    /// Per-field breakdown of this response's body — name, actual value, and a short
    /// explanation of what it means. Only populated for display when Flow Simulator's
    /// "Show flow explanations" setting is on; empty otherwise.
    /// </summary>
    public List<ParameterExplanation> Parameters { get; set; } = [];
}

/// <summary>One request or response parameter, explained for the "Show flow explanations" breakdown table.</summary>
public sealed class ParameterExplanation
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>Describes a flow step failure with context-aware explanation.</summary>
public sealed class FlowError
{
    public string ErrorCode { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
    public string PlainEnglishExplanation { get; set; } = string.Empty;
    public List<string> LikelyCauses { get; set; } = [];
    public string ActionableFix { get; set; } = string.Empty;
}
