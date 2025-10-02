# Jezda.Common.Data

Production-ready implementations of Repository Pattern, Unit of Work, and Specification Pattern for Entity Framework Core.

## 📦 Installation

```bash
dotnet add package Jezda.Common.Data
```

## 🎯 What's Included

### GenericRepository<T>

A comprehensive repository implementation with **50+ methods**:

- ✅ **CRUD Operations** - Add, Update, Remove, GetById
- ✅ **Queries** - Get, GetFirstOrDefault, filtering, projections
- ✅ **Pagination** - PagedList support with sorting and searching
- ✅ **AsNoTracking** - Read-only queries for better performance
- ✅ **Soft Delete** - SoftDelete/Restore with IDeletedEntity
- ✅ **Batch Operations** - ExecuteDeleteAsync, ExecuteUpdateAsync (EF Core 7+)
- ✅ **Specification Pattern** - Reusable query specifications
- ✅ **Aggregates** - Sum, Average, Min, Max
- ✅ **Helpers** - Exists, Count, Any, All, Detach, Reload, Upsert

### UnitOfWork<TContext>

- Transaction management
- Coordinate SaveChanges across multiple repositories
- DbContext lifecycle management

## 💡 Quick Start

```csharp
// 1. Create repository
public class ProductRepository : GenericRepository<Product>
{
    public ProductRepository(AppDbContext context) : base(context) { }
}

// 2. Register in DI
builder.Services.AddScoped<IUnitOfWork<AppDbContext>, UnitOfWork<AppDbContext>>();
builder.Services.AddScoped<IGenericRepository<Product>, ProductRepository>();

// 3. Use in services
public class ProductService
{
    private readonly IUnitOfWork<AppDbContext> _unitOfWork;
    private readonly IGenericRepository<Product> _repository;

    public async Task<Product> CreateAsync(Product product)
    {
        _repository.Add(product);
        await _unitOfWork.SaveChangesAsync();
        return product;
    }

    public async Task<PagedList<Product>> GetPagedAsync(PagingInfo paging)
    {
        return await _repository.GetPagedItemsAsync(paging);
    }
}
```

## 🚀 Key Features

### Specification Pattern

```csharp
public class ActiveProductsSpec : BaseSpecification<Product>
{
    public ActiveProductsSpec(decimal minPrice)
        : base(x => x.IsActive && x.Price >= minPrice)
    {
        AddInclude(x => x.Category);
        ApplyOrderBy(x => x.Name);
        ApplyAsNoTracking();
    }
}

var products = await _repository.GetBySpecAsync(new ActiveProductsSpec(100m));
```

### Batch Operations (10-100x faster)

```csharp
// Bulk delete
await _repository.DeleteWhereAsync(x => x.IsActive == false);

// Bulk update
await _repository.UpdateWhereAsync(
    where: x => x.CategoryId == oldId,
    setters: s => s.SetProperty(p => p.CategoryId, newId)
);
```

### Soft Delete

```csharp
var product = await _repository.GetByIdAsync(1);
_repository.SoftDelete(product);
await _unitOfWork.SaveChangesAsync();
// Product.IsDeleted = true
```

## 📚 Documentation

For complete documentation with examples, see the [main repository README](https://github.com/jezda-solutions/jezda-common-libs).

## 🔗 Dependencies

- **Jezda.Common.Abstractions** - Repository and UoW interfaces
- **Jezda.Common.Extensions** - Extension methods
- **Entity Framework Core 9.0.8**

## 📄 License

MIT License - see [LICENSE](https://github.com/jezda-solutions/jezda-common-libs/blob/master/LICENSE) for details.
