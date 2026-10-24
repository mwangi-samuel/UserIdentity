using System.Linq.Expressions;

using PolyzenKit.Domain.Dto;

namespace UserIdentity.Domain.WaitLists;

public class WaitListDto : WaitListEntity, IBaseAuditableEntityDto<long>
{
  public string? AppName { get; set; }

  public static Expression<Func<WaitListEntity, WaitListDto>> Basic
    => entity => new WaitListDto
    {
      Id = entity.Id,
      UserEmail = entity.UserEmail,
      AppId = entity.AppId
    };

  private static readonly Func<WaitListEntity, WaitListDto> _compiledBasic
       = Basic.Compile();

  public static WaitListDto FromEntityBasic(WaitListEntity entity)
        => _compiledBasic(entity);

  public static Expression<Func<WaitListEntity, WaitListDto>> Detailed
    => entity => new WaitListDto
    {
      Id = entity.Id,
      UserEmail = entity.UserEmail,
      AppId = entity.AppId,
      AppName = entity.App.AppName,
      CreatedBy = entity.CreatedBy,
      CreatedAt = entity.CreatedAt,
      UpdatedBy = entity.UpdatedBy,
      UpdatedAt = entity.UpdatedAt
    };

  private static readonly Func<WaitListEntity, WaitListDto> _compiledDetailed
        = Detailed.Compile();

  public static WaitListDto FromEntityDetailed(WaitListEntity entity)
        => _compiledDetailed(entity);
}
