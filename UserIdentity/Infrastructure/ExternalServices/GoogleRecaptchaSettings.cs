using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Infrastructure.ExternalServices;

public record GoogleRecaptchaSettings : IValidatableObject
{
    public bool Enabled { get; set; }
    public string? SiteKey { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enabled && string.IsNullOrWhiteSpace(SiteKey))
            yield return new ValidationResult(
                $"The {nameof(SiteKey)} field is required when {nameof(Enabled)} is true.",
                [nameof(SiteKey)]
            );
    }
}
