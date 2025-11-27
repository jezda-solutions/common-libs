using System.Collections.Generic;

namespace Jezda.Common.Domain.Paged;

public sealed record PagedResult<T>(
    List<T> Items, 
    int TotalCount, 
    int CurrentPage, 
    int PageSize
);