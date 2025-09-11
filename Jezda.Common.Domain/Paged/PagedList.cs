using System.Collections.Generic;

namespace Jezda.Common.Domain.Paged;

public class PagedList<T>
{
    public List<T> Items { get; set; } = [];

    public PagingInfo PagingInfo { get; set; } = new();
}
