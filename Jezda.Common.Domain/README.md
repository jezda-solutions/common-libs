# Jezda.Common.Domain

Common domain models, base entities, and value objects for Jezda enterprise applications.

## 📦 Installation

```bash
dotnet add package Jezda.Common.Domain
```

## 🎯 What's Included

### Base Entities

- **`BaseEntity`** - Base class with Id, CreatedDate, UpdatedDate
- **`IDeletedEntity`** - Interface for soft delete support (IsDeleted property)

### Domain Models

- **`PagingInfo`** - Pagination request parameters (PageNumber, PageSize, SortColumn, SortDirection, SearchTerm)
- **`PagedList<T>`** - Paginated response wrapper with metadata (TotalCount, TotalPages, HasNextPage, etc.)

### Enums & Value Objects

- **`OrganisationType`** - Organization type enumeration
- Various domain-specific value objects

## 💡 Quick Example

```csharp
// Using BaseEntity
public class Product : BaseEntity
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// Using IDeletedEntity for soft delete
public class Category : BaseEntity, IDeletedEntity
{
    public string Name { get; set; }
    public bool IsDeleted { get; set; }
}

// Using PagedList
var pagedResult = new PagedList<Product>
{
    Items = products,
    TotalCount = 100,
    PageNumber = 1,
    PageSize = 20
};
```

## 📚 Documentation

For complete documentation, see the [main repository README](https://github.com/jezda-solutions/common-libs).

## 📄 License

MIT License - see [LICENSE](https://github.com/jezda-solutions/common-libs/blob/master/LICENSE.txt) for details.
