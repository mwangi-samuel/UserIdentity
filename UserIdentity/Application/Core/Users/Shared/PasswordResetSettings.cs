using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.Users.Shared;

public record PasswordResetSettings
{
    // Expired requests are kept this long, so MaxRequestsPerDay can count them
    public static readonly TimeSpan RequestCountWindow = TimeSpan.FromDays(1);

    [Range(1, int.MaxValue)]
    public int ValidForSeconds { get; set; }
    public int RequestCooldownSeconds { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxRequestsPerDay { get; set; } = 5;
}
