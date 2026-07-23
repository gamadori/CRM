# Task 02 – Setup CPM: Progress Details

## Outcome
✅ Central Package Management (CPM) migrated successfully. Build: **0 errors, 0 warnings** introduced by this task.

## Changes Made

### Created
- `Directory.Packages.props` – root CPM file with `ManagePackageVersionsCentrally=true` and `<PackageVersion>` entries for all packages used across the 6 solution projects.

### Modified – version attributes removed
| File | Notes |
|------|-------|
| `AGUtility\AGUtility.csproj` | Versions removed from 12 PackageReferences |
| `BlazoringComponents\BlazoringComponents.csproj` | Versions removed; `Radzen.Blazor` pinned with `VersionOverride="5.7.4"` (library targets older API) |
| `CRM.WebAPI\CRM.WebAPI.csproj` | Versions removed; `Swashbuckle.AspNetCore` pinned with `VersionOverride="6.6.2"` (incompatible with 7.x API) |
| `CRM\Client\CRM.Client.csproj` | Versions removed; `System.Net.Http` (redundant framework package) dropped; `Microsoft.AspNetCore.WebUtilities` kept after build error |
| `CRM\Server\CRM.Server.csproj` | Versions removed from all package groups (ASP.NET Core, EF Core, OpenIddict, PDF/export, utilities) |
| `CRM\Shared\CRM.Shared.csproj` | Versions removed from 8 PackageReferences |

## Issues Fixed
- **NU1605 downgrade**: `Microsoft.Extensions.Caching.Abstractions` bumped from 9.0.4 → 9.0.15 in CPM to align with `Caching.Memory 9.0.15` transitive requirement.
- **CS0234**: `Microsoft.AspNetCore.WebUtilities` was missing from `CRM.Client`; added back to CPM and project file.

## VersionOverride decisions
| Package | Project | Central version | Override | Reason |
|---------|---------|----------------|----------|--------|
| Radzen.Blazor | BlazoringComponents | 9.0.7 | 5.7.4 | BlazoringComponents uses Radzen 5.x API surface |
| Swashbuckle.AspNetCore | CRM.WebAPI | 7.1.0 | 6.6.2 | CRM.WebAPI targets 6.x Swagger API |
