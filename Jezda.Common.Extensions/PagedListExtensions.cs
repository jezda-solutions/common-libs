using Jezda.Common.Domain.Paged;
using System.Linq.Expressions;
using System.Reflection;

namespace Jezda.Common.Abstractions.Extensions;

public static class PagedListExtensions
{
    public static PagedList<T> ApplyPagingAndFiltering<T>(
        this IQueryable<T> query,
        PagingInfo pagingInfo,
        string defaultSortColumn = "Id",
        bool searchProjection = true)
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

        // Sortiranje na osnovu kolone
        query = query.ApplySorting(!string.IsNullOrEmpty(pagingInfo.SortColumn)
            ? pagingInfo.SortColumn
            : defaultSortColumn, pagingInfo.SortDescending);

        // Pretvaranje query-a u paging list
        return query.ToPagedList(pagingInfo, defaultSortColumn);
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

        if (!properties.Any())
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
                    // Direktno upoređivanje za DateOnly
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
                // Ako parsiranje ne uspe, vrati null (preskoči filtriranje za ovu kolonu)
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
    public static IQueryable<T> ApplySearchFilters<T>(this IQueryable<T> query, Dictionary<string, string> searchTerms)
    {
        // Ako je prazan search term nema sta da pretrazujemo
        if (searchTerms == null || searchTerms.Count == 0) return query;

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

                // Prolazimo kroz sva svojstva (uključujući ugnježđena)
                for (int i = 0; i < properties.Length; i++)
                {
                    var propertyName = properties[i];

                    // Trazimo polje na osnovu imena
                    var propertyInfo = propertyExpression.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ArgumentException($"Property '{propertyName}' not found on type '{propertyExpression.Type.Name}'.");

                    propertyExpression = Expression.PropertyOrField(propertyExpression, propertyInfo.Name);

                    // Ako je polje kolekcija ulazimo u nju i pravimo dalje upit npr x.Collection.Any(y => y.Property.Constains(value));
                    if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyInfo.PropertyType) &&
                        propertyInfo.PropertyType != typeof(string))
                    {
                        var collectionType = propertyInfo.PropertyType.GetGenericArguments().FirstOrDefault()
                            ?? throw new ArgumentException($"Unable to determine collection element type for property '{propertyName}'.");

                        var collectionParameter = Expression.Parameter(collectionType, "y");

                        // Pravimo izraz
                        Expression collectionExpression = collectionParameter;
                        for (int j = i + 1; j < properties.Length; j++)
                        {
                            var innerPropertyName = properties[j];
                            var innerPropertyInfo = collectionExpression.Type.GetProperty(innerPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                ?? throw new ArgumentException($"Property '{innerPropertyName}' not found on type '{collectionExpression.Type.Name}'.");

                            collectionExpression = Expression.PropertyOrField(collectionExpression, innerPropertyInfo.Name);
                        }

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
            // Pokušavamo da parsiramo vrednost kao GUID
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
                // Ako ne možemo da parsiramo kao GUID, vraćamo uvek false
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

    // Sortiranje
    public static IQueryable<T> ApplySorting<T>(this IQueryable<T> query, string sortColumn, bool sortDescending)
    {
        // Ako nema sortiranja, ne sortiramo
        if (string.IsNullOrWhiteSpace(sortColumn)) return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        var property
            = typeof(T).GetProperty(sortColumn, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
            ?? throw new ArgumentException($"Property '{sortColumn}' does not exist on type {typeof(T).Name}");
        var propertyAccess = Expression.Property(parameter, property);
        var orderByExpression = Expression.Lambda(propertyAccess, parameter);

        //Biramo da li je descending ili ascending
        var methodName = sortDescending ? "OrderByDescending" : "OrderBy";
        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(T), property.PropertyType],
            query.Expression,
            Expression.Quote(orderByExpression));

        return query.Provider.CreateQuery<T>(resultExpression);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="pagingInfo"></param>
    /// <param name="defaultSortColumn"></param>
    /// <returns></returns>
    public static PagedList<T> ToPagedList<T>(
        this IQueryable<T> query,
        PagingInfo pagingInfo,
        string defaultSortColumn)
    {
        // Ukupan broj itema
        var totalRecords = query.Count();

        // Primena paginacije i sortiranja
        var items = query
            .Skip((pagingInfo.CurrentPage - 1) * pagingInfo.PageSize)
            .Take(pagingInfo.PageSize)
            .ApplySorting(pagingInfo.SortColumn ?? defaultSortColumn, pagingInfo.SortDescending); // Podrazumevano sortiranje jer EF zahteva da posle skip i take postoji neko sortiranje

        // Kreiranje PagedList
        return new PagedList<T>
        {
            Items = items,
            PagingInfo = new PagingInfo
            {
                CurrentPage = pagingInfo.CurrentPage,
                PageSize = pagingInfo.PageSize,
                TotalCount = totalRecords,
                TotalPages = (totalRecords + pagingInfo.PageSize - 1) / pagingInfo.PageSize,
                SortColumn = pagingInfo.SortColumn ?? string.Empty,
                SortDescending = pagingInfo.SortDescending,
                SearchTerm = pagingInfo.SearchTerm
            }
        };
    }
}
