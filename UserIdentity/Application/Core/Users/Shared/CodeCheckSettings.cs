using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.Users.Shared;

public sealed class CodeCheckSettings
{
    [Required, Url]
    public required string VerifyUrl { get; init; }

    [Required, Url]
    public required string ResendUrl { get; init; }

    [Required]
    public required string ServiceName { get; init; }
}
