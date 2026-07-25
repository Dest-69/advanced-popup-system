---
type: code
status: active
description: PopupSettings + SettingsManager (JSON in consumer Resources) and APLogger log-level filtering. Read when touching settings, the AP_Settings.json contract, or logging.
code_paths:
  - Assets/advanced-popup-system/Runtime/Settings/
  - Assets/advanced-popup-system/Runtime/Utils/APLogger.cs
---

# Settings & Logging

## PopupSettings

`PopupSettings` (`AdvancedPS.Core.System`) is the serialized config: `InspectorView` (`InspectorEnum` —
`APSInspector`/`APSOptimized`/`UnityInspector`, drives which custom inspector renders),
`AutoSwitchInputModule`, `LogType` (`string`: `Error`/`Warning`/`Info`/`None`). Edited via the APS **Settings** panel
([[Editor & Codegen]]).

**No keyboard settings left.** `EscapeCloseEnabled` + `EscapeCloseKey` were removed with the key tracking itself (user
call, 2026-07-25) — the escape stack is now stepped from consumer code, so there is nothing project-wide to configure
([[Core System]], [[Input Backends]]). They followed `KeyEventSystemEnabled` ("Key Event Tracking"), dropped earlier
with the show/hide key bindings. Removing a property is safe for existing `AP_Settings.json`: Newtonsoft ignores unknown
keys, so stale entries are simply dropped on the next save.

## SettingsManager

Static, loaded lazily on first access. `LoadSettings()` reads `Resources.Load<TextAsset>("AP_Settings")` (Newtonsoft
deserialize); if absent, it creates defaults (`APSInspector`, `LogType="Warning"`) and
**saves**. `SaveSettings()` writes JSON to **`Application.dataPath/Resources/AP_Settings.json`** — i.e. the **consumer
project's** `Assets/Resources`, not the package ([[Project Map]]) — then `AssetDatabase.Refresh()` in editor.

- **Gotcha:** the file lives in the game's `Resources` and ships in builds; it's created on demand. Newtonsoft.Json is
  required for (de)serialization ([[Invariants]]).

## APLogger

All runtime logging goes through `APLogger` (`Utils`) so `LogType` gates verbosity ([[Code Style]]):

- `Log` → only when `LogType == "Info"`; `LogWarning` → `Info` or `Warning`; `LogError` → unless `None`;
  `LogException` → always. So `"Error"` (or `"None"`) silences info/warning chatter; `"Info"` is the most verbose.

## Depends on

- [[Editor & Codegen]] (the Settings panel that writes these), [[Input Backends]] (consumer of `AutoSwitchInputModule`)
