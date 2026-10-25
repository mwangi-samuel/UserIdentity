using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

using PolyzenKit.Domain.Entity;
using PolyzenKit.Domain.RegisteredApps;

namespace UserIdentity.Domain.InviteCodes;

public class InviteCodeEntity : BaseAuditableEntity<long>
{
  [Required]
  public string InviteCode { get; set; } = null!;

  [Required]
  public string UserEmail { get; set; } = null!;

  public bool Applied { get; set; }

  [MaxLength(256)]
  public string? Role { get; set; }

  public int AppId { get; set; }

  [JsonIgnore]
  [ValidateNever]
  [ForeignKey(nameof(AppId))]
  public virtual RegisteredAppEntity App { get; set; } = null!;
}
