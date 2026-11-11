namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record UserTokenDto
{
    public required AccessTokenDto AccessToken { get; init; }

    public required string RefreshToken { get; init; }
}
