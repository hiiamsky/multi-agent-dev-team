namespace VeggieAlly.Domain.ValueObjects;

/// <summary>
/// 驗證結果 Value Object 
/// </summary>
public record ValidationResult(ValidationStatus Status, string? Message)
{
    public static ValidationResult Ok() => new(ValidationStatus.Ok, null);
    public static ValidationResult Anomaly(string message) => new(ValidationStatus.Anomaly, message);
    public static ValidationResult Error(string message) => new(ValidationStatus.Error, message);
}