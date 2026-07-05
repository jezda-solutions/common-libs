using Jezda.Common.Data.Tests.TestInfrastructure;
using Jezda.Common.Domain.Paged;
using Jezda.Common.Extensions;
using Xunit;

namespace Jezda.Common.Data.Tests;

public class PagedListExtensionsTests : SqliteTestBase
{
    // Member-init projection (not a positional constructor) — EF can only translate
    // sorting/filtering through the projection when properties are member-initialized.
    private class ProductDto
    {
        public string Name { get; init; } = string.Empty;

        public decimal Price { get; init; }
    }

    private async Task SeedDefaultAsync() =>
        await SeedProductsAsync(
            ("Alpha", 30m), ("Bravo", 10m), ("Charlie", 20m), ("Delta", 40m));

    [Fact]
    public async Task ApplyPagingAndFilteringAsync_SortsByRequestedColumn()
    {
        await SeedDefaultAsync();

        var paging = new PagingInfo { CurrentPage = 1, PageSize = 10, SortColumn = "Price" };

        var result = await Context.Products
            .Select(x => new ProductDto { Name = x.Name, Price = x.Price })
            .ApplyPagingAndFilteringAsync(paging, defaultSortColumn: "Name");

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(["Bravo", "Charlie", "Alpha", "Delta"], result.Items.Select(x => x.Name).ToArray());
    }

    [Fact]
    public async Task ApplyPagingAndFilteringAsync_GlobalSearch_MatchesCaseInsensitive()
    {
        await SeedDefaultAsync();

        var paging = new PagingInfo { CurrentPage = 1, PageSize = 10, GlobalSearch = "ALPHA" };

        var result = await Context.Products
            .Select(x => new ProductDto { Name = x.Name, Price = x.Price })
            .ApplyPagingAndFilteringAsync(paging, defaultSortColumn: "Name");

        Assert.Single(result.Items);
        Assert.Equal("Alpha", result.Items[0].Name);
    }

    [Fact]
    public async Task ApplyPagingAndFilteringAsync_SecondPage_ReturnsRemainingItems()
    {
        await SeedDefaultAsync();

        var paging = new PagingInfo { CurrentPage = 2, PageSize = 3 };

        var result = await Context.Products
            .Select(x => new ProductDto { Name = x.Name, Price = x.Price })
            .ApplyPagingAndFilteringAsync(paging, defaultSortColumn: "Name");

        Assert.Equal(4, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Delta", result.Items[0].Name);
    }

    [Fact]
    public void ApplySorting_UnknownColumn_Throws()
    {
        var query = Context.Products.AsQueryable();

        Assert.Throws<ArgumentException>(() => query.ApplySorting("Nonexistent", false));
    }
}
