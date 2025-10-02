# Jezda.Common.Abstractions

Interfaces, abstractions, and contracts for repository pattern, Unit of Work, and Specification Pattern.

## 📦 Installation

```bash
dotnet add package Jezda.Common.Abstractions
```

## 🎯 What's Included

### Repository Interfaces

- **`IGenericRepository<T>`** - Comprehensive repository interface with 50+ methods
  - CRUD operations
  - Pagination support
  - Projection and filtering
  - AsNoTracking queries
  - Soft delete operations
  - Batch operations (bulk update/delete)
  - Aggregate functions (Sum, Avg, Min, Max)
  - Helper methods (Exists, Count, Any, All)

- **`IUnitOfWork<TContext>`** - Unit of Work pattern interface
  - Transaction management
  - SaveChanges coordination
  - Multiple repository coordination

### Specification Pattern

- **`ISpecification<T>`** - Specification pattern interface
- **`BaseSpecification<T>`** - Base class for creating reusable query specifications

### Other Abstractions

- **Security interfaces** - Authentication and authorization abstractions
- **Configuration interfaces** - Configuration contracts
- **Response models** - Standard API response contracts

## 💡 Quick Example

```csharp
// Using IGenericRepository
public class ProductService
{
    private readonly IGenericRepository<Product> _repository;

    public ProductService(IGenericRepository<Product> repository)
    {
        _repository = repository;
    }

    public async Task<List<Product>> GetActiveProducts()
    {
        return await _repository.GetAsync(x => x.IsActive);
    }
}

// Using Specification Pattern
public class ActiveProductsSpec : BaseSpecification<Product>
{
    public ActiveProductsSpec() : base(x => x.IsActive)
    {
        AddInclude(x => x.Category);
        ApplyOrderBy(x => x.Name);
    }
}
```

## 📚 Documentation

For complete documentation, see the [main repository README](https://github.com/jezda-solutions/jezda-common-libs).

## 🔗 Related Packages

- **Jezda.Common.Domain** - Domain models and base entities
- **Jezda.Common.Data** - Concrete implementations of repositories

## 📄 License

MIT License - see [LICENSE](https://github.com/jezda-solutions/jezda-common-libs/blob/master/LICENSE) for details.
