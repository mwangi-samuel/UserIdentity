using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.Tokens.Shared;

public sealed class RefreshTokenSettings
{
    [Range(1, int.MaxValue)]
    public required int ValidForSeconds { get; init; }
}
