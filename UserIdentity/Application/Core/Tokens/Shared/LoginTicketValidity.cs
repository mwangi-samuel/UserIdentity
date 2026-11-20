using System.Diagnostics.CodeAnalysis;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.LoginTickets;

namespace UserIdentity.Application.Core.Tokens.Shared;

public static class LoginTicketValidity
{
    public static bool IsUsable([NotNullWhen(true)] LoginTicketEntity? loginTicket, SignInStep step, DateTime now) =>
        loginTicket is not null && loginTicket.Step == step && loginTicket.Expires > now;
}
