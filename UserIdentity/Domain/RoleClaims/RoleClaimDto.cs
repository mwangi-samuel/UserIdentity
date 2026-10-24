namespace UserIdentity.Domain.RoleClaims;

public record RoleClaimDto
{
  public string Resource { get; init; } = null!;

  public string Action { get; init; } = null!;

  public string Scope { get; init; } = null!;
}
