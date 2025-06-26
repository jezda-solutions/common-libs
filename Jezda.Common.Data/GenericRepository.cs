using Jezda.Common.Abstractions.Extensions;
using Jezda.Common.Abstractions.Repositories;
using Jezda.Common.Domain.Paged;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

namespace Jezda.Common.Data;

internal abstract class GenericRepository<T>(DbContext context) : IGenericRepository<T> where T : class
{
    protected readonly DbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    #region GET

    public ValueTask<T?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default)
    {
        return _dbSet.FindAsync([id], cancellationToken);
    }

    public T? GetFirstOrDefault()
    {
        return _dbSet.FirstOrDefault();
    }

    public T? GetFirstOrDefault(Expression<Func<T, bool>> predicate)
    {
        return _dbSet.FirstOrDefault(predicate);
    }

    public Task<T?> GetFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return _dbSet.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> where, CancellationToken cancellationToken = default)
    {
        return _dbSet.FirstOrDefaultAsync(where, cancellationToken);
    }

    public Task<List<T>> GetAsync(CancellationToken cancellationToken = default)
    {
        return _dbSet.ToListAsync(cancellationToken);
    }

    public Task<List<T>> GetAsync(Expression<Func<T, bool>> where, CancellationToken cancellationToken = default)
    {
        return _dbSet.Where(where).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// This unified method allows for flexible querying by supporting both entity
    /// retrieval with included relationships and projection to a custom type.
    /// When no projection is provided (i.e., projection is null), the result
    /// type TProjection must be the same as the entity type T.
    /// This means that if you want to return the full entity without projection, you must
    /// explicitly specify T (e.g., MyEntity) when calling the method.
    /// </summary>
    /// <typeparam name="TProjection"></typeparam>
    /// <param name="where"></param>
    /// <param name="include"></param>
    /// <param name="projection"></param>
    /// <param name="ignoreQueryFilters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Task<TProjection?> FirstWithIncludesOrProjectionAsync<TProjection>(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, TProjection>>? projection = null,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (ignoreQueryFilters)
            query = query.IgnoreQueryFilters();

        if (include != null)
            query = include(query);

        query = query.Where(where);

        if (projection == null)
        {
            if (typeof(TProjection) != typeof(T))
            {
                throw new InvalidOperationException(
                    $"When {nameof(projection)} is null, {typeof(TProjection)} must be {typeof(T)}");
            }

            var typedQuery = (IQueryable<TProjection>)(object)query;
            return typedQuery.FirstOrDefaultAsync(cancellationToken);
        }

        return query
            .Select(projection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<TProjection>> GetProjectionsAsync<TProjection>(
        Expression<Func<T, TProjection>> projection,
        Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, object>>? orderBy = null,
        bool? descending = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = _dbSet.AsQueryable();

        if (include is not null)
            queryable = include(queryable);

        if (where is not null)
            queryable = queryable.Where(where);

        if (orderBy is not null)
        {
            if (descending == true)
                queryable = queryable.OrderByDescending(orderBy);
            else
                queryable = queryable.OrderBy(orderBy);
        }

        return queryable
            .Select(projection)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetIncludingAsync(
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet.AsQueryable();
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.ToListAsync();
    }

    #endregion

    #region PAGING

    public PagedList<T> GetPagedResponse(
        PagingInfo pagingInfo,
        Expression<Func<T, bool>>? where = null,
        string defaultSortColumn = "Id",
        Func<IQueryable<T>, IQueryable<T>>? include = null)
    {
        var query = _dbSet.AsQueryable();

        if (where != null)
            query = query.Where(where);

        if (include != null)
            query = include(query);

        return query.ApplyPagingAndFiltering(pagingInfo, defaultSortColumn);
    }

    public PagedList<Tprojection> GetPagedProjection<Tprojection>(
        PagingInfo pagingInfo,
        Expression<Func<T, Tprojection>> projection,
        Expression<Func<T, bool>>? where = null,
        string defaultSortColumn = "Id",
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        bool searchProjection = true)
    {
        var query = _dbSet.AsQueryable();

        if (where != null)
            query = query.Where(where);

        // Kada je ovaj parametar na false, globalno pretrazujemo direktno iz entiteta i njegovih relacija.
        // Na ovaj nacin zadrzavamo pristup query, where, select, order , skip, take ...
        // Ukoliko imamo izuzetke gde treba da prodjemo kroz projekciju, postavicemo parametar na true i prekociti ovaj deo.
        if (!searchProjection)
            query = query.ApplyGlobalSearchFilter(pagingInfo.GlobalSearch);

        if (include != null)
            query = include(query);

        query = query.ApplySearchFilters(pagingInfo.SearchTerm);

        var projectedQuery = query.Select(projection);

        return projectedQuery.ApplyPagingAndFiltering(
            pagingInfo,
            defaultSortColumn,
            searchProjection);
    }

    #endregion

    #region ADD

    public EntityEntry<T> Add(T entity)
    {
        return _dbSet.Add(entity);
    }

    public void AddRange(IEnumerable<T> entities)
    {
        _dbSet.AddRange(entities);
    }

    public ValueTask<EntityEntry<T>> AddAsync(T entity, CancellationToken cancellationToken)
    {
        return _dbSet.AddAsync(entity, cancellationToken);
    }

    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken)
    {
        return _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    #endregion

    #region UPDATE

    public EntityEntry<T> Update(T entity)
    {
        return _dbSet.Update(entity);
    }

    public void UpdateRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
    }

    #endregion

    #region REMOVE

    public EntityEntry<T> Remove(T entity)
    {
        return _dbSet.Remove(entity);
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    #endregion

    public Task<int> CountAsync(Expression<Func<T, bool>>? where = null, CancellationToken cancellationToken = default)
    {
        return where == null
            ? _dbSet.CountAsync(cancellationToken)
            : _dbSet.CountAsync(where, cancellationToken);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>>? where = null, CancellationToken cancellationToken = default)
    {
        return where == null
            ? _dbSet.AnyAsync(cancellationToken)
            : _dbSet.AnyAsync(where, cancellationToken);
    }

    public Task<bool> AllAsync(Expression<Func<T, bool>> where, CancellationToken cancellationToken = default)
    {
        return _dbSet.AllAsync(where, cancellationToken);
    }
}