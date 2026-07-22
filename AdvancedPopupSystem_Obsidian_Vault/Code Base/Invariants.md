---
type: code
status: active
description: Hard, unbreakable rules of the APS asset. Mandatory reading before ANY task — violating any point breaks the public API, the codegen, assembly boundaries, or the show/hide contract.
code_paths:
  - Assets/advanced-popup-system/Runtime/
  - Assets/advanced-popup-system/package.json
---

# Invariants

Do not break these. Deviation only after explicit agreement in the current task.

## Package & public API

- **This is a published UPM package** (`com.dest-69.advanced-popup-system`, versioned, its own git repo at
  `Assets/advanced-popup-system/.git`, mirrored to GitHub). Everything under `AdvancedPS.Core*` is **public API** that
  consumers depend on. Treat signature/behavior changes as breaking.
- **Never bump `package.json` `version` yourself** — always ask the user. A bump ripples to every consumer via UPM and
  pairs with a `CHANGELOG.md` entry (see [[Shipped Docs]]). Same for `unity` (min editor) and `dependencies`.
- **Public API or observable behavior changed → sync the shipped docs** (`README.md`, `documentation.md`) in the same
  task. They are what the repo ships to humans. See [[Shipped Docs]].
- **Newtonsoft.Json is the only runtime dependency** (`com.unity.nuget.newtonsoft-json`, used by `SettingsManager`).
  Keep the runtime lean — do not add heavy deps to `dest-69.advanced-popup-system.asmdef`. DoTween is **optional**,
  isolated behind the `DOTWEEN` define in its own assembly (see [[Project Map]]).

## Generated code & codegen

- **Generated code lives in the CONSUMER project, never in the package** (so a read-only UPM install — Git URL /
  registry, landing in `Library/PackageCache` — can still regenerate it, and a package update never clobbers it). Both
  the layer enum and custom displays are written under `Assets/AdvancedPopupSystem/Generated/`, each in its **own
  consumer-side assembly**: `AdvancedPS.Generated.Layers` (holds `PopupLayerEnum`, **no references** so core can
  reference it without a cycle) and `AdvancedPS.Generated.Displays` (references core). The core runtime asmdef
  **references `AdvancedPS.Generated.Layers` by name** — that is what makes `LayerShow(PopupLayerEnum)` compile.
- **`PopupLayerEnum.generated.cs` is a projection, not the source** — regenerated from the external layer store
  (`ProjectSettings/APS_Layers.json`) by `LayerCatalog`. **Never hand-edit it** (edits are lost). Add/remove/rename
  layers only through the Layers panel. On a **fresh install** the consumer-side layers assembly doesn't exist yet, so
  core can't compile and the APS tooling (which needs core) can't run to create it — the dependency-free
  **`AdvancedPS.Bootstrap`** editor assembly (`Editor/Bootstrap/`, **zero core references**) breaks that deadlock: it
  runs while core is red and seeds the assembly from the store (else defaults). Thereafter `LayerCatalog` +
  `LayerEnumSyncPostprocessor` own it. Max 31 flags (`int` bitmask). See [[Layers]], [[Editor & Codegen]],
  [[Build & Packaging]].
- **Display stubs are generated once, then owned by you.** The **Displays** panel creates
  `Assets/AdvancedPopupSystem/Generated/Displays/<Name>Display/…generated.cs` **only if the folder is missing** (it
  never overwrites existing bodies); built-in displays stay in the package (read-only, listed but not editable). After
  generation, the animation body is yours to fill. Any assembly (a package sub-assembly, or a consumer's own asmdef)
  that names `PopupLayerEnum` must reference `AdvancedPS.Generated.Layers` — the package's own assemblies already do;
  consumer game code in the predefined `Assembly-CSharp` gets it via `autoReferenced`.
- **Naming is load-bearing:** a display type ends with `Display`, its settings with `Settings`, and both live in a
  folder named `<Name>Display`. `FileSearcher` + `TypeHelper.RemoveDisplaySuffix` and the Displays panel rely on this;
  breaking it hides the display from the tooling (see [[Editor & Codegen]]).

## Popup contract

- **`IAdvancedPopup` is an abstract `MonoBehaviour` base, not an interface** (the "I" prefix is legacy). User popups
  extend **`AdvancedPopup`**; the concrete Show/Hide/Switch overrides live there (see [[Popup Lifecycle]]).
- **When overriding lifecycle methods, call `base`**: `Init()` first (it sets up cache, `RectTransform`/`CanvasGroup`,
  auto-hide, and registers with the system), then your setup; `Subscribe()`/`Unsubscribe()` must call base and stay
  **strictly symmetric** (base wires the `Closable` module's button `Modules.CloseButton`, `OnShowing`/`OnHided`, and
`ActivePopups` add/remove).
- **`CanvasGroup` is required** (`[RequireComponent]`); `RectTransform` + `CanvasGroup` are auto-added in `Init()` if
  missing. Displays animate via the `RectTransform` and `CanvasGroup`.

## Show/Hide & cancellation

- **Every show/hide runs under a per-popup linked `CancellationTokenSource`**, (re)created via
  `TaskUtils.UpdateCancellationTokenSource` — starting a new transition **cancels the previous one** on that popup.
  Never call `Cancel()`/`Dispose()` on a CTS by hand; use the `TaskUtils` helpers (see [[Operations & Cancellation]]).
- **A cancelled transition must roll back state and not finalize:** on cancel, restore `IsBeVisible`/`IsVisible` to the
  pre-transition value and skip the final `gameObject.SetActive(false)` / `IsVisible = true`. This ordering is the
  contract every display and the base popup honor — preserve it.
- **Async is `Task`-based** (no UniTask — keeps the asset dependency-light). Guard every animation loop with
  `TaskUtils.OperationCancelled(token)` (also treats exiting play mode as cancelled).

## Static state & lifetime

- **The only global state is in `AdvancedPopupSystem`**: `AllPopups`, `ActivePopups`, `PopupCacheByType`,
  `ActiveLayer`. There is **no `.Instance`/singleton** — the manager is a static coordinator. Any new static collection
  must be **cleared on scene unload and on editor play-mode exit** (existing leak guards in `AdvancedPopupSystem` and
  both `KeyEventSystemAPS`). See [[Core System]].
- **`ActiveLayer` is mutated only by `AdvancedPopupSystem` `Layer*`/`HideAll` APIs.** A manual `popup.Show()/Hide()`
  updates only `ActivePopups`, never `ActiveLayer` — don't couple manual calls to layer state (see [[Layers]]).

## Addressables (optional integration)

- **Optional and isolated like DoTween.** On-demand loading lives in its own assemblies gated by `APS_ADDRESSABLES`
  (`Runtime/Addressables/`, `Editor/Addressables/`); **core never references Addressables** — keep it that way (lean
  runtime). Do **not** add `com.unity.addressables` to `package.json` `dependencies`. See [[Addressables]].
- **The Addressable popup index is a data asset** — `AddressablePopupIndexAsset` (a ScriptableObject at the consumer's
  `Assets/Resources/APS_AddressablePopupIndex.asset`), regenerated by the editor (scan of Addressable-flagged prefabs);
  never hand-edit. Queries live in the hand-written `AddressablePopupIndex.cs`. Writing an **asset** (not C#) is the point:
  flagging a popup Addressable no longer recompiles scripts or reloads the domain.
- **New global state** (`SpawnedPopups`, the pool, `_inFlightLoads`, `Root`) obeys the leak-guard rule: cleared/nulled on
  play-mode exit, null-pruned on scene unload ([[Core System]]). Documented exception: `_inFlightLoads` (Lane-A
  concurrent-load dedup) is cleared on play-mode exit **only** — it holds short-lived, self-removing Tasks, not
  destroyable Unity refs, so scene unload needs no prune.
- **`AdvancedPopupInstantiate` was removed** (a NoOp stub) in favor of `SpawnAsync`/`Despawn` + pooling — a **breaking**
  public-API change; record it in `CHANGELOG` on the next (user-gated) version bump ([[Shipped Docs]]).

## Packaging & non-destructive updates

- **Consumer state lives outside the package** so an update (UPM or `.unitypackage`) never clobbers it: settings in
  `Assets/Resources/AP_Settings.json`, layers in `ProjectSettings/APS_Layers.json`, canvas config + Addressable index in
  `Assets/Resources/`, and **generated code** (enum + custom displays) under `Assets/AdvancedPopupSystem/Generated/`. Do
  not move any of these back inside the package; new consumer-editable state must follow the same rule
  ([[Build & Packaging]]).
- **The shipped package is built only by `APSPackageExporter`** (`Editor/Build/`) — never a raw "Export Package" on the
  folder (that leaks internal tooling). It excludes the vault, `CLAUDE.md`, and itself; ships samples as nested
  `.unitypackage`s (not raw sources); pulls **no** third-party dependencies. It ships **no** `PopupLayerEnum` (generated
  consumer-side now) but **must ship `Editor/Bootstrap/`** so consumers self-heal on import. Keep `Editor/Build/` on its
  deny-list.
- **A missing generated `PopupLayerEnum` file must seed a _compilable_ default, never empty** — an empty `.cs` drops the
  `PopupLayerEnum` type and hard-fails the compile. Both `FileSearcher` (when core is alive) and `AdvancedPS.Bootstrap`
  (on a fresh install, before core compiles) enforce this, seeding from the store when present else defaults. Keep their
  default enum + asmdef content in sync. (The Addressable index is a data asset, so a missing index is just an empty
  catalog — no default, no compile error.)

## Depends on

- [[Code Style]] (paired mandatory reading), [[Project Map]] (assemblies & namespaces), [[Shipped Docs]] (public sync),
  [[Build & Packaging]] (what ships / update-safety).
