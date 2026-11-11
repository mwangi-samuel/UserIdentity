using PolyzenKit.Domain.Dtos;

namespace UserIdentity.Domain.Users;

public class UserDto : UserEntity, IBaseAuditableEntityDto<string>
{
    public string? UserName { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }
}
