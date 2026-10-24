namespace UserIdentity.Domain.Roles;

public record RoleDto
{
  public string Id { get; init; } = null!;
  public string Name { get; init; } = null!;
}
