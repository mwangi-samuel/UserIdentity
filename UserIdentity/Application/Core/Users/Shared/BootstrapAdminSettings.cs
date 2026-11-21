using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Validation;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.Shared;

public sealed class BootstrapAdminSettings : IValidatableObject
{
    [StringLength(RegisteredAppLimits.AppNameMaxLength)]
    public string? AppName { get; init; }

    [StringLength(RegisteredAppLimits.BaseUrlMaxLength)]
    public string? AppBaseUrl { get; init; }

    [StringLength(UserLimits.UserNameMaxLength)]
    public string? UserName { get; init; }

    [StringLength(UserLimits.FirstNameMaxLength)]
    public string? FirstName { get; init; }

    [StringLength(UserLimits.LastNameMaxLength)]
    public string? LastName { get; init; }

    [StringLength(UserLimits.EmailMaxLength), RegularExpression(ValidationPatterns.Email, ErrorMessage = ErrorMessages.Users.InvalidEmail)]
    public string? Email { get; init; }

    [StringLength(UserLimits.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; init; }

    public string? Password { get; init; }

    public bool IsEnabled => Settings.Any(setting => !string.IsNullOrWhiteSpace(setting.Value));

    private (string Name, string? Value)[] Settings =>
        [
            (nameof(AppName), AppName),
            (nameof(AppBaseUrl), AppBaseUrl),
            (nameof(UserName), UserName),
            (nameof(FirstName), FirstName),
            (nameof(LastName), LastName),
            (nameof(Email), Email),
            (nameof(Password), Password),
        ];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsEnabled)
            yield break;

        var missingSettings = Settings.Where(setting => string.IsNullOrWhiteSpace(setting.Value)).Select(setting => setting.Name).ToList();

        if (missingSettings.Count > 0)
            yield return new ValidationResult(ErrorMessages.Users.BootstrapAdminIncomplete(missingSettings), missingSettings);
    }
}
