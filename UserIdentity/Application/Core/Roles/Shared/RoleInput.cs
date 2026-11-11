using System.ComponentModel.DataAnnotations;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.Shared;

public record RoleInput
{
    [Required, StringLength(RoleLimits.NameMaxLength)]
    public string RoleName { get; init; } = null!;
}
