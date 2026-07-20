---
type: code
status: active
description: AdvancedPopupSystem — the static coordinator. Registries, type cache, scene/play-mode cleanup, hierarchy-depth sorting, and layer show/hide/hide-all orchestration. Read when touching registration, lookup, or layer batch logic.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/APSystem/AdvancedPopupSystem.cs
---

# Core System

`AdvancedPopupSystem` (`AdvancedPS.Core`) is a **static coordinator**, not a singleton — there is no `.Instance`
([[Invariants]]). It owns all global state and orchestrates layer-level batches; individual show/hide lives on the
popup ([[Popup Lifecycle]]).

## Registries (the only global state)

- **`AllPopups`** — every popup present in loaded scenes (visible or not). Source of truth for lookups and key events.
- **`ActivePopups`** — currently visible popups. **Mutated by the popup's `Subscribe`/`Unsubscribe`**, not here.
- **`PopupCacheByType`** (`Dictionary<Type, IAdvancedPopup>`) — O(1) `TryGetPopup<T>` for the non-`activeOnly` path. One
  representative per **concrete** type; removal re-points to a survivor rather than dropping the entry (see below).
- **`ActiveLayer`** (`PopupLayerEnum` bitmask) — combined active layers; **only** `Layer*`/`HideAll` here change it
  ([[Layers]]).

## Lifetime & cleanup

- `Initialize()` (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`) subscribes `SceneManager.sceneUnloaded` and, in
  editor, `playModeStateChanged`.
- **Leak guards ([[Invariants]]):** on `ExitingPlayMode` all registries + `ActiveLayer` are cleared; on scene unload,
  destroyed (`== null`) entries are pruned and `PopupCacheByType` is rebuilt from survivors. Any new static collection
  needs the same treatment.
- **Registration:** `InitAdvancedPopup(popup)` (called from `AdvancedPopup.Init`) adds to `AllPopups` + type cache and
  re-sorts; `DeactivateAdvancedPopup(popup)` (from `OnDestroy`) removes from `AllPopups`/`ActivePopups`, and from the
  type cache **only if this popup was the cached representative** — then re-points that type to a surviving instance if
  one remains. (Guards the case of several popups sharing a type: removing a non-cached one must not evict the entry,
  and removing the cached one must not blind-drop it.)

## Sort order (non-obvious)

`SortPopups()` orders `AllPopups`: **active-scene popups first**, then background-scene popups; within each group by
**hierarchy depth descending (deepest first)**. So nested/child popups are iterated before their parents — relevant to
key-event resolution ([[Input & Hotkeys]]) and any "first match" lookup.

## Lookups

- `TryGetPopup<T>(out popup, activeOnly=false)` — type-cache fast path when `!activeOnly`; otherwise linear over
  `ActivePopups`/`AllPopups`.
- `GetPopupByLayer(layer, activeOnly=true)` — first popup whose `PopupLayer` has the flag.
- `GetPopupByName(name, activeOnly=true)` — by `GameObject.name`, **case-sensitive**.

## Layer orchestration

All `Layer*`/`HideAll` methods return an `Operation` ([[Operations & Cancellation]]) and internally fan out to each
popup's `ShowAsync`/`HideAsync` via `Task.WhenAll`; exceptions are caught and logged through `APLogger`.

- **`LayerShow(layer, autohide=true)`** — `autohide=true`: no-op if `ActiveLayer == layer`; else set `ActiveLayer =
  layer`, **hide** popups excluding the layer, then **show** the layer's popups (sequential await — hide finishes
  before show). `autohide=false`: no-op if the flag is already set; else OR the flag and only show.
- **Typed overloads** `LayerShow<T>(…, settings, autohide)` and `LayerShow<T,J>(…, showSettings, hideSettings)` run the
  batch with a **per-call** display type/settings instead of each popup's cached display ([[Displays & Animations]]).
- **`LayerHide(layer)` / `LayerHide<T>`** — clear the flag (`&= ~layer`) and hide the layer's popups.
- **`HideAll()` / `HideAll<T>`** — no-op if `ActiveLayer == 0`; else reset to 0 and hide every popup.

**Gotchas:** the `ActiveLayer` equality/flag checks make layer calls idempotent — don't add your own guards on top.
Because manual `popup.Show()` doesn't touch `ActiveLayer`, mixing manual and layer control can desync what "active
layer" means vs. what's visible ([[Layers]]).

## Escape stack step

`EscapeStep()` — one step of escape-close. Walks `ActivePopups` **from the end**: the list is a recency stack for free
(`Subscribe` appends at show-start, `Unsubscribe` removes at hide-start — self-cleaning; no separate static stack, so
no new leak guards). Skips `null`/`!IsBeVisible` (also shields the known cancel-rollback gap) and `ShownByCascade`
popups (cascade groups are represented by their root — [[Popup Lifecycle]]). First relevant popup's `EscapePolicy`:
`Hide` → `popup.Hide()` + consumed; `Block` → consumed without closing (modal); `Ignore` → keep walking. Returns false
when nothing consumed → the key backends fall through to the normal binding scan ([[Input & Hotkeys]]). Public API —
also drivable from a UI "back" button, independent of the key/settings toggles. `LayerShow` batches get one step per
popup (no layer grouping in v1 — group via DeepPopups instead).

## Depends on

- [[Popup Lifecycle]] (the `ShowAsync`/`HideAsync` it drives), [[Operations & Cancellation]] (return type), [[Layers]]
  (`PopupLayerEnum`, `ActiveLayer` semantics)
