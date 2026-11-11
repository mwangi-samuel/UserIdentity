using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Domain.WaitLists;
using Xunit;

namespace UserIdentity.UnitTests.Domain.WaitLists;

public class WaitListDtoTests
{
    private static readonly WaitListEntity _entity = new()
    {
        Id = 3,
        UserEmail = "shopper@example.com",
        AppId = 1,
        App = new RegisteredAppEntity { Id = 1, AppName = "TestApp" },
        CreatedBy = "creator",
        CreatedAt = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
        UpdatedBy = "updater",
        UpdatedAt = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void FromEntityDetailed_Adds_The_App_Name_And_Audit_Fields()
    {
        // Act
        var dto = WaitListDto.FromEntityDetailed(_entity);

        // Assert
        Assert.Equal("TestApp", dto.AppName);
        Assert.Equal("creator", dto.CreatedBy);
        Assert.Equal(_entity.CreatedAt, dto.CreatedAt);
        Assert.Equal("updater", dto.UpdatedBy);
        Assert.Equal(_entity.UpdatedAt, dto.UpdatedAt);
    }
}
