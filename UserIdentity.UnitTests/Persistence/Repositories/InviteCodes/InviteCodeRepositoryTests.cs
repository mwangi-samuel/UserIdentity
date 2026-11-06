using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.InviteCodes;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories.InviteCodes;

public class InviteCodeRepositoryTests
{
    [Fact]
    public async Task CreateEntityItem_Persists_InviteCode()
    {
        var context = GetAppDbContext();
        await SeedRegisteredApp(context);
        var repository = new InviteCodeRepository(context);
        var inviteCode = BuildInviteCode();

        repository.CreateEntityItem(inviteCode);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saved = await context.InviteCode.SingleOrDefaultAsync(
            x => x.Id == inviteCode.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.NotNull(saved);
        Assert.Equal(inviteCode.InviteCode, saved!.InviteCode);
        Assert.Equal(inviteCode.UserEmail, saved.UserEmail);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_Returns_Entity()
    {
        var context = GetAppDbContext();
        await SeedRegisteredApp(context);
        var repository = new InviteCodeRepository(context);
        var inviteCode = BuildInviteCode();

        context.InviteCode.Add(inviteCode);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await repository.GetEntityByAlternateIdAsync(
            new InviteCodeEntity { AppId = inviteCode.AppId, UserEmail = inviteCode.UserEmail },
            QueryCondition.MustExist,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(inviteCode.Id, result!.Id);
        Assert.Equal(inviteCode.InviteCode, result.InviteCode);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
    {
        var context = GetAppDbContext();
        var repository = new InviteCodeRepository(context);

        await Assert.ThrowsAsync<NoRecordException>(() =>
            repository.GetEntityByAlternateIdAsync(
                new InviteCodeEntity { UserEmail = "notfound@example.com" },
                QueryCondition.MustExist,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Exists_Throws_RecordExistsException()
    {
        var context = GetAppDbContext();
        await SeedRegisteredApp(context);
        var repository = new InviteCodeRepository(context);
        var inviteCode = BuildInviteCode();

        context.InviteCode.Add(inviteCode);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<RecordExistsException>(() =>
            repository.GetEntityByAlternateIdAsync(
                new InviteCodeEntity { AppId = inviteCode.AppId, UserEmail = inviteCode.UserEmail },
                QueryCondition.MustNotExist,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Missing_Returns_Null()
    {
        var context = GetAppDbContext();
        var repository = new InviteCodeRepository(context);

        var result = await repository.GetEntityByAlternateIdAsync(
            new InviteCodeEntity { UserEmail = "new@example.com" },
            QueryCondition.MustNotExist,
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_For_Another_App_Returns_Null()
    {
        var context = GetAppDbContext();
        await SeedRegisteredApp(context);
        var repository = new InviteCodeRepository(context);
        var inviteCode = BuildInviteCode();

        context.InviteCode.Add(inviteCode);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await repository.GetEntityByAlternateIdAsync(
            new InviteCodeEntity { AppId = inviteCode.AppId + 1, UserEmail = inviteCode.UserEmail },
            QueryCondition.MayOrMayNotExist,
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetEntityDtoByIdAsync_Returns_Dto()
    {
        var context = GetAppDbContext();
        var app = await SeedRegisteredApp(context);
        var repository = new InviteCodeRepository(context);
        var inviteCode = BuildInviteCode();
        inviteCode.AppId = app.Id;

        context.InviteCode.Add(inviteCode);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await repository.GetEntityDtoByIdAsync(inviteCode.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(inviteCode.Id, result.Id);
        Assert.Equal(inviteCode.InviteCode, result.InviteCode);
        Assert.Equal(inviteCode.UserEmail, result.UserEmail);
        Assert.Equal(app.AppName, result.AppName);
    }

    [Fact]
    public async Task GetEntityDtoByIdAsync_When_Not_Found_Throws_NoRecordException()
    {
        var context = GetAppDbContext();
        var repository = new InviteCodeRepository(context);

        await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityDtoByIdAsync(999, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchEntityDtosByAppIdAsync_Without_Filters_Returns_The_Apps_InviteCodes_Newest_First()
    {
        var context = GetAppDbContext();
        var app = await SeedRegisteredApp(context);
        context.RegisteredApp.Add(
            new RegisteredAppEntity
            {
                Id = 2,
                AppName = "OtherApp",
                AppSecretKey = "other-secret-key",
                BaseUrl = "https://other.com",
            }
        );
        var repository = new InviteCodeRepository(context);
        var now = DateTime.UtcNow;

        for (var i = 0; i < 3; i++)
        {
            var inviteCode = BuildInviteCode();
            inviteCode.UserEmail = $"user{i}@example.com";
            inviteCode.CreatedAt = now.AddMinutes(i);
            inviteCode.Role = i == 2 ? "testapp:owner" : null;
            context.InviteCode.Add(inviteCode);
        }

        var otherAppInviteCode = BuildInviteCode();
        otherAppInviteCode.AppId = 2;
        context.InviteCode.Add(otherAppInviteCode);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var page = await repository.SearchEntityDtosByAppIdAsync(app.Id, null, null, 1, 2, TestContext.Current.CancellationToken);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(["user2@example.com", "user1@example.com"], page.Items.Select(i => i.UserEmail));
        Assert.Equal("testapp:owner", page.Items[0].Role);
    }

    [Fact]
    public async Task InvitationSearch_FiltersTheWholeAppBeforePaging()
    {
        // Arrange
        var context = GetAppDbContext();
        await SeedRegisteredApp(context);
        context.RegisteredApp.Add(
            new RegisteredAppEntity
            {
                Id = 2,
                AppName = "OtherApp",
                AppSecretKey = "other-key",
                BaseUrl = "https://other.test",
            }
        );
        for (var index = 0; index < 10; index++)
        {
            var invite = BuildInviteCode();
            invite.UserEmail = $"owner{index}@example.test";
            invite.Applied = index % 2 == 0;
            invite.CreatedAt = DateTime.UtcNow.AddMinutes(index);
            context.InviteCode.Add(invite);
        }

        var other = BuildInviteCode();
        other.AppId = 2;
        other.UserEmail = "owner-other@example.test";
        context.InviteCode.Add(other);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await new InviteCodeRepository(context).SearchEntityDtosByAppIdAsync(
            1,
            "owner",
            false,
            2,
            2,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(
            result.Items,
            invite =>
            {
                Assert.False(invite.Applied);
                Assert.DoesNotContain("other", invite.UserEmail);
            }
        );
    }

    private static AppDbContext GetAppDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetAppliedEntityItemsForUserAsync_Returns_The_Applied_Invite_Of_The_Users_Own_App_Only()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        await SeedRegisteredApp(context);
        context.RegisteredApp.Add(
            new RegisteredAppEntity
            {
                Id = 2,
                AppName = "OtherApp",
                AppSecretKey = "other-secret-key",
                BaseUrl = "https://other.test",
            }
        );

        var user = new IdentityUser { Id = "user-id", Email = "test@example.com" };
        context.Users.Add(user);
        context.UserRegisteredApp.Add(
            new UserRegisteredAppEntity
            {
                Id = "user-id-1",
                UserId = user.Id,
                AppId = 1,
            }
        );

        var ownInvite = BuildInviteCode();
        ownInvite.Applied = true;
        var otherAppInvite = BuildInviteCode();
        otherAppInvite.AppId = 2;
        otherAppInvite.Applied = true;
        var otherEmailInvite = BuildInviteCode();
        otherEmailInvite.UserEmail = "someone@example.com";
        otherEmailInvite.Applied = true;
        context.InviteCode.AddRange(ownInvite, otherAppInvite, otherEmailInvite);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await new InviteCodeRepository(context).GetAppliedEntityItemsForUserAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([ownInvite.Id], result.Select(invite => invite.Id));
    }

    [Fact]
    public async Task GetAppliedEntityItemsForUserAsync_Skips_An_Unused_Invite()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        await SeedRegisteredApp(context);

        var user = new IdentityUser { Id = "user-id", Email = "test@example.com" };
        context.Users.Add(user);
        context.UserRegisteredApp.Add(
            new UserRegisteredAppEntity
            {
                Id = "user-id-1",
                UserId = user.Id,
                AppId = 1,
            }
        );
        context.InviteCode.Add(BuildInviteCode());
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await new InviteCodeRepository(context).GetAppliedEntityItemsForUserAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    private static async Task<RegisteredAppEntity> SeedRegisteredApp(AppDbContext context)
    {
        var app = new RegisteredAppEntity
        {
            Id = 1,
            AppName = "TestApp",
            AppSecretKey = "test-secret-key",
            BaseUrl = "https://test.com",
            RequireInviteCode = true,
        };
        context.RegisteredApp.Add(app);
        await context.SaveChangesAsync();
        return app;
    }

    private static InviteCodeEntity BuildInviteCode()
    {
        return new InviteCodeEntity
        {
            InviteCode = "INVITE123",
            UserEmail = "test@example.com",
            AppId = 1,
            Applied = false,
            CreatedBy = "system",
            UpdatedBy = "system",
            IsDeleted = false,
        };
    }
}
