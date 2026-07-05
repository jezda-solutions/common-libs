using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Jezda.Common.Data.Tests.TestInfrastructure;

/// <summary>
/// Each test class gets its own in-memory SQLite database. SQLite is a relational
/// provider, so ExecuteUpdate/ExecuteDelete and transactions behave like production
/// (unlike the EF InMemory provider, which does not support them).
/// </summary>
public abstract class SqliteTestBase : IDisposable
{
    private readonly SqliteConnection _connection;

    protected TestDbContext Context { get; }

    protected ProductRepository Repository { get; }

    protected TestUnitOfWork UnitOfWork { get; }

    protected SqliteTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new TestDbContext(options);
        Context.Database.EnsureCreated();

        Repository = new ProductRepository(Context);
        UnitOfWork = new TestUnitOfWork(Context);
    }

    protected async Task<List<Product>> SeedProductsAsync(params (string Name, decimal Price)[] products)
    {
        var entities = products
            .Select(p => new Product { Id = Guid.NewGuid(), Name = p.Name, Price = p.Price })
            .ToList();

        Context.Products.AddRange(entities);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        return entities;
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
