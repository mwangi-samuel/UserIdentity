using System.ComponentModel.DataAnnotations;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Validation;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.Shared;

public record RegisterUserInput : IValidatableObject
{
    [Required, StringLength(UserLimits.FirstNameMaxLength)]
    public string FirstName { get; init; } = null!;

    [StringLength(UserLimits.LastNameMaxLength)]
    public string? LastName { get; init; }

    [Required, StringLength(UserLimits.UserNameMaxLength)]
    public string UserName { get; init; } = null!;

    [StringLength(UserLimits.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; init; }

    [StringLength(UserLimits.EmailMaxLength), RegularExpression(ValidationPatterns.Email, ErrorMessage = ErrorMessages.Users.InvalidEmail)]
    public string? UserEmail { get; init; }

    [Required]
    public string UserPassword { get; init; } = null!;

    [StringLength(InviteCodeLimits.InviteCodeMaxLength)]
    public string? InviteCode { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber) && string.IsNullOrWhiteSpace(UserEmail))
            yield return new ValidationResult(ErrorMessages.Users.PhoneNumberOrEmailRequired, [nameof(PhoneNumber), nameof(UserEmail)]);

        // Login and reset look up the user name first, so an email-like user name would shadow that email's real account
        if (UserName?.Contains('@') == true && !string.Equals(UserName, UserEmail, StringComparison.OrdinalIgnoreCase))
            yield return new ValidationResult(ErrorMessages.Users.UserNameCannotBeAnotherEmail, [nameof(UserName)]);
    }
}
