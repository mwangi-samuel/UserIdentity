using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.StepUps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.StepUps;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Commands;

public class SendStepUpCodeCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IStepUpRepository _stepUpRepository = A.Fake<IStepUpRepository>();
    private readonly IGetItemQueryHandler<GetStepUpUserQuery, StepUpUserViewModel> _getStepUpUserQueryHandler = A.Fake<
        IGetItemQueryHandler<GetStepUpUserQuery, StepUpUserViewModel>
    >();
    private readonly ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> _sendLoginCodeCommandHandler = A.Fake<
        ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel>
    >();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new()
    {
        Id = "admin-id",
        Email = "admin@test.com",
        PhoneNumber = "+254700000123",
    };

    private readonly UserDto _userDetails = new() { Id = "admin-id", FirstName = "Admin" };

    private readonly RequestContext _requestContext = RequestContext.Create("203.0.113.7", "Test Browser");

    private StepUpEntity? _createdStepUp;

    public SendStepUpCodeCommandHandlerTests()
    {
        A.CallTo(() => _stepUpRepository.CreateEntityItem(A<StepUpEntity>._)).Invokes((StepUpEntity stepUp) => _createdStepUp = stepUp);
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new LoginCodeViewModel { Sent = true, CodeSentTo = "a***@test.com" });
    }

    [Fact]
    public async Task An_Authenticator_Admin_Gets_No_Code()
    {
        // Arrange
        SetUpMethod(TwoFactorMethod.Authenticator);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TwoFactorMethod.Authenticator, vm.Method);
        Assert.Null(vm.CodeSentTo);
        A.CallTo(_stepUpRepository).MustNotHaveHappened();
        A.CallTo(_sendLoginCodeCommandHandler).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task An_Email_Admin_Gets_A_Confirm_Action_Code_At_Their_Email_Correlated_By_A_Pending_Step_Up()
    {
        // Arrange
        SetUpMethod(TwoFactorMethod.Email);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TwoFactorMethod.Email, vm.Method);
        Assert.Equal("a***@test.com", vm.CodeSentTo);
        var stepUp = Assert.IsType<StepUpEntity>(_createdStepUp);
        var correlationId = stepUp.Id.ToString();

        Assert.Equal(_user.Id, stepUp.UserId);
        Assert.Equal(1, stepUp.AppId);
        Assert.Null(stepUp.Ticket);
        Assert.Equal(_now.AddSeconds(600), stepUp.Expires);
        A.CallTo(() =>
                _sendLoginCodeCommandHandler.CreateItemAsync(
                    A<SendLoginCodeCommand>.That.Matches(c =>
                        c.UserId == _user.Id
                        && c.UserDetails == _userDetails
                        && c.AppName == "TestApp"
                        && c.Purpose == LoginCodePurpose.ConfirmAction
                        && c.CorrelationId == correlationId
                        && c.ExpiresAt == _now.AddSeconds(600)
                        && c.Email == "admin@test.com"
                        && c.PhoneNumber == null
                        && c.RequestContext == _requestContext
                    ),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task A_Text_Admin_Gets_The_Code_At_Their_Phone_Only()
    {
        // Arrange
        SetUpMethod(TwoFactorMethod.Sms);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TwoFactorMethod.Sms, vm.Method);
        A.CallTo(() =>
                _sendLoginCodeCommandHandler.CreateItemAsync(
                    A<SendLoginCodeCommand>.That.Matches(c =>
                        c.Purpose == LoginCodePurpose.ConfirmAction && c.Email == null && c.PhoneNumber == "+254700000123"
                    ),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Past_The_Daily_Code_Cap_Nothing_Is_Saved()
    {
        // Arrange
        SetUpMethod(TwoFactorMethod.Email);
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new LoginCodeViewModel { Sent = false });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.TooManyCodes, exception.Message);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Refused_Admin_Gets_No_Code()
    {
        // Arrange
        A.CallTo(() => _getStepUpUserQueryHandler.GetItemAsync(A<GetStepUpUserQuery>._, A<CancellationToken>._))
            .Throws(new IllegalOperationException(ErrorMessages.TwoFactor.SetUpToConfirm));

        // Act & Assert
        await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken)
        );

        A.CallTo(_stepUpRepository).MustNotHaveHappened();
        A.CallTo(_sendLoginCodeCommandHandler).MustNotHaveHappened();
    }

    private void SetUpMethod(TwoFactorMethod method) =>
        A.CallTo(() => _getStepUpUserQueryHandler.GetItemAsync(new GetStepUpUserQuery(_user.Id, _registeredApp), A<CancellationToken>._))
            .Returns(
                new StepUpUserViewModel
                {
                    User = _user,
                    UserDetails = _userDetails,
                    Method = method,
                }
            );

    private SendStepUpCodeCommand GetCommand() => new(_user.Id, _registeredApp, _requestContext);

    private SendStepUpCodeCommandHandler GetHandler() =>
        new(
            Options.Create(new TwoFactorSettings()),
            _unitOfWork,
            _stepUpRepository,
            _getStepUpUserQueryHandler,
            _sendLoginCodeCommandHandler,
            new FakeTimeProvider(_now)
        );
}
