using Jezda.Common.Data.Tests.TestInfrastructure;
using Xunit;

namespace Jezda.Common.Data.Tests;

public class UnitOfWorkTests : SqliteTestBase
{
    [Fact]
    public async Task HasChanges_ReflectsPendingModifications()
    {
        Assert.False(UnitOfWork.HasChanges());

        Context.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Pending", Price = 1m });
        Assert.True(UnitOfWork.HasChanges());

        await UnitOfWork.SaveChangesAsync();
        Assert.False(UnitOfWork.HasChanges());
    }

    [Fact]
    public async Task Transaction_Commit_PersistsChanges()
    {
        await using var transaction = await UnitOfWork.BeginTransactionAsync();

        Context.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Committed", Price = 1m });
        await UnitOfWork.SaveChangesAsync();
        await UnitOfWork.CommitTransactionAsync();
        Context.ChangeTracker.Clear();

        var all = await Repository.GetAsNoTrackingAsync();
        Assert.Single(all);
        Assert.Equal("Committed", all[0].Name);
    }

    [Fact]
    public async Task Transaction_Rollback_DiscardsChanges()
    {
        await using (var transaction = await UnitOfWork.BeginTransactionAsync())
        {
            Context.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Discarded", Price = 1m });
            await UnitOfWork.SaveChangesAsync();
            await UnitOfWork.RollbackTransactionAsync();
        }

        Context.ChangeTracker.Clear();
        var all = await Repository.GetAsNoTrackingAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task DetachAllEntities_ClearsTracking()
    {
        Context.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Tracked", Price = 1m });
        await UnitOfWork.SaveChangesAsync();

        Assert.NotEmpty(Repository.GetTrackedEntities());
        UnitOfWork.DetachAllEntities();
        Assert.Empty(Repository.GetTrackedEntities());
    }
}
