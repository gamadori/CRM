# 02-setup-cpm: Set up Central Package Management

Introduce `Directory.Packages.props` at the solution root to centralize all NuGet package versions. Migrate all `PackageReference` version attributes from the 6 project files into the central file. Remove version attributes from individual project files, leaving only `<PackageReference Include="..." />` entries.

This is a prerequisite to the main upgrade: CPM must be in place before package versions are bumped, so all version updates happen in a single location. The solution currently has 65 packages across 6 projects with 27 recommended upgrades — centralization prevents version drift.

Also remove redundant framework-included packages (`Microsoft.AspNetCore.WebUtilities`, `System.Net.Http`) and handle the two deprecated packages (`Azure.Identity`, `Microsoft.AspNetCore.Http.Features`) by finding their replacements.

**Done when**: `Directory.Packages.props` exists at solution root; all project files reference packages without version attributes; solution builds successfully on current TFMs; no version conflicts.
