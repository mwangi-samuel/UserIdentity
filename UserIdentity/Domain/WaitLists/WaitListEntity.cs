using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;

namespace UserIdentity.Domain.WaitLists;

public class WaitListEntity : BaseAuditableEntity<long>
{
  public const int UserEmailMaxLength = 255;

  public string UserEmail { get; set; } = null!;

  public int AppId { get; set; }

  [JsonIgnore]
  [ForeignKey(nameof(AppId))]
  public virtual RegisteredAppEntity App { get; set; } = null!;
}
