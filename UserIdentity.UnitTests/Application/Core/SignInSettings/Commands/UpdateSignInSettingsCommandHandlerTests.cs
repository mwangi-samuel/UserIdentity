using FakeItEasy;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.SignInSettings.Commands;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Persistence.Repositories.AppSignInSettings;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.SignInSettings.Commands;

public class UpdateSignInSettingsCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IAppSignInSettingsRepository _repository = A.Fake<IAppSignInSettingsRepository>();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 3,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    [Fact]
    public async Task Saving_For_An_App_Without_Settings_Creates_Them()
    {
        // Arrange
        A.CallTo(() =>
                _repository.GetEntityByAlternateIdAsync(
                    A<AppSignInSettingsEntity>.That.Matches(e => e.AppId == 3),
                    A<QueryCondition>._,
                    A<CancellationToken>._
                )
            )
            .Returns((AppSignInSettingsEntity?)null);

        // Act
        var vm = await GetHandler()
            .UpdateItemAsync(
                new UpdateSignInSettingsCommand
                {
                    RegisteredApp = _registeredApp,
                    RequireConfirmedAccount = true,
                    DefaultRole = " Member ",
                    RequireTwoFactorForAdmins = true,
                    AllowAuthenticatorTwoFactor = true,
                },
                "admin-id",
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.Equal(3, vm.SignInSettings.AppId);
        Assert.True(vm.SignInSettings.RequireConfirmedAccount);
        Assert.Equal("member", vm.SignInSettings.DefaultRole);
        Assert.True(vm.SignInSettings.RequireTwoFactorForAdmins);
        Assert.Equal("admin-id", vm.SignInSettings.CreatedBy);
        A.CallTo(() => _repository.CreateEntityItem(A<AppSignInSettingsEntity>.That.Matches(e => e.AppId == 3)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Saving_Replaces_Every_Existing_Setting()
    {
        // Arrange
        var existing = new AppSignInSettingsEntity
        {
            AppId = 3,
            RequireConfirmedAccount = true,
            RequireInviteCode = true,
            DefaultRole = "member",
            AllowEmailTwoFactor = true,
        };
        A.CallTo(() => _repository.GetEntityByAlternateIdAsync(A<AppSignInSettingsEntity>._, A<QueryCondition>._, A<CancellationToken>._))
            .Returns(existing);

        // Act
        await GetHandler()
            .UpdateItemAsync(
                new UpdateSignInSettingsCommand { RegisteredApp = _registeredApp, AllowSmsTwoFactor = true },
                "admin-id",
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.False(existing.RequireConfirmedAccount);
        Assert.False(existing.RequireInviteCode);
        Assert.Null(existing.DefaultRole);
        Assert.False(existing.AllowEmailTwoFactor);
        Assert.True(existing.AllowSmsTwoFactor);
        Assert.Equal(_now, existing.UpdatedAt);
        A.CallTo(() => _repository.UpdateEntityItem(existing)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Owner")]
    public async Task An_Admin_Role_Is_Refused_As_The_Default_Role(string defaultRole)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler()
                .UpdateItemAsync(
                    new UpdateSignInSettingsCommand { RegisteredApp = _registeredApp, DefaultRole = defaultRole },
                    "admin-id",
                    TestContext.Current.CancellationToken
                )
        );

        Assert.Equal(ErrorMessages.SignInSettings.AdminDefaultRole(defaultRole), exception.Message);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    private UpdateSignInSettingsCommandHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity", AdminRoles = "admin,owner" }),
            _unitOfWork,
            _repository,
            new FakeTimeProvider(_now)
        );
}
