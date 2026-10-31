using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Models.Requests;

public sealed class PostPaymentRequest : IValidatableObject
{
    [Required, RegularExpression("^[0-9]{14,19}$")]
    public string? CardNumber { get; init; }

    [Required, Range(1, 12)]
    public int? ExpiryMonth { get; init; }

    [Required, Range(1, 9999)]
    public int? ExpiryYear { get; init; }

    [Required, RegularExpression("^(GBP|USD|EUR)$")]
    public string? Currency { get; init; }

    [Required, Range(1, int.MaxValue)]
    public int? Amount { get; init; }

    [Required, RegularExpression("^[0-9]{3,4}$")]
    public string? Cvv { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var clock = (TimeProvider)validationContext.GetService(typeof(TimeProvider))!;
        var now = clock.GetUtcNow();
        if (ExpiryYear < now.Year || (ExpiryYear == now.Year && ExpiryMonth <= now.Month))
        {
            yield return new ValidationResult("Expiry month and year must be in the future.",
                new[] { nameof(ExpiryMonth), nameof(ExpiryYear) });
        }
    }
}