using System.ComponentModel.DataAnnotations;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Common.Errors;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class ResendCodeCommandTests
{
    [Theory]
    [InlineData("ticket", null)]
    [InlineData(null, "reset-request")]
    public void Validate_Accepts_Exactly_One_Of_Ticket_And_Reset_Request_Id(string? ticket, string? resetRequestId)
    {
        // Arrange
        var command = new ResendCodeCommand { Ticket = ticket, ResetRequestId = resetRequestId };

        // Act
        var results = command.Validate(new ValidationContext(command)).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", "")]
    [InlineData("ticket", "reset-request")]
    public void Validate_Refuses_Neither_Or_Both(string? ticket, string? resetRequestId)
    {
        // Arrange
        var command = new ResendCodeCommand { Ticket = ticket, ResetRequestId = resetRequestId };

        // Act
        var results = command.Validate(new ValidationContext(command)).ToList();

        // Assert
        var result = Assert.Single(results);
        Assert.Equal(ErrorMessages.Users.TicketOrResetRequestIdRequired, result.ErrorMessage);
    }
}
