namespace UserIdentity.Application.Core.Users.Shared;

public record CleanupSettings
{
  public int IntervalInMinutes { get; set; }
  public int UnconfirmedUserLifetimeInDays { get; set; }
}
