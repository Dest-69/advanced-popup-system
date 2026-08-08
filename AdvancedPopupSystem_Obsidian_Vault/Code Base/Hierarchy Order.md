---
type: code
status: active
description: In-canvas draw order — the PopupOrderConfig prefab catalog and its _orderKey stamp, the sibling-index placement (ApplyOrder/BringToFront/SendToBack), the recency-stack coupling, and the Focusable raise-on-press feature. Read when touching who-is-drawn-above-whom.
code_paths:
  - Assets/advanced-popup-system/Runtime/Settings/PopupOrderConfig.cs
  - Assets/advanced-popup-system/Runtime/Core/Abstract/IAdvancedPopup.cs
  - Assets/advanced-popup-system/Runtime/Core/APSystem/AdvancedPopupSystem.cs
  - Assets/advanced-popup-system/Runtime/Core/Modules/FocusConfig.cs
  - Assets/advanced-popup-system/Editor/MenuEditor/PopupOrderEditorPanel.cs
  - Assets/advanced-popup-system/Editor/MenuEditor/PopupOrderConfigStore.cs
  - Assets/advanced-popup-system/Editor/PopupOrderPostprocessor.cs
---

# Hierarchy Order

The **second** ordering axis, under canvas routing: layers pick the canvas + its `sortingOrder` ([[Layers]]), this picks
the **sibling order inside one canvas**. Added 2026-07-25 to close a real gap — before it, in-canvas order was whatever
instantiation left behind (`InstantiateAsync`/`SetParent` append last), so it encoded **load order**, not intent: a popup
loaded early was stuck behind one loaded later even when opened after it, and the behavior even depended on
`PoolCapacity` (a `0` popup re-loads on each show → always on top; a resident one keeps its stale slot).

## The catalog (data, not codegen)

- **`PopupOrderConfig`** (`Runtime/Settings/`, ScriptableObject at the consumer's
  `Assets/Resources/APS_PopupOrderConfig.asset`) holds `List<Entry> Order` — `{PrefabGuid, TypeName, Layer, PrefabName}`,
  **front-most first** (index 0 = closest to the viewer). Same consumer-side + `Resources.Load`-cached +
  `ClearCache()`-on-play-mode-exit shape as [[Layers]]' `LayerCanvasConfig`.
- **Entries are prefabs, keyed by prefab GUID** (2026-08-08 rework; the original catalog keyed popup **types**). Two
  reasons the type key had to go: a class with no prefab still occupied a row — with no prefab there was nothing to read a
  layer from, so it sat in the panel's *Unassigned* group forever — and two prefabs of one class were one slot, so their
  relative order was unauthorable.
- **The prefab key must live in the prefab** — `IAdvancedPopup._orderKey` (`[SerializeField, HideInInspector]`, public
  getter `OrderKey`), stamped with the prefab's own GUID by the editor store. There is no alternative: a build has no
  `AssetDatabase`, so an instance can only know which asset it came from if the asset carries it (`PreloadSceneGuids`
  stores GUIDs for the same reason). It is initialized to `string.Empty`, not left null — an unassigned serialized field
  is a `CS0649` in every consumer's console.
  - This **reverses** the note's earlier "per type, not per instance — a per-prefab int would scatter the arrangement
    across prefabs". The concern was about the *arrangement*, and it still holds: what lives in the prefab is **identity
    only**, one write ever; the sequence stays in the single catalog asset, so a reorder still touches exactly one file.
  - A **prefab variant** inherits its base's stamp, which is exactly the "key ≠ my own GUID" mismatch the store
    overwrites — that check is the whole dedup, no bookkeeping needed. Immutable assets (a popup inside a package) are
    skipped and fall back to their type.
- **The class fallback is in the runtime, not in a row.** `GetRank(orderKey, typeName)` probes the key map, then the type
  map, so a keyless popup (scene-authored, unstamped) lands on the **front-most prefab entry of its class** — no row of
  its own needed. Type rows (`Entry.IsTypeOnly`, empty `PrefabGuid`) therefore exist only as **pre-rework data in
  transit**: the upgrade scan expands them into prefabs, and drops the ones whose class has no prefab (2026-08-08, user
  request — a class with no prefab was the phantom row this rework set out to remove; a scene-only popup keeps ranking by
  the class fallback, or sits at the back when its class has no prefab at all).
  `PopupOrderConfig.SchemaVersion` (0 = per-type, 1 = per-prefab) is what makes the upgrade one-shot; "does a type row
  exist" would re-trigger forever.
- **`Entry.Layer`/`PrefabName` are editor metadata only** (layer grouping: user request, 2026-07-25 — popups land on
  their layer's canvas, so ordering is reviewed per layer). Both are read off the prefab at discovery, which is what
  finally makes the tag reliable — the old catalog had no source for a type with no prefab. The stored sequence stays
  **one total order across all layers**, deliberately: a per-layer list would leave two layers routed to the same canvas
  (`RegisterLayerCanvas` with a mask, or the `Root` fallback) with incomparable ranks, while a total order sorts every
  canvas deterministically and lets a stale tag affect nothing but the panel's filter.
- **Rank = index in the list**; anything absent is `UnrankedRank` (`int.MaxValue`) → **behind** every listed popup. Both
  maps are built by one lazy `EnsureRanks()` pass (first occurrence wins in each), invalidated by `InvalidateRanks()` (the
  editor store calls it on save) and `OnValidate`. The hot path stays **one** dictionary hit per canvas child per show: a
  stamped popup resolves on its key, a keyless one skips that probe entirely.
- **Per prefab, not per instance.** Copies of one prefab — every Lane-B `SpawnAsync` copy — share a rank and order among
  themselves by show order.

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
  - **Rows are prefabs**, drawn from the entry's cached `PrefabName`/`TypeName`/`Layer` — **no asset loads to draw the
    list**. `onSelectCallback` → `PingRow` is the single place an asset is loaded: `Selection.activeObject` +
    `PingObject` reveal the prefab in the Project window (user request, 2026-08-08).
  - **No cleanup UI, on purpose** (user request, 2026-08-08: "this should be automatic in the background"). The panel
    is a header, a filter, the list and a footer — no upgrade notice, no stale notice, no per-row delete. Everything
    those buttons used to do happens in the scan and the postprocessor. The one manual affordance left is **Rescan
    Prefabs** in the footer.
  - **`_missing` is resolved once per `RebuildView`, into a set keyed by `Entry` instance** — not an index-parallel
    array, which a drag would desync; the check loads nothing but would still run per row per repaint. It only dims a
    row that is about to be cleaned up anyway.
  - **Layer filter, global storage.** The list draws `_view` (entries matching the filter) while `_slots` remembers the
    global positions they occupy; `ApplyViewOrder` writes the reordered view back into exactly those slots, so arranging
    one layer can never move another layer's popups. Filter options are rebuilt from `PopupLayerEnum` on every
    `RebuildView` — the Layers panel regenerates that enum, so they are never cached across reloads (same rule as the
    inspector's layer row), and a filter naming a deleted layer falls back to "All".
  - **Deep links:** `PopupSystemEditor.ShowOrder(layerName)` → `FocusLayer`, called by the Layers panel's per-row
    **Order** button (outside the Customization lock — it edits a consumer-side asset, like the canvas fields) and by the
    popup inspector's *Edit Order* (which passes the popup's own layer).
- **`PopupOrderConfigStore`** — `LoadOrCreate`/`Load`/`SyncPrefab`/`ScanPrefabs`/`PruneMissing`/`Save` mirroring
  `LayerCanvasConfigStore` ([[Editor & Codegen]]). `Save` uses **`SaveAssetIfDirty`**, not `SaveAssets`: the postprocessor
  and the inspector call it, and neither may flush the user's other unsaved assets. `Load` is the **cold-path** variant
  that never creates the asset. `TypeCache` is gone from this file entirely — the catalog is prefabs, and nothing needs a
  list of classes any more.
- **Discovery is prefab-driven, in two grains.** `SyncPrefab` (one asset load, from the postprocessor) and `ScanPrefabs`
  (the full `t:Prefab` pass the panel offers). Both stamp `_orderKey` and refresh the row; the incremental one also
  **takes over a type row of the same class**, so a legacy slot upgrades on a plain prefab save, with no full scan.
- **`ScanPrefabs` = discovery *and* migration, in `Rebuild`.** Walk the stored order: a keyed row keeps its position
  (metadata refreshed), a **type row expands in place** into that class's prefabs (sorted by asset path); everything the
  scan did not find is **dropped**, and undiscovered prefabs are appended at the **back**. That is why no authored
  position moves across the upgrade. A **cancelled** scan returns without rebuilding — with dropping as the rule, half a
  project read would look exactly like "these prefabs are gone".
- **Cleanup is automatic, and the cap is what makes it safe.** A prefab that still exists but no longer carries a popup
  is dropped by `SyncPrefab`/`RemoveByGuid`; a deleted prefab's row is dropped by `PruneMissing`, run by the postprocessor
  whenever a batch deleted popup prefabs. Both are bounded by `BulkPrefabCap` — a VCS checkout deleting half the project
  must not drop rows that are about to come back; above the cap the pass defers to a scan instead.
- **The scan is signal-driven and silent.** `IsScanNeeded` (`SessionState`) defaults to **false** and is raised in exactly
  three places: `LoadOrCreate` when the catalog is first created, `LoadState` when `NeedsUpgrade` (`SchemaVersion < 1`),
  and the postprocessor's **bulk** branch (contents deliberately unknown there). The panel then just runs it — a
  cancelable progress bar, no confirm dialog (user request, 2026-08-08: a prompt for something the tool needs in order to
  be correct is a chore) — and clears the flag either way, so a cancel doesn't rescan on the next repaint. The **Rescan
  Prefabs** footer button is the manual override.
  - **Two bugs this shape fixes** (reported 2026-07-25): defaulting to *true* scanned on the first open of every session
    for nothing; and marking the flag from the import callback on *any* `.prefab` meant first-time setup — where the Layers
    panel writes `APS_DefaultCanvas.prefab` — looked like a popup change and produced a **second** scan on the next open.
    Hence: the flag is raised only after a prefab is confirmed to carry a popup, or when contents can't be checked at all.
- Mechanics: the scan is dispatched through `EditorApplication.delayCall` (a dialog + row rebuild inside an open IMGUI
  layout group is asking for layout errors), guarded by `_scanQueued` so a repaint can't queue it twice, and shows a
  cancelable progress bar.
- **Inspector** (`IAdvancedPopupEditor`): a read-only **Draw Order** row (`#n of N`, or "not listed") beside the layer row
  + an *Edit Order* button, resolved once in `OnEnable` through the same `GetRank(OrderKey, FullName)` the runtime uses,
  via a direct `Resources.Load` (not `PopupOrderConfig.Loaded` — its cached null-resolution would hide a catalog created
  later in the same session, and the inspector must never create the asset). `RecordOrderLayer` passes the **popups**, not
  their type names, so `SetLayer` can tag by key first. The `Focus` config block follows the house conditional-reveal
  pattern.

## Postprocessor budget

`PopupOrderPostprocessor` (main Editor assembly, **not** define-gated) keeps the prefab rows fresh. Its cost profile is
the rule every APS postprocessor follows ([[Editor & Codegen]], the editor-perf rule):

1. **Import callback** — string `EndsWith(".prefab")` only, no loads; no pending paths → no `delayCall` at all.
2. **Cold path** — the deferred pass calls `PopupOrderConfigStore.Load()` **before** touching a prefab, so a project that
   never opened the Order panel pays nothing but those string checks.
3. **Bulk cap (`BulkPrefabCap = 64`)** — a batch above the cap is a VCS checkout / Library rebuild / "Reimport All" /
   imported package, not someone editing a popup. Inspecting it would turn "one asset load per changed prefab" into
   loading **every prefab in the project**, so the pass is skipped and says so via `APLogger` (never a silent cap); the
   cost is at worst a catalog that lags until the next single save or the panel's rescan, which it then offers itself.
4. **Writes** — only when something actually changed, and only this asset (`SaveAssetIfDirty`).
5. **Deletions are ignored** — see "Nothing is deleted on a miss" above; the panel flags and the user decides.
6. **Stamping converges, it doesn't loop.** `EnsureOrderKey` writing `_orderKey` re-imports that prefab, so this runs once
   more; the second pass sees the stamp already correct, `Apply` reports no change, and nothing is written.
7. **The full scan lives in the panel, not here** — the postprocessor only flips the `SessionState` flag the panel reads
   on open (see "Editor"), so the expensive pass happens when a human is looking at the tool, never during import.

The same cap was added to **`AddressablePopupIndexGenerator.SyncChanged`** ([[Addressables]]), which had the identical
exposure: above it the per-prefab pass is skipped and the index is re-baked from the (small) group membership instead, so
the index still matches what is grouped — a flag flipped *inside* such a batch waits for that prefab's next save or the
menu item's full rescan. Audited alongside and left as they were: `LayerEnumSyncPostprocessor` (one `EndsWith` per
imported path, acts only on the enum file) and `FolderRenamePrevention` (rename callback only).

## Gotchas

- **Equal rank = show order.** Two popups in the same slot (or both unlisted) stack by show order — that is the
  documented default, and it is why an unconfigured project behaves sanely with no catalog at all.
- **A keyless popup takes the front-most row of its class**, not the back one. Deliberate: a scene-authored popup is
  usually *the* instance of that class, and the front-most row is the one the user reasoned about.
- **A popup whose prefab was never stamped falls back to its type** — an immutable (package) prefab, a project that never
  opened the panel, or a prefab saved while the catalog didn't exist. Never an error, just coarser ranking.
- **The stamp is on the popup's root component**, matched by `TryGetComponent` on the prefab root — the same assumption
  the runtime makes when it parents a popup to a canvas. A popup component on a *child* of the prefab root is invisible
  to discovery.
- **`AssetDatabase.GUIDToAssetPath` keeps resolving a deleted asset** — it returns the last known path, and
  `AssetPathToGUID` even round-trips it back, so "the GUID resolves" is *not* an existence test (verified 2026-08-08
  against a just-deleted prefab; the first `PruneMissing` silently pruned nothing). `IsMissing` asks
  `GetMainAssetTypeAtPath` instead — still no asset load.
- **The postprocessor's deferred pass needs an editor tick.** Import an asset and inspect the catalog in the same
  synchronous block and nothing has happened yet; a domain reload in between drops the queued `delayCall` and the pending
  paths entirely. Worth knowing when testing this from a script or over MCP — it is a test artifact, not a bug.
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
