using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class UpdateUserCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        A.Fake<ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>>();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new()
    {
        Id = "user-1",
        Email = "user@test.com",
        PhoneNumber = "+254700000111",
        PhoneNumberConfirmed = true,
    };

    private readonly UserEntity _userDetails = new()
    {
        Id = "user-1",
        FirstName = "Old",
        LastName = "Name",
    };

    public UpdateUserCommandHandlerTests()
    {
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);
        A.CallTo(() => _userManager.SetPhoneNumberAsync(_user, A<string?>._))
            .Invokes(
                (IdentityUser user, string? phoneNumber) =>
                {
                    user.PhoneNumber = phoneNumber;
                    user.PhoneNumberConfirmed = false;
                }
            )
            .Returns(IdentityResult.Success);
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _userRepository.GetEntityItemAsync(_user.Id, A<CancellationToken>._)).Returns(_userDetails);
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(new UserDto { Id = _user.Id });
    }

    [Fact]
    public async Task The_Names_Change_At_Once_And_An_Unchanged_Phone_Sends_Nothing()
    {
        // Act
        await GetHandler().UpdateItemAsync(GetCommand("New", "Person", "+254700000111"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("New", _userDetails.FirstName);
        Assert.Equal("Person", _userDetails.LastName);
        Assert.Equal(_now, _userDetails.UpdatedAt);
        Assert.True(_user.PhoneNumberConfirmed);
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("+254700000222", "+254700000222")]
    [InlineData(" ", null)]
    public async Task A_New_Or_Removed_Phone_Is_Stored_Unconfirmed_And_Notified_To_The_Email(string phoneNumber, string? stored)
    {
        // Act
        await GetHandler().UpdateItemAsync(GetCommand("Old", "Name", phoneNumber), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(stored, _user.PhoneNumber);
        Assert.False(_user.PhoneNumberConfirmed);
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.ContactChanged
                        && c.Message.Recipient!.Email == "user@test.com"
                        && c.Message.GetData<UserMessageData>()!.Change == nameof(ContactChange.Phone)
                    ),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Changing_The_Phone_Of_The_Text_Message_Method_Is_Refused()
    {
        // Arrange
        _userDetails.TwoFactorMethod = TwoFactorMethod.Sms;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().UpdateItemAsync(GetCommand("Old", "Name", "+254700000222"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.PhoneUsedForTwoFactor, exception.Message);
        A.CallTo(() => _userManager.SetPhoneNumberAsync(A<IdentityUser>._, A<string?>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task An_App_Admin_Cannot_Change_An_Admin_Account()
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:admin"]);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler()
                .UpdateItemAsync(
                    GetCommand("New", "Name", "+254700000111") with
                    {
                        Changer = new UserChanger("admin-id", false),
                    },
                    "admin-id",
                    TestContext.Current.CancellationToken
                )
        );

        A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    private UpdateUserCommand GetCommand(string firstName, string? lastName, string? phoneNumber) =>
        new()
        {
            UserId = _user.Id,
            RegisteredApp = _registeredApp,
            Changer = new UserChanger(_user.Id, false),
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
        };

    private UpdateUserCommandHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _unitOfWork,
            _userRepository,
            _userRegisteredAppRepository,
            _createOutboxMessageCommandHandler,
            new FakeTimeProvider(_now)
        );
}
