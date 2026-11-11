namespace UserIdentity.Domain.Roles;

public record RoleDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
