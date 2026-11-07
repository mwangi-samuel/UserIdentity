namespace UserIdentity.Domain.Roles;

public record RoleDto
{
    public const int NameMaxLength = 256;

    public string Id { get; init; } = null!;
    public string Name { get; init; } = null!;
}
