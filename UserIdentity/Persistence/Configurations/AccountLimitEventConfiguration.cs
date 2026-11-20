using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PolyzenKit.Persistence.Configurations;
using UserIdentity.Domain.AccountLimitEvents;

namespace UserIdentity.Persistence.Configurations;

public class AccountLimitEventConfiguration : IEntityTypeConfiguration<AccountLimitEventEntity>
{
    public void Configure(EntityTypeBuilder<AccountLimitEventEntity> builder)
    {
        builder.ConfigureGuidPrimaryKey();

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(AccountLimitEventLimits.KindMaxLength);

        builder.HasOne<IdentityUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new
        {
            e.UserId,
            e.Kind,
            e.CreatedAt,
        });
        builder.HasIndex(e => e.CreatedAt);
    }
}
