using System.Diagnostics;
using System.Linq.Expressions;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Configuration;
using Nop.Core.Events;
using Nop.Data;

namespace Nop.Web.Framework.Infrastructure.Observability;

/// <summary>
/// Decorator that wraps <see cref="IRepository{TEntity}"/> with OpenTelemetry spans
/// for write operations (Insert, Update, Delete).
///
/// Because nopCommerce uses LINQ2DB (not EF Core), there is no built-in
/// DiagnosticSource for SQL — the ORM is invisible to tracing. This decorator
/// is the only way to get database-level visibility into the checkout flow.
///
/// Read operations (GetById, GetAll, Table) are intentionally left un-instrumented
/// to avoid excessive span noise — they happen dozens of times per request. Write
/// operations are fewer, higher-value, and directly correlate to state changes
/// (order created, inventory adjusted, etc.).
/// </summary>
public class InstrumentedRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    private readonly EntityRepository<TEntity> _inner;
    private static readonly string EntityName = typeof(TEntity).Name;

    public InstrumentedRepository(
        IEventPublisher eventPublisher,
        INopDataProvider dataProvider,
        IShortTermCacheManager shortTermCacheManager,
        IStaticCacheManager staticCacheManager,
        AppSettings appSettings)
    {
        _inner = new EntityRepository<TEntity>(
            eventPublisher, dataProvider, shortTermCacheManager, staticCacheManager, appSettings);
    }

    // --- Instrumented write operations ---

    public async Task InsertAsync(TEntity entity, bool publishEvent = true)
    {
        using var activity = DiagnosticsConfig.DataSource.StartActivity(
            $"DB Insert {EntityName}", ActivityKind.Client);
        activity?.SetTag("db.system", "linq2db");
        activity?.SetTag("db.operation", "INSERT");
        activity?.SetTag("db.entity", EntityName);

        await _inner.InsertAsync(entity, publishEvent);

        activity?.SetTag("db.entity_id", entity.Id);
    }

    public async Task InsertAsync(IList<TEntity> entities, bool publishEvent = true)
    {
        using var activity = DiagnosticsConfig.DataSource.StartActivity(
            $"DB BulkInsert {EntityName}", ActivityKind.Client);
        activity?.SetTag("db.system", "linq2db");
        activity?.SetTag("db.operation", "BULK_INSERT");
        activity?.SetTag("db.entity", EntityName);
        activity?.SetTag("db.row_count", entities.Count);

        await _inner.InsertAsync(entities, publishEvent);
    }

    public async Task UpdateAsync(TEntity entity, bool publishEvent = true)
    {
        using var activity = DiagnosticsConfig.DataSource.StartActivity(
            $"DB Update {EntityName}", ActivityKind.Client);
        activity?.SetTag("db.system", "linq2db");
        activity?.SetTag("db.operation", "UPDATE");
        activity?.SetTag("db.entity", EntityName);
        activity?.SetTag("db.entity_id", entity.Id);

        await _inner.UpdateAsync(entity, publishEvent);
    }

    public async Task UpdateAsync(IList<TEntity> entities, bool publishEvent = true)
    {
        using var activity = DiagnosticsConfig.DataSource.StartActivity(
            $"DB BulkUpdate {EntityName}", ActivityKind.Client);
        activity?.SetTag("db.system", "linq2db");
        activity?.SetTag("db.operation", "BULK_UPDATE");
        activity?.SetTag("db.entity", EntityName);
        activity?.SetTag("db.row_count", entities.Count);

        await _inner.UpdateAsync(entities, publishEvent);
    }

    public async Task DeleteAsync(TEntity entity, bool publishEvent = true)
    {
        using var activity = DiagnosticsConfig.DataSource.StartActivity(
            $"DB Delete {EntityName}", ActivityKind.Client);
        activity?.SetTag("db.system", "linq2db");
        activity?.SetTag("db.operation", "DELETE");
        activity?.SetTag("db.entity", EntityName);
        activity?.SetTag("db.entity_id", entity.Id);

        await _inner.DeleteAsync(entity, publishEvent);
    }

    public async Task DeleteAsync(IList<TEntity> entities, bool publishEvent = true)
    {
        using var activity = DiagnosticsConfig.DataSource.StartActivity(
            $"DB BulkDelete {EntityName}", ActivityKind.Client);
        activity?.SetTag("db.system", "linq2db");
        activity?.SetTag("db.operation", "BULK_DELETE");
        activity?.SetTag("db.entity", EntityName);
        activity?.SetTag("db.row_count", entities.Count);

        await _inner.DeleteAsync(entities, publishEvent);
    }

    public async Task<int> DeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        using var activity = DiagnosticsConfig.DataSource.StartActivity(
            $"DB DeleteWhere {EntityName}", ActivityKind.Client);
        activity?.SetTag("db.system", "linq2db");
        activity?.SetTag("db.operation", "DELETE_WHERE");
        activity?.SetTag("db.entity", EntityName);

        var count = await _inner.DeleteAsync(predicate);
        activity?.SetTag("db.row_count", count);
        return count;
    }

    // --- Pass-through read operations (no extra spans to avoid noise) ---

    public Task<TEntity> GetByIdAsync(int? id, Func<ICacheKeyService, CacheKey> getCacheKey = null,
        bool includeDeleted = true, bool useShortTermCache = false)
        => _inner.GetByIdAsync(id, getCacheKey, includeDeleted, useShortTermCache);

    public Task<IList<TEntity>> GetByIdsAsync(IList<int> ids,
        Func<ICacheKeyService, CacheKey> getCacheKey = null, bool includeDeleted = true)
        => _inner.GetByIdsAsync(ids, getCacheKey, includeDeleted);

    public Task<IList<TEntity>> GetAllAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>> func = null,
        Func<ICacheKeyService, CacheKey> getCacheKey = null, bool includeDeleted = true)
        => _inner.GetAllAsync(func, getCacheKey, includeDeleted);

    public Task<IList<TEntity>> GetAllAsync(Func<IQueryable<TEntity>, Task<IQueryable<TEntity>>> func = null,
        Func<ICacheKeyService, CacheKey> getCacheKey = null, bool includeDeleted = true)
        => _inner.GetAllAsync(func, getCacheKey, includeDeleted);

    public Task<IList<TEntity>> GetAllAsync(Func<IQueryable<TEntity>, Task<IQueryable<TEntity>>> func,
        Func<ICacheKeyService, Task<CacheKey>> getCacheKey, bool includeDeleted = true)
        => _inner.GetAllAsync(func, getCacheKey, includeDeleted);

    public Task<IPagedList<TEntity>> GetAllPagedAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> func = null,
        int pageIndex = 0, int pageSize = int.MaxValue, bool getOnlyTotalCount = false,
        bool includeDeleted = true)
        => _inner.GetAllPagedAsync(func, pageIndex, pageSize, getOnlyTotalCount, includeDeleted);

    public Task<IPagedList<TEntity>> GetAllPagedAsync(
        Func<IQueryable<TEntity>, Task<IQueryable<TEntity>>> func = null,
        int pageIndex = 0, int pageSize = int.MaxValue, bool getOnlyTotalCount = false,
        bool includeDeleted = true)
        => _inner.GetAllPagedAsync(func, pageIndex, pageSize, getOnlyTotalCount, includeDeleted);

    public Task<TEntity> LoadOriginalCopyAsync(TEntity entity)
        => _inner.LoadOriginalCopyAsync(entity);

    public Task TruncateAsync(bool resetIdentity = false)
        => _inner.TruncateAsync(resetIdentity);

    public IQueryable<TEntity> Table => _inner.Table;
}
