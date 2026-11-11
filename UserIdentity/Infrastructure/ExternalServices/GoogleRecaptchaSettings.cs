using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Infrastructure.ExternalServices;

public sealed class GoogleRecaptchaSettings : IValidatableObject
{
    public bool Enabled { get; init; }
    public string? SiteKey { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enabled && string.IsNullOrWhiteSpace(SiteKey))
            yield return new ValidationResult(
                $"The {nameof(SiteKey)} field is required when {nameof(Enabled)} is true.",
                [nameof(SiteKey)]
            );
    }
}
