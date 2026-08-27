using System.Collections.Generic;

namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// Result of validating dependencies among script groups.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = new List<string>();

    public static ValidationResult Ok() => new() { IsValid = true };
    public static ValidationResult Fail(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };

    public static ValidationResult Combine(ValidationResult a, ValidationResult b)
    {
        if (a.IsValid && b.IsValid) return Ok();
        var errors = new List<string>();
        errors.AddRange(a.Errors);
        errors.AddRange(b.Errors);
        return new ValidationResult { IsValid = false, Errors = errors };
    }
}
