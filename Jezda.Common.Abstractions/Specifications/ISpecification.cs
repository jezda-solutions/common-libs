using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Jezda.Common.Abstractions.Specifications;

/// <summary>
/// Specification pattern interface for encapsulating query logic.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
/// <remarks>
/// <para>The Specification Pattern allows you to encapsulate complex query logic
/// into reusable, testable, and composable specification classes.</para>
///
/// <para><strong>Benefits:</strong></para>
/// <list type="bullet">
///   <item>Encapsulates query logic in a single place</item>
///   <item>Reusable across multiple queries</item>
///   <item>Testable in isolation</item>
///   <item>More readable than inline lambda expressions</item>
///   <item>Easier to maintain complex queries</item>
/// </list>
///
/// <para><strong>Example Implementation:</strong></para>
/// <code>
/// public class ActiveProductsWithCategorySpec : ISpecification&lt;Product&gt;
/// {
///     public Expression&lt;Func&lt;Product, bool&gt;&gt; Criteria =&gt;
///         x =&gt; x.IsActive &amp;&amp; !x.IsDeleted;
///
///     public List&lt;Expression&lt;Func&lt;Product, object&gt;&gt;&gt; Includes =&gt; new()
///     {
///         x =&gt; x.Category,
///         x =&gt; x.Supplier
///     };
///
///     public Expression&lt;Func&lt;Product, object&gt;&gt;? OrderBy =&gt; x =&gt; x.Name;
///
///     public Expression&lt;Func&lt;Product, object&gt;&gt;? OrderByDescending =&gt; null;
///
///     public int? Take =&gt; null;
///
///     public int? Skip =&gt; null;
/// }
///
/// // Usage:
/// var spec = new ActiveProductsWithCategorySpec();
/// var products = await repository.GetBySpecAsync(spec);
/// </code>
/// </remarks>
public interface ISpecification<T>
{
    /// <summary>
    /// The filter criteria (WHERE clause).
    /// </summary>
    Expression<Func<T, bool>> Criteria { get; }

    /// <summary>
    /// List of navigation properties to include (eager loading).
    /// </summary>
    List<Expression<Func<T, object>>> Includes { get; }

    /// <summary>
    /// Order by expression (ascending).
    /// </summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>
    /// Order by expression (descending).
    /// </summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>
    /// Number of records to take (for pagination).
    /// </summary>
    int? Take { get; }

    /// <summary>
    /// Number of records to skip (for pagination).
    /// </summary>
    int? Skip { get; }

    /// <summary>
    /// Whether to use AsNoTracking for read-only queries.
    /// </summary>
    bool AsNoTracking { get; }
}
