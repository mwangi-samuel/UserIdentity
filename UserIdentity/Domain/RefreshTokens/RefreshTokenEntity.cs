using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.RefreshTokens;

public class RefreshTokenEntity : BaseAuditableEntity<Guid>
{
    [MaxLength(RefreshTokenLimits.TokenMaxLength)]
    public string Token { get; set; } = null!;

    [MaxLength(RefreshTokenLimits.PreviousTokenMaxLength)]
    public string? PreviousToken { get; set; }

    public DateTime Expires { get; set; }

    public string UserId { get; set; } = null!;

    [ConcurrencyCheck]
    public Guid Version { get; set; }
}
