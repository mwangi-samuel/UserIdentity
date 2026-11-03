using System.Collections.Concurrent;
using System.Data.Common;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace UserIdentity.IntegrationTests.TestUtils;

// Counts SQL commands per request id, so background services' commands aren't counted
public sealed class SqlCommandCounter : DbCommandInterceptor
{
  private readonly HttpContextAccessor _httpContextAccessor = new();
  private readonly ConcurrentDictionary<string, int> _counts = new();

  public int CountFor(string requestId) => _counts.GetValueOrDefault(requestId);

  public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
  {
    Count();
    return result;
  }

  public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
  {
    Count();
    return ValueTask.FromResult(result);
  }

  public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
  {
    Count();
    return result;
  }

  public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
    Count();
    return ValueTask.FromResult(result);
  }

  public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
  {
    Count();
    return result;
  }

  public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
  {
    Count();
    return ValueTask.FromResult(result);
  }

  private void Count()
  {
    if (_httpContextAccessor.HttpContext?.TraceIdentifier is { } requestId)
      _counts.AddOrUpdate(requestId, 1, (_, count) => count + 1);
  }
}
