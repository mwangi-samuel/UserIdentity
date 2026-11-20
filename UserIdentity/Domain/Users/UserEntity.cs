using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;
using UserIdentity.Common.Enums;

namespace UserIdentity.Domain.Users;

public class UserEntity : BaseAuditableEntity<string>
{
    [MaxLength(UserLimits.FirstNameMaxLength)]
    public string? FirstName { get; set; }

    [MaxLength(UserLimits.LastNameMaxLength)]
    public string? LastName { get; set; }

    public TwoFactorMethod? TwoFactorMethod { get; set; }
}
