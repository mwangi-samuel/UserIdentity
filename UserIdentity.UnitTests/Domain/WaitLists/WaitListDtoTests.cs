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
    public void FromEntityBasic_Copies_The_Entry_Without_Audit_Fields()
    {
        // Act
        var dto = WaitListDto.FromEntityBasic(_entity);

        // Assert
        Assert.Equal(3, dto.Id);
        Assert.Equal("shopper@example.com", dto.UserEmail);
        Assert.Equal(1, dto.AppId);
        Assert.Null(dto.AppName);
        Assert.Null(dto.CreatedBy);
    }

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
