using PolyzenKit.Domain.Entity;

namespace UserIdentity.Domain.PasswordResets;

public class PasswordResetEntity : BaseAuditableEntity<Guid>
{
  public string RequestId { get; set; } = null!;

  public string UserId { get; set; } = null!;

  public DateTime Expires { get; set; }

  public bool Verified { get; set; }

  public bool Applied { get; set; }
}
