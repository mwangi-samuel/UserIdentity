namespace UserIdentity.Domain.RoleClaims;

public record RoleClaimDto
{
    public const int ResourceMaxLength = 200;

    public string Resource { get; init; } = null!;

    public string Action { get; init; } = null!;

    public string Scope { get; init; } = null!;
}
