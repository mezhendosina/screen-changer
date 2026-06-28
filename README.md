# ScreenChanger

A Windows system tray app for switching between monitors with global hotkeys.

## Features

- Switches the active display (single-monitor mode) via Win32 `SetDisplayConfig`
- Global hotkeys: `Ctrl+Alt+1` through `Ctrl+Alt+N` — one per connected monitor
- Fluent Design UI (WPF-UI 3.0.5, dark theme)
- Tray icon with balloon tip notifications
- Single-instance enforcement

## Requirements

- Windows 10/11 (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Desktop)

## Download

Grab the latest `ScreenChanger.exe` from [Releases](../../releases).

## Build from source

```powershell
# Debug
dotnet build

# Publish — single .exe, framework-dependent (~5.5 MB)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
# Output: bin\Release\net8.0-windows\win-x64\publish\ScreenChanger.exe
```

> Close any running instance of `ScreenChanger.exe` before publishing — the file will be locked.

## Usage

1. Run `ScreenChanger.exe` — it appears in the system tray.
2. Click the tray icon to open the monitor list.
3. Click a monitor card or press `Ctrl+Alt+<N>` to activate it.

## Tech stack

| | |
|---|---|
| Framework | .NET 8 WPF + WinForms (tray icon) |
| UI library | [WPF-UI](https://github.com/lepoco/wpfui) 3.0.5 |
| Display switching | Win32 `QueryDisplayConfig` / `SetDisplayConfig` |
| Hotkeys | Win32 `RegisterHotKey` via `HwndSource` |
