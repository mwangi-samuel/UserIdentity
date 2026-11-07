using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.Users;

public class UserEntity : BaseAuditableEntity<string>
{
    public const int FirstNameMaxLength = 20;
    public const int LastNameMaxLength = 20;
    public const int UserNameMaxLength = 256;
    public const int EmailMaxLength = 256;
    public const int PhoneNumberMaxLength = 20;
    public const string EmailPattern = @"^[^\s@]+@[^\s@]+$";

    public string? FirstName { get; internal set; }

    public string? LastName { get; internal set; }
}
