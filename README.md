# win-move

Windows system tray utility for managing windows with global hotkeys. All actions target the window under the mouse cursor, not the focused window.

## Default Keybindings

| Action | Shortcut |
|--------|----------|
| Move (drag) | `Win+Shift+Z` |
| Resize (drag) | `Win+Shift+X` |
| Minimize | `Win+Shift+Down` |
| Maximize | `Win+Shift+Up` |
| Restore | `Win+Shift+R` |
| Snap Left | `Win+Shift+Left` |
| Snap Right | `Win+Shift+Right` |
| Opacity + | `Win+Shift+Plus` |
| Opacity - | `Win+Shift+Minus` |

All keybindings are user-configurable via the settings window or by editing the config file directly.

## Behavior

**Sticky window targeting** — When a hotkey moves a window out from under the cursor (e.g. snap left), subsequent hotkeys continue targeting that window until the mouse physically moves.

**Seamless key switching** — Hold your modifiers and swap the primary key to fire a different action without releasing anything. Hold `Win+Shift+Z` to start moving, release `Z`, press `X` — switches to resize.

**Cycling snap** — Repeated snap presses in the same direction cycle through widths: 2/3 → 1/2 → 1/3. Changing direction or targeting a new window resets the cycle.

**Resize clamping** — Windows cannot be resized beyond screen bounds or the taskbar. No dead zones when reversing direction at an edge.

## Configuration

Config lives at `%APPDATA%\win-move\config.json`. Changes are picked up automatically via file watcher. Copy the file between machines for portable keybindings.

## Build & Run

```
dotnet build
dotnet run
```

Runs as a single-instance system tray app. Right-click the tray icon for settings, config reload, or exit. `Ctrl+C` in the terminal to stop.

Requires .NET 8+ on Windows.
