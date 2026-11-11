namespace UserIdentity.Domain.RoleClaims;

public record RoleClaimDto
{
    public required string Resource { get; init; }

    public required string Action { get; init; }

    public required string Scope { get; init; }
}
