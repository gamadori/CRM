# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade all 6 projects from net8.0/net9.0 to net10.0 (LTS)
**Scope**: 6 projects — 3 class libraries, 3 ASP.NET Core apps (Blazor WASM hosted), ~108 affected files

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 6 projects, all on modern .NET (net8.0/net9.0), SDK-style, clear 4-tier dependency structure. Straightforward TFM bump with package updates and minor API fixes.

---

## Tasks

### 01-prerequisites: Validate prerequisites and toolchain

Verify that the .NET 10 SDK is installed and compatible with the solution. Check whether a `global.json` file exists and, if so, update it to allow the .NET 10 SDK. Ensure the development environment is ready before any project files are modified.

**Done when**: .NET 10 SDK is confirmed installed; `global.json` (if present) is updated to allow net10.0; no blocking toolchain issues.

---

### 02-setup-cpm: Set up Central Package Management

Introduce `Directory.Packages.props` at the solution root to centralize all NuGet package versions. Migrate all `PackageReference` version attributes from the 6 project files into the central file. Remove version attributes from individual project files, leaving only `<PackageReference Include="..." />` entries.

This is a prerequisite to the main upgrade: CPM must be in place before package versions are bumped, so all version updates happen in a single location. The solution currently has 65 packages across 6 projects with 27 recommended upgrades — centralization prevents version drift.

Also remove redundant framework-included packages (`Microsoft.AspNetCore.WebUtilities`, `System.Net.Http`) and handle the two deprecated packages (`Azure.Identity`, `Microsoft.AspNetCore.Http.Features`) by finding their replacements.

**Done when**: `Directory.Packages.props` exists at solution root; all project files reference packages without version attributes; solution builds successfully on current TFMs; no version conflicts.

---

### 03-upgrade-projects: Upgrade all 6 projects to .NET 10

Update all project files to `net10.0` and update all package versions to their .NET 10-compatible versions in `Directory.Packages.props`. This covers the full upgrade scope across all 6 projects simultaneously:

**Projects** (in dependency order for reference): CRM.Shared, CRM.WebAPI, AGUtility, BlazoringComponents, CRM.Client, CRM.Server

**Package updates**: 27 Microsoft packages need version bumps to their `10.0.x` equivalents (AspNetCore, EF Core, Extensions, Blazor WebAssembly, etc.). Update all in one pass.

**API breaking changes**: The assessment identified binary-incompatible (Api.0001) and source-incompatible (Api.0002) APIs in CRM.Server and CRM.Client, plus behavioral changes (Api.0003) in CRM.Server, CRM.Client, and CRM.Shared. Fix all API issues inline. For GDI+/System.Drawing usage, add `Microsoft.Windows.Compatibility` package to affected projects.

**Incompatible packages**: 2 packages have no compatible version for net10.0 — resolve inline by finding replacements or removing consuming code.

Build the solution and fix all compilation errors in a single bounded pass. Do not leave any build errors unresolved.

**Done when**: All 6 projects target `net10.0`; solution builds with 0 errors and 0 warnings in modified projects; all package updates applied; API issues resolved; `Microsoft.Windows.Compatibility` added where needed.

---

### 04-final-validation: Final validation and cleanup

Run the full test suite to verify no regressions. Validate that the Blazor WebAssembly client and server both start correctly. Document any deferred items or post-upgrade recommendations (e.g., future cross-platform refactoring of GDI+ usages, eventual Nullable Reference Types adoption).

**Done when**: All tests pass; solution builds clean; upgrade branch is committed and ready for review.

---
