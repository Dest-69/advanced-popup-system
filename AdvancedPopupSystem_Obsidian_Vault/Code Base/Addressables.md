---
type: code
status: active
description: On-demand popup loading via Addressables — the resolver seam, the generated index, the two loading lanes (unique auto-resolve vs explicit spawn/pool), Root parenting, preload, and the optional runtime/editor assemblies. Read when touching loading, spawning, or the index codegen.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/APSystem/IPopupResolver.cs
  - Assets/advanced-popup-system/Runtime/Core/APSystem/AddressablePopupIndex.cs
  - Assets/advanced-popup-system/Runtime/Generated/AddressablePopupIndex.generated.cs
  - Assets/advanced-popup-system/Runtime/Addressables/
  - Assets/advanced-popup-system/Editor/Addressables/
---

# Addressables

Optional integration that lets popups load from **Addressables on demand** instead of being pre-placed in a scene.
Everything here is **additive and off by default**: with no Addressable-flagged popups and no resolver, the system is
scene-only exactly as before (see [[Core System]]).

## The seam (why core stays dependency-light)

`IPopupResolver` (`AdvancedPS.Core.System`) + the static `AdvancedPopupSystem.Resolver` (null by default) are the whole
coupling. **Core owns the catalog, the dedup, and the scene-wins rule; a resolver only turns an address into a live
instance and releases it** (`LoadAsync(address, parent, token)` / `Release(popup)`). Core never references Addressables
— keeping the "lean runtime" invariant ([[Invariants]]). The optional assembly registers `AddressablesPopupResolver`
into the seam at load; without the package the seam stays null.

## The index (catalog)

`AddressablePopupIndex` (`internal`, partial): the **generated data half**
(`Runtime/Generated/AddressablePopupIndex.generated.cs`) is an `Entry[]` of `{TypeName, Address, Layer, LoadMode,
HideBehavior}`; the **hand-written half** (`AddressablePopupIndex.cs`) is the queries (`ForLayer`, `Preloads`,
`TryGetByTypeName`, `HasEntries`).

- **Keyed by type `FullName` (string), not `Type`** — deliberate: user popups live in the **consumer** assembly, which
  references APS, not vice-versa, so this core-side file cannot `typeof` them. The string also bridges the scene-wins
  check (`IsLive(typeName)` scans `AllPopups` by `GetType().FullName`).
- **Address = the popup type's FullName too** (the generator sets both the Addressables entry address and the index
  Address to it) — deterministic, survives prefab moves, unique per type.

## Two lanes (the core distinction)

- **Lane A — unique / singletons.** `LayerShow`, `Show`, `TryGetPopup`. One instance per type, lives in
  `AllPopups`/`PopupCacheByType`, participates in layers + escape. `LayerShow` calls `EnsureLayerLoadedAsync` before
  showing → materializes the layer's Addressable popups not already live. **Scene wins the index:** a scene-authored
  popup of the same type suppresses the load (dedup by type name), so testing one screen = drop its prefab in a scene
  and press Play — no Addressables round-trip.
- **Lane B — many copies.** `SpawnAsync<T>(parent)` / `Despawn(popup, release)`. For toasts / list rows. Reuses a
  pooled instance or loads a fresh one, then **pulls it out of the unique registries** its `Init()` joined
  (`DeactivateAdvancedPopup`) and tracks it in `SpawnedPopups` — so `LayerShow` ignores it, but it is still in
  `ActivePopups` while visible, so **escape + HideAll reach it** ([[Core System]]).

## Loading, parenting, preload

- **Parenting:** loaded/spawned popups go under `AdvancedPopupSystem.Root` — an auto-created persistent
  (DontDestroyOnLoad) overlay Canvas by default, overridable (assign your own before the first load); `SpawnAsync` takes
  an explicit parent. Scene-authored popups never touch it.
- **Preload:** `LoadMode.Preload` entries load up-front. `PreloadOnBoot` (`[RuntimeInitializeOnLoadMethod`
  **`AfterSceneLoad`**`]`) runs the pass — *after* scene load so scene-authored popups have registered and scene-wins
  can suppress duplicates. The resolver registers at **`BeforeSceneLoad`**, so it is set in time. `PreloadAll()` /
  `PreloadLayer(layer)` return `Operation`s to gate a loading screen.
- **HideBehavior** (Lane A, in `AdvancedPopup.HideAsync`): `Deactivate` keeps the instance resident (default, like
  scene popups); `Despawn` releases the Addressables handle on hide so memory can unload. Spawned Lane B instances
  ignore it (pool-managed via `Despawn`).

## Optional assemblies (isolation, mirrors DoTween/input)

Both gated by `APS_ADDRESSABLES` (a `versionDefine` on `com.unity.addressables`, per-asmdef) + `defineConstraints`:

- **Runtime** `Runtime/Addressables/` → `dest-69.advanced-popup-system.addressables`: `AddressablesPopupResolver`
  (`Addressables.InstantiateAsync` → `TryGetComponent<IAdvancedPopup>`; `ReleaseInstance` per tracked handle;
  self-registers `BeforeSceneLoad`).
- **Editor** `Editor/Addressables/` → `dest-69.advanced-popup-system.addressables.editor`: the index generator +
  postprocessor. The main Editor asmdef also carries the versionDefine so the inspector's Addressable box can `#if`.

## Editor tooling ([[Editor & Codegen]])

`AddressablePopupIndexGenerator.Regenerate()` (menu `Tools/Advanced Popup System/…`, and auto via
`AddressablePopupPostprocessor` on any `.prefab` change, deferred + idempotent): scans `t:Prefab` for
`IAdvancedPopup.Addressable`, `CreateOrMoveEntry` into the **"Advanced Popup System"** group with address = type
FullName, prunes un-flagged entries, and rewrites the index (only when content changed → no recompile churn). The
inspector (`IAdvancedPopupEditor`) shows an "Addressable" box (toggle + `LoadMode`) and a **separate "Pool" box**
(`HideBehavior`, labelled "On Hide"); both are editable only on the prefab asset (`IsEditingPrefabAsset`), read-only with
a small note on a scene object.

## Gotchas

- **Flag `Addressable` on the PREFAB**, not a scene instance — only the prefab asset gets grouped/indexed (the
  postprocessor fires on prefab changes). The inspector **enforces** this: `IAdvancedPopupEditor.DrawAddressable`
  disables the toggle unless `IsEditingPrefabAsset()` (prefab asset selected, or Prefab Mode), with a small note —
  editing the flag on a scene instance would desync it from the prefab.
- **One Addressable prefab per popup type.** The index is type-keyed; the generator warns and skips a second prefab
  sharing a type. Give each Addressable popup a distinct `AdvancedPopup` subclass.
- **`TryGetPopup<T>` stays synchronous** → returns only *resident* popups (scene / preloaded / already loaded); "load if
  missing" happens only on the async paths (`LayerShow`/`Show`/`SpawnAsync`). This is why `Preload` matters for sync gets.
- `AdvancedPopupInstantiate` (the old NoOp stub) was **removed** — its planned role is now `SpawnAsync`/`Despawn` + the
  pool ([[Popup Lifecycle]]). Breaking API change — see [[Shipped Docs]] CHANGELOG.

## Depends on

- [[Invariants]] (lean runtime / optional-assembly rule, static cleanup), [[Core System]] (registries, Root, preload,
  HideAll), [[Popup Lifecycle]] (fields, HideBehavior, spawn), [[Operations & Cancellation]] (`Operation` return type),
  [[Editor & Codegen]] (the generator), [[Project Map]] (assemblies & the `APS_ADDRESSABLES` define)
