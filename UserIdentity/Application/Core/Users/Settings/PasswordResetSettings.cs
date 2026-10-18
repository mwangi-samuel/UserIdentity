namespace UserIdentity.Application.Core.Users.Settings;

public record PasswordResetSettings
{
  public int ValidForSeconds { get; set; }
  public int RequestCooldownSeconds { get; set; }
}
