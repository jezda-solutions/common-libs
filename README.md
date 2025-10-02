# Jezda Common Libraries

A comprehensive collection of reusable .NET libraries providing common functionality for enterprise applications, including repository patterns, data access abstractions, domain models, extensions, and helpers.

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-9.0.8-blue.svg)](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

## 📦 NuGet Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| **Jezda.Common.Domain** | Domain models and entities | [![NuGet](https://img.shields.io/nuget/v/Jezda.Common.Domain.svg)](https://www.nuget.org/packages/Jezda.Common.Domain/) |
| **Jezda.Common.Abstractions** | Interfaces and abstractions | [![NuGet](https://img.shields.io/nuget/v/Jezda.Common.Abstractions.svg)](https://www.nuget.org/packages/Jezda.Common.Abstractions/) |
| **Jezda.Common.Data** | Repository and Unit of Work implementations | [![NuGet](https://img.shields.io/nuget/v/Jezda.Common.Data.svg)](https://www.nuget.org/packages/Jezda.Common.Data/) |
| **Jezda.Common.Extensions** | Extension methods | [![NuGet](https://img.shields.io/nuget/v/Jezda.Common.Extensions.svg)](https://www.nuget.org/packages/Jezda.Common.Extensions/) |
| **Jezda.Common.Helpers** | Helper utilities | [![NuGet](https://img.shields.io/nuget/v/Jezda.Common.Helpers.svg)](https://www.nuget.org/packages/Jezda.Common.Helpers/) |

## 🚀 Quick Start

### Installation

```bash
# Install the Data package (includes Abstractions and Domain)
dotnet add package Jezda.Common.Data

# Optional: Install Extensions and Helpers
dotnet add package Jezda.Common.Extensions
dotnet add package Jezda.Common.Helpers
```

### Basic Setup

```csharp
// 1. Create your DbContext
public class AppDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
}

// 2. Create repositories
public class ProductRepository : GenericRepository<Product>
{
    public ProductRepository(AppDbContext context) : base(context) { }
}

// 3. Register in DI container (Program.cs)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IUnitOfWork<AppDbContext>, UnitOfWork<AppDbContext>>();
builder.Services.AddScoped<IGenericRepository<Product>, ProductRepository>();

// 4. Use in your services
public class ProductService
{
    private readonly IUnitOfWork<AppDbContext> _unitOfWork;
    private readonly IGenericRepository<Product> _productRepository;

    public ProductService(
        IUnitOfWork<AppDbContext> unitOfWork,
        IGenericRepository<Product> productRepository)
    {
        _unitOfWork = unitOfWork;
        _productRepository = productRepository;
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        _productRepository.Add(product);
        await _unitOfWork.SaveChangesAsync();
        return product;
    }
}
```

## 📚 Core Features

### 1. Generic Repository Pattern

The `IGenericRepository<T>` provides a comprehensive set of methods for data access:

#### Basic CRUD Operations

```csharp
// Create
var product = new Product { Name = "Laptop", Price = 999.99m };
_repository.Add(product);
await _unitOfWork.SaveChangesAsync();

// Read
var product = await _repository.GetByIdAsync(1);
var products = await _repository.GetAsync(x => x.IsActive);

// Update (Tracked Entity)
var product = await _repository.GetByIdAsync(1);
product.Name = "Gaming Laptop";
await _unitOfWork.SaveChangesAsync(); // EF tracks changes automatically

// Update (Disconnected Entity - from API/DTO)
var product = new Product { Id = 1, Name = "Updated Name" };
_repository.UpdateDisconnected(product);
await _unitOfWork.SaveChangesAsync();

// Delete
var product = await _repository.GetByIdAsync(1);
_repository.Remove(product);
await _unitOfWork.SaveChangesAsync();
```

#### Advanced Queries

```csharp
// Get with includes (eager loading)
var products = await _repository.GetAsync(
    where: x => x.IsActive,
    include: q => q.Include(x => x.Category)
                   .Include(x => x.Supplier)
);

// Projection (select specific fields)
var productDtos = await _repository.GetAsync<ProductDto>(
    projection: x => new ProductDto { Id = x.Id, Name = x.Name },
    where: x => x.IsActive
);

// First or default with includes
var product = await _repository.GetFirstOrDefaultAsync<Product>(
    where: x => x.Sku == "LAP-001",
    include: q => q.Include(x => x.Category)
);
```

#### Pagination

```csharp
// Using PagedList (with sorting and filtering)
var pagingInfo = new PagingInfo
{
    PageNumber = 1,
    PageSize = 20,
    SortColumn = "Name",
    SortDirection = "asc",
    SearchTerm = "laptop"
};

var pagedProducts = await _repository.GetPagedItemsAsync(
    pagingInfo: pagingInfo,
    where: x => x.IsActive,
    defaultSortColumn: "Id"
);

// pagedProducts.Items - current page items
// pagedProducts.TotalCount - total records
// pagedProducts.TotalPages - total pages
// pagedProducts.HasNextPage, HasPreviousPage - navigation info

// Simple pagination (skip/take)
var products = await _repository.GetPageAsync(
    pageNumber: 2,
    pageSize: 10,
    where: x => x.IsActive
);
```

### 2. Specification Pattern

Encapsulate complex query logic into reusable specification classes:

```csharp
// Define a specification
public class ActiveProductsWithCategorySpec : BaseSpecification<Product>
{
    public ActiveProductsWithCategorySpec(decimal minPrice)
        : base(x => x.IsActive && x.Price >= minPrice)
    {
        AddInclude(x => x.Category);
        AddInclude(x => x.Supplier);
        ApplyOrderBy(x => x.Name);
        ApplyAsNoTracking(); // Read-only query
    }
}

// Use the specification
var spec = new ActiveProductsWithCategorySpec(minPrice: 100m);
var products = await _repository.GetBySpecAsync(spec);

// Count using specification
var count = await _repository.CountBySpecAsync(spec);

// Check existence
var exists = await _repository.AnyBySpecAsync(spec);

// Get first
var firstProduct = await _repository.GetFirstBySpecAsync(spec);
```

**Benefits:**
- ✅ Reusable query logic
- ✅ Testable in isolation
- ✅ Composable and maintainable
- ✅ Type-safe
- ✅ Keeps repositories clean

### 3. AsNoTracking Queries (Read-Only)

Use for better performance when you don't need to update entities:

```csharp
// Get single entity (read-only)
var product = await _repository.GetFirstOrDefaultAsNoTrackingAsync(
    where: x => x.Id == 1,
    include: q => q.Include(x => x.Category)
);

// Get multiple entities (read-only)
var products = await _repository.GetAsNoTrackingAsync(
    where: x => x.IsActive,
    include: q => q.Include(x => x.Category)
);

// Get with projection (read-only)
var productDtos = await _repository.GetAsNoTrackingAsync(
    projection: x => new ProductDto { Id = x.Id, Name = x.Name },
    where: x => x.IsActive
);
```

**Performance Benefit:** AsNoTracking queries are ~30-50% faster and use less memory.

### 4. Soft Delete

Implement soft delete functionality with `IDeletedEntity` interface:

```csharp
// Your entity must implement IDeletedEntity
public class Product : IDeletedEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsDeleted { get; set; } // Must have a setter
}

// Soft delete
var product = await _repository.GetByIdAsync(1);
_repository.SoftDelete(product);
await _unitOfWork.SaveChangesAsync();
// Product.IsDeleted = true, still in database

// Soft delete multiple
_repository.SoftDeleteRange(products);
await _unitOfWork.SaveChangesAsync();

// Restore soft-deleted entity
var product = await _repository.GetByIdAsync(1);
_repository.Restore(product);
await _unitOfWork.SaveChangesAsync();
// Product.IsDeleted = false

// Restore multiple
_repository.RestoreRange(products);
await _unitOfWork.SaveChangesAsync();
```

### 5. Batch Operations (EF Core 7+)

Perform bulk updates/deletes in a single database operation:

```csharp
// Bulk delete (without loading entities into memory)
var deletedCount = await _repository.DeleteWhereAsync(
    where: x => x.IsActive == false && x.CreatedDate < DateTime.Now.AddYears(-1)
);
// Executes: DELETE FROM Products WHERE IsActive = 0 AND CreatedDate < '...'

// Bulk update (without loading entities into memory)
var updatedCount = await _repository.UpdateWhereAsync(
    where: x => x.CategoryId == oldCategoryId,
    setters: s => s.SetProperty(p => p.CategoryId, newCategoryId)
                   .SetProperty(p => p.UpdatedDate, DateTime.UtcNow)
);
// Executes: UPDATE Products SET CategoryId = X, UpdatedDate = '...' WHERE CategoryId = Y
```

**Performance:** 10-100x faster than loading entities and calling SaveChanges.

### 6. Helper Methods

```csharp
// Check existence
bool exists = await _repository.ExistsAsync(productId);

// Get multiple by IDs (bulk fetch)
var ids = new[] { 1, 2, 3, 4, 5 };
var products = await _repository.GetManyByIdsAsync(ids);

// Get first or throw exception
var product = await _repository.GetFirstOrThrowAsync(
    where: x => x.Sku == "LAP-001"
);
// Throws InvalidOperationException if not found

// Detach entity from change tracker
_repository.Detach(product);

// Detach all entities of type
_repository.DetachAll();

// Dynamic includes via string paths
var products = await _repository.GetWithIncludesAsync(
    where: x => x.IsActive,
    includePaths: new[] { "Category", "Category.Parent", "Supplier" }
);

// Count with long (for very large tables)
long count = await _repository.LongCountAsync(x => x.IsActive);

// Standard count
int count = await _repository.CountAsync(x => x.IsActive);

// Check if any exist
bool hasActive = await _repository.AnyAsync(x => x.IsActive);

// Check if all match condition
bool allActive = await _repository.AllAsync(x => x.IsActive);
```

### 7. Aggregate Functions

```csharp
// Maximum value
var maxPrice = await _repository.MaxAsync(
    selector: x => x.Price,
    where: x => x.IsActive
);

// Minimum value
var minPrice = await _repository.MinAsync(
    selector: x => x.Price,
    where: x => x.IsActive
);

// Sum
var totalRevenue = await _repository.SumAsync(
    selector: x => x.Price * x.Quantity,
    where: x => x.IsActive
);

// Average
var avgPrice = await _repository.AverageAsync(
    selector: x => x.Price,
    where: x => x.IsActive
);
```

### 8. Advanced Operations

```csharp
// Reload entity from database (discard local changes)
var product = await _repository.GetByIdAsync(1);
product.Name = "Changed";
await _repository.ReloadAsync(product);
// product.Name is reverted to database value

// Get all tracked entities (debugging)
var trackedProducts = _repository.GetTrackedEntities();

// Get tracking info (debugging)
var trackingInfo = _repository.GetTrackingInfo();
foreach (var (entity, state) in trackingInfo)
{
    Console.WriteLine($"Entity: {entity.Id}, State: {state}");
}

// Upsert (add or update)
var product = new Product { Sku = "LAP-001", Name = "Laptop" };
bool wasAdded = await _repository.UpsertAsync(
    entity: product,
    predicate: x => x.Sku == product.Sku
);
await _unitOfWork.SaveChangesAsync();
// Returns true if added, false if updated
```

### 9. Update Child Collections

For many-to-many relationships (junction tables):

```csharp
// Load product with categories
var product = await _repository.GetFirstOrDefaultAsync<Product>(
    where: x => x.Id == productId,
    include: q => q.Include(x => x.ProductCategories)
);

// Replace categories using Clear + Add pattern
var newCategories = new List<ProductCategory>
{
    new() { ProductId = productId, CategoryId = 1 },
    new() { ProductId = productId, CategoryId = 2 }
};

_repository.ReplaceChildCollection(
    currentCollection: product.ProductCategories,
    newItems: newCategories
);

await _unitOfWork.SaveChangesAsync();
```

## 🎯 Best Practices

### Tracked vs Disconnected Entities

**Tracked Entities** (from database queries):
```csharp
// ✅ CORRECT: Entity is tracked, just modify properties
var product = await _repository.GetByIdAsync(1);
product.Name = "New Name";
product.Price = 199.99m;
await _unitOfWork.SaveChangesAsync(); // EF detects changes automatically
```

**Disconnected Entities** (from API/DTOs):
```csharp
// ✅ CORRECT: Use UpdateDisconnected for entities from external sources
var product = new Product
{
    Id = 1,
    Name = "Updated Name",
    Price = 199.99m
};
_repository.UpdateDisconnected(product);
await _unitOfWork.SaveChangesAsync();
```

**Common Mistake:**
```csharp
// ❌ WRONG: Calling Update() on tracked entity causes duplicate tracking error
var product = await _repository.GetByIdAsync(1);
product.Name = "New Name";
_repository.Update(product); // ❌ Don't do this!
await _unitOfWork.SaveChangesAsync();
```

### Performance Tips

1. **Use AsNoTracking for read-only queries:**
   ```csharp
   // ✅ Faster for reporting/display
   var products = await _repository.GetAsNoTrackingAsync(x => x.IsActive);
   ```

2. **Use Projections instead of loading full entities:**
   ```csharp
   // ✅ Only load needed fields
   var dtos = await _repository.GetAsync<ProductDto>(
       projection: x => new ProductDto { Id = x.Id, Name = x.Name }
   );
   ```

3. **Use Batch Operations for bulk updates:**
   ```csharp
   // ✅ Single SQL statement
   await _repository.UpdateWhereAsync(
       where: x => x.IsActive == false,
       setters: s => s.SetProperty(p => p.IsDeleted, true)
   );
   ```

4. **Use Specification Pattern for complex queries:**
   ```csharp
   // ✅ Reusable, testable, maintainable
   var spec = new ActiveProductsSpec(minPrice: 100m);
   var products = await _repository.GetBySpecAsync(spec);
   ```

## 🔧 Development

### Build

```bash
dotnet build
```

### Pack NuGet Packages

```bash
dotnet pack --configuration Release
```

### Publish to NuGet

```bash
dotnet nuget push ./Jezda.Common.Data/bin/Release/Jezda.Common.Data.1.0.0.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY
```

## 📖 Additional Documentation

- See [CLAUDE.md](CLAUDE.md) for detailed developer notes
- See [.claude/analysis/generic-repository-analysis.md](.claude/analysis/generic-repository-analysis.md) for implementation details

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details

## 🤝 Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## 📧 Contact

For questions or support, please open an issue on GitHub.

---

Made with ❤️ by Jezda Solutions
