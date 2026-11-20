using UserIdentity.Application.Core.Users.Shared;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Shared;

public class ContactMaskTests
{
    [Theory]
    [InlineData("sam@example.com", null, "s***@example.com")]
    [InlineData("sam@example.com", "+254700000123", "s***@example.com")]
    [InlineData(null, "+254700000123", "***123")]
    [InlineData(" ", "+254700000123", "***123")]
    [InlineData(null, "12", "***")]
    [InlineData("not-an-email", null, "***")]
    [InlineData(null, null, null)]
    public void For_Masks_The_Email_Or_Else_The_Phone(string? email, string? phoneNumber, string? expected) =>
        // Act & Assert
        Assert.Equal(expected, ContactMask.For(email, phoneNumber));
}
