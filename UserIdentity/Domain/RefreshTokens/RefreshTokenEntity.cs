using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.RefreshTokens;

public class RefreshTokenEntity : BaseAuditableEntity<Guid>
{
    public string? Token { get; internal set; }

    public string? PreviousToken { get; internal set; }

    public DateTime Expires { get; internal set; }

    public string? UserId { get; internal set; }

    public Guid Version { get; internal set; }
}
