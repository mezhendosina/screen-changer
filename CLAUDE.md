# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Debug build
dotnet build

# Run from debug output
dotnet run

# Publish — single .exe, framework-dependent (~5.5 MB, requires .NET 8 Runtime)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
# Output: bin\Release\net8.0-windows\win-x64\publish\ScreenChanger.exe
```

Before building, close any running instance of `ScreenChanger.exe` — the single-file publish overwrites the exe and will fail if it's locked.

## Architecture

WPF (.NET 8) tray application styled with **WPF-UI 3.0.5** (Fluent Design). No MVVM — thin code-behind pattern.

**Data flow on startup:**
1. `App.OnStartup` — acquires single-instance Mutex, applies dark theme, shows `MainWindow`
2. `MainWindow()` — calls `DisplayConfigService.GetConnectedMonitors()`, builds cards dynamically, sets up tray icon, calls `HighlightCard(DisplayConfigService.ActiveIndex)`
3. `DisplayConfigService.GetConnectedMonitors()` uses `QueryDisplayConfig(QDC_ALL_PATHS)` to enumerate all connected monitors and detect which one is currently active (single-monitor mode only; ActiveIndex = -1 for extended/clone)

**Switching a monitor:**
`DoSwitch(monitor)` → `DisplayConfigService.ActivateOnly(monitor)` → Win32 `SetDisplayConfig` (activates one target, deactivates all others) → `HighlightCard()` updates card border/glow + status text → `NotifyTray()` shows balloon tip

## Key files

| File | Responsibility |
|------|---------------|
| `Services/MonitorService.cs` | `DisplayConfigService`: Win32 `QueryDisplayConfig` / `SetDisplayConfig` for N-monitor switching; `DisplayConfigGetDeviceInfo` for friendly names |
| `Services/HotkeyService.cs` | Win32 `RegisterHotKey` + `HwndSource` WndProc hook for global hotkeys |
| `Services/MonitorNameService.cs` | WMI `WmiMonitorID` query (legacy, currently unused) |
| `MainWindow.xaml` / `.cs` | FluentWindow UI; dynamic card generation in `BuildCards()`; WinForms `NotifyIcon` tray |
| `App.xaml.cs` | Single-instance Mutex; `ApplicationThemeManager.Apply(Dark)` |
| `Models/MonitorMode.cs` | `record MonitorInfo(Index, Name, AdapterLow, AdapterHigh, TargetId)` |

## Hotkeys

Registered dynamically in `OnSourceInitialized` for `Ctrl+Alt+1` through `Ctrl+Alt+N` (max 9), one per connected monitor in the order returned by `GetConnectedMonitors()`.

## N-monitor switching

`DisplayConfigService.ActivateOnly(monitor)`:
1. `GetDisplayConfigBufferSizes(QDC_ALL_PATHS)` → allocate arrays
2. `QueryDisplayConfig(QDC_ALL_PATHS)` → get all paths (active + inactive)
3. Set `DISPLAYCONFIG_PATH_ACTIVE` on the target path; clear it on all others; reset `ModeInfoIdx = 0xFFFFFFFF` for the new active path
4. `SetDisplayConfig(SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE)` — Windows picks modes via `SDC_ALLOW_CHANGES`

## Naming pitfalls

Both `UseWPF` and `UseWindowsForms` are enabled, so many types are ambiguous. Use aliases:
```csharp
using WpfApplication = System.Windows.Application;
using MediaColor     = System.Windows.Media.Color;
using WpfBrush      = System.Windows.Media.Brush;
using WpfButton     = System.Windows.Controls.Button;
using WpfTextBlock  = System.Windows.Controls.TextBlock;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfRectangle  = System.Windows.Shapes.Rectangle;
using WpfCanvas     = System.Windows.Controls.Canvas;
```

`HorizontalAlignment` in object initializers must be qualified as `System.Windows.HorizontalAlignment.Center` — the compiler confuses the property name with the type.
