# Upgrade Options — CRM

Assessment: 6 projects (net8.0/net9.0 → net10.0), 4-tier dependency graph, 2 incompatible packages, API breaking changes, GDI+/System.Drawing usage detected.

## Strategy

### Upgrade Strategy
All projects are on modern .NET (net8.0/net9.0) and 6 projects ≤ 15. A mechanical TFM bump with package updates; 4-tier depth is structural (Blazor WASM architecture) rather than a true complexity signal.

| Value | Description |
|-------|-------------|
| **All-at-Once** (selected) | Upgrade all 6 projects together in one pass. Fastest approach, no multi-targeting overhead. |
| Top-Down | Upgrade entry-point apps first, temporarily multi-target shared libraries. Safer for large/complex solutions. |

## Project Structure

### Package Management
6 projects with per-project PackageReference, all SDK-style, modern-to-modern upgrade — CPM adds consistency with no VersionOverride friction.

| Value | Description |
|-------|-------------|
| **Central Package Management (CPM)** (selected) | Creates `Directory.Packages.props`, centralizes all package versions. Better consistency across 6 projects. |
| Per-Project (defer CPM to post-migration) | Each project keeps its own versions. Recommended for Framework migrations; lower value here. |

## Compatibility

### Unsupported Packages
2 incompatible packages identified — small enough to research and resolve within the same task.

| Value | Description |
|-------|-------------|
| **Resolve Inline** (selected) | Research and resolve each incompatible package within the same upgrade task. No deferred work. |
| Defer Resolution | Generate minimal stubs to keep project building, create follow-up tasks for replacements. |
| Compatibility Mode | Keep .NET Framework reference with NU1701 suppression. Only for transitive deps not directly called. |

### Unsupported API Handling
Api.0001 (binary incompatible) and Api.0002 (source incompatible) flagged in CRM.Server and CRM.Client. Modern-to-modern upgrade — changes are expected to be minor.

| Value | Description |
|-------|-------------|
| **Fix Inline** (selected) | Resolve every API change in the same task, including complex ones. No stubs, no deferred work. |
| Defer Complex Changes | Apply simple replacements inline; generate stubs for complex changes and create resolution subtasks. |

### Windows Native APIs
GDI+/System.Drawing usage detected (2 issues). `System.Drawing.Common` package already referenced; adding Windows Compatibility Pack covers any remaining Windows-specific surface.

| Value | Description |
|-------|-------------|
| **Windows Compatibility Pack** (selected) | Adds `Microsoft.Windows.Compatibility`. Covers Windows APIs in .NET Core. App stays Windows-only until APIs are replaced. |
| No Compatibility Pack | Windows API build errors surface immediately; must be replaced with cross-platform alternatives. |

## Modernization

### Nullable Reference Types
6 projects (>5) and breaking API changes already present — enabling NRTs simultaneously would add a large volume of warnings during an already complex migration.

| Value | Description |
|-------|-------------|
| **Leave Disabled** (selected) | Does not enable nullable. Enable separately after migration as a distinct effort. |
| Enable Nullable Reference Types | Adds `<Nullable>enable</Nullable>` to project files. May require code updates to address warnings. |
