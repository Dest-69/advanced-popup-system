---
type: code
status: active
description: PopupLayerEnum (generated flags), ActiveLayer bitmask semantics, autohide, and how a popup's multi-flag PopupLayer participates in layer show/hide. Read when working with layer grouping.
code_paths:
  - Assets/advanced-popup-system/Runtime/Generated/Layers/PopupLayerEnum.generated.cs
  - Assets/advanced-popup-system/Editor/MenuEditor/LayerCatalog.cs
  - Assets/advanced-popup-system/Editor/MenuEditor/PopupLayerEditorPanel.cs
  - Assets/advanced-popup-system/Editor/LayerEnumSyncPostprocessor.cs
---

# Layers

Layers group popups into logical screens (`GUI`, `GAME`, `MENU`, …) so you control many popups with one call
instead of per-popup.

- **`PopupLayerEnum`** is a **generated** `[Flags]` enum (`None = 0`, then `1 << 0`, `1 << 1`, …). It is **fully
  regenerated** by the APS **Layers** panel — **never hand-edit** the file ([[Invariants]], [[Editor & Codegen]]). Max
  31 flags.
- **The enum is a projection; the store is the source of truth.** The durable layer list lives **outside** the package
  in `ProjectSettings/APS_Layers.json` (the same "state in the consumer project" idea as `AP_Settings.json` —
  [[Settings & Logging]]), written by the panel via `LayerCatalog`. The enum `.cs` is a rebuildable view of that list. It
  **ships inside the package** (`Runtime/Generated/Layers/`, assembly `AdvancedPS.Generated.Layers`, referenced by the
  core runtime) — it must, because it is a compile-time type and a fresh install can't run code to create it first (the
  two consumer-side approaches — bootstrap, define-swap — were proven impossible; see [[Build & Packaging]]). **Editing
  needs a writable package:** the panel gates add/rename/delete behind a **Customization** toggle, and on a read-only UPM
  install unlocking offers to **embed** the package first (`FileSearcher.IsPackageWritable`/`EmbedPackage`); canvas
  order/prefab stay editable regardless (consumer-side asset). On a writable install `LayerCatalog.Reconcile` +
  `LayerEnumSyncPostprocessor` regenerate the enum from the store and heal it after an update. **Store safety:**
  `LayerCatalog` writes the store atomically (`.tmp` + `File.Replace` → `.bak`) and distinguishes *missing* (seed
  defaults) from *unreadable* (abort — **never** overwrite real layers on a read hiccup), recovering from `.bak`. Details
  in [[Editor & Codegen]].
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
