using Jezda.Common.Domain.Paged;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Abstractions.Repositories;

public interface IGenericRepository<T> where T : class
{
    #region GET

    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <param name="id"></param>
    /// <returns></returns>
    T? GetById<TId>(TId id);

    ValueTask<T?> GetByIdsAsync<TId>(TId[] ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<T?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity that matches the specified filter or returns null if no match is found.
    /// </summary>
    /// <returns></returns>
    T? GetFirstOrDefault();

    /// <summary>
    /// Gets the first entity that matches the specified filter or returns null if no match is found.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T?> GetFirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity that matches the specified filter.
    /// </summary>
    /// <param name="where">The filter expression.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The first entity that matches the filter if found; otherwise, null.</returns>
    Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> where, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of entities.</returns>
    Task<List<T>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entities that match the specified filter.
    /// </summary>
    /// <param name="where">The filter expression.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of entities that match the filter.</returns>
    Task<List<T>> GetAsync(Expression<Func<T, bool>> where, CancellationToken cancellationToken = default);

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
    public Task<TProjection?> GetFirstOrDefaultAsync<TProjection>(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, TProjection>>? projection = null,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entities that match the specified filter.
    /// </summary>
    /// <param name="where">The filter expression.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of entities that match the filter.</returns>
    Task<List<TProjection>> GetAsync<TProjection>(
        Expression<Func<T, TProjection>>? projection = null,
        Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, object>>? orderBy = null,
        bool? descending = null,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Gets entities including the specified related entities.
    /// </summary>
    /// <param name="includes">The related entities to include.</param>
    /// <returns>A list of entities including the related entities.</returns>
    Task<IReadOnlyList<T>> GetIncludingAsync(params Expression<Func<T, object>>[] includes);

    #endregion

    #region PAGING

    /// <summary>
    /// Gets a paged response of entities.
    /// </summary>
    /// <param name="pagingInfo"></param>
    /// <param name="where"></param>
    /// <param name="defaultSortColumn"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<PagedList<T>> GetPagedItemsAsync(
        PagingInfo pagingInfo,
        Expression<Func<T, bool>>? where = null,
        string defaultSortColumn = "Id",
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of projected entities.
    /// </summary>
    /// <param name="pagingInfo"><see cref="PagingInfo"/> request.</param>
    /// <param name="projection">Expression that projects entities of type T to type TProjection.</param>
    /// <param name="where">Predicate expression.</param>
    /// <param name="defaultSortColumn">Name of default sort column, that will be used of <see cref="PagingInfo.SortColumn"/> is not set.</param>
    /// <param name="cancellationToken">Stopping token,</param>
    /// <typeparam name="TProjection">Type of the projected entity.</typeparam>
    /// <returns><see cref="PagedList{TProjection}"/> of items of type TProjection.</returns>
    Task<PagedList<Tprojection>> GetPagedProjection<Tprojection>(
        PagingInfo pagingInfo,
        Expression<Func<T, Tprojection>> projection,
        Expression<Func<T, bool>>? where = null,
        string defaultSortColumn = "Id",
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        bool searchProjection = true,
        CancellationToken cancellationToken = default);

    #endregion

    #region ADD

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    T Add(T entity);

    /// <summary>
    /// Adds a range of new entities.
    /// </summary>
    /// <param name="entities"></param>
    void AddRange(IEnumerable<T> entities);

    /// <summary>
    /// Asynchronously adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The added entity.</returns>
    ValueTask<EntityEntry<T>> AddAsync(T entity, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously adds a range of new entities to the repository.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The added entities.</returns>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken);

    #endregion

    #region UPDATE

    /// <summary>
    /// Updates the specified entity in the database context and marks it as modified.
    /// </summary>
    /// <remarks>Use this method to update an existing entity in the database. Ensure that the entity being
    /// updated is already tracked by the context or has a valid key for identification. Changes made to the entity will
    /// be persisted to the database when <c>SaveChanges</c> is called.</remarks>
    /// <param name="entity">The entity to update. Cannot be null.</param>
    /// <returns>An <see cref="EntityEntry{T}"/> representing the updated entity, including its current state in the context.</returns>
    EntityEntry<T> Update(T entity);

    /// <summary>
    /// Updates a range of entities in the database context and marks them as modified.
    /// </summary>
    /// <param name="entities"></param>
    void UpdateRange(IEnumerable<T> entities);

    #endregion

    #region REMOVE

    /// <summary>
    /// Removes the specified entity from the database context and marks it for deletion.
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    T Remove(T entity);

    /// <summary>
    /// Removes a range of entities from the database context and marks them for deletion.
    /// </summary>
    /// <param name="entities"></param>
    void RemoveRange(IEnumerable<T> entities);

    #endregion

    /// <summary>
    /// Counts the number of entities that match the specified filter.
    /// </summary>
    /// <param name="where">The filter expression.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The count of entities that match the filter.</returns>
    Task<int> CountAsync(Expression<Func<T, bool>>? where = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether any entities exist that match the specified filter.
    /// </summary>
    /// <param name="where">The optional filter expression.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if any entities exist that match the filter; otherwise, false.</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>>? where = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether all entities match the specified filter.
    /// </summary>
    /// <param name="where">The filter expression.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if all entities match the filter; otherwise, false.</returns>
    Task<bool> AllAsync(Expression<Func<T, bool>> where, CancellationToken cancellationToken = default);
}
