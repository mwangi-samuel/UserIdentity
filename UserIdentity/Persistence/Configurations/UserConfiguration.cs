using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserIdentity.Domain.Users;

namespace UserIdentity.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne<IdentityUser>().WithOne().HasForeignKey<UserEntity>(e => e.Id).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CreatedAt);
    }
}
