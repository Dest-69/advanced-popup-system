---
type: code
status: active
description: PopupLayerEnum (generated flags), ActiveLayer bitmask semantics, autohide, and how a popup's multi-flag PopupLayer participates in layer show/hide. Read when working with layer grouping.
code_paths:
  - Assets/advanced-popup-system/Editor/MenuEditor/LayerCatalog.cs
  - Assets/advanced-popup-system/Editor/LayerEnumSyncPostprocessor.cs
  - Assets/advanced-popup-system/Editor/Bootstrap/LayerBootstrap.cs
---

# Layers

Layers group popups into logical screens (`GUI`, `GAME`, `MENU`, …) so you control many popups with one call
instead of per-popup.

- **`PopupLayerEnum`** is a **generated** `[Flags]` enum (`None = 0`, then `1 << 0`, `1 << 1`, …). It is **fully
  regenerated** by the APS **Layers** panel — **never hand-edit** the file ([[Invariants]], [[Editor & Codegen]]). Max
  31 flags.
- **The enum is a projection; the store is the source of truth.** The durable layer list lives **outside** the package
  in `ProjectSettings/APS_Layers.json` (the same "state in the consumer project" idea as `AP_Settings.json` —
  [[Settings & Logging]]), written by the panel via `LayerCatalog`. The enum `.cs` is a rebuildable view of that list and
  is **generated into the consumer project** (`Assets/AdvancedPopupSystem/Generated/Layers/`, assembly
  `AdvancedPS.Generated.Layers`, which the core runtime references) — **not shipped in the package** (so a read-only UPM
  install can regenerate it and an update never clobbers it). Non-destructive flow: a fresh install is seeded by the
  dependency-free `AdvancedPS.Bootstrap` from the store (breaking the compile chicken-and-egg — see
  [[Build & Packaging]]), then `LayerEnumSyncPostprocessor` (an `AssetPostprocessor` running **before** the consumer's
  scripts recompile) + `LayerCatalog.Reconcile` keep it in sync so consumer code referencing `PopupLayerEnum.SHOP`
  compiles. **Store safety:** `LayerCatalog` writes it atomically (`.tmp` + `File.Replace` → `.bak`) and distinguishes
  *missing* (seed defaults) from *unreadable* (abort — **never** overwrite real layers with defaults on a read hiccup),
  recovering from `.bak` when it can. Mechanism details in [[Editor & Codegen]].
- A popup's inspector **`PopupLayer` may hold several flags** — it shows for **any** of them (`HasFlag` matching in
  `GetPopupsByLayer`). So one popup can belong to multiple screens.
- **`AdvancedPopupSystem.ActiveLayer`** is the OR of currently active layers. It is mutated **only** by
  `LayerShow`/`LayerHide`/`HideAll` ([[Core System]]) — `autohide:true` sets it to the single layer (hiding the rest),
  `autohide:false` ORs it in, `LayerHide` clears the flag, `HideAll` resets to `0`.
- **Gotcha:** a manual `popup.Show()/Hide()` changes `ActivePopups` but **not** `ActiveLayer` — mixing manual and layer
  control desyncs "what layer is active" from "what's visible", and the layer calls' idempotency guards
  (`ActiveLayer == layer`) may then no-op unexpectedly.
- Layers also **gate hotkeys**: a `PopupKeyBinding.Layers` value restricts when a key fires ([[Input & Hotkeys]]).
- Layers also **select a canvas** for popups the system instantiates. Configured in the **Layers panel**
  ([[Editor & Codegen]]): each layer carries a **sorting order** + optional **canvas prefab**, persisted to the runtime
  `LayerCanvasConfig` asset (`Assets/Resources/APS_LayerCanvasConfig.asset`, consumer-side like `AP_Settings.json`). APS
  gives each layer its own canvas (the prefab, or an auto-created overlay) at that sort order, created **lazily** on the
  first load/spawn of one of its popups; a runtime `AdvancedPopupSystem.RegisterLayerCanvas(layer, canvas)` still
  overrides. Unmapped → `Root`; scene-authored popups unaffected; multi-flag popups resolve to the lowest-bit mapped
  layer. The panel's **sorting order is display + canvas order only — it never reorders the name/bit store**, so
  serialized `PopupLayer` masks stay valid. Mechanism in [[Core System]] ("Canvas routing"); parenting sites in
  [[Addressables]].

## Depends on

- [[Core System]] (`ActiveLayer` and the layer batch APIs), [[Editor & Codegen]] (how the enum is generated)
