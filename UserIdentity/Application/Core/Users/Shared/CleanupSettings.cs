using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.Users.Shared;

public sealed class CleanupSettings
{
    [Range(1, int.MaxValue)]
    public required int IntervalInMinutes { get; init; }

    [Range(1, int.MaxValue)]
    public required int UnconfirmedUserLifetimeInDays { get; init; }
}
