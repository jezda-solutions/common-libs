# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Class-by-class inventories are left out — read the package directories for those. What follows
is the layering, the conventions, and the EF Core tracking rules that have caused real bugs.

## Project Overview

**Jezda Solutions Common Libraries** — reusable .NET 10.0 libraries published as NuGet packages
for the microservices. 15 packages in one monorepo (the 5 core packages below, plus
`Jezda.Common.Contracts`, `Jezda.Common.Files` and the `Jezda.Common.Integrations.*` family)
and 3 test projects.

**This is a shared library repo.** No application-specific logic, everything generic and
reusable, and public API changes have to consider backward compatibility.

## Layering

```
Jezda.Common.Domain (base layer — no dependencies)
    ↓
Jezda.Common.Abstractions (depends on Domain)
    ↓
Jezda.Common.Helpers + Jezda.Common.Extensions (depend on Abstractions + Domain)
    ↓
Jezda.Common.Data (depends on Abstractions + Extensions)
```

- **Domain** — base entities (`AuditableBaseEntity<T>` with CreatedBy/CreatedOnUtc/ModifiedBy/
  ModifiedOnUtc/IsDeleted), `PagingInfo` and `PagedList<T>`, shared enums
- **Abstractions** — `IGenericRepository<T>`, `IUnitOfWork`, response base types, options
  classes under `Configuration/Options/`, `IUserContext`, security interfaces
- **Data** — `GenericRepository<T>` and `UnitOfWork<TContext>`, EF Core implementations. Both
  are **abstract base classes**, meant to be inherited by concrete types in consumer projects
- **Extensions** — `PagedListExtensions`, `HangfireExtensions`, `DateTimeExtensions`,
  `HttpResponseDataExtension`
- **Helpers** — `DateTimeOffsetHelper`, `EncryptionHelper`, `DisplayMasker`, `StringHelper`,
  `CurrencyCodeHelper`, `PermissionHelper`, `Identity/`

Adding shared functionality means picking the layer by its dependencies: contracts and
interfaces to Abstractions, base entities and enums to Domain, EF Core implementations to Data,
extension methods to Extensions, utilities to Helpers.

## Commands

```bash
dotnet build [--configuration Release]
dotnet restore

# pack all
for proj in $(find . -name 'Jezda.Common*.csproj'); do
  dotnet pack "$proj" --configuration Release -p:PackageVersion=1.0.0 --output ./nupkgs
done

# pack one
dotnet pack Jezda.Common.Abstractions/Jezda.Common.Abstractions.csproj --configuration Release -p:PackageVersion=1.0.0
```

## Publishing

`.github/workflows/publish-nuget.yml` publishes to NuGet.org, triggered by a version tag
(`v1.0.0`) or manual dispatch. Authentication is
[NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
(OIDC): `NuGet/login@v1` exchanges the GitHub OIDC token for a short-lived API key — **there is
no long-lived key secret**. Requires the Trusted Publishing policy on nuget.org (repo
`jezda-solutions/common-libs`, workflow `publish-nuget.yml`) plus the `NUGET_USER` secret.

Re-runs and manual publishes go through the same workflow — **never a local API key**:

```bash
gh workflow run publish-nuget.yml -f version=1.2.3
```

## EF Core Tracking — read before writing an update

This is where the bugs come from. `GenericRepository<T>` behaves differently depending on how
the entity reached you.

**Tracked** (loaded through the repository): modify properties and save. No `Update()` call.
For child collections use `ReplaceChildCollection(existing, newItems)` (clear + add).

**Disconnected** (mapped from a DTO or API request): EF knows nothing about it, so it must be
marked explicitly with `UpdateDisconnected(entity)` before `SaveChangesAsync()`. Skip that and
the save silently does nothing.

Helpers: `UpdateDisconnected(T)`, `ReplaceChildCollection<TChild>(collection, newItems)`,
`IsTracked(T)`.

**Projection decides tracking too:** `GetFirstOrDefaultAsync<TProjection>()` and
`GetPagedProjection<TProjection>()` return a **tracked** entity when `projection` is null, and
an **untracked, read-only** result when a projection is given. Use the null-projection form
when you intend to update.

## Repository & Unit of Work

Repositories inherit `GenericRepository<T>` and implement `IGenericRepository<T>`: async-first
with `CancellationToken`, include/projection support, pagination via `GetPagedItemsAsync` and
`GetPagedProjection`, global and column-specific search.

`UnitOfWork<TContext>` carries transactions (BeginTransaction, Commit, Rollback), change
tracking (`HasChanges`, `DetachAllEntities`), and implements both `IDisposable` and
`IAsyncDisposable`.

## Pagination

`PagingInfo` is the query parameter model (**snake_case binding** for FastEndpoints and MVC);
`PagedList<T>` carries results plus TotalCount, CurrentPage and TotalPages.
`ApplyPagingAndFilteringAsync` handles global search across properties, column-specific search
through the SearchTerm dictionary, dynamic sorting by any column, and skip/take.

## Conventions

- **.NET 10.0**, nullable reference types enabled, everywhere
- Projects, packages and namespaces all named `Jezda.Common.[Purpose]`
- `GeneratePackageOnBuild` is on for every project; package metadata lives in the `.csproj`;
  MIT licensed; repository URL `https://github.com/jezda-solutions/jezda-common-libs`
- Versions live in the `.csproj` files (currently 1.0.0). For a release either bump them all or
  override with `-p:PackageVersion`; the workflow reads the version from the git tag
- **Newtonsoft.Json is pinned in Extensions as a security pin** — `Hangfire.PostgreSql` pulls a
  vulnerable transitive version, so do not drop the explicit reference. Other versions
  (EF Core, Hangfire, FastEndpoints, Npgsql) live in the `.csproj` files

## Git Workflow

Single long-lived branch: **`master`** (`dev` was retired in July 2026). All work goes through
feature/fix branches with PRs targeting `master`. A release is cut by pushing a version tag
(`v1.2.3`) to `master`, which triggers the NuGet publish workflow.
