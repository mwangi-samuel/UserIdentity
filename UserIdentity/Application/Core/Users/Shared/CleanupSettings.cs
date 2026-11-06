using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.Users.Shared;

public record CleanupSettings
{
    [Range(1, int.MaxValue)]
    public int IntervalInMinutes { get; set; }

    [Range(1, int.MaxValue)]
    public int UnconfirmedUserLifetimeInDays { get; set; }
}
