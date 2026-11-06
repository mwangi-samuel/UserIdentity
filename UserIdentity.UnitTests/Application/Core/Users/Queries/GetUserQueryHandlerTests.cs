using FakeItEasy;
using PolyzenKit.Common.Exceptions;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Queries;

public class GetUserQueryHandlerTests
{
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();

    [Fact]
    public async Task Get_User_Returns_The_Users_Profile_For_The_App()
    {
        // Arrange
        var user = new UserDto
        {
            Id = "user-id",
            UserName = "test.user",
            FirstName = "Test",
        };

        A.CallTo(() => _userRepository.GetUserDtoAsync("user-id", "TestApp", A<CancellationToken>._)).Returns(user);

        var handler = new GetUserQueryHandler(_userRepository);

        // Act
        var vm = await handler.GetItemAsync(
            new GetUserQuery { UserId = "user-id", AppName = "TestApp" },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Same(user, vm.User);
    }

    [Fact]
    public async Task Get_User_For_A_Global_Admin_Reads_Without_An_App()
    {
        // Arrange
        A.CallTo(() => _userRepository.GetUserDtoAsync("user-id", null, A<CancellationToken>._)).Returns(new UserDto { Id = "user-id" });

        var handler = new GetUserQueryHandler(_userRepository);

        // Act
        var vm = await handler.GetItemAsync(new GetUserQuery { UserId = "user-id" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("user-id", vm.User.Id);
    }

    [Fact]
    public async Task Get_User_That_Is_Missing_Or_Of_Another_App_Throws_NoRecordException()
    {
        // Arrange
        A.CallTo(() => _userRepository.GetUserDtoAsync("user-id", "TestApp", A<CancellationToken>._)).Returns((UserDto?)null);

        var handler = new GetUserQueryHandler(_userRepository);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NoRecordException>(() =>
            handler.GetItemAsync(new GetUserQuery { UserId = "user-id", AppName = "TestApp" }, TestContext.Current.CancellationToken)
        );

        Assert.Equal("No record exists for the provided identifier - user-id", exception.Message);
    }
}
