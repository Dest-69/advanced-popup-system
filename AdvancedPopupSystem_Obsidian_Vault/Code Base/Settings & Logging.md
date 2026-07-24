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
`AutoSwitchInputModule`, `LogType` (`string`: `Error`/`Warning`/`Info`/`None`), `EscapeCloseEnabled` (default
**false** — upgrade-safe for existing consumers) + `EscapeCloseKey` (`KeyCode`, default `Escape` via **property
initializer** so old JSON without the key keeps the default — don't move the default into `LoadSettings` only). Edited
via the APS **Settings** panel ([[Editor & Codegen]]).

`EscapeCloseEnabled` is the **single** switch for APS's keyboard handling: it now also gates whether
`KeyEventSystemAPS` installs its player-loop update at all ([[Input & Hotkeys]]). The separate `KeyEventSystemEnabled`
("Key Event Tracking") field was **removed** with the show/hide key bindings — the two toggles described one behavior
and one disabled the other. Removing a property is safe for existing `AP_Settings.json`: Newtonsoft ignores unknown
keys, so a stale `KeyEventSystemEnabled` entry is simply dropped on the next save.

## SettingsManager

Static, loaded lazily on first access. `LoadSettings()` reads `Resources.Load<TextAsset>("AP_Settings")` (Newtonsoft
deserialize); if absent, it creates defaults (`APSInspector`, `LogType="Warning"`, `EscapeCloseEnabled=true`) and
**saves**. `SaveSettings()` writes JSON to **`Application.dataPath/Resources/AP_Settings.json`** — i.e. the **consumer
project's** `Assets/Resources`, not the package ([[Project Map]]) — then `AssetDatabase.Refresh()` in editor.

- **Gotcha:** the file lives in the game's `Resources` and ships in builds; it's created on demand. Newtonsoft.Json is
  required for (de)serialization ([[Invariants]]).

## APLogger

All runtime logging goes through `APLogger` (`Utils`) so `LogType` gates verbosity ([[Code Style]]):

- `Log` → only when `LogType == "Info"`; `LogWarning` → `Info` or `Warning`; `LogError` → unless `None`;
  `LogException` → always. So `"Error"` (or `"None"`) silences info/warning chatter; `"Info"` is the most verbose.

## Depends on

- [[Editor & Codegen]] (the Settings panel that writes these), [[Input & Hotkeys]] (consumers of the input toggles)
