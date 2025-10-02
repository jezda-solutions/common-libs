using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Jezda.Common.Abstractions.Specifications;

/// <summary>
/// Base class for creating specifications with a fluent API.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
/// <remarks>
/// <para>This base class provides a convenient way to build specifications
/// using a fluent interface pattern.</para>
///
/// <para><strong>Example:</strong></para>
/// <code>
/// public class ProductsByCategorySpec : BaseSpecification&lt;Product&gt;
/// {
///     public ProductsByCategorySpec(int categoryId)
///         : base(x =&gt; x.CategoryId == categoryId)
///     {
///         AddInclude(x =&gt; x.Category);
///         AddInclude(x =&gt; x.Supplier);
///         ApplyOrderBy(x =&gt; x.Name);
///     }
/// }
/// </code>
/// </remarks>
public abstract class BaseSpecification<T> : ISpecification<T>
{
    /// <summary>
    /// Initializes a new instance of BaseSpecification with a criteria expression.
    /// </summary>
    /// <param name="criteria">The filter criteria</param>
    protected BaseSpecification(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    public Expression<Func<T, bool>> Criteria { get; }

    public List<Expression<Func<T, object>>> Includes { get; } = new();

    public Expression<Func<T, object>>? OrderBy { get; private set; }

    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public int? Take { get; private set; }

    public int? Skip { get; private set; }

    public bool AsNoTracking { get; private set; }

    /// <summary>
    /// Adds a navigation property to include in the query.
    /// </summary>
    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    /// <summary>
    /// Applies ascending ordering to the query.
    /// </summary>
    protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
    }

    /// <summary>
    /// Applies descending ordering to the query.
    /// </summary>
    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
    {
        OrderByDescending = orderByDescExpression;
    }

    /// <summary>
    /// Applies pagination - number of records to take.
    /// </summary>
    protected void ApplyTake(int take)
    {
        Take = take;
    }

    /// <summary>
    /// Applies pagination - number of records to skip.
    /// </summary>
    protected void ApplySkip(int skip)
    {
        Skip = skip;
    }

    /// <summary>
    /// Configures the query to use AsNoTracking for read-only operations.
    /// </summary>
    protected void ApplyAsNoTracking()
    {
        AsNoTracking = true;
    }

    /// <summary>
    /// Applies paging (both skip and take).
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Page size</param>
    protected void ApplyPaging(int pageNumber, int pageSize)
    {
        Skip = (pageNumber - 1) * pageSize;
        Take = pageSize;
    }
}
