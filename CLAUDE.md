# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

<!-- Class-by-class inventories are left out on purpose: the package directories answer them.
     Keep to what cannot be read from the code: layering, conventions, and the gotchas that
     have caused real bugs. -->

## Project Overview

Jezda Solutions Common Libraries — reusable .NET 10.0 libraries published as NuGet packages
for the microservices: the 5 core packages below, plus `Jezda.Common.Contracts`,
`Jezda.Common.Files` and the `Jezda.Common.Integrations.*` family, with test projects alongside.

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

Adding shared functionality means picking the layer by its dependencies:

- contracts and interfaces → Abstractions
- base entities and enums → Domain
- EF Core implementations → Data (`GenericRepository<T>` and `UnitOfWork<TContext>` are
  abstract base classes, inherited by concrete types in consumer projects)
- extension methods → Extensions
- utilities → Helpers

## Commands

Build and test the narrowest target, with capped parallelism:

```bash
dotnet build Jezda.Common.Data/Jezda.Common.Data.csproj -m:4
dotnet test Jezda.Common.Integrations.Tests -m:4 --filter "FullyQualifiedName~AzureDevOpsTaskProvider"
```

Test projects: `Jezda.Common.Data.Tests`, `Jezda.Common.Files.Tests`,
`Jezda.Common.Integrations.Tests`. CI (`pr-validation.yml`) builds and tests the whole solution
in Release on every PR.

## Publishing

`.github/workflows/publish-nuget.yml` publishes to NuGet.org, triggered by a version tag
(`v1.2.3`) or manual dispatch. The package version comes from the tag (or the dispatch
input), not from the `.csproj` files — those hold stale per-project values.

Authentication is
[NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
(OIDC): `NuGet/login@v1` exchanges the GitHub OIDC token for a short-lived API key — there is
no long-lived key secret. Requires the Trusted Publishing policy on nuget.org (repo
`jezda-solutions/common-libs`, workflow `publish-nuget.yml`) plus the `NUGET_USER` secret.

Re-runs and manual publishes go through the same workflow, never a local API key:

```bash
gh workflow run publish-nuget.yml -f version=1.2.3
```

## EF Core Tracking — read before writing an update

This is where the bugs come from. `GenericRepository<T>` behaves differently depending on how
the entity reached you.

- **Tracked** (loaded through the repository): modify properties and save. No `Update()` call.
  For child collections use `ReplaceChildCollection(existing, newItems)` (clear + add).
- **Disconnected** (mapped from a DTO or API request): EF knows nothing about it, so mark it
  with `UpdateDisconnected(entity)` before `SaveChangesAsync()`. Skip that and the save
  **silently does nothing**. `IsTracked(entity)` tells the two apart.
- **Projection decides tracking too:** `GetFirstOrDefaultAsync<TProjection>()` and
  `GetPagedProjection<TProjection>()` return a tracked entity when `projection` is null, and
  an untracked, read-only result when a projection is given. Use the null-projection form
  when you intend to update.

## Integrations

Mock-based provider tests only prove what the code sends, not what the external API accepts,
so a request the API rejects can still pass every test. Before releasing a provider change
that alters a request — query syntax, URL parameters, batch shape — verify it once against
the live API with a real token.

## Conventions

- .NET 10.0, nullable reference types enabled, everywhere
- Projects, packages and namespaces all named `Jezda.Common.[Purpose]`
- Package metadata lives in each `.csproj`; MIT licensed
- `PagingInfo` binds query parameters in snake_case (FastEndpoints and MVC)
- **Newtonsoft.Json is pinned in Extensions as a security pin** — `Hangfire.PostgreSql` pulls a
  vulnerable transitive version, so do not drop the explicit reference

## Git Workflow

Single long-lived branch: `master`; there is no `dev` branch. All work goes through
feature/fix branches with PRs targeting `master`. A release is cut by pushing a version tag
(`v1.2.3`) to `master`, which triggers the NuGet publish workflow.
