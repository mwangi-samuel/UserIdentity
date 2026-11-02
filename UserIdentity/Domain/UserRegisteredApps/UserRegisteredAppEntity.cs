using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;

using UserIdentity.Domain.Users;

namespace UserIdentity.Domain.UserRegisteredApps;

public class UserRegisteredAppEntity : BaseAuditableEntity<string>
{
  public int AppId { get; set; }

  [JsonIgnore]
  [ForeignKey(nameof(AppId))]
  public virtual RegisteredAppEntity App { get; set; } = null!;

  public string UserId { get; set; } = null!;

  [JsonIgnore]
  [ForeignKey(nameof(UserId))]
  public virtual UserEntity User { get; set; } = null!;
}
