using PolyzenKit.Domain.Entity;

namespace UserIdentity.Domain.Users;

public class UserEntity : BaseAuditableEntity<string>
{
  public string? FirstName { get; internal set; }

  public string? LastName { get; internal set; }

  public string? EmailConfirmationToken { get; internal set; }
}
