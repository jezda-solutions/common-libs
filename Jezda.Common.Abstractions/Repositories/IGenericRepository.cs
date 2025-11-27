using Jezda.Common.Abstractions.Specifications;
using Jezda.Common.Domain.Paged;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
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
    /// <param name="orderBy">Optional ordering expression. If not specified, orders by Id to avoid EF Core warnings.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The first entity that matches the filter if found; otherwise, null.</returns>
    Task<T?> GetFirstOrDefaultAsync(
        Expression<Func<T, bool>> where,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of entities.</returns>
    Task<List<T>> GetAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entities that match the specified filter.
    /// </summary>
    /// <param name="where"></param>
    /// <returns></returns>
    Task<List<T>> GetAsync(
        Expression<Func<T, bool>> where);

    /// <summary>
    /// Gets entities that match the specified filter.
    /// </summary>
    /// <param name="where">The filter expression.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of entities that match the filter.</returns>
    Task<List<T>> GetAsync(
        Expression<Func<T, bool>> where,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entities that match the specified filter and includes the specified related entities.
    /// </summary>
    /// <param name="where"></param>
    /// <param name="include"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<T>> GetAsync(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default);

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
    /// <param name="orderBy">Optional ordering expression. If not specified, orders by Id to avoid EF Core warnings.</param>
    /// <param name="ignoreQueryFilters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Task<TProjection?> GetFirstOrDefaultAsync<TProjection>(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, TProjection>>? projection = null,
        Expression<Func<T, object>>? orderBy = null,
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
    
    #endregion

    #region SPECIFICATION PATTERN

    /// <summary>
    /// Gets entities using a specification pattern.
    /// </summary>
    /// <param name="specification">The specification containing query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of entities matching the specification</returns>
    Task<List<T>> GetBySpecAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity using a specification pattern.
    /// </summary>
    /// <param name="specification">The specification containing query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first entity matching the specification, or null</returns>
    Task<T?> GetFirstBySpecAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities using a specification pattern.
    /// </summary>
    /// <param name="specification">The specification containing query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The count of entities matching the specification</returns>
    Task<int> CountBySpecAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entities exist using a specification pattern.
    /// </summary>
    /// <param name="specification">The specification containing query criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if any entities match the specification; otherwise, false</returns>
    Task<bool> AnyBySpecAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default);

    #endregion

    #region GET (NO TRACKING - READ ONLY)

    /// <summary>
    /// Gets the first entity without tracking (read-only).
    /// Use for scenarios where you don't need to update the entity.
    /// </summary>
    Task<T?> GetFirstOrDefaultAsNoTrackingAsync(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities without tracking (read-only).
    /// </summary>
    Task<List<T>> GetAsNoTrackingAsync(
        Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entities with projection without tracking (read-only).
    /// Combines AsNoTracking with projection for maximum performance.
    /// </summary>
    Task<List<TProjection>> GetAsNoTrackingAsync<TProjection>(
        Expression<Func<T, TProjection>> projection,
        Expression<Func<T, bool>>? where = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default);

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
    Task<PagedResult<T>> GetPagedItemsAsync(
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
    Task<PagedResult<Tprojection>> GetPagedProjection<Tprojection>(
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

    #endregion

    #region UPDATE

    /// <summary>
    /// Marks the specified entity as Modified.
    /// </summary>
    /// <param name="entity">The entity to update</param>
    /// <returns>The EntityEntry for the updated entity</returns>
    /// <remarks>
    /// ⚠️ CRITICAL WARNING: Use this method ONLY for disconnected entities!
    /// See GenericRepository implementation for detailed documentation and examples.
    /// </remarks>
    EntityEntry<T> Update(T entity);

    /// <summary>
    /// Updates a disconnected entity (from API/DTO or external source).
    /// This is an explicit method for disconnected entity scenarios.
    /// </summary>
    /// <param name="entity">The disconnected entity to update</param>
    /// <returns>The EntityEntry for the updated entity</returns>
    EntityEntry<T> UpdateDisconnected(T entity);

    /// <summary>
    /// Replaces all items in a child collection using the Clear + Add pattern.
    /// This is the recommended pattern for updating junction table collections on tracked entities.
    /// </summary>
    /// <typeparam name="TChild">The type of child entities in the collection</typeparam>
    /// <param name="currentCollection">The existing collection to update (must be tracked by EF)</param>
    /// <param name="newItems">The new items to add to the collection</param>
    void ReplaceChildCollection<TChild>(
        ICollection<TChild> currentCollection,
        IEnumerable<TChild> newItems) where TChild : class;

    /// <summary>
    /// Checks whether the specified entity is currently being tracked by the context.
    /// </summary>
    /// <param name="entity">The entity to check</param>
    /// <returns>True if the entity is tracked, false otherwise</returns>
    bool IsTracked(T entity);

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

    #region SOFT DELETE

    /// <summary>
    /// Soft deletes an entity by marking it as deleted without physically removing it from the database.
    /// Only works if entity implements IDeletedEntity interface.
    /// </summary>
    /// <param name="entity">The entity to soft delete</param>
    /// <returns>The soft-deleted entity</returns>
    T SoftDelete(T entity);

    /// <summary>
    /// Soft deletes multiple entities by marking them as deleted without physically removing them from the database.
    /// </summary>
    /// <param name="entities">The entities to soft delete</param>
    void SoftDeleteRange(IEnumerable<T> entities);

    /// <summary>
    /// Restores a soft-deleted entity by marking it as not deleted.
    /// </summary>
    /// <param name="entity">The entity to restore</param>
    /// <returns>The restored entity</returns>
    T Restore(T entity);

    /// <summary>
    /// Restores multiple soft-deleted entities.
    /// </summary>
    /// <param name="entities">The entities to restore</param>
    void RestoreRange(IEnumerable<T> entities);

    #endregion

    #region BATCH OPERATIONS

    /// <summary>
    /// Deletes all entities matching the predicate in a single database operation.
    /// Uses ExecuteDeleteAsync (EF Core 7+) for optimal performance.
    /// </summary>
    Task<int> DeleteWhereAsync(
        Expression<Func<T, bool>> where,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates all entities matching the predicate in a single database operation.
    /// Uses ExecuteUpdateAsync (EF Core 7+) for optimal performance.
    /// </summary>
    Task<int> UpdateWhereAsync(
        Expression<Func<T, bool>> where,
        Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setters,
        CancellationToken cancellationToken = default);

    #endregion

    #region HELPER METHODS

    /// <summary>
    /// Checks if an entity with the specified ID exists.
    /// </summary>
    Task<bool> ExistsAsync<TId>(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple entities by their IDs in a single query (bulk fetch).
    /// </summary>
    Task<List<T>> GetManyByIdsAsync<TId>(IEnumerable<TId> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity matching the criteria, or throws exception if not found.
    /// </summary>
    Task<T> GetFirstOrThrowAsync(
        Expression<Func<T, bool>> where,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches the specified entity from the change tracker.
    /// </summary>
    void Detach(T entity);

    /// <summary>
    /// Detaches all entities of type T from the change tracker.
    /// </summary>
    void DetachAll();

    /// <summary>
    /// Gets entities with navigation properties included via string paths.
    /// </summary>
    Task<List<T>> GetWithIncludesAsync(
        Expression<Func<T, bool>> where,
        IEnumerable<string> includePaths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities using long (Int64) for very large tables.
    /// </summary>
    Task<long> LongCountAsync(
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default);

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

    #endregion

    #region ADDITIONAL OPERATIONS

    /// <summary>
    /// Reloads an entity from the database, overwriting any local changes.
    /// </summary>
    Task ReloadAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all currently tracked entities of type T from the change tracker.
    /// </summary>
    List<T> GetTrackedEntities();

    /// <summary>
    /// Gets detailed tracking information for all entities of type T.
    /// </summary>
    Dictionary<T, EntityState> GetTrackingInfo();

    /// <summary>
    /// Gets the maximum value for a specific property.
    /// </summary>
    Task<TResult?> MaxAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the minimum value for a specific property.
    /// </summary>
    Task<TResult?> MinAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the sum of a specific property.
    /// </summary>
    Task<decimal> SumAsync(
        Expression<Func<T, decimal>> selector,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the average of a specific property.
    /// </summary>
    Task<decimal> AverageAsync(
        Expression<Func<T, decimal>> selector,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a simple paginated result without sorting or complex pagination logic.
    /// </summary>
    Task<List<T>> GetPageAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<T, bool>>? where = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity or updates an existing one based on a predicate.
    /// </summary>
    Task<bool> UpsertAsync(
        T entity,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    #endregion
}
