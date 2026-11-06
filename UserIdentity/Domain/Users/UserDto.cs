using PolyzenKit.Domain.Dtos;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.Users;

public class UserDto : BaseAuditableEntity<string>, IBaseAuditableEntityDto<string>
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? UserName { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }
}
