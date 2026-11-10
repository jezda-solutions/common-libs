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
    public void ApplySorting_UnknownColumn_ThrowsInvalidPagingParameter()
    {
        var query = Context.Products.AsQueryable();

        var exception = Assert.Throws<InvalidPagingParameterException>(() => query.ApplySorting("Nonexistent", false));

        Assert.Equal(PagingParameter.SortColumn, exception.Parameter);
        Assert.Equal(InvalidPagingParameterReason.UnknownColumn, exception.Reason);
        Assert.Equal("Nonexistent", exception.Column);
    }

    [Fact]
    public async Task ApplyPagingAndFilteringAsync_UnknownSortColumn_ThrowsInvalidPagingParameter()
    {
        var paging = new PagingInfo { SortColumn = "Nme" };

        var exception = await Assert.ThrowsAsync<InvalidPagingParameterException>(() => Context.Products
            .Select(x => new ProductDto { Name = x.Name, Price = x.Price })
            .ApplyPagingAndFilteringAsync(paging, defaultSortColumn: "Name"));

        Assert.Equal(PagingParameter.SortColumn, exception.Parameter);
        Assert.Equal("Nme", exception.Column);
    }

    [Fact]
    public async Task ApplyPagingAndFilteringAsync_UnknownDefaultSortColumn_StaysAProgrammingError()
    {
        // The consumer's own default is not the caller's mistake, so it must not become a 400.
        var paging = new PagingInfo();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => Context.Products
            .Select(x => new ProductDto { Name = x.Name, Price = x.Price })
            .ApplyPagingAndFilteringAsync(paging, defaultSortColumn: "Id"));

        Assert.IsNotType<InvalidPagingParameterException>(exception);
    }

    [Fact]
    public async Task ApplyPagingAndFilteringAsync_WithoutSortColumn_SortsByDefaultColumn()
    {
        await SeedDefaultAsync();

        var result = await Context.Products
            .Select(x => new ProductDto { Name = x.Name, Price = x.Price })
            .ApplyPagingAndFilteringAsync(new PagingInfo(), defaultSortColumn: "Price");

        Assert.Equal(["Bravo", "Charlie", "Alpha", "Delta"], result.Items.Select(x => x.Name).ToArray());
    }

    [Theory]
    [InlineData("Nme")]
    [InlineData("Name.Length.Nope")]
    public void ApplySearchFilters_UnknownColumn_ThrowsInvalidPagingParameter(string column)
    {
        var query = Context.Products.AsQueryable();

        var exception = Assert.Throws<InvalidPagingParameterException>(
            () => query.ApplySearchFilters(new Dictionary<string, string> { [column] = "x" }));

        Assert.Equal(PagingParameter.SearchTerm, exception.Parameter);
        Assert.Equal(column, exception.Column);
    }


    private sealed class Owner
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class TeamRow
    {
        public string Name { get; init; } = string.Empty;

        public int MemberCount { get; init; }

        public Owner Owner { get; init; } = new();
    }

    private static IQueryable<TeamRow> Teams() => new[]
    {
        new TeamRow { Name = "Alpha", MemberCount = 12 },
        new TeamRow { Name = "Bravo", MemberCount = 3 },
        new TeamRow { Name = "Charlie", MemberCount = 7 },
    }.AsQueryable();

    [Theory]
    [InlineData("member_count")]
    [InlineData("MEMBERCOUNT")]
    public void ApplySorting_SnakeCaseOrAnyCase_FindsTheProperty(string column)
    {
        // Clients send the snake_case names the API serialises.
        var sorted = Teams().ApplySorting(column, sortDescending: true);

        Assert.Equal([12, 7, 3], sorted.Select(x => x.MemberCount).ToArray());
    }

    [Fact]
    public void ApplySearchFilters_SnakeCaseColumn_Filters()
    {
        var filtered = Teams().ApplySearchFilters(new Dictionary<string, string> { ["member_count"] = "7" });

        Assert.Equal(["Charlie"], filtered.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void NavigationProperty_IsNotAColumn()
    {
        // EF cannot sort or compare a navigation; it used to fail at translation instead.
        var sort = Assert.Throws<InvalidPagingParameterException>(() => Teams().ApplySorting("owner", false));
        var search = Assert.Throws<InvalidPagingParameterException>(
            () => Teams().ApplySearchFilters(new Dictionary<string, string> { ["owner"] = "x" }));

        Assert.Equal(InvalidPagingParameterReason.UnknownColumn, sort.Reason);
        Assert.Equal(InvalidPagingParameterReason.UnknownColumn, search.Reason);
    }

    // A response class with a member the projection does not assign, as when it is filled after the query.
    private sealed class ProductWithLateField
    {
        public string Name { get; init; } = string.Empty;

        public decimal Price { get; init; }

        public string? OwnerName { get; set; }
    }

    private IQueryable<ProductWithLateField> ProjectedWithLateField() =>
        Context.Products.Select(x => new ProductWithLateField { Name = x.Name, Price = x.Price });

    [Fact]
    public async Task GlobalSearch_SkipsMembersTheProjectionDoesNotAssign()
    {
        // EF cannot translate a filter on OwnerName: it is not in the SELECT. Searching it failed the query.
        await SeedDefaultAsync();

        var result = await ProjectedWithLateField()
            .ApplyPagingAndFilteringAsync(new PagingInfo { GlobalSearch = "alp" }, defaultSortColumn: "Name");

        Assert.Equal(["Alpha"], result.Items.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void SearchAndSortOnAMemberTheProjectionDoesNotAssign_AreUnknownColumns()
    {
        var search = Assert.Throws<InvalidPagingParameterException>(() => ProjectedWithLateField()
            .ApplySearchFilters(new Dictionary<string, string> { ["owner_name"] = "x" }));
        var sort = Assert.Throws<InvalidPagingParameterException>(() => ProjectedWithLateField()
            .Where(x => x.Price > 0)
            .ApplySorting("owner_name", false));

        Assert.Equal(InvalidPagingParameterReason.UnknownColumn, search.Reason);
        Assert.Equal(InvalidPagingParameterReason.UnknownColumn, sort.Reason);
    }
}
