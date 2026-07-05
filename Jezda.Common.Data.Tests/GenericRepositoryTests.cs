using Jezda.Common.Data.Tests.TestInfrastructure;
using Jezda.Common.Domain.Paged;
using Xunit;

namespace Jezda.Common.Data.Tests;

public class GenericRepositoryTests : SqliteTestBase
{
    [Fact]
    public async Task AddAsync_And_GetByIdAsync_Roundtrip()
    {
        var product = new Product { Id = Guid.NewGuid(), Name = "Widget", Price = 9.99m };

        await Repository.AddAsync(product, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var loaded = await Repository.GetByIdAsync(product.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Widget", loaded.Name);
        Assert.Equal(9.99m, loaded.Price);
    }

    [Fact]
    public async Task GetFirstOrDefaultAsync_TrackedEntity_SavesModificationWithoutUpdateCall()
    {
        var seeded = await SeedProductsAsync(("Original", 10m));

        var tracked = await Repository.GetFirstOrDefaultAsync(x => x.Id == seeded[0].Id);
        Assert.NotNull(tracked);

        tracked.Name = "Renamed";
        await UnitOfWork.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var reloaded = await Repository.GetByIdAsync(seeded[0].Id);
        Assert.Equal("Renamed", reloaded!.Name);
    }

    [Fact]
    public async Task GetFirstOrDefaultAsync_WithProjection_ReturnsUntrackedResult()
    {
        var seeded = await SeedProductsAsync(("Projected", 5m));

        var name = await Repository.GetFirstOrDefaultAsync(
            projection: x => x.Name,
            where: x => x.Id == seeded[0].Id);

        Assert.Equal("Projected", name);
        Assert.Empty(Repository.GetTrackedEntities());
    }

    [Fact]
    public async Task UpdateWhereAsync_Ef10SettersApi_BulkUpdatesMatchingRows()
    {
        await SeedProductsAsync(("Cheap A", 5m), ("Cheap B", 8m), ("Expensive", 100m));

        var updated = await Repository.UpdateWhereAsync(
            where: x => x.Price < 10m,
            setters: s => s.SetProperty(x => x.IsActive, false)
                           .SetProperty(x => x.Name, x => x.Name + " (sale)"));

        Assert.Equal(2, updated);

        var all = await Repository.GetAsNoTrackingAsync();
        Assert.Equal(2, all.Count(x => !x.IsActive && x.Name.EndsWith("(sale)")));
        Assert.True(all.Single(x => x.Name == "Expensive").IsActive);
    }

    [Fact]
    public async Task DeleteWhereAsync_BulkDeletesMatchingRows()
    {
        await SeedProductsAsync(("Keep", 50m), ("Drop A", 1m), ("Drop B", 2m));

        var deleted = await Repository.DeleteWhereAsync(x => x.Price < 10m, CancellationToken.None);

        Assert.Equal(2, deleted);
        var remaining = await Repository.GetAsNoTrackingAsync();
        Assert.Single(remaining);
        Assert.Equal("Keep", remaining[0].Name);
    }

    [Fact]
    public async Task SoftDelete_SetsIsDeletedFlagAndPersists()
    {
        var seeded = await SeedProductsAsync(("Ghost", 1m));

        var tracked = await Repository.GetFirstOrDefaultAsync(x => x.Id == seeded[0].Id);
        Repository.SoftDelete(tracked!);
        await UnitOfWork.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var reloaded = await Repository.GetByIdAsync(seeded[0].Id);
        Assert.True(reloaded!.IsDeleted);
    }

    [Fact]
    public async Task ExistsAsync_And_GetManyByIdsAsync_Work()
    {
        var seeded = await SeedProductsAsync(("One", 1m), ("Two", 2m), ("Three", 3m));

        Assert.True(await Repository.ExistsAsync(seeded[0].Id));
        Assert.False(await Repository.ExistsAsync(Guid.NewGuid()));

        var two = await Repository.GetManyByIdsAsync([seeded[0].Id, seeded[2].Id]);
        Assert.Equal(2, two.Count);
    }

    [Fact]
    public async Task GetPagedItemsAsync_PagesAndSortsDescending()
    {
        await SeedProductsAsync(
            ("A", 1m), ("B", 2m), ("C", 3m), ("D", 4m), ("E", 5m));

        var paging = new PagingInfo
        {
            CurrentPage = 2,
            PageSize = 2,
            SortColumn = "Price",
            SortDescending = true,
        };

        var page = await Repository.GetPagedItemsAsync(paging, defaultSortColumn: "Name");

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.CurrentPage);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(["C", "B"], page.Items.Select(x => x.Name).ToArray());
    }

    [Fact]
    public async Task GetPagedProjection_WithGlobalSearch_FiltersProjectedRows()
    {
        await SeedProductsAsync(
            ("Red Apple", 1m), ("Green Apple", 2m), ("Banana", 3m));

        var paging = new PagingInfo { CurrentPage = 1, PageSize = 10, GlobalSearch = "apple" };

        var page = await Repository.GetPagedProjection(
            paging,
            projection: x => new { x.Name, x.Price },
            defaultSortColumn: "Name");

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, x => Assert.Contains("Apple", x.Name));
    }
}
