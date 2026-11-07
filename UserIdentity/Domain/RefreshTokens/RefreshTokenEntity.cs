using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.RefreshTokens;

public class RefreshTokenEntity : BaseAuditableEntity<Guid>
{
    [MaxLength(RefreshTokenLimits.TokenMaxLength)]
    public string? Token { get; internal set; }

    [MaxLength(RefreshTokenLimits.PreviousTokenMaxLength)]
    public string? PreviousToken { get; internal set; }

    public DateTime Expires { get; internal set; }

    public string? UserId { get; internal set; }

    [ConcurrencyCheck]
    public Guid Version { get; internal set; }
}
