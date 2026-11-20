using PolyzenKit.Domain.Entities;
using UserIdentity.Common.Enums;

namespace UserIdentity.Domain.AccountLimitEvents;

public class AccountLimitEventEntity : BaseEntity<Guid>
{
    public string UserId { get; set; } = null!;

    public AccountLimitKind Kind { get; set; }

    public DateTime CreatedAt { get; set; }
}
