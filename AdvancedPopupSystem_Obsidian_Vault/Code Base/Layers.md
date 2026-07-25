---
type: code
status: active
description: PopupLayerEnum (generated flags), ActiveLayer bitmask semantics, autohide, and the one-layer-per-popup contract (layers are canvas-bound). Read when working with layer grouping.
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
  install unlocking offers to **embed** the package first (`FileSearcher.IsPackageWritable`, fired via
  `PackageUpdater.BeginEmbed`, which also relocks a lock left over from an install that is no longer embedded, owns the
  panel's **Remove embedded copy** way back out, and the window's version badge + **Update** — [[Editor & Codegen]]); canvas
  order/prefab stay editable regardless (consumer-side asset). On a writable install `LayerCatalog.Reconcile` +
  `LayerEnumSyncPostprocessor` regenerate the enum from the store and heal it after an update. **Store safety:**
  `LayerCatalog` writes the store atomically (`.tmp` + `File.Replace` → `.bak`) and distinguishes *missing* (seed
  defaults) from *unreadable* (abort — **never** overwrite real layers on a read hiccup), recovering from `.bak`. Details
  in [[Editor & Codegen]].
- **One layer per popup.** `PopupLayer` holds exactly one flag (`None` = outside layer control) — layers are
  canvas-bound, so multi-membership would make the popup's canvas ambiguous. The inspector is a **single-select**
  (not a flags mask). Matching is **any-of bitwise** (`(popup.PopupLayer & query) != 0` in `GetPopupsByLayer`/
  `GetPopupByLayer`/index `ForLayer`), so a `Layer*` **query** may still be a mask spanning several layers. **Legacy
  multi-flag data** (pre-canvas-bound era) keeps working — any-of matching, canvas from the lowest bit — but warns at
  registration (`InitAdvancedPopup`) and at index bake; the inspector shows a warning with a one-click
  "keep lowest" fix.
- **`AdvancedPopupSystem.ActiveLayer`** is the OR of currently active layers. It is mutated **only** by
  `LayerShow`/`LayerHide`/`HideAll` ([[Core System]]) — `autohide:true` sets it to the single layer (hiding the rest),
  `autohide:false` ORs it in, `LayerHide` clears the flag, `HideAll` resets to `0`.
- **Gotcha:** a manual `popup.Show()/Hide()` changes `ActivePopups` but **not** `ActiveLayer` — mixing manual and layer
  control desyncs "what layer is active" from "what's visible", and the layer calls' idempotency guards
  (`ActiveLayer == layer`) may then no-op unexpectedly.
- **A layer is the unit of a "screen".** `LayerShow` opens its popups together and awaits them all, so a screen built from
  several independently animated popups needs no parent→children list (that is what `DeepPopups` was for — deleted in
  2.2.0, see [[Popup Lifecycle]]). Its per-layer **`EscapeClosesLayer`** flag makes one escape step close the whole screen
  ([[Core System]] "Escape stack step"); stored beside the canvas fields in `LayerCanvasConfig`, edited in the Layers panel.
- Layers also **select a canvas** for popups the system instantiates. Configured in the **Layers panel**
  ([[Editor & Codegen]]): each layer carries a **sorting order** + a **canvas prefab** (mandatory in the panel — seeded with the
  consumer-owned `APS_DefaultCanvas` you edit to control the default; a cleared prefab still falls back to an auto-created
  overlay), persisted to the runtime
  `LayerCanvasConfig` asset (`Assets/Resources/APS_LayerCanvasConfig.asset`, consumer-side like `AP_Settings.json`). APS
  gives each layer its own canvas (the prefab, or an auto-created overlay) at that sort order, named `<Layer> - APS Canvas`, created **lazily** on the
  first load/spawn of one of its popups; a runtime `AdvancedPopupSystem.RegisterLayerCanvas(layer, canvas)` still
  overrides. Unmapped → `Root`; scene-authored popups unaffected; legacy multi-flag popups resolve to the lowest-bit mapped
  layer. The panel's **sorting order is display + canvas order only — it never reorders the name/bit store**, so
  serialized `PopupLayer` values stay valid. Mechanism in [[Core System]] ("Canvas routing"); parenting sites in
  [[Addressables]]. The layer/canvas `sortingOrder` is the **coarse** axis only — who is in front *within* one canvas is a
  separate per-type catalog (see [[Hierarchy Order]]).

## Depends on

- [[Core System]] (`ActiveLayer` and the layer batch APIs), [[Editor & Codegen]] (how the enum is generated),
  [[Hierarchy Order]] (the in-canvas order below this axis)
