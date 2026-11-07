using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserIdentity.Domain.UserRegisteredApps;

namespace UserIdentity.Persistence.Configurations;

public class UserRegisteredAppConfiguration : IEntityTypeConfiguration<UserRegisteredAppEntity>
{
    public void Configure(EntityTypeBuilder<UserRegisteredAppEntity> builder) => builder.HasKey(e => e.Id);
}
