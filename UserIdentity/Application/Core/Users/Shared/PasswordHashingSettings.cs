using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.Users.Shared;

public record PasswordHashingSettings
{
    [Range(1, int.MaxValue)]
    public int MaxConcurrent { get; set; } = 1;

    [Range(0, int.MaxValue)]
    public int QueueLimit { get; set; } = 32;
}
