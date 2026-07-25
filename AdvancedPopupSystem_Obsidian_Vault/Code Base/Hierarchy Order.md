---
type: code
status: active
description: In-canvas draw order — the PopupOrderConfig type catalog, the sibling-index placement (ApplyOrder/BringToFront/SendToBack), the recency-stack coupling, and the Focusable raise-on-press feature. Read when touching who-is-drawn-above-whom.
code_paths:
  - Assets/advanced-popup-system/Runtime/Settings/PopupOrderConfig.cs
  - Assets/advanced-popup-system/Runtime/Core/APSystem/AdvancedPopupSystem.cs
  - Assets/advanced-popup-system/Runtime/Core/Modules/FocusConfig.cs
  - Assets/advanced-popup-system/Editor/MenuEditor/PopupOrderEditorPanel.cs
  - Assets/advanced-popup-system/Editor/MenuEditor/PopupOrderConfigStore.cs
---

# Hierarchy Order

The **second** ordering axis, under canvas routing: layers pick the canvas + its `sortingOrder` ([[Layers]]), this picks
the **sibling order inside one canvas**. Added 2026-07-25 to close a real gap — before it, in-canvas order was whatever
instantiation left behind (`InstantiateAsync`/`SetParent` append last), so it encoded **load order**, not intent: a popup
loaded early was stuck behind one loaded later even when opened after it, and the behavior even depended on
`PoolCapacity` (a `0` popup re-loads on each show → always on top; a resident one keeps its stale slot).

## The catalog (data, not codegen)

- **`PopupOrderConfig`** (`Runtime/Settings/`, ScriptableObject at the consumer's
  `Assets/Resources/APS_PopupOrderConfig.asset`) holds `List<Entry> Order` — `{TypeName, Layer}`, **front-most first**
  (index 0 = closest to the viewer). Same consumer-side + `Resources.Load`-cached + `ClearCache()`-on-play-mode-exit shape
  as [[Layers]]' `LayerCanvasConfig`; same **type-FullName keying** as the Addressable index, for the same reason (user
  popups live in the consumer assembly — [[Addressables]]).
- **`Entry.Layer` is editor grouping metadata only** (user request, 2026-07-25: popups land on their layer's canvas, so
  ordering is reviewed per layer). The stored sequence stays **one total order across all layers**, deliberately: a
  per-layer list would leave two layers routed to the same canvas (`RegisterLayerCanvas` with a mask, or the `Root`
  fallback) with incomparable ranks, while a total order sorts every canvas deterministically and lets a stale tag affect
  nothing but the panel's filter.
- **Rank = index in the list**; anything absent is `UnrankedRank` (`int.MaxValue`) → **behind** every listed popup.
  `GetRank` is backed by a lazily built `Dictionary`, invalidated by `InvalidateRanks()` (the editor store calls it on
  save) and `OnValidate` — the hot path is one dictionary hit per canvas child per show.
- **Per type, not per instance** — deliberate: it is a project-wide arrangement, editable in one screen; a per-prefab int
  would scatter it across prefabs and hide the global picture. Lane-B copies of one type therefore share a rank and order
  among themselves by show order.

## Placement (`AdvancedPopupSystem`, region `HIERARCHY ORDER`)

`Place(popup, front)` **sorts the whole canvas**: build one `OrderSortKey` per child (rank + current sibling index, with
the popup being placed forced to `int.MaxValue`/`MinValue` so it takes the front/back edge of its band), `List.Sort` by
rank descending then index ascending (stable → show order inside a band), and write the result back with ascending
`SetSiblingIndex` calls, skipping the ones already in place.

- **Why sort, not insert.** v1 counted the siblings that must precede the popup and used that count as the target index.
  Correct only when the existing children are already ordered — an in-editor probe on a canvas APS had **not** built
  (the `RegisterLayerCanvas`-a-scene-canvas case) left a decoration stranded between two popups. Sorting removes the
  precondition entirely, so no arrival order can produce a wrong arrangement.
- **`CompareTo`, never subtraction**, in the comparison: `UnrankedRank` is `int.MaxValue` and `b.Rank - a.Rank` overflows
  into the wrong sign.
- Alloc-light in the house style ([[Core System]] `SortPopups`): one reused `_orderScratch` list + a cached `Comparison`
  delegate; the scratch is cleared after every call so it never holds `Transform` references between shows.
- Verified in the live editor on a deliberately hostile canvas (front-ranked popup first, decoration last): one call
  sorts it, a second call on the other popup changes nothing, `BringToFront` stays inside its band, two instances of one
  type order by show order with `SendToBack` moving between them, and an unrouted canvas is left untouched.

- **`ApplyOrder(popup)`** — hierarchy only. Called from `AdvancedPopup.ShowAsync` (**both** overloads, right after
  `SetActive(true)`), from `SpawnAsync` (both pool-reuse re-parents + the fresh load), from `GetPopupAsync` and from
  `EnsureEntryLoadedAsync`. Applying it **on show** (not only at load) is what makes a re-show reclaim its slot.
- **`BringToFront` / `SendToBack`** — public API: front/back of the popup's **own band** (never past a higher-ranked
  popup) **plus** `Restack` in `ActivePopups`.
- **`Restack`** moves the entry to the end/start of `ActivePopups`. Membership stays owned by
  `Subscribe`/`Unsubscribe` ([[Popup Lifecycle]]) — this only reorders, so no new collection and no new leak guard. The
  point: `ActivePopups` is the recency stack that [[Core System]] `EscapeStep` and the pointer walk
  ([[Interaction Modules]]) read as "topmost", and after a raise it must agree with what is visually in front.
- **Scope gate `IsRoutedCanvas(parent)`** — ordering runs **only** when the parent is `Root`, a lazily materialized layer
  canvas, or a `RegisterLayerCanvas` mapping (i.e. exactly the canvases APS parents popups to — [[Core System]] "Canvas
  routing"). Scene-authored hierarchies are never rearranged (a shipped promise, `documentation.md` §9.5/§9.6). It reads
  the `_root` **field**, not the `Root` property, so an ordering check can't create the fallback canvas as a side effect.
- **Non-popup children** rank as `UnrankedRank`, so canvas-prefab decorations end up **behind** popups regardless of the
  authored sibling index (documented; overlay decor belongs on a higher-`sortingOrder` canvas).

## `Focusable` — raise on press

Second **non-gesture** feature after `Closable` ([[Interaction Modules]] explains why that category exists):
`PopupFeatureEnum.Focusable` + `FocusConfig.FocusZone` (null → the popup rect) + `PopupModules.Focus`.

- Driven by **`PopupInteractionSystem.TryRaiseFocus`**, run on the press edge **before** `TryBeginGesture` — so the
  gesture walk already sees the new recency order (press a window behind another → it raises *and* grabs). Deliberately
  **not** an `IPopupFeatureHandler`: the first handler whose `TryBegin` returns true *owns* the press, and focusing must
  leave the press to the button/drag the user actually aimed at.
- One popup per press: the walk `return`s after `BringToFront`, which mutates the very list it iterates.
- No flag-gated accessor on `PopupModules` (unlike `CloseButton`) — a null zone already *means* "whole rect", so nulling
  it on an unset flag would be ambiguous; the pass checks the flag itself.
- Refactor done alongside: the canvas+camera resolution duplicated in the grab and hover passes became
  `TryResolveCanvas`, now shared by all three.

## Editor (`APS ▸ Order`)

- **`PopupOrderEditorPanel`** — 2nd tab of `PopupSystemEditor` (Layers · **Order** · Displays · Settings) + `APS/Order`.
  A `UnityEditorInternal.ReorderableList` gives real drag-sort (the rest of the panel keeps the hand-rolled
  `APSEditorStyles` look). Removal is deferred through `_removeIndex` — mutating inside a draw callback breaks the list's
  bookkeeping (same class of bug as the Layers panel's deferred `deleteIndex`). Auto-Save shares the Layers panel's
  `APS_AutoSaveEnabled` PlayerPref.
  - **Layer filter, global storage.** The list draws `_view` (entries matching the filter) while `_slots` remembers the
    global positions they occupy; `ApplyViewOrder` writes the reordered view back into exactly those slots, so arranging
    one layer can never move another layer's popups. Filter options are rebuilt from `PopupLayerEnum` on every
    `RebuildView` — the Layers panel regenerates that enum, so they are never cached across reloads (same rule as the
    inspector's layer row), and a filter naming a deleted layer falls back to "All".
  - **Deep links:** `PopupSystemEditor.ShowOrder(layerName)` → `FocusLayer`, called by the Layers panel's per-row
    **Order** button (outside the Customization lock — it edits a consumer-side asset, like the canvas fields) and by the
    popup inspector's *Edit Order* (which passes the popup's own layer).
- **`PopupOrderConfigStore`** — `LoadOrCreate`/`Load`/`Reconcile`/`Save` mirroring `LayerCanvasConfigStore`
  ([[Editor & Codegen]]). Type discovery is **`TypeCache.GetTypesDerivedFrom<IAdvancedPopup>()`** (abstract + open
  generics skipped): an index Unity already maintains — no assembly walk, **no prefab load**, so the panel costs nothing
  to open (the editor-perf rule). `Reconcile` keeps the authored order, appends new types at the **back** (a new popup
  never jumps in front of an authored arrangement), and **keeps** entries whose type is missing — a mid-rename class or a
  failed compile must not silently drop a slot; the panel marks them and offers removal. `Save` uses
  **`SaveAssetIfDirty`**, not `SaveAssets`: the postprocessor and the inspector call it, and neither may flush the user's
  other unsaved assets. `Load` is the **cold-path** variant that never creates the asset.
- **Layer tags come from three sources:** the popup inspector (on a layer change — the only source for scene-authored
  popups), `PopupOrderPostprocessor` (per saved prefab), and a **full `t:Prefab` scan the panel runs itself** when the tab
  is opened (user request, 2026-07-25: no button to press). The scan is gated so it can't become routine cost: the
  postprocessor sets a `SessionState` flag on any prefab change (`MarkScanNeeded`, string checks only), and the panel scans
  only when `IsScanNeeded()` — default true, so it runs once per editor session and after real prefab churn, **not** on
  every recompile that reopens the window. It runs on the **Layout** event only (it can re-group rows, and changing the row
  set between Layout and Repaint corrupts IMGUI's layout state) and shows a cancelable progress bar instead of freezing.
- **Inspector** (`IAdvancedPopupEditor`): a read-only **Draw Order** row (`#n of N`, or "not listed") beside the layer row
  + an *Edit Order* button, resolved once in `OnEnable` via a direct `Resources.Load` (not `PopupOrderConfig.Loaded` — its
  cached null-resolution would hide a catalog created later in the same session, and the inspector must never create the
  asset). The `Focus` config block follows the house conditional-reveal pattern.

## Postprocessor budget

`PopupOrderPostprocessor` (main Editor assembly, **not** define-gated) keeps the layer tags fresh. Its cost profile is the
rule every APS postprocessor follows ([[Editor & Codegen]], the editor-perf rule):

1. **Import callback** — string `EndsWith(".prefab")` only, no loads; no pending paths → no `delayCall` at all.
2. **Cold path** — the deferred pass calls `PopupOrderConfigStore.Load()` **before** touching a prefab, so a project that
   never opened the Order panel pays nothing but those string checks.
3. **Bulk cap (`BulkPrefabCap = 64`)** — a batch above the cap is a VCS checkout / Library rebuild / "Reimport All" /
   imported package, not someone editing a popup. Inspecting it would turn "one component lookup per changed prefab" into
   loading **every prefab in the project**, so the pass is skipped and says so via `APLogger` (never a silent cap); tags
   are grouping metadata, so the cost is at worst a stale filter until the next save or *Rescan Layers*.
4. **Writes** — only when a tag actually changed, and only this asset (`SaveAssetIfDirty`).
5. **The full scan lives in the panel, not here** — the postprocessor only flips the `SessionState` flag the panel reads
   on open (see "Editor"), so the expensive pass happens when a human is looking at the tool, never during import.

The same cap was added to **`AddressablePopupIndexGenerator.SyncChanged`** ([[Addressables]]), which had the identical
exposure: above it the per-prefab pass is skipped and the index is re-baked from the (small) group membership instead, so
the index still matches what is grouped — a flag flipped *inside* such a batch waits for that prefab's next save or the
menu item's full rescan. Audited alongside and left as they were: `LayerEnumSyncPostprocessor` (one `EndsWith` per
imported path, acts only on the enum file) and `FolderRenamePrevention` (rename callback only).

## Gotchas

- **Equal rank = show order.** Two popups in the same slot (or both unlisted) stack by show order — that is the
  documented default, and it is why an unconfigured project behaves sanely with no catalog at all.
- **Two layers on one canvas** (a `RegisterLayerCanvas` mask, or several unmapped layers falling back to `Root`) share one
  sequence, so their relative order is whatever the catalog happens to say; give each layer its own canvas when the order
  between them matters.
- **Only popups parented *directly* to an APS canvas compete** — a popup nested inside another popup's hierarchy is
  ordered by its parent's slot, not its own rank.
- **Hidden popups keep their slot** (`HideAsync` only deactivates); the next show re-claims it. A `PoolCapacity == 0`
  popup is released instead and re-ordered on its next load — both paths now end in the same place, which is the
  inconsistency this feature removed.

## Depends on

- [[Core System]] (the registries, `Root`/`_layerCanvases` and the canvas routing it hooks into), [[Layers]] (the
  canvas + `sortingOrder` axis above this one), [[Popup Lifecycle]] (`ShowAsync` call site, `ActivePopups` ownership),
  [[Interaction Modules]] (the pointer system that hosts the focus pass), [[Editor & Codegen]] (panel/store patterns)
