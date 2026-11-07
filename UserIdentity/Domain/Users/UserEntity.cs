using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.Users;

public class UserEntity : BaseAuditableEntity<string>
{
    [MaxLength(UserLimits.FirstNameMaxLength)]
    public string? FirstName { get; internal set; }

    [MaxLength(UserLimits.LastNameMaxLength)]
    public string? LastName { get; internal set; }
}
