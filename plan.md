# Dev Enhancements Plan

## 1. Hot-Reload Dev Script

**Problem**: Currently you must manually close the app, run `dotnet build`, then launch the exe again.

**Approach**: Create a PowerShell script `dev.ps1` that uses `dotnet watch` with a custom workflow:

- `dotnet watch` doesn't natively support WinUI/WinExe apps well (it's designed for ASP.NET), so we'll use a **file-watcher + rebuild + relaunch** script instead.
- The script will:
  1. Build the app for x64 Debug
  2. Launch the exe
  3. Watch all `.cs`, `.xaml`, and `.csproj` files for changes using `FileSystemWatcher`
  4. On change: kill the running Tactadile process, rebuild, relaunch
  5. Handle the single-instance mutex gracefully (the process kill handles this)
  6. Set an environment variable `TACTADILE_DEV=1` before launching so the app knows it's in dev mode

**File**: `dev.ps1` in repo root

**Usage**: `.\dev.ps1` (or `.\dev.ps1 -Platform arm64` for ARM)

---

## 2. Dev-Mode Pro Subscription Default

**Problem**: During development, all Pro features should be unlocked without needing a real license.

**Approach**: Use a compile-time constant set only during Debug builds. This is **bulletproof** because:

- The check uses `#if DEBUG` preprocessor directives — these are resolved at compile time
- Release builds physically **do not contain** the dev-mode code path — it's stripped by the compiler
- There is no environment variable, config file, or runtime flag that a user could manipulate
- Free-tier users only ever get Release builds, so this code literally doesn't exist in their binary

**Changes to `LicenseManager.cs`**:
- Add a `IsDevMode` property: `#if DEBUG` → `true`, `#else` → `false`
- Modify `Initialize()`: if `IsDevMode`, set `CurrentTier = LicenseTier.Pro` and return early
- Modify `IsActionAllowed()`: if `IsDevMode`, always return `true`

**Changes to `Tactadile.csproj`**:
- No changes needed — .NET already defines `DEBUG` for Debug configuration by default

This approach is superior to environment variables or runtime checks because:
- **Compile-time elimination**: The code doesn't exist in release binaries
- **No attack surface**: Users can't set env vars, edit config files, or modify runtime state to enable it
- **Zero overhead**: No runtime check in production builds
