using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.StepUps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.StepUps;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Commands;

public class CreateStepUpCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IStepUpRepository _stepUpRepository = A.Fake<IStepUpRepository>();
    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
    private readonly ICodeCheckService _codeCheckService = A.Fake<ICodeCheckService>();
    private readonly IGetItemQueryHandler<GetStepUpUserQuery, StepUpUserViewModel> _getStepUpUserQueryHandler = A.Fake<
        IGetItemQueryHandler<GetStepUpUserQuery, StepUpUserViewModel>
    >();
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler = A.Fake<
        ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel>
    >();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new() { Id = "admin-id" };

    private readonly StepUpEntity _pendingStepUp = new()
    {
        Id = Guid.CreateVersion7(),
        UserId = "admin-id",
        AppId = 1,
        Expires = _now.AddMinutes(4),
    };

    private StepUpEntity? _createdStepUp;

    public CreateStepUpCommandHandlerTests()
    {
        A.CallTo(() => _tokenFactory.GenerateToken(A<int>._)).Returns("ticket");
        A.CallTo(() => _tokenFactory.HashToken("ticket")).Returns("hashed-ticket");
        A.CallTo(() => _stepUpRepository.GetPendingEntityAsync(_user.Id, 1, _now, A<CancellationToken>._)).Returns(_pendingStepUp);
        A.CallTo(() => _stepUpRepository.CreateEntityItem(A<StepUpEntity>._)).Invokes((StepUpEntity stepUp) => _createdStepUp = stepUp);
    }

    [Fact]
    public async Task A_Right_Authenticator_Code_Gives_A_Ticket_Stored_As_A_Hash_For_The_Step_Up_Lifetime()
    {
        // Arrange
        SetUpMethod(TwoFactorMethod.Authenticator);
        A.CallTo(() => _userManager.VerifyTwoFactorTokenAsync(_user, TokenOptions.DefaultAuthenticatorProvider, "123456")).Returns(true);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand("123 456"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ticket", vm.Ticket);
        Assert.Equal(_now.AddSeconds(300), vm.ExpiresAt);

        var stepUp = Assert.IsType<StepUpEntity>(_createdStepUp);

        Assert.Equal(_user.Id, stepUp.UserId);
        Assert.Equal(1, stepUp.AppId);
        Assert.Equal("hashed-ticket", stepUp.Ticket);
        Assert.Equal(_now.AddSeconds(300), stepUp.Expires);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(_codeCheckService).MustNotHaveHappened();
        A.CallTo(() => _stepUpRepository.GetPendingEntityAsync(A<string>._, A<int>._, A<DateTime>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Wrong_Authenticator_Code_Counts_As_A_Failed_Second_Step_And_Saves_Nothing()
    {
        // Arrange
        SetUpMethod(TwoFactorMethod.Authenticator);
        A.CallTo(() => _userManager.VerifyTwoFactorTokenAsync(A<IdentityUser>._, A<string>._, A<string>._)).Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand("000000"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        A.CallTo(() =>
                _recordFailedSignInCommandHandler.CreateItemAsync(
                    new RecordFailedSignInCommand(_user, true),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _stepUpRepository.CreateEntityItem(A<StepUpEntity>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(TwoFactorMethod.Email)]
    [InlineData(TwoFactorMethod.Sms)]
    public async Task A_Right_Emailed_Or_Texted_Code_Turns_The_Pending_Step_Up_Into_A_Ticket(TwoFactorMethod method)
    {
        // Arrange
        SetUpMethod(method);
        AnswerCodeCheck(CodeCheckOutcome.Verified);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand("123456"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ticket", vm.Ticket);
        Assert.Equal(_now.AddSeconds(300), vm.ExpiresAt);
        Assert.Equal("hashed-ticket", _pendingStepUp.Ticket);
        Assert.Equal(_now.AddSeconds(300), _pendingStepUp.Expires);
        A.CallTo(() =>
                _codeCheckService.CheckAsync(
                    "TestApp",
                    UserMessages.LoginCodeAction,
                    _pendingStepUp.Id.ToString(),
                    "123456",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _stepUpRepository.UpdateEntityItem(_pendingStepUp))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
        A.CallTo(() => _stepUpRepository.CreateEntityItem(A<StepUpEntity>._)).MustNotHaveHappened();
        A.CallTo(_recordFailedSignInCommandHandler).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Wrong_Emailed_Code_Counts_As_A_Failed_Second_Step()
    {
        // Arrange
        SetUpMethod(TwoFactorMethod.Email);
        AnswerCodeCheck(CodeCheckOutcome.WrongCode);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand("000000"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        Assert.Null(_pendingStepUp.Ticket);
        A.CallTo(() =>
                _recordFailedSignInCommandHandler.CreateItemAsync(
                    new RecordFailedSignInCommand(_user, true),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(CodeCheckOutcome.Expired)]
    [InlineData(CodeCheckOutcome.NoAttemptsLeft)]
    [InlineData(CodeCheckOutcome.NotFound)]
    public async Task Any_Other_Failed_Check_Gives_The_One_Message_Without_Counting(CodeCheckOutcome outcome)
    {
        // Arrange
        SetUpMethod(TwoFactorMethod.Email);
        AnswerCodeCheck(outcome);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand("123456"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        A.CallTo(_recordFailedSignInCommandHandler).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Without_An_Unexpired_Pending_Code_The_Check_Still_Runs_And_Fails_Without_Counting()
    {
        // Arrange
        SetUpMethod(TwoFactorMethod.Email);
        AnswerCodeCheck(CodeCheckOutcome.Verified);
        A.CallTo(() => _stepUpRepository.GetPendingEntityAsync(_user.Id, 1, _now, A<CancellationToken>._)).Returns((StepUpEntity?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand("123456"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        A.CallTo(() =>
                _codeCheckService.CheckAsync(
                    "TestApp",
                    UserMessages.LoginCodeAction,
                    A<string>.That.Not.IsEqualTo(_pendingStepUp.Id.ToString()),
                    "123456",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(_recordFailedSignInCommandHandler).MustNotHaveHappened();
        A.CallTo(() => _stepUpRepository.CreateEntityItem(A<StepUpEntity>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Refused_Admin_Gets_No_Check()
    {
        // Arrange
        A.CallTo(() => _getStepUpUserQueryHandler.GetItemAsync(A<GetStepUpUserQuery>._, A<CancellationToken>._))
            .Throws(new InvalidDataException(ErrorMessages.Users.AccountLocked));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand("123456"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.AccountLocked, exception.Message);
        A.CallTo(_codeCheckService).MustNotHaveHappened();
        A.CallTo(() => _userManager.VerifyTwoFactorTokenAsync(A<IdentityUser>._, A<string>._, A<string>._)).MustNotHaveHappened();
    }

    private void SetUpMethod(TwoFactorMethod method) =>
        A.CallTo(() => _getStepUpUserQueryHandler.GetItemAsync(new GetStepUpUserQuery(_user.Id, _registeredApp), A<CancellationToken>._))
            .Returns(
                new StepUpUserViewModel
                {
                    User = _user,
                    UserDetails = new UserDto { Id = _user.Id },
                    Method = method,
                }
            );

    private void AnswerCodeCheck(CodeCheckOutcome outcome) =>
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(outcome);

    private CreateStepUpCommand GetCommand(string code) =>
        new()
        {
            UserId = _user.Id,
            RegisteredApp = _registeredApp,
            Code = code,
        };

    private CreateStepUpCommandHandler GetHandler() =>
        new(
            Options.Create(new TwoFactorSettings()),
            _userManager,
            _unitOfWork,
            _stepUpRepository,
            _tokenFactory,
            _codeCheckService,
            _getStepUpUserQueryHandler,
            _recordFailedSignInCommandHandler,
            new FakeTimeProvider(_now)
        );
}
