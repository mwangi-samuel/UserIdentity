using System.ComponentModel.DataAnnotations;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Validation;
using UserIdentity.Domain.RoleClaims;

namespace UserIdentity.Application.Core.Roles.Shared;

public record RoleClaimInput
{
    [Required]
    public string RoleId { get; init; } = null!;

    [Required, StringLength(RoleClaimLimits.ResourceMaxLength)]
    public string Resource { get; init; } = null!;

    [Required, RegularExpression(ValidationPatterns.ScopeAction, ErrorMessage = ErrorMessages.Roles.InvalidScopeAction)]
    public string Action { get; init; } = null!;
}
