using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PolyzenKit.Infrastructure.Security.AppSecrets;
using UserIdentity.Common.Identity;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.Users;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories.Users;

public sealed class ProtectedUserStoreTests : IDisposable
{
    private readonly AppDbContext _context = AppDbContextTestFactory.GetAppDbContext();
    private readonly IdentityUser _user = new() { Id = "user-1", UserName = "test" };
    private readonly ProtectedUserStore _store;

    public ProtectedUserStoreTests()
    {
        var protector = new AppSecretProtector(
            Options.Create(
                new AppSecretSettings
                {
                    CurrentKeyId = "key1",
                    Keys = new Dictionary<string, string> { ["key1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
                }
            )
        );

        _context.Users.Add(_user);
        _context.SaveChanges();

        _store = new ProtectedUserStore(_context, protector);
    }

    public void Dispose()
    {
        _store.Dispose();
        _context.Dispose();
    }

    [Fact]
    public async Task The_Authenticator_Key_Is_Stored_Encrypted_And_Read_Back_In_Clear()
    {
        // Act
        await _store.SetAuthenticatorKeyAsync(_user, "JBSWY3DPEHPK3PXP", TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var key = await _store.GetAuthenticatorKeyAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("JBSWY3DPEHPK3PXP", key);
        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", await StoredValueAsync(UserTokenNames.AuthenticatorKey));
    }

    [Fact]
    public async Task A_Missing_Authenticator_Key_Reads_As_Null()
    {
        // Act
        var key = await _store.GetAuthenticatorKeyAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(key);
    }

    [Fact]
    public async Task Recovery_Codes_Are_Stored_Encrypted_Counted_And_Redeemed_Once()
    {
        // Arrange
        await _store.ReplaceCodesAsync(_user, ["aaaa-1111", "bbbb-2222", "cccc-3333"], TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var redeemed = await _store.RedeemCodeAsync(_user, "bbbb-2222", TestContext.Current.CancellationToken);
        var redeemedAgain = await _store.RedeemCodeAsync(_user, "bbbb-2222", TestContext.Current.CancellationToken);
        var count = await _store.CountCodesAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(redeemed);
        Assert.False(redeemedAgain);
        Assert.Equal(2, count);
        Assert.DoesNotContain("aaaa-1111", await StoredValueAsync(UserTokenNames.RecoveryCodes));
    }

    [Fact]
    public async Task Redeeming_The_Last_Recovery_Code_Leaves_None()
    {
        // Arrange
        await _store.ReplaceCodesAsync(_user, ["aaaa-1111"], TestContext.Current.CancellationToken);

        // Act
        var redeemed = await _store.RedeemCodeAsync(_user, "aaaa-1111", TestContext.Current.CancellationToken);
        var count = await _store.CountCodesAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(redeemed);
        Assert.Equal(0, count);
    }

    private async Task<string> StoredValueAsync(string tokenName) =>
        (
            await _context
                .Set<IdentityUserToken<string>>()
                .SingleAsync(
                    token =>
                        token.UserId == _user.Id && token.LoginProvider == UserTokenNames.InternalLoginProvider && token.Name == tokenName,
                    TestContext.Current.CancellationToken
                )
        ).Value!;
}
