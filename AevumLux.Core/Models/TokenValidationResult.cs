namespace AevumLux.Core.Models;

/// <summary>Represents the outcome of cryptographic and claims validation on a JWT token.</summary>
public sealed class TokenValidationResult
{
    /// <summary>Gets or sets whether all validation checks passed.</summary>
    public bool IsValid { get; set; }

    /// <summary>Gets or sets the individual validation check results.</summary>
    public List<ValidationCheck> Checks { get; set; } = [];

    /// <summary>Gets a plain English summary of the overall result.</summary>
    public string Summary => IsValid
        ? "Token passed all validation checks."
        : $"Token failed validation: {string.Join("; ", Checks.Where(c => !c.Passed).Select(c => c.FailureReason))}";
}

/// <summary>Represents the result of a single validation step.</summary>
public sealed class ValidationCheck
{
    /// <summary>Gets or sets the name of this validation check.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets whether this individual check passed.</summary>
    public bool Passed { get; set; }

    /// <summary>Gets or sets the plain English description of what was checked.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason for failure, if applicable.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Gets or sets the actual value that was checked.</summary>
    public string? ActualValue { get; set; }

    /// <summary>Gets or sets the expected value, if applicable.</summary>
    public string? ExpectedValue { get; set; }
}
