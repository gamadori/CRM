# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0

## Strategy
**Selected**: All-at-Once
**Rationale**: 6 projects, all modern .NET (net8/net9), SDK-style. Straightforward TFM bump with package updates and API fixes.

### Execution Constraints
- Upgrade all 6 projects simultaneously — no tier ordering
- Set up CPM before bumping package versions (task 02 before 03)
- Fix all API issues inline — no stubs or deferred work
- Add Microsoft.Windows.Compatibility for GDI+/System.Drawing usages
- Build must be 0 errors and 0 warnings before completing any task

## Upgrade Options
- **Upgrade Strategy**: All-at-Once
- **Package Management**: Central Package Management (CPM)
- **Unsupported Packages**: Resolve Inline
- **Unsupported API Handling**: Fix Inline
- **Windows Native APIs**: Windows Compatibility Pack
- **Nullable Reference Types**: Leave Disabled

## Source Control
- **Source Branch**: feat/tema-chiaro-scuro-coerenza
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)
