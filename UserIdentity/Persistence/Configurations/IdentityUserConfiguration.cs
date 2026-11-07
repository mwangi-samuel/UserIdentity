using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserIdentity.Domain.Users;

namespace UserIdentity.Persistence.Configurations;

// Applied after Identity's own model, whose email index isn't unique
public class IdentityUserConfiguration : IEntityTypeConfiguration<IdentityUser>
{
    public void Configure(EntityTypeBuilder<IdentityUser> builder)
    {
        builder.Property(user => user.UserName).HasMaxLength(UserLimits.UserNameMaxLength);
        builder.Property(user => user.NormalizedUserName).HasMaxLength(UserLimits.UserNameMaxLength);
        builder.Property(user => user.Email).HasMaxLength(UserLimits.EmailMaxLength);
        builder.Property(user => user.NormalizedEmail).HasMaxLength(UserLimits.EmailMaxLength);
        builder.Property(user => user.PhoneNumber).HasMaxLength(UserLimits.PhoneNumberMaxLength);

        builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
    }
}
