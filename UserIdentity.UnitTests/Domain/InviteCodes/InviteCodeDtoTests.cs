using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Domain.InviteCodes;
using Xunit;

namespace UserIdentity.UnitTests.Domain.InviteCodes;

public class InviteCodeDtoTests
{
    private static readonly InviteCodeEntity _entity = new()
    {
        Id = 4,
        InviteCode = "ABCDEFGHJK",
        UserEmail = "owner@example.com",
        Applied = true,
        Role = "testapp:owner",
        AppId = 1,
        App = new RegisteredAppEntity { Id = 1, AppName = "TestApp" },
        CreatedBy = "creator",
        CreatedAt = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void FromEntityDetailed_Adds_The_App_Name_And_Audit_Fields()
    {
        // Act
        var dto = InviteCodeDto.FromEntityDetailed(_entity);

        // Assert
        Assert.Equal("TestApp", dto.AppName);
        Assert.Equal("creator", dto.CreatedBy);
        Assert.Equal(_entity.CreatedAt, dto.CreatedAt);
    }
}
