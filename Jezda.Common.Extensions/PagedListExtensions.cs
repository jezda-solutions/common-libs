using Jezda.Common.Domain.Paged;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Extensions;

public static class PagedListExtensions
{
    public static async Task<PagedResult<T>> ApplyPagingAndFilteringAsync<T>(
        this IQueryable<T> query,
        PagingInfo pagingInfo,
        string defaultSortColumn = "Id",
        bool searchProjection = true,
        CancellationToken cancellationToken = default)
    {
        // Globalno pretrazivanje
        // Kada zelimo da pretrazimo kroz projekciju postavicemo searchProjection na true
        // To znaci da posle Selecta radimo jos jedan where za dodatne provere.
        // Pazljivo koristiti jer moze biti pretesko za EF da pretrazi, uglavnom koristiti za proste podatke
        // i lako dostupne.
        if (searchProjection)
        {
            query = query.ApplyGlobalSearchFilter(pagingInfo.GlobalSearch);
        }

        // Sorted before counting, so a wrong sort_column fails without a database round trip.
        var sorted = string.IsNullOrEmpty(pagingInfo.SortColumn)
            ? query.ApplyDefaultSorting(defaultSortColumn, pagingInfo.SortDescending)
            : query.ApplySorting(pagingInfo.SortColumn, pagingInfo.SortDescending);

        return await sorted.ToPagedListAsync(pagingInfo, cancellationToken);
    }

    public static IQueryable<T> ApplyGlobalSearchFilter<T>(this IQueryable<T> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return query;

        var searchLower = search.ToLower();
        var parameter = Expression.Parameter(typeof(T), "x");
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetMethod != null && IsSearchableProperty(p))
            .ToList();

        if (properties.Count == 0)
            return query;

        // Kreiramo jedan veliki OR uslov
        Expression? combinedExpression = null;

        foreach (var property in properties)
        {
            var propertyExpression = CreatePropertySearchExpression(parameter, property, searchLower);
            if (propertyExpression != null)
            {
                combinedExpression = combinedExpression == null
                    ? propertyExpression
                    : Expression.OrElse(combinedExpression, propertyExpression);
            }
        }

        if (combinedExpression != null)
        {
            var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);
            return query.Where(lambda);
        }

        return query;
    }

    private static bool IsSearchableProperty(PropertyInfo property)
    {
        // Samo osnovni tipovi koji se mogu pretvoriti u string
        var type = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType == typeof(string) ||
               underlyingType == typeof(int) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(decimal) ||
               underlyingType == typeof(double) ||
               underlyingType == typeof(float) ||
               underlyingType == typeof(DateTime) ||
               underlyingType == typeof(DateOnly) ||
               underlyingType == typeof(DateTimeOffset) ||
               underlyingType == typeof(bool) ||
               underlyingType == typeof(Guid);
    }

    private static Expression? CreatePropertySearchExpression(ParameterExpression parameter, PropertyInfo property, string searchLower)
    {
        var propertyAccess = Expression.Property(parameter, property);

        if (property.PropertyType == typeof(string))
        {
            // Za string: x.Property != null && x.Property.ToLower().Contains(search)
            var notNull = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            var toLower = Expression.Call(propertyAccess, typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
            var contains = Expression.Call(toLower, typeof(string).GetMethod("Contains", [typeof(string)])!,
                Expression.Constant(searchLower));

            return Expression.AndAlso(notNull, contains);
        }
        else if (property.PropertyType == typeof(DateOnly) || Nullable.GetUnderlyingType(property.PropertyType) == typeof(DateOnly))
        {
            // Za DateOnly ili Nullable<DateOnly>
            if (DateOnly.TryParse(searchLower, out var parsedDate))
            {
                if (property.PropertyType == typeof(DateOnly))
                {
        // Direct comparison for DateOnly
                    var constant = Expression.Constant(parsedDate, typeof(DateOnly));
                    return Expression.Equal(propertyAccess, constant);
                }
                else
                {
                    // Za Nullable<DateOnly>: x.Property.HasValue && x.Property.Value == parsedDate
                    var hasValue = Expression.Property(propertyAccess, "HasValue");
                    var value = Expression.Property(propertyAccess, "Value");
                    var constant = Expression.Constant(parsedDate, typeof(DateOnly));
                    var equals = Expression.Equal(value, constant);
                    return Expression.AndAlso(hasValue, equals);
                }
            }
            else
            {
        // If parsing fails, return null (skip filtering for this column)
                return null;
            }
        }
        else if (Nullable.GetUnderlyingType(property.PropertyType) != null)
        {
            // Za ostale nullable tipove: x.Property.HasValue && x.Property.Value.ToString().ToLower().Contains(search)
            var hasValue = Expression.Property(propertyAccess, "HasValue");
            var value = Expression.Property(propertyAccess, "Value");
            var toString = Expression.Call(value, typeof(object).GetMethod("ToString")!);
            var toLower = Expression.Call(toString, typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
            var contains = Expression.Call(toLower, typeof(string).GetMethod("Contains", [typeof(string)])!,
                Expression.Constant(searchLower));

            return Expression.AndAlso(hasValue, contains);
        }
        else
        {
            // Za ostale value tipove: x.Property.ToString().ToLower().Contains(search)
            var toString = Expression.Call(propertyAccess, typeof(object).GetMethod("ToString")!);
            var toLower = Expression.Call(toString, typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
            var contains = Expression.Call(toLower, typeof(string).GetMethod("Contains", [typeof(string)])!,
                Expression.Constant(searchLower));

            return contains;
        }
    }

    // Filtriranje po search termu
    public static IQueryable<T> ApplySearchFilters<T>(this IQueryable<T> query, Dictionary<string, string>? searchTerms)
    {
        // Ako je prazan search term nema sta da pretrazujemo
        if (searchTerms is null || searchTerms.Count == 0) 
            return query;

        // Za svaki search term filtriramo
        foreach (var term in searchTerms)
        {
            var key = term.Key;
            var value = term.Value;

            // Ako vrednost nije prazna, filtriramo po tom polju
            if (!string.IsNullOrWhiteSpace(value))
            {
                var properties = key.Split('.'); // Ako je polje u formatu 'Entitet.Child...'
                var parameter = Expression.Parameter(typeof(T), "x");

                Expression propertyExpression = parameter;

        // Iterate through all properties (including nested ones)
                for (int i = 0; i < properties.Length; i++)
                {
                    var propertyName = properties[i];

                    // Trazimo polje na osnovu imena
                    var propertyInfo = FindProperty(propertyExpression.Type, propertyName)
                        ?? throw UnknownColumn(PagingParameter.SearchTerm, key);

                    propertyExpression = Expression.PropertyOrField(propertyExpression, propertyInfo.Name);

                    // Ako je polje kolekcija ulazimo u nju i pravimo dalje upit npr x.Collection.Any(y => y.Property.Constains(value));
                    if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyInfo.PropertyType) &&
                        propertyInfo.PropertyType != typeof(string))
                    {
                        var collectionType = propertyInfo.PropertyType.GetGenericArguments().FirstOrDefault()
                            ?? throw UnknownColumn(PagingParameter.SearchTerm, key);

                        var collectionParameter = Expression.Parameter(collectionType, "y");

                        // Pravimo izraz
                        Expression collectionExpression = collectionParameter;
                        for (int j = i + 1; j < properties.Length; j++)
                        {
                            var innerPropertyName = properties[j];
                            var innerPropertyInfo = FindProperty(collectionExpression.Type, innerPropertyName)
                                ?? throw UnknownColumn(PagingParameter.SearchTerm, key);

                            collectionExpression = Expression.PropertyOrField(collectionExpression, innerPropertyInfo.Name);
                        }

                        if (!IsScalar(collectionExpression.Type))
                            throw UnknownColumn(PagingParameter.SearchTerm, key);

                        Expression filterExpression = CreateFilterExpression(collectionExpression, value);

                        var collectionLambda = Expression.Lambda(filterExpression, collectionParameter);
                        var anyCall = Expression.Call(
                            typeof(Enumerable),
                            "Any",
                            [collectionType],
                            propertyExpression,
                            collectionLambda
                        );

                        // Dodajemo Any u upit
                        var lambda = Expression.Lambda<Func<T, bool>>(anyCall, parameter);
                        query = query.Where(lambda);
                        break;
                    }
                }

                // Ako nije kolekcija ili je string, filtriramo direktno
                if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyExpression.Type) || propertyExpression.Type == typeof(string))
                {
                    // A navigation or other complex property is not a column: EF cannot translate it.
                    if (!IsScalar(propertyExpression.Type))
                        throw UnknownColumn(PagingParameter.SearchTerm, key);

                    Expression filterExpression = CreateFilterExpression(propertyExpression, value);
                    var lambda = Expression.Lambda<Func<T, bool>>(filterExpression, parameter);
                    query = query.Where(lambda);
                }
            }
        }

        return query;
    }

    private static Expression CreateFilterExpression(Expression propertyExpression, string value)
    {
        var propertyType = propertyExpression.Type;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        // Proveravamo da li je GUID
        if (underlyingType == typeof(Guid))
        {
        // Try to parse the value as GUID
            if (Guid.TryParse(value, out var guidValue))
            {
                // Direktna poredba GUID vrednosti
                var constantExpression = Expression.Constant(guidValue, underlyingType);

                // Ako je nullable GUID, moramo da handled i null vrednosti
                if (propertyType != underlyingType) // Nullable<Guid>
                {
                    var hasValueProperty = Expression.Property(propertyExpression, "HasValue");
                    var valueProperty = Expression.Property(propertyExpression, "Value");
                    var equalExpression = Expression.Equal(valueProperty, constantExpression);
                    return Expression.AndAlso(hasValueProperty, equalExpression);
                }
                else
                {
                    return Expression.Equal(propertyExpression, constantExpression);
                }
            }
            else
            {
        // If we cannot parse as GUID, always return false
                return Expression.Constant(false);
            }
        }

        // Ako je tip string, filtriramo po string vrednosti
        if (propertyType == typeof(string))
        {
            var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
            var loweredPropertyExpression = Expression.Call(propertyExpression, toLowerMethod!);
            var loweredValue = Expression.Constant(value.ToLower(), typeof(string));
            var contains = typeof(string).GetMethod("Contains", [typeof(string)]);
            return Expression.Call(loweredPropertyExpression, contains!, loweredValue);
        }

        // Za ostale tipove, konvertujemo u string i filtriramo
        var toStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes);
        var toStringExpression = Expression.Call(propertyExpression, toStringMethod!);
        var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)]);
        return Expression.Call(toStringExpression, containsMethod!, Expression.Constant(value.ToString(), typeof(string)));
    }

    /// <summary>
    /// Orders <paramref name="query"/> by <paramref name="sortColumn"/>, a column name sent by the caller.
    /// </summary>
    /// <exception cref="InvalidPagingParameterException">No sortable public property of <typeparamref name="T"/> has that name.</exception>
    public static IQueryable<T> ApplySorting<T>(this IQueryable<T> query, string sortColumn, bool sortDescending)
    {
        if (string.IsNullOrWhiteSpace(sortColumn)) return query;

        var property = FindProperty(typeof(T), sortColumn);
        if (property is null || !IsScalar(property.PropertyType))
            throw UnknownColumn(PagingParameter.SortColumn, sortColumn);

        return query.OrderByProperty(property, sortDescending);
    }

    // The consumer's own default column: a wrong one is a programming error, not the caller's.
    private static IQueryable<T> ApplyDefaultSorting<T>(this IQueryable<T> query, string defaultSortColumn, bool sortDescending)
    {
        if (string.IsNullOrWhiteSpace(defaultSortColumn)) return query;

        var property = FindProperty(typeof(T), defaultSortColumn)
            ?? throw new ArgumentException($"Property '{defaultSortColumn}' does not exist on type {typeof(T).Name}");

        return query.OrderByProperty(property, sortDescending);
    }

    private static IQueryable<T> OrderByProperty<T>(this IQueryable<T> query, PropertyInfo property, bool sortDescending)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var orderByExpression = Expression.Lambda(Expression.Property(parameter, property), parameter);

        var resultExpression = Expression.Call(
            typeof(Queryable),
            sortDescending ? "OrderByDescending" : "OrderBy",
            [typeof(T), property.PropertyType],
            query.Expression,
            Expression.Quote(orderByExpression));

        return query.Provider.CreateQuery<T>(resultExpression);
    }

    /// <summary>
    /// The public property a column name refers to. Case is ignored, and so are underscores, because
    /// clients send the snake_case names the API serialises (<c>member_count</c> for <c>MemberCount</c>).
    /// </summary>
    private static PropertyInfo? FindProperty(Type type, string column)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        return properties.FirstOrDefault(p => string.Equals(p.Name, column, StringComparison.OrdinalIgnoreCase))
            ?? properties.FirstOrDefault(p => string.Equals(p.Name, column.Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase));
    }

    // What a database column can hold: sortable and comparable by EF. Navigations and owned types are not.
    private static bool IsScalar(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(Guid)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
            || underlying == typeof(TimeSpan);
    }

    private static InvalidPagingParameterException UnknownColumn(PagingParameter parameter, string column) =>
        new(parameter, InvalidPagingParameterReason.UnknownColumn, column);

    /// <summary>
    /// Counts <paramref name="query"/> and returns the page <paramref name="pagingInfo"/> asks for.
    /// The query is already sorted.
    /// </summary>
    private static async Task<PagedResult<T>> ToPagedListAsync<T>(
        this IQueryable<T> query,
        PagingInfo pagingInfo,
        CancellationToken cancellationToken)
    {
        var totalRecords = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pagingInfo.CurrentPage - 1) * pagingInfo.PageSize)
            .Take(pagingInfo.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalRecords, pagingInfo.CurrentPage, pagingInfo.PageSize);
    }
}
