using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Commands;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AccountLimitEvents;
using UserIdentity.Domain.EmailChanges;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests.Persistence;

public class UserCleanupTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private void SeedAgedUser(bool emailConfirmed)
    {
        var identityUser = TestDbHelper.CreateIdentityUser();
        identityUser.EmailConfirmed = emailConfirmed;
        TestDbHelper.UpdateIdentityUser(identityUser);

        var userEntity = TestDbHelper.CreateUserEntity();
        userEntity.CreatedAt = DateTime.UtcNow.AddDays(-40);
        TestDbHelper.UpdateUserEntity(userEntity);
    }

    private Task<DeleteRecordViewModel> DeleteUnconfirmedUsersAsync() =>
        ServiceProvider
            .GetRequiredService<IDeleteItemCommandHandler<DeleteUnconfirmedUsersCommand, DeleteRecordViewModel>>()
            .DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

    [Fact]
    public async Task DeleteUnconfirmedUsers_RemovesAnOldUnconfirmedUser()
    {
        // Arrange
        SeedAgedUser(emailConfirmed: false);

        // Act
        await DeleteUnconfirmedUsersAsync();

        // Assert
        Assert.Null(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
        Assert.Null(TestDbHelper.GetUserEntity(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task DeleteUnconfirmedUsers_KeepsAnOldConfirmedUser()
    {
        // Arrange
        SeedAgedUser(emailConfirmed: true);

        // Act
        await DeleteUnconfirmedUsersAsync();

        // Assert
        Assert.NotNull(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task DeleteUnconfirmedUsers_KeepsAnOldUnconfirmedUserOfAnAppThatDoesNotRequireConfirmation()
    {
        // Arrange
        const string appName = "NoConfirmationApp";

        await ServiceProvider
            .GetRequiredService<ICreateItemCommandHandler<CreateRegisteredAppCommand, RegisteredAppViewModel>>()
            .CreateItemAsync(
                new CreateRegisteredAppCommand { AppName = appName, BaseUrl = "http://localhost:3004" },
                "system",
                TestContext.Current.CancellationToken
            );

        var userId = TestDbHelper.CreateUserOfApp(appName);
        var userEntity = TestDbHelper.GetUserEntity(userId)!;
        userEntity.CreatedAt = DateTime.UtcNow.AddDays(-40);
        TestDbHelper.UpdateUserEntity(userEntity);

        // Act
        await DeleteUnconfirmedUsersAsync();

        // Assert
        Assert.NotNull(TestDbHelper.GetIdentityUserWithNoTracking(userId));
    }

    [Fact]
    public async Task DeleteUnconfirmedUsers_Frees_The_Invite_Of_The_Users_App_And_Leaves_Another_Apps_Invite()
    {
        // Arrange
        SeedAgedUser(emailConfirmed: false);
        var ownInvite = TestDbHelper.CreateInviteCodeEntity(UserSettingHelper.UserEmail, RegisteredApp.AppName, applied: true);
        var otherAppInvite = new InviteCodeEntity
        {
            InviteCode = "OTHERAPPCODE",
            UserEmail = UserSettingHelper.UserEmail,
            AppId = TestDbHelper.GetRegisteredAppEntityByAppName(TestingWebAppFactory.OtherAppName).Id,
            Applied = true,
        };

        var context = ServiceProvider.GetRequiredService<AppDbContext>();
        context.InviteCode.Add(otherAppInvite);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await DeleteUnconfirmedUsersAsync();

        // Assert
        Assert.Null(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
        Assert.False(TestDbHelper.GetInviteCodeEntityWithNoTracking(ownInvite.Id)?.Applied);
        Assert.True(TestDbHelper.GetInviteCodeEntityWithNoTracking(otherAppInvite.Id)?.Applied);
    }

    [Fact]
    public async Task DeleteExpiredRefreshTokens_Deletes_Only_Expired_Tokens_Across_Batches()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        var now = DateTime.UtcNow;
        var context = ServiceProvider.GetRequiredService<AppDbContext>();

        context.RefreshToken.AddRange(
            Enumerable.Range(0, CleanupBatch.Size + 20).Select(index => BuildRefreshToken(now.AddMinutes(-index - 1)))
        );
        context.RefreshToken.AddRange(Enumerable.Range(0, 3).Select(index => BuildRefreshToken(now.AddDays(index + 1))));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await ServiceProvider
            .GetRequiredService<IDeleteItemCommandHandler<DeleteExpiredRefreshTokensCommand, DeleteRecordViewModel>>()
            .DeleteItemAsync(new DeleteExpiredRefreshTokensCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        var remaining = TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId);
        Assert.Equal(3, remaining.Count);
        Assert.All(remaining, token => Assert.True(token.Expires > now));
    }

    [Fact]
    public async Task DeleteExpiredPasswordResets_Deletes_Only_Requests_Expired_Before_The_Count_Window_Across_Batches()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        var now = DateTime.UtcNow;

        for (var index = 0; index < CleanupBatch.Size + 20; index++)
            TestDbHelper.CreatePasswordResetEntity($"old-request-{index}", expires: now.AddDays(-2));

        var recentlyExpired = TestDbHelper.CreatePasswordResetEntity("recently-expired", expires: now.AddHours(-1));
        var active = TestDbHelper.CreatePasswordResetEntity("active", expires: now.AddMinutes(15));

        // Act
        await ServiceProvider
            .GetRequiredService<IDeleteItemCommandHandler<DeleteExpiredPasswordResetsCommand, DeleteRecordViewModel>>()
            .DeleteItemAsync(new DeleteExpiredPasswordResetsCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        var remaining = await ServiceProvider
            .GetRequiredService<AppDbContext>()
            .PasswordReset.AsNoTracking()
            .Select(reset => reset.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { recentlyExpired.Id, active.Id }.Order(), remaining.Order());
    }

    [Fact]
    public async Task DeleteExpiredEmailChanges_Deletes_Only_Expired_Changes()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        var otherUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        var now = DateTime.UtcNow;
        var context = ServiceProvider.GetRequiredService<AppDbContext>();

        context.EmailChange.AddRange(
            BuildEmailChange(UserSettingHelper.UserId, now.AddMinutes(-1)),
            BuildEmailChange(otherUserId, now.AddMinutes(10))
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await ServiceProvider
            .GetRequiredService<IDeleteItemCommandHandler<DeleteExpiredEmailChangesCommand, DeleteRecordViewModel>>()
            .DeleteItemAsync(new DeleteExpiredEmailChangesCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(TestDbHelper.GetEmailChangeEntityWithNoTracking(UserSettingHelper.UserId));
        Assert.NotNull(TestDbHelper.GetEmailChangeEntityWithNoTracking(otherUserId));
    }

    [Fact]
    public async Task DeleteExpiredAccountLimitEvents_Deletes_Only_Events_Older_Than_A_Day()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        var context = ServiceProvider.GetRequiredService<AppDbContext>();

        context.AccountLimitEvent.AddRange(
            Enumerable
                .Range(0, CleanupBatch.Size + 20)
                .Select(index => new AccountLimitEventEntity
                {
                    Id = Guid.CreateVersion7(),
                    UserId = UserSettingHelper.UserId,
                    Kind = AccountLimitKind.LoginCodeSent,
                    CreatedAt = DateTime.UtcNow.AddDays(-2).AddMinutes(-index),
                })
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        TestDbHelper.CreateAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.LoginCodeSent, 2);

        // Act
        await ServiceProvider
            .GetRequiredService<IDeleteItemCommandHandler<DeleteExpiredAccountLimitEventsCommand, DeleteRecordViewModel>>()
            .DeleteItemAsync(new DeleteExpiredAccountLimitEventsCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.LoginCodeSent));
    }

    private static EmailChangeEntity BuildEmailChange(string userId, DateTime expires) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            NewEmail = $"{userId}@example.com",
            Expires = expires,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };

    private static RefreshTokenEntity BuildRefreshToken(DateTime expires) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = UserSettingHelper.UserId,
            Token = Guid.NewGuid().ToString("N"),
            Expires = expires,
            CreatedBy = UserSettingHelper.UserId,
            CreatedAt = DateTime.UtcNow,
        };
}
