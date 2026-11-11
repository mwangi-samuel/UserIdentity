using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolyzenKit.Persistence.Configurations;
using UserIdentity.Domain.UserRegisteredApps;

namespace UserIdentity.Persistence.Configurations;

public class UserRegisteredAppConfiguration : IEntityTypeConfiguration<UserRegisteredAppEntity>
{
    public void Configure(EntityTypeBuilder<UserRegisteredAppEntity> builder)
    {
        builder.ConfigureGuidPrimaryKey();

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => new { e.UserId, e.AppId }).IsUnique();
    }
}
