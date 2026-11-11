using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.Users;

public class UserEntity : BaseAuditableEntity<string>
{
    [MaxLength(UserLimits.FirstNameMaxLength)]
    public string? FirstName { get; set; }

    [MaxLength(UserLimits.LastNameMaxLength)]
    public string? LastName { get; set; }
}
