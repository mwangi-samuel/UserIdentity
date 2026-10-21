namespace UserIdentity.Application.Core.Users.Settings;

public record PasswordResetSettings
{
  // Expired requests are kept this long, so MaxRequestsPerDay can count them
  public static readonly TimeSpan RequestCountWindow = TimeSpan.FromDays(1);

  public int ValidForSeconds { get; set; }
  public int RequestCooldownSeconds { get; set; }
  public int MaxRequestsPerDay { get; set; } = 5;
}
