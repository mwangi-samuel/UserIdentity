namespace UserIdentity.Application.Core.Users.Shared;

public sealed record UserChanger(string UserId, bool IsGlobalAdmin)
{
    public bool IsUser(string userId) => UserId == userId;

    public RequestContext NoticeContext(string userId, RequestContext requestContext) => IsUser(userId) ? requestContext : new();
}
