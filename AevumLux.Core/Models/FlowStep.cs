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

    /// <summary>Gets or sets the UTC time this step started executing.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Gets or sets the UTC time this step completed.</summary>
    public DateTime? CompletedAt { get; set; }
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
}

/// <summary>Captures the details of an HTTP response for display.</summary>
public sealed class HttpResponseDetail
{
    public int StatusCode { get; set; }
    public string ReasonPhrase { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = [];
    public string? Body { get; set; }
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
