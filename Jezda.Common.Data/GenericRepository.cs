using Jezda.Common.Abstractions.Repositories;
using Jezda.Common.Abstractions.Specifications;
using Jezda.Common.Domain.Paged;
using Jezda.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Data;

public abstract class GenericRepository<T>(DbContext context) : IGenericRepository<T> where T : class
{
    protected readonly DbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    #region GET

    /// <summary>
    /// Gets an entity by its primary key.
    /// </summary>
    /// <typeparam name="TId">The type of the primary key</typeparam>
    /// <param name="id">The primary key value</param>
    /// <returns>The entity if found; otherwise, null</returns>
    /// <remarks>
    /// This method uses EF's Find() which checks the local cache first before querying the database.
    /// The returned entity is tracked by the context.
    /// </remarks>
    public T? GetById<TId>(TId id)
    {
        return _dbSet.Find(id);
    }

    /// <summary>
    /// Gets an entity by composite primary keys asynchronously.
    /// </summary>
    /// <typeparam name="TId">The type of the primary key components</typeparam>
    /// <param name="ids">The composite primary key values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found; otherwise, null</returns>
    /// <remarks>
    /// Use this for entities with composite keys. The returned entity is tracked by the context.
    /// </remarks>
    public async ValueTask<T?> GetByIdsAsync<TId>(TId[] ids, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([ids], cancellationToken);
    }

    /// <summary>
    /// Gets an entity by its primary key asynchronously.
    /// </summary>
    /// <typeparam name="TId">The type of the primary key</typeparam>
    /// <param name="id">The primary key value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entity if found; otherwise, null</returns>
    /// <remarks>
    /// This method uses EF's FindAsync() which checks the local cache first before querying the database.
    /// The returned entity is tracked by the context.
    /// </remarks>
    public async ValueTask<T?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([id], cancellationToken);
    }

    /// <summary>
    /// Gets the first entity from the set.
    /// </summary>
    /// <returns>The first entity if the set is not empty; otherwise, null</returns>
    /// <remarks>
    /// The returned entity is tracked by the context.
    /// </remarks>
    public T? GetFirstOrDefault()
    {
        return _dbSet.FirstOrDefault();
    }

    /// <summary>
    /// Gets the first entity that matches the specified predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate</param>
    /// <returns>The first matching entity if found; otherwise, null</returns>
    /// <remarks>
    /// The returned entity is tracked by the context.
    /// </remarks>
    public T? GetFirstOrDefault(Expression<Func<T, bool>> predicate)
    {
        return _dbSet.FirstOrDefault(predicate);
    }

    /// <summary>
    /// Gets the first entity from the set asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first entity if the set is not empty; otherwise, null</returns>
    /// <remarks>
    /// The returned entity is tracked by the context.
    /// </remarks>
    public async Task<T?> GetFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the first entity that matches the specified filter asynchronously.
    /// </summary>
    /// <param name="where">The filter expression</param>
    /// <param name="orderBy">Optional ordering expression. If not specified, orders by Id to avoid EF Core warnings.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first matching entity if found; otherwise, null</returns>
    /// <remarks>
    /// The returned entity is tracked by the context.
    /// </remarks>
    public async Task<T?> GetFirstOrDefaultAsync(
        Expression<Func<T, bool>> where,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(where);

        // Apply ordering - default to Id if not specified to avoid EF Core warnings
        if (orderBy != null)
            query = query.OrderBy(orderBy);
        else
            query = query.OrderBy(x => EF.Property<object>(x, "Id"));

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all entities asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of all entities</returns>
    /// <remarks>
    /// ⚠️ WARNING: Use with caution on large tables. Consider using pagination or filtering.
    /// All returned entities are tracked by the context.
    /// </remarks>
    public async Task<List<T>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets entities that match the specified filter.
    /// </summary>
    /// <param name="where">The filter expression</param>
    /// <returns>A list of entities that match the filter</returns>
    /// <remarks>
    /// All returned entities are tracked by the context.
    /// </remarks>
    public async Task<List<T>> GetAsync(Expression<Func<T, bool>> where)
    {
        return await _dbSet.Where(where).ToListAsync();
    }

    /// <summary>
    /// Gets entities that match the specified filter asynchronously.
    /// </summary>
    /// <param name="where">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of entities that match the filter</returns>
    /// <remarks>
    /// All returned entities are tracked by the context.
    /// </remarks>
    public async Task<List<T>> GetAsync(
        Expression<Func<T, bool>> where,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(where).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets entities that match the specified filter with included navigation properties.
    /// </summary>
    /// <param name="where">The filter expression</param>
    /// <param name="include">Navigation properties to include (e.g., q => q.Include(x => x.RelatedEntity))</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of entities that match the filter with included navigation properties</returns>
    /// <remarks>
    /// All returned entities are tracked by the context.
    /// </remarks>
    public async Task<List<T>> GetAsync(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.AsQueryable();

        if (include != null)
        {
            query = include(query);
        }

        return await query
            .Where(where)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the first entity that matches the specified criteria, or default if none found.
    /// </summary>
    /// <typeparam name="TProjection">The type of result (use same as T for entity, or different for projection)</typeparam>
    /// <param name="where">Filter expression</param>
    /// <param name="include">Include navigation properties (optional)</param>
    /// <param name="projection">Select specific properties (optional - if provided, returns untracked projection)</param>
    /// <param name="ignoreQueryFilters">Whether to ignore global query filters (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first matching entity or default</returns>
    /// <remarks>
    /// <para><strong>⚠️ IMPORTANT - Tracking Behavior:</strong></para>
    /// <list type="bullet">
    ///   <item>If projection is NULL → returns TRACKED entity (use for updates)</item>
    ///   <item>If projection is provided → returns UNTRACKED result (read-only)</item>
    /// </list>
    ///
    /// <para><strong>Example - Get tracked entity for update:</strong></para>
    /// <code>
    /// var product = await repository.GetFirstOrDefaultAsync&lt;Product&gt;(
    ///     where: x => x.Id == id,
    ///     include: q => q.Include(x => x.ProductCategoryRelations),
    ///     projection: null  // NULL = tracked entity for updates
    /// );
    /// product.Name = "New Name"; // Tracked, no need to call Update()
    /// await unitOfWork.SaveChangesAsync();
    /// </code>
    ///
    /// <para><strong>Example - Get untracked projection for read-only:</strong></para>
    /// <code>
    /// var productDto = await repository.GetFirstOrDefaultAsync(
    ///     where: x => x.Id == id,
    ///     projection: x => new ProductDto { Id = x.Id, Name = x.Name } // Untracked projection
    /// );
    /// </code>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when projection is null but TProjection differs from T</exception>
    public async Task<TProjection?> GetFirstOrDefaultAsync<TProjection>(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, TProjection>>? projection = null,
        Expression<Func<T, object>>? orderBy = null,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (ignoreQueryFilters)
            query = query.IgnoreQueryFilters();

        if (include != null)
            query = include(query);

        query = query.Where(where);

        // Apply ordering - default to Id if not specified to avoid EF Core warnings
        if (orderBy != null)
            query = query.OrderBy(orderBy);
        else
            query = query.OrderBy(x => EF.Property<object>(x, "Id"));

        if (projection == null)
        {
            if (typeof(TProjection) != typeof(T))
            {
                throw new InvalidOperationException(
                    $"When {nameof(projection)} is null, {typeof(TProjection)} must be {typeof(T)}");
            }

            var typedQuery = (IQueryable<TProjection>)(object)query;
            return await typedQuery.FirstOrDefaultAsync(cancellationToken);
        }

        return await query
            .Select(projection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<TProjection>> GetAsync<TProjection>(
        Expression<Func<T, TProjection>>? projection = null,
        Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, object>>? orderBy = null,
        bool? descending = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (include is not null)
            query = include(query);

        if (where is not null)
            query = query.Where(where);

        if (orderBy is not null)
        {
            if (descending == true)
                query = query.OrderByDescending(orderBy);
            else
                query = query.OrderBy(orderBy);
        }

        if (projection == null)
        {
            if (typeof(TProjection) != typeof(T))
            {
                throw new InvalidOperationException(
                    $"When {nameof(projection)} is null, {typeof(TProjection)} must be {typeof(T)}");
            }

            var typedQuery = (IQueryable<TProjection>)(object)query;
            return await typedQuery.ToListAsync(cancellationToken);
        }

        return await query
            .Select(projection)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region SPECIFICATION PATTERN

    /// <summary>
    /// Gets entities using a specification pattern.
    /// </summary>
    /// <param name="specification">The specification containing query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of entities matching the specification</returns>
    /// <remarks>
    /// <para>Applies all specification rules: Criteria, Includes, OrderBy, Take, Skip, and AsNoTracking.</para>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// public class ActiveProductsSpec : BaseSpecification&lt;Product&gt;
    /// {
    ///     public ActiveProductsSpec() : base(x =&gt; x.IsActive)
    ///     {
    ///         AddInclude(x =&gt; x.Category);
    ///         ApplyOrderBy(x =&gt; x.Name);
    ///     }
    /// }
    ///
    /// var products = await repository.GetBySpecAsync(new ActiveProductsSpec());
    /// </code>
    /// </remarks>
    public virtual async Task<List<T>> GetBySpecAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(specification);
        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the first entity using a specification pattern.
    /// </summary>
    /// <param name="specification">The specification containing query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first entity matching the specification, or null</returns>
    public virtual async Task<T?> GetFirstBySpecAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(specification);
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Counts entities using a specification pattern.
    /// </summary>
    /// <param name="specification">The specification containing query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of entities matching the specification</returns>
    public virtual async Task<int> CountBySpecAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        var query = ApplySpecificationForCount(specification);
        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if any entities exist using a specification pattern.
    /// </summary>
    /// <param name="specification">The specification containing query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if any entities match the specification; otherwise, false</returns>
    public virtual async Task<bool> AnyBySpecAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        var query = ApplySpecificationForCount(specification);
        return await query.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Applies specification criteria to a queryable.
    /// </summary>
    /// <param name="specification">The specification to apply</param>
    /// <returns>IQueryable with all specification rules applied</returns>
    private IQueryable<T> ApplySpecification(ISpecification<T> specification)
    {
        var query = _dbSet.AsQueryable();

        // Apply AsNoTracking if specified
        if (specification.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        // Apply Includes
        query = specification.Includes
            .Aggregate(query, (current, include) => current.Include(include));

        // Apply Criteria
        query = query.Where(specification.Criteria);

        // Apply OrderBy
        if (specification.OrderBy != null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending != null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        // Apply Skip
        if (specification.Skip.HasValue)
        {
            query = query.Skip(specification.Skip.Value);
        }

        // Apply Take
        if (specification.Take.HasValue)
        {
            query = query.Take(specification.Take.Value);
        }

        return query;
    }

    /// <summary>
    /// Applies specification criteria for count/any operations (ignores ordering and paging).
    /// </summary>
    /// <param name="specification">The specification to apply</param>
    /// <returns>IQueryable with criteria applied</returns>
    private IQueryable<T> ApplySpecificationForCount(ISpecification<T> specification)
    {
        var query = _dbSet.AsQueryable();

        // Apply Includes (may be needed for criteria)
        query = specification.Includes
            .Aggregate(query, (current, include) => current.Include(include));

        // Apply Criteria only (no ordering or paging needed for count)
        query = query.Where(specification.Criteria);

        return query;
    }

    #endregion

    #region GET (NO TRACKING - READ ONLY)

    /// <summary>
    /// Gets the first entity without tracking (read-only).
    /// Use for scenarios where you don't need to update the entity.
    /// </summary>
    /// <param name="where">The filter expression</param>
    /// <param name="include">Navigation properties to include (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first matching entity (untracked) if found; otherwise, null</returns>
    /// <remarks>
    /// <para><strong>Performance Benefits:</strong></para>
    /// <list type="bullet">
    ///   <item>No change tracking overhead - faster query execution</item>
    ///   <item>Lower memory usage - entity not stored in change tracker</item>
    ///   <item>Ideal for read-only operations, reports, and DTOs</item>
    /// </list>
    ///
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// // Read-only data for display
    /// var product = await repository.GetFirstOrDefaultAsNoTrackingAsync(
    ///     where: x => x.Id == id,
    ///     include: q => q.Include(x => x.Category)
    /// );
    /// </code>
    /// </remarks>
    public async Task<T?> GetFirstOrDefaultAsNoTrackingAsync(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking();

        if (include != null)
            query = include(query);

        query = query.Where(where);

        // Apply ordering - default to Id if not specified to avoid EF Core warnings
        if (orderBy != null)
            query = query.OrderBy(orderBy);
        else
            query = query.OrderBy(x => EF.Property<object>(x, "Id"));

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all entities without tracking (read-only).
    /// </summary>
    /// <param name="where">Optional filter expression</param>
    /// <param name="include">Navigation properties to include (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of entities (untracked)</returns>
    /// <remarks>
    /// <para><strong>Performance Benefits:</strong></para>
    /// <list type="bullet">
    ///   <item>20-30% faster than tracked queries for large result sets</item>
    ///   <item>Significantly lower memory consumption</item>
    ///   <item>Perfect for exporting, reporting, and bulk read operations</item>
    /// </list>
    ///
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// // Export all active products
    /// var products = await repository.GetAsNoTrackingAsync(
    ///     where: x => x.IsActive,
    ///     include: q => q.Include(x => x.Category)
    /// );
    /// </code>
    /// </remarks>
    public async Task<List<T>> GetAsNoTrackingAsync(
        Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking();

        if (include != null)
            query = include(query);

        if (where != null)
            query = query.Where(where);

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets entities with projection without tracking (read-only).
    /// Combines AsNoTracking with projection for maximum performance.
    /// </summary>
    /// <typeparam name="TProjection">The type to project to</typeparam>
    /// <param name="projection">The projection expression</param>
    /// <param name="where">Optional filter expression</param>
    /// <param name="include">Navigation properties to include before projection (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of projected results (untracked)</returns>
    /// <remarks>
    /// <para><strong>Best Performance Pattern:</strong></para>
    /// <code>
    /// // Project to DTO - most efficient read operation
    /// var productDtos = await repository.GetAsNoTrackingAsync(
    ///     projection: x => new ProductDto { Id = x.Id, Name = x.Name },
    ///     where: x => x.IsActive
    /// );
    /// </code>
    /// </remarks>
    public async Task<List<TProjection>> GetAsNoTrackingAsync<TProjection>(
        Expression<Func<T, TProjection>> projection,
        Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking();

        if (include != null)
            query = include(query);

        if (where != null)
            query = query.Where(where);

        return await query.Select(projection).ToListAsync(cancellationToken);
    }

    #endregion

    #region PAGING

    public async Task<PagedResult<T>> GetPagedItemsAsync(
        PagingInfo pagingInfo,
        Expression<Func<T, bool>>? where = null,
        string defaultSortColumn = "Id",
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (where != null)
            query = query.Where(where);

        if (include != null)
            query = include(query);

        return await query.ApplyPagingAndFilteringAsync(
            pagingInfo,
            defaultSortColumn, 
            searchProjection: true,
            cancellationToken: cancellationToken
        );
    }

    public async Task<PagedResult<Tprojection>> GetPagedProjection<Tprojection>(
        PagingInfo pagingInfo,
        Expression<Func<T, Tprojection>> projection,
        Expression<Func<T, bool>>? where = null,
        string defaultSortColumn = "Id",
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        bool searchProjection = true,
        CancellationToken cancellationToken = default)
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

        return await projectedQuery.ApplyPagingAndFilteringAsync(
            pagingInfo,
            defaultSortColumn,
            searchProjection,
            cancellationToken
        );
    }

    #endregion

    #region ADD

    /// <summary>
    /// Adds a new entity to the context.
    /// </summary>
    /// <param name="entity">The entity to add</param>
    /// <returns>The added entity</returns>
    /// <remarks>
    /// The entity will be marked as Added and inserted into the database when SaveChanges is called.
    /// Child collections are automatically tracked and will be inserted as well (cascade add).
    /// </remarks>
    public T Add(T entity)
    {
        _dbSet.Add(entity);

        return entity;
    }

    /// <summary>
    /// Adds a range of new entities to the context.
    /// </summary>
    /// <param name="entities">The entities to add</param>
    /// <remarks>
    /// All entities will be marked as Added and inserted into the database when SaveChanges is called.
    /// More efficient than calling Add() multiple times.
    /// </remarks>
    public void AddRange(IEnumerable<T> entities)
    {
        _dbSet.AddRange(entities);
    }

    /// <summary>
    /// Adds a new entity to the context asynchronously.
    /// </summary>
    /// <param name="entity">The entity to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The EntityEntry for the added entity</returns>
    /// <remarks>
    /// <para><strong>EF Change Tracking Behavior:</strong></para>
    /// <list type="bullet">
    ///   <item>Marks root entity as Added</item>
    ///   <item>Automatically propagates Added state to all navigation properties (cascade add)</item>
    ///   <item>Child collections are automatically tracked and inserted</item>
    /// </list>
    ///
    /// <para><strong>Example - Add with child collections:</strong></para>
    /// <code>
    /// var product = new Product
    /// {
    ///     Name = "New Product",
    ///     ProductCategoryRelations = categoryIds
    ///         .Select(id => new ProductCategoryRelation { CategoryId = id })
    ///         .ToList()
    /// };
    ///
    /// await repository.AddAsync(product, cancellationToken);
    /// await unitOfWork.SaveChangesAsync();
    /// // Result: Product inserted + all ProductCategoryRelations inserted (automatic!)
    /// </code>
    /// </remarks>
    public async ValueTask<EntityEntry<T>> AddAsync(T entity, CancellationToken cancellationToken)
    {
        return await _dbSet.AddAsync(entity, cancellationToken);
    }

    #endregion

    #region UPDATE

    /// <summary>
    /// Marks the specified entity as Modified.
    /// </summary>
    /// <param name="entity">The entity to update</param>
    /// <returns>The EntityEntry for the updated entity</returns>
    /// <remarks>
    /// ⚠️ CRITICAL WARNING: Use this method ONLY for disconnected entities!
    ///
    /// <para><strong>For TRACKED entities (loaded with tracking enabled):</strong></para>
    /// <list type="bullet">
    ///   <item>DO NOT call this method</item>
    ///   <item>Simply modify entity properties directly</item>
    ///   <item>EF Change Tracker automatically detects changes</item>
    ///   <item>For child collections, use .Clear() + .Add() pattern or ReplaceChildCollection()</item>
    /// </list>
    ///
    /// <para><strong>Use this method ONLY for:</strong></para>
    /// <list type="bullet">
    ///   <item>DTOs mapped to entities from API requests</item>
    ///   <item>Entities loaded with AsNoTracking()</item>
    ///   <item>Disconnected/deserialized entities from external sources</item>
    /// </list>
    ///
    /// <para><strong>Example - WRONG (tracked entity):</strong></para>
    /// <code>
    /// var product = await repository.GetFirstOrDefaultAsync(x => x.Id == id); // Tracked!
    /// product.Name = "New Name";
    /// repository.Update(product); // ❌ WRONG - redundant and harmful!
    /// await unitOfWork.SaveChangesAsync();
    /// </code>
    ///
    /// <para><strong>Example - CORRECT (tracked entity):</strong></para>
    /// <code>
    /// var product = await repository.GetFirstOrDefaultAsync(x => x.Id == id); // Tracked!
    /// product.Name = "New Name";
    /// // Don't call Update() - just save!
    /// await unitOfWork.SaveChangesAsync();
    /// </code>
    ///
    /// <para><strong>Example - CORRECT (disconnected entity):</strong></para>
    /// <code>
    /// var product = MapDtoToEntity(dto); // Disconnected entity
    /// repository.Update(product); // ✅ CORRECT - entity is disconnected
    /// await unitOfWork.SaveChangesAsync();
    /// </code>
    /// </remarks>
    public EntityEntry<T> Update(T entity)
    {
        return _dbSet.Update(entity);
    }

    /// <summary>
    /// Updates a disconnected entity (from API/DTO or external source).
    /// This is an explicit method for disconnected entity scenarios.
    /// </summary>
    /// <param name="entity">The disconnected entity to update</param>
    /// <returns>The EntityEntry for the updated entity</returns>
    /// <remarks>
    /// <para><strong>Use this method when:</strong></para>
    /// <list type="bullet">
    ///   <item>Entity comes from API request (mapped from DTO)</item>
    ///   <item>Entity was loaded with AsNoTracking()</item>
    ///   <item>Entity was deserialized from external source</item>
    ///   <item>You need to explicitly mark entity as modified</item>
    /// </list>
    ///
    /// <para><strong>Example - API Controller scenario:</strong></para>
    /// <code>
    /// [HttpPut("{id}")]
    /// public async Task&lt;IActionResult&gt; Update(Guid id, UpdateProductRequest request)
    /// {
    ///     var product = _mapper.Map&lt;Product&gt;(request); // Disconnected entity from DTO
    ///     product.Id = id;
    ///
    ///     _unitOfWork.Products.UpdateDisconnected(product);
    ///     await _unitOfWork.SaveChangesAsync();
    ///
    ///     return Ok();
    /// }
    /// </code>
    ///
    /// <para><strong>Note:</strong> This method is functionally identical to Update(), but the explicit naming
    /// makes the intent clear and prevents misuse with tracked entities.</para>
    /// </remarks>
    public virtual EntityEntry<T> UpdateDisconnected(T entity)
    {
        return _dbSet.Update(entity);
    }

    /// <summary>
    /// Replaces all items in a child collection using the Clear + Add pattern.
    /// This is the recommended pattern for updating junction table collections on tracked entities.
    /// </summary>
    /// <typeparam name="TChild">The type of child entities in the collection</typeparam>
    /// <param name="currentCollection">The existing collection to update (must be tracked by EF)</param>
    /// <param name="newItems">The new items to add to the collection</param>
    /// <remarks>
    /// <para><strong>Why use this pattern:</strong></para>
    /// <list type="bullet">
    ///   <item>EF Change Tracker reliably detects .Clear() operation</item>
    ///   <item>Generates efficient SQL: DELETE ... WHERE ParentId = X, then INSERT new items</item>
    ///   <item>Works correctly with composite key entities (junction tables)</item>
    ///   <item>More reliable than selective .Remove() for complex scenarios</item>
    /// </list>
    ///
    /// <para><strong>When to use:</strong></para>
    /// <list type="bullet">
    ///   <item>Many-to-many relationships (junction tables like ProductCategoryRelation)</item>
    ///   <item>One-to-many collections that need full replacement</item>
    ///   <item>Update handlers where parent entity is tracked</item>
    /// </list>
    ///
    /// <para><strong>Example - ProductCategoryRelation update:</strong></para>
    /// <code>
    /// var product = await repository.GetFirstOrDefaultAsync(
    ///     where: x => x.Id == productId,
    ///     include: q => q.Include(x => x.ProductCategoryRelations)
    /// );
    ///
    /// var newRelations = categoryIds.Select(id => new ProductCategoryRelation
    /// {
    ///     ProductId = product.Id,
    ///     CategoryId = id
    /// });
    ///
    /// repository.ReplaceChildCollection(product.ProductCategoryRelations, newRelations);
    /// await unitOfWork.SaveChangesAsync();
    /// </code>
    ///
    /// <para><strong>SQL Generated:</strong></para>
    /// <code>
    /// DELETE FROM ProductCategoryRelations WHERE ProductId = @p0;
    /// INSERT INTO ProductCategoryRelations (ProductId, CategoryId) VALUES (@p1, @p2), (@p1, @p3), ...
    /// </code>
    /// </remarks>
    public virtual void ReplaceChildCollection<TChild>(
        ICollection<TChild> currentCollection,
        IEnumerable<TChild> newItems) where TChild : class
    {
        ArgumentNullException.ThrowIfNull(currentCollection);
        ArgumentNullException.ThrowIfNull(newItems);

        currentCollection.Clear();

        foreach (var item in newItems)
        {
            currentCollection.Add(item);
        }
    }

    /// <summary>
    /// Checks whether the specified entity is currently being tracked by the context.
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <returns>True if the entity is tracked, false otherwise</returns>
    /// <remarks>
    /// <para><strong>Use this method to:</strong></para>
    /// <list type="bullet">
    ///   <item>Debug tracking issues</item>
    ///   <item>Validate assumptions before calling Update()</item>
    ///   <item>Implement conditional update logic</item>
    /// </list>
    ///
    /// <para><strong>Example - Conditional update:</strong></para>
    /// <code>
    /// if (repository.IsTracked(product))
    /// {
    ///     // Entity is tracked, just modify properties
    ///     product.Name = newName;
    /// }
    /// else
    /// {
    ///     // Entity is disconnected, must call Update()
    ///     repository.UpdateDisconnected(product);
    /// }
    /// await unitOfWork.SaveChangesAsync();
    /// </code>
    /// </remarks>
    public virtual bool IsTracked(T entity)
    {
        return _context.Entry(entity).State != EntityState.Detached;
    }

    public void UpdateRange(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
    }

    #endregion

    #region REMOVE

    /// <summary>
    /// Removes (deletes) the specified entity from the database context.
    /// </summary>
    /// <param name="entity">The entity to remove</param>
    /// <returns>The removed entity</returns>
    /// <remarks>
    /// The entity will be marked as Deleted and physically removed from the database when SaveChanges is called.
    /// For soft delete functionality, use SoftDelete() method instead.
    /// </remarks>
    public T Remove(T entity)
    {
        _dbSet.Remove(entity);
        return entity;
    }

    /// <summary>
    /// Removes (deletes) a range of entities from the database context.
    /// </summary>
    /// <param name="entities">The entities to remove</param>
    /// <remarks>
    /// All entities will be marked as Deleted and physically removed from the database when SaveChanges is called.
    /// More efficient than calling Remove() multiple times.
    /// </remarks>
    public void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    #endregion

    #region SOFT DELETE

    /// <summary>
    /// Soft deletes an entity by marking it as deleted without physically removing it from the database.
    /// Only works if entity implements IDeletedEntity interface.
    /// </summary>
    /// <param name="entity">The entity to soft delete</param>
    /// <returns>The soft-deleted entity</returns>
    /// <remarks>
    /// <para><strong>Behavior:</strong></para>
    /// <list type="bullet">
    ///   <item>If entity implements IDeletedEntity: Sets IsDeleted = true and marks entity as Modified</item>
    ///   <item>Otherwise: Throws InvalidOperationException</item>
    /// </list>
    ///
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var product = await repository.GetByIdAsync(productId);
    /// repository.SoftDelete(product);
    /// await unitOfWork.SaveChangesAsync();
    /// // Product remains in database but IsDeleted = true
    /// </code>
    ///
    /// <para><strong>Note:</strong> Global query filters should be configured to exclude soft-deleted entities automatically.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when entity doesn't implement IDeletedEntity</exception>
    public virtual T SoftDelete(T entity)
    {
        if (entity is Domain.Entities.Interfaces.IDeletedEntity)
        {
            // Use reflection to set IsDeleted since interface property is read-only
            var property = entity.GetType().GetProperty(nameof(Domain.Entities.Interfaces.IDeletedEntity.IsDeleted));
            if (property != null && property.CanWrite)
            {
                property.SetValue(entity, true);
                _dbSet.Update(entity);
                return entity;
            }

            throw new InvalidOperationException(
                $"Entity type '{typeof(T).Name}' implements IDeletedEntity but IsDeleted property is not writable.");
        }

        throw new InvalidOperationException(
            $"Entity type '{typeof(T).Name}' does not implement IDeletedEntity interface and cannot be soft deleted. " +
            $"Use Remove() for physical deletion or implement IDeletedEntity interface.");
    }

    /// <summary>
    /// Soft deletes multiple entities by marking them as deleted without physically removing them from the database.
    /// Only works if entities implement IDeletedEntity interface.
    /// </summary>
    /// <param name="entities">The entities to soft delete</param>
    /// <remarks>
    /// More efficient than calling SoftDelete() multiple times.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when any entity doesn't implement IDeletedEntity</exception>
    public virtual void SoftDeleteRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            SoftDelete(entity);
        }
    }

    /// <summary>
    /// Restores a soft-deleted entity by marking it as not deleted.
    /// </summary>
    /// <param name="entity">The entity to restore</param>
    /// <returns>The restored entity</returns>
    /// <remarks>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// // Get soft-deleted entity (requires IgnoreQueryFilters)
    /// var product = await repository.GetFirstOrDefaultAsync&lt;Product&gt;(
    ///     where: x => x.Id == productId,
    ///     ignoreQueryFilters: true
    /// );
    ///
    /// if (product?.IsDeleted == true)
    /// {
    ///     repository.Restore(product);
    ///     await unitOfWork.SaveChangesAsync();
    /// }
    /// </code>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when entity doesn't implement IDeletedEntity</exception>
    public virtual T Restore(T entity)
    {
        if (entity is Domain.Entities.Interfaces.IDeletedEntity)
        {
            // Use reflection to set IsDeleted since interface property is read-only
            var property = entity.GetType().GetProperty(nameof(Domain.Entities.Interfaces.IDeletedEntity.IsDeleted));
            if (property != null && property.CanWrite)
            {
                property.SetValue(entity, false);
                _dbSet.Update(entity);
                return entity;
            }

            throw new InvalidOperationException(
                $"Entity type '{typeof(T).Name}' implements IDeletedEntity but IsDeleted property is not writable.");
        }

        throw new InvalidOperationException(
            $"Entity type '{typeof(T).Name}' does not implement IDeletedEntity interface and cannot be restored.");
    }

    /// <summary>
    /// Restores multiple soft-deleted entities.
    /// </summary>
    /// <param name="entities">The entities to restore</param>
    /// <exception cref="InvalidOperationException">Thrown when any entity doesn't implement IDeletedEntity</exception>
    public virtual void RestoreRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            Restore(entity);
        }
    }

    #endregion

    #region BATCH OPERATIONS

    /// <summary>
    /// Deletes all entities matching the predicate in a single database operation.
    /// Uses ExecuteDeleteAsync (EF Core 7+) for optimal performance.
    /// </summary>
    /// <param name="where">The filter predicate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of entities deleted</returns>
    /// <remarks>
    /// <para><strong>⚠️ WARNING: This bypasses the change tracker and executes directly on the database.</strong></para>
    /// <list type="bullet">
    ///   <item>No entity tracking - changes happen directly in database</item>
    ///   <item>No cascade deletes from EF (database cascade rules still apply)</item>
    ///   <item>No interceptors/events triggered</item>
    ///   <item>Significantly faster than loading entities into memory and deleting them</item>
    /// </list>
    ///
    /// <para><strong>Example - Delete all inactive products:</strong></para>
    /// <code>
    /// var deletedCount = await repository.DeleteWhereAsync(x => !x.IsActive);
    /// // SQL: DELETE FROM Products WHERE IsActive = 0
    /// </code>
    ///
    /// <para><strong>Performance:</strong> 100x faster than Load + Delete for large datasets.</para>
    /// </remarks>
    public virtual async Task<int> DeleteWhereAsync(
        Expression<Func<T, bool>> where,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(where).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Updates all entities matching the predicate in a single database operation.
    /// Uses ExecuteUpdateAsync (EF Core 7+) for optimal performance.
    /// </summary>
    /// <param name="where">The filter predicate</param>
    /// <param name="setters">The property setters expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of entities updated</returns>
    /// <remarks>
    /// <para><strong>⚠️ WARNING: This bypasses the change tracker and executes directly on the database.</strong></para>
    /// <list type="bullet">
    ///   <item>No entity tracking - changes happen directly in database</item>
    ///   <item>No interceptors/events triggered</item>
    ///   <item>Audit fields (ModifiedBy, ModifiedOnUtc) must be set manually in the setters</item>
    ///   <item>Significantly faster than loading entities into memory and updating them</item>
    /// </list>
    ///
    /// <para><strong>Example - Deactivate products in category:</strong></para>
    /// <code>
    /// var updatedCount = await repository.UpdateWhereAsync(
    ///     where: x => x.CategoryId == categoryId,
    ///     setters: s => s.SetProperty(x => x.IsActive, false)
    ///                    .SetProperty(x => x.ModifiedOnUtc, DateTimeOffset.UtcNow)
    /// );
    /// // SQL: UPDATE Products SET IsActive = 0, ModifiedOnUtc = @p0 WHERE CategoryId = @p1
    /// </code>
    ///
    /// <para><strong>Performance:</strong> 100x faster than Load + Update for large datasets.</para>
    /// </remarks>
    public virtual async Task<int> UpdateWhereAsync(
        Expression<Func<T, bool>> where,
        Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setters,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(where).ExecuteUpdateAsync(setters, cancellationToken);
    }

    #endregion

    #region HELPER METHODS

    /// <summary>
    /// Checks if an entity with the specified ID exists.
    /// More efficient than loading the entire entity.
    /// </summary>
    /// <typeparam name="TId">The type of the ID</typeparam>
    /// <param name="id">The ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if entity exists; otherwise, false</returns>
    /// <remarks>
    /// This method uses FindAsync which checks local cache first, then queries database.
    /// Entity is detached immediately to avoid tracking overhead.
    /// </remarks>
    public virtual async Task<bool> ExistsAsync<TId>(TId id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync([id], cancellationToken);
        if (entity != null)
        {
            _context.Entry(entity).State = EntityState.Detached;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets multiple entities by their IDs in a single query (bulk fetch).
    /// </summary>
    /// <typeparam name="TId">The type of the ID</typeparam>
    /// <param name="ids">The collection of IDs to fetch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entities with matching IDs</returns>
    /// <remarks>
    /// <para>This method performs a single database query using WHERE ID IN (@ids).</para>
    /// <para>More efficient than multiple Find() calls.</para>
    /// <para>⚠️ Note: Assumes entity has a property named "Id". Override if different.</para>
    /// </remarks>
    public virtual async Task<List<T>> GetManyByIdsAsync<TId>(
        IEnumerable<TId> ids,
        CancellationToken cancellationToken = default)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, "Id");
        var idsArray = ids.ToArray();
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TId));

        var containsCall = Expression.Call(
            null,
            containsMethod,
            Expression.Constant(idsArray),
            property);

        var lambda = Expression.Lambda<Func<T, bool>>(containsCall, parameter);

        return await _dbSet.Where(lambda).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the first entity matching the criteria, or throws exception if not found.
    /// </summary>
    /// <param name="where">The filter expression</param>
    /// <param name="include">Navigation properties to include (optional)</param>
    /// <param name="orderBy">Optional ordering expression. If not specified, orders by Id to avoid EF Core warnings.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first matching entity</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity matches the criteria</exception>
    /// <remarks>
    /// <para>Use this method when entity MUST exist - eliminates null checking in calling code.</para>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// // No null check needed - throws if not found
    /// var product = await repository.GetFirstOrThrowAsync(x => x.Id == id);
    /// product.Name = "New Name"; // Safe - product is never null
    /// </code>
    /// </remarks>
    public virtual async Task<T> GetFirstOrThrowAsync(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (include != null)
            query = include(query);

        query = query.Where(where);

        // Apply ordering - default to Id if not specified to avoid EF Core warnings
        if (orderBy != null)
            query = query.OrderBy(orderBy);
        else
            query = query.OrderBy(x => EF.Property<object>(x, "Id"));

        var entity = await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' matching the specified criteria was not found.");
        return entity;
    }

    /// <summary>
    /// Detaches the specified entity from the change tracker.
    /// </summary>
    /// <param name="entity">The entity to detach</param>
    /// <remarks>
    /// <para>Useful when you want to reload an entity from database or prevent automatic tracking.</para>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var product = await repository.GetByIdAsync(id);
    /// repository.Detach(product);
    /// // Entity is now detached - changes won't be tracked
    /// </code>
    /// </remarks>
    public virtual void Detach(T entity)
    {
        _context.Entry(entity).State = EntityState.Detached;
    }

    /// <summary>
    /// Detaches all entities of type T from the change tracker.
    /// </summary>
    /// <remarks>
    /// Useful for clearing the change tracker of all entities of this type.
    /// </remarks>
    public virtual void DetachAll()
    {
        var entries = _context.ChangeTracker.Entries<T>().ToList();
        foreach (var entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Gets entities with navigation properties included via string paths.
    /// Useful for dynamic includes or deeply nested properties.
    /// </summary>
    /// <param name="where">Filter expression</param>
    /// <param name="includePaths">Navigation property paths (e.g., "Category.Parent")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entities with included navigation properties</returns>
    /// <remarks>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var products = await repository.GetWithIncludesAsync(
    ///     where: x => x.IsActive,
    ///     includePaths: new[] { "Category", "ProductCategoryRelations.Category" }
    /// );
    /// </code>
    /// </remarks>
    public virtual async Task<List<T>> GetWithIncludesAsync(
        Expression<Func<T, bool>> where,
        IEnumerable<string> includePaths,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        foreach (var path in includePaths)
        {
            query = query.Include(path);
        }

        return await query.Where(where).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Counts entities using long (Int64) for very large tables.
    /// </summary>
    /// <param name="where">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of entities as long</returns>
    /// <remarks>
    /// Use this instead of CountAsync when dealing with tables that may exceed 2 billion rows.
    /// </remarks>
    public virtual async Task<long> LongCountAsync(
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default)
    {
        if (where != null)
            return await _dbSet.LongCountAsync(where, cancellationToken);

        return await _dbSet.LongCountAsync(cancellationToken);
    }

    /// <summary>
    /// Counts the number of entities that match the specified filter.
    /// </summary>
    /// <param name="where">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of entities that match the filter</returns>
    public async Task<int> CountAsync(Expression<Func<T, bool>>? where = null, CancellationToken cancellationToken = default)
    {
        if (where != null)
            return await _dbSet.CountAsync(where, cancellationToken);

        return await _dbSet.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Determines whether any entities exist that match the specified filter.
    /// </summary>
    /// <param name="where">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if any entities exist that match the filter; otherwise, false</returns>
    public async Task<bool> AnyAsync(Expression<Func<T, bool>>? where = null, CancellationToken cancellationToken = default)
    {
        if (where != null)
            return await _dbSet.AnyAsync(where, cancellationToken);

        return await _dbSet.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Determines whether all entities match the specified filter.
    /// </summary>
    /// <param name="where">The filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all entities match the filter; otherwise, false</returns>
    public async Task<bool> AllAsync(Expression<Func<T, bool>> where, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AllAsync(where, cancellationToken);
    }

    #endregion

    #region ADDITIONAL OPERATIONS

    /// <summary>
    /// Reloads an entity from the database, overwriting any local changes.
    /// </summary>
    /// <param name="entity">The entity to reload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    /// <remarks>
    /// <para>Useful when you need to discard local changes and get fresh data from the database.</para>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var product = await repository.GetByIdAsync(id);
    /// product.Name = "Changed"; // Local change
    /// await repository.ReloadAsync(product); // Discards change, reloads from DB
    /// </code>
    /// </remarks>
    public virtual async Task ReloadAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _context.Entry(entity).ReloadAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all currently tracked entities of type T from the change tracker.
    /// </summary>
    /// <returns>A list of tracked entities</returns>
    /// <remarks>
    /// Useful for debugging or understanding what's currently in the change tracker.
    /// </remarks>
    public virtual List<T> GetTrackedEntities()
    {
        return _context.ChangeTracker.Entries<T>()
            .Select(e => e.Entity)
            .ToList();
    }

    /// <summary>
    /// Gets detailed tracking information for all entities of type T.
    /// </summary>
    /// <returns>A dictionary mapping each entity to its EntityState</returns>
    /// <remarks>
    /// Advanced debugging tool to see the state of all tracked entities.
    /// </remarks>
    public virtual Dictionary<T, EntityState> GetTrackingInfo()
    {
        return _context.ChangeTracker.Entries<T>()
            .ToDictionary(e => e.Entity, e => e.State);
    }

    /// <summary>
    /// Gets the maximum value for a specific property.
    /// </summary>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="selector">Expression to select the property</param>
    /// <param name="where">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The maximum value</returns>
    public virtual async Task<TResult?> MaxAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        if (where != null)
            query = query.Where(where);

        return await query.MaxAsync(selector, cancellationToken);
    }

    /// <summary>
    /// Gets the minimum value for a specific property.
    /// </summary>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="selector">Expression to select the property</param>
    /// <param name="where">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The minimum value</returns>
    public virtual async Task<TResult?> MinAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        if (where != null)
            query = query.Where(where);

        return await query.MinAsync(selector, cancellationToken);
    }

    /// <summary>
    /// Calculates the sum of a specific property.
    /// </summary>
    /// <param name="selector">Expression to select the numeric property</param>
    /// <param name="where">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The sum of the selected property</returns>
    public virtual async Task<decimal> SumAsync(
        Expression<Func<T, decimal>> selector,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        if (where != null)
            query = query.Where(where);

        return await query.SumAsync(selector, cancellationToken);
    }

    /// <summary>
    /// Calculates the average of a specific property.
    /// </summary>
    /// <param name="selector">Expression to select the numeric property</param>
    /// <param name="where">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The average of the selected property</returns>
    public virtual async Task<decimal> AverageAsync(
        Expression<Func<T, decimal>> selector,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        if (where != null)
            query = query.Where(where);

        return await query.AverageAsync(selector, cancellationToken);
    }

    /// <summary>
    /// Gets a simple paginated result without sorting or complex pagination logic.
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="where">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of entities for the specified page</returns>
    /// <remarks>
    /// This is a lightweight alternative to GetPagedItemsAsync when you don't need sorting or PagedList metadata.
    /// </remarks>
    public virtual async Task<List<T>> GetPageAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (where != null)
            query = query.Where(where);

        return await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a new entity or updates an existing one based on a predicate.
    /// </summary>
    /// <param name="entity">The entity to add or update</param>
    /// <param name="predicate">Expression to find existing entity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if entity was added, false if updated</returns>
    /// <remarks>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// var wasAdded = await repository.UpsertAsync(
    ///     product,
    ///     x => x.Sku == product.Sku
    /// );
    /// await unitOfWork.SaveChangesAsync();
    /// </code>
    /// </remarks>
    public virtual async Task<bool> UpsertAsync(
        T entity,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);

        if (existing == null)
        {
            _dbSet.Add(entity);
            return true; // Added
        }

        _context.Entry(existing).CurrentValues.SetValues(entity);
        return false; // Updated
    }

    #endregion
}