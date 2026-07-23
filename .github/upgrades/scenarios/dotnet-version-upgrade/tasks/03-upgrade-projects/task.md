# 03-upgrade-projects: Upgrade all 6 projects to .NET 10

Update all project files to `net10.0` and update all package versions to their .NET 10-compatible versions in `Directory.Packages.props`. This covers the full upgrade scope across all 6 projects simultaneously:

**Projects** (in dependency order for reference): CRM.Shared, CRM.WebAPI, AGUtility, BlazoringComponents, CRM.Client, CRM.Server

**Package updates**: 27 Microsoft packages need version bumps to their `10.0.x` equivalents (AspNetCore, EF Core, Extensions, Blazor WebAssembly, etc.). Update all in one pass.

**API breaking changes**: The assessment identified binary-incompatible (Api.0001) and source-incompatible (Api.0002) APIs in CRM.Server and CRM.Client, plus behavioral changes (Api.0003) in CRM.Server, CRM.Client, and CRM.Shared. Fix all API issues inline. For GDI+/System.Drawing usage, add `Microsoft.Windows.Compatibility` package to affected projects.

**Incompatible packages**: 2 packages have no compatible version for net10.0 — resolve inline by finding replacements or removing consuming code.

Build the solution and fix all compilation errors in a single bounded pass. Do not leave any build errors unresolved.

**Done when**: All 6 projects target `net10.0`; solution builds with 0 errors and 0 warnings in modified projects; all package updates applied; API issues resolved; `Microsoft.Windows.Compatibility` added where needed.
