# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is **Jezda Solutions Common Libraries** - a collection of reusable .NET 9.0 libraries published as NuGet packages to support microservices architecture. The solution contains 5 packages organized as a monorepo.

## Project Structure

The solution follows a layered dependency architecture:

```
Jezda.Common.Domain (base layer - no dependencies)
    ↓
Jezda.Common.Abstractions (depends on Domain)
    ↓
Jezda.Common.Helpers + Jezda.Common.Extensions (depend on Abstractions + Domain)
    ↓
Jezda.Common.Data (depends on Abstractions + Extensions)
```

### Package Descriptions

- **Jezda.Common.Domain**: Base entities, enums, and domain models
  - `Entities/Base/AuditableBaseEntity<T>`: Base entity with audit fields (CreatedBy, CreatedOnUtc, ModifiedBy, ModifiedOnUtc, IsDeleted)
  - `Paged/PagingInfo`: Query parameter model for pagination (CurrentPage, PageSize, SortColumn, SortDescending, SearchTerm, GlobalSearch)
  - `Paged/PagedList<T>`: Paginated result container
  - `Enums/`: Shared enumerations (CurrencyCode, OrganisationType)

- **Jezda.Common.Abstractions**: Interfaces and contracts
  - `Repositories/IGenericRepository<T>`: Comprehensive repository interface with CRUD, paging, and projection support
  - `Repositories/IUnitOfWork`: Transaction and change tracking interface
  - `Responses/`: API response base types (BaseResponse, CodeResponse, IBaseResponse, ICodeResponse)
  - `Configuration/Options/`: Configuration option classes (HangfireOptions, JwtOptions, NationalityOptions, NexusOptions)
  - `Identity/IUserContext`: User context abstraction
  - `Security/`: Security-related interfaces

- **Jezda.Common.Data**: Entity Framework Core implementations
  - `GenericRepository<T>`: Full implementation of IGenericRepository with EF Core
  - `UnitOfWork<TContext>`: Transaction management and change tracking implementation
  - Both are abstract base classes meant to be inherited by concrete implementations in consumer projects

- **Jezda.Common.Extensions**: Extension methods
  - `PagedListExtensions`: Pagination and filtering extensions (ApplyPagingAndFilteringAsync, ApplyGlobalSearchFilter, ApplySorting)
  - `HangfireExtensions`: Hangfire job scheduling configuration
  - `DateTimeExtensions`: DateTime utility methods
  - `HttpResponseDataExtension`: HTTP response helpers

- **Jezda.Common.Helpers**: Utility classes
  - `DateTimeOffsetHelper`: Date/time conversion utilities
  - `EncryptionHelper`: Encryption utilities
  - `DisplayMasker`: Data masking utilities
  - `StringHelper`: String manipulation utilities
  - `CurrencyCodeHelper`: Currency code utilities
  - `PermissionHelper`: Permission utilities
  - `Identity/`: Identity-related helper classes

## Common Development Commands

### Build
```bash
dotnet build
dotnet build --configuration Release
```

### Restore Dependencies
```bash
dotnet restore
```

### Pack NuGet Packages
```bash
# Pack all projects
for proj in $(find . -name 'Jezda.Common*.csproj'); do
  dotnet pack "$proj" --configuration Release -p:PackageVersion=1.0.0 --output ./nupkgs
done

# Pack single project
dotnet pack Jezda.Common.Abstractions/Jezda.Common.Abstractions.csproj --configuration Release -p:PackageVersion=1.0.0
```

### Publish to NuGet
GitHub Actions workflow (`.github/workflows/publish-nuget.yml`) handles automated publishing:
- Triggered by version tags (e.g., `v1.0.0`) or manual workflow dispatch
- Builds all projects, packs them, and publishes to NuGet.org
- Uses `NUGET_API_KEY` secret

Manual publishing using the PowerShell script:
```powershell
./push-nuget-packages.ps1
```

## Architecture Patterns

### Repository Pattern
- All repositories inherit from `GenericRepository<T>` (in Jezda.Common.Data)
- Implement `IGenericRepository<T>` (in Jezda.Common.Abstractions)
- Key features:
  - Async-first design with CancellationToken support
  - Flexible querying with include/projection support
  - Built-in pagination via `GetPagedItemsAsync` and `GetPagedProjection`
  - Global and column-specific search capabilities

#### Working with Tracked vs Disconnected Entities

**CRITICAL**: Understand EF Core tracking behavior to avoid bugs:

**For TRACKED entities** (loaded from repository):
```csharp
// Load entity (tracked by default)
var product = await repository.GetFirstOrDefaultAsync<Product>(
    where: x => x.Id == id,
    include: q => q.Include(x => x.ProductCategoryRelations)
);

// Simply modify properties - EF tracks changes automatically
product.Name = "New Name";

// For child collections, use ReplaceChildCollection helper
var newRelations = categoryIds.Select(id => new ProductCategoryRelation
{
    ProductId = product.Id,
    CategoryId = id
});
repository.ReplaceChildCollection(product.ProductCategoryRelations, newRelations);

// Save - no need to call Update()!
await unitOfWork.SaveChangesAsync();
```

**For DISCONNECTED entities** (from DTOs/API):
```csharp
// Entity from DTO mapping
var product = mapper.Map<Product>(request);

// Must explicitly mark as modified
repository.UpdateDisconnected(product);

await unitOfWork.SaveChangesAsync();
```

**Helper Methods:**
- `UpdateDisconnected(T entity)` - Explicitly update disconnected entities
- `ReplaceChildCollection<TChild>(collection, newItems)` - Clear + Add pattern for child collections
- `IsTracked(T entity)` - Check if entity is tracked by context

### Unit of Work Pattern
- Inherit from `UnitOfWork<TContext>` where TContext is your DbContext
- Provides transaction support (BeginTransaction, Commit, Rollback)
- Change tracking utilities (HasChanges, DetachAllEntities)
- Implements IDisposable and IAsyncDisposable

### Pagination
- Use `PagingInfo` for query parameters (supports snake_case binding for FastEndpoints and MVC)
- `PagedList<T>` contains results + metadata (TotalCount, CurrentPage, TotalPages)
- Extension method `ApplyPagingAndFilteringAsync` handles:
  - Global search across all properties
  - Column-specific search via SearchTerm dictionary
  - Dynamic sorting by any column
  - Skip/take pagination

### Projection Support
- `GetFirstOrDefaultAsync<TProjection>()` supports both full entity retrieval and projection
- `GetPagedProjection<TProjection>()` for paginated projections
- **Tracking behavior:**
  - If `projection` is NULL → returns TRACKED entity (use for updates)
  - If `projection` is provided → returns UNTRACKED result (read-only)

## Key Conventions

### Target Framework
All projects target **.NET 9.0** with nullable reference types enabled.

### NuGet Package Configuration
- All projects have `GeneratePackageOnBuild` set to true
- Package metadata defined in .csproj files (Authors, Company, Description, PackageTags, etc.)
- MIT License
- Repository URL: https://github.com/jezda-solutions/jezda-common-libs

### Dependencies
- Entity Framework Core 9.0.8
- Hangfire 1.8.21 + Hangfire.PostgreSql 1.20.12 (in Extensions)
- FastEndpoints 7.0.1 (in Domain for query binding)
- Npgsql 9.0.3 (in Extensions)

### Naming
- Projects: `Jezda.Common.[Purpose]`
- NuGet packages: Same as project names
- Namespace: Matches project name

## Working with This Repository

### Adding New Shared Functionality
1. Determine the appropriate layer based on dependencies
2. If adding interfaces/contracts → Jezda.Common.Abstractions
3. If adding base entities/enums → Jezda.Common.Domain
4. If adding EF Core implementations → Jezda.Common.Data
5. If adding extension methods → Jezda.Common.Extensions
6. If adding utility classes → Jezda.Common.Helpers

### Version Management
- Version is specified in .csproj files (currently 1.0.0)
- For releases, update version in all .csproj files or override with -p:PackageVersion during pack
- GitHub Actions workflow extracts version from git tags (v1.0.0 format)

### Git Workflow
- Main branch: `master`
- Development branch: `dev`
- When creating PRs, target the `master` branch by default

## Notes

- This is a shared library repository - avoid adding application-specific logic
- All code should be generic and reusable across multiple microservices
- Consider backward compatibility when modifying existing public APIs
- The `nuget.config` file contains GitHub Packages feed configuration (note: contains credentials - should be gitignored in production)
