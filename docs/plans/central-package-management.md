# Central Package Management + Build Hygiene

**Status:** Pending
**Origin:** Code-review findings from PR #138 (.NET 10 upgrade) — valid, but out of scope there.

## Problem

1. **Package versions are copy-pasted across 18 csproj files.** The .NET 10 upgrade had to
   hand-edit ~60 version lines; Microsoft.Extensions.Logging.Abstractions appears in 8 files,
   DI.Abstractions in 6, EF Core in 3, the xunit/Test.Sdk block in 3. One missed file leaves
   mixed versions that restore silently unifies (or NU1605-downgrades) in consumers.
2. **TargetFramework is declared per-csproj (18 times)** instead of once.
3. **Security pins live in single csproj files** (Newtonsoft.Json in Extensions,
   SQLitePCLRaw in Data.Tests) — a sibling project adding the same transitive source
   would silently reintroduce the vulnerable version.
4. **publish-nuget.yml redundancy:** `GeneratePackageOnBuild=true` in every library csproj
   means the build step already packs everything at the stale csproj version, then the pack
   loop re-packs at the tag version; the pack loop also rebuilds per-project (no `--no-build`).
5. **xunit 2.9.2 is paired with xunit.runner.visualstudio 2.8.2** in all three test projects.

## Suggested fix

- Introduce `Directory.Build.props` (TargetFramework, Nullable, ImplicitUsings, shared package
  metadata) and `Directory.Packages.props` with `ManagePackageVersionsCentrally` +
  `CentralPackageTransitivePinningEnabled` (moves the security pins repo-wide).
  The `dotnet-nuget:convert-to-cpm` skill automates this conversion with build validation.
- Drop `GeneratePackageOnBuild` from csprojs; let the publish workflow's pack loop (with
  `--no-build`) be the only pack step.
- Align xunit runner version with xunit core in one central place.

## Also worth validating (runtime, not build)

- **Hangfire.PostgreSql 1.20.12 + Npgsql 10.0.3 pairing** — Hangfire.PostgreSql declares
  Npgsql >= 6.0.11 and now resolves to a 4-majors-newer assembly. The Hangfire storage path
  (`HangfireExtensions`) has no test coverage in this repo (tests are SQLite-based).
  Smoke-test job storage init + polling against PostgreSQL in a consumer before relying on
  scheduled jobs in production, or add a PostgreSQL-backed integration test (Testcontainers).
