namespace UserIdentity.Application.Core.Users.Settings;

public record CleanupSettings
{
  public int IntervalInMinutes { get; set; }
  public int UnconfirmedUserLifetimeInDays { get; set; }
}
