---
type: code
status: active
description: PopupLayerEnum (generated flags), ActiveLayer bitmask semantics, autohide, and how a popup's multi-flag PopupLayer participates in layer show/hide. Read when working with layer grouping.
code_paths:
  - Assets/advanced-popup-system/Runtime/Generated/PopupLayerEnum.generated.cs
---

# Layers

Layers group popups into logical screens (`LOGIN`, `HUB`, `SETTINGS`, …) so you control many popups with one call
instead of per-popup.

- **`PopupLayerEnum`** is a **generated** `[Flags]` enum (`None = 0`, then `1 << 0`, `1 << 1`, …). It is **fully
  regenerated** by the APS **Layers** panel — **never hand-edit** the file ([[Invariants]], [[Editor & Codegen]]). Max
  31 flags.
- A popup's inspector **`PopupLayer` may hold several flags** — it shows for **any** of them (`HasFlag` matching in
  `GetPopupsByLayer`). So one popup can belong to multiple screens.
- **`AdvancedPopupSystem.ActiveLayer`** is the OR of currently active layers. It is mutated **only** by
  `LayerShow`/`LayerHide`/`HideAll` ([[Core System]]) — `autohide:true` sets it to the single layer (hiding the rest),
  `autohide:false` ORs it in, `LayerHide` clears the flag, `HideAll` resets to `0`.
- **Gotcha:** a manual `popup.Show()/Hide()` changes `ActivePopups` but **not** `ActiveLayer` — mixing manual and layer
  control desyncs "what layer is active" from "what's visible", and the layer calls' idempotency guards
  (`ActiveLayer == layer`) may then no-op unexpectedly.
- Layers also **gate hotkeys**: a `PopupKeyBinding.Layers` value restricts when a key fires ([[Input & Hotkeys]]).

## Depends on

- [[Core System]] (`ActiveLayer` and the layer batch APIs), [[Editor & Codegen]] (how the enum is generated)
