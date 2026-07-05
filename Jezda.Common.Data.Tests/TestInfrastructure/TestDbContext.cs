using Jezda.Common.Domain.Entities.Base;
using Microsoft.EntityFrameworkCore;

namespace Jezda.Common.Data.Tests.TestInfrastructure;

public class Product : AuditableBaseEntity<Guid>
{
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired();
        });
    }
}

public class ProductRepository(TestDbContext context) : GenericRepository<Product>(context);

public class TestUnitOfWork(TestDbContext context) : UnitOfWork<TestDbContext>(context);
