using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.RefreshTokens;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Shared;

public class RequestContextTests
{
    [Fact]
    public void Create_Keeps_The_Ip_Address_And_The_User_Agent()
    {
        // Act
        var requestContext = RequestContext.Create("203.0.113.7", "Test Browser");

        // Assert
        Assert.Equal("203.0.113.7", requestContext.IpAddress);
        Assert.Equal("Test Browser", requestContext.Device);
    }

    [Fact]
    public void Create_Cuts_A_Long_User_Agent_To_The_Device_Limit()
    {
        // Act
        var requestContext = RequestContext.Create("203.0.113.7", new string('a', RefreshTokenLimits.DeviceMaxLength + 50));

        // Assert
        Assert.Equal(new string('a', RefreshTokenLimits.DeviceMaxLength), requestContext.Device);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_Leaves_Out_Blank_Values(string? value)
    {
        // Act
        var requestContext = RequestContext.Create(value, value);

        // Assert
        Assert.Null(requestContext.IpAddress);
        Assert.Null(requestContext.Device);
    }
}
