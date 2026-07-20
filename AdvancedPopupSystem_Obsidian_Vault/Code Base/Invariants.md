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

- **`Runtime/Generated/PopupLayerEnum.generated.cs` is fully regenerated** by the APS **Layers** panel — **never
  hand-edit it** (edits are lost on next save). Add/remove/rename layers only through the window (see [[Layers]],
  [[Editor & Codegen]]). Max 31 flags (`int` bitmask).
- **Display stubs are generated once, then owned by you.** The **Displays** panel creates
  `Runtime/Generated/Displays/<Name>Display/<Name>Display.generated.cs` + `<Name>Settings.generated.cs` **only if the
  folder is missing** (it never overwrites existing bodies). After generation, the animation body is yours to fill.
- **Naming is load-bearing:** a display type ends with `Display`, its settings with `Settings`, and both live in a
  folder named `<Name>Display`. `FileSearcher` + `TypeHelper.RemoveDisplaySuffix` and the Displays panel rely on this;
  breaking it hides the display from the tooling (see [[Editor & Codegen]]).

## Popup contract

- **`IAdvancedPopup` is an abstract `MonoBehaviour` base, not an interface** (the "I" prefix is legacy). User popups
  extend **`AdvancedPopup`**; the concrete Show/Hide/Switch overrides live there (see [[Popup Lifecycle]]).
- **When overriding lifecycle methods, call `base`**: `Init()` first (it sets up cache, `RectTransform`/`CanvasGroup`,
  auto-hide, and registers with the system), then your setup; `Subscribe()`/`Unsubscribe()` must call base and stay
  **strictly symmetric** (base wires `closeButton`, `OnShowing`/`OnHided`, and `ActivePopups` add/remove).
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
- **`Runtime/Generated/AddressablePopupIndex.generated.cs` is regenerated** by the editor (scan of Addressable-flagged
  prefabs) — never hand-edit; queries live in the hand-written `AddressablePopupIndex.cs` (same partial).
- **New global state** (`SpawnedPopups`, the pool, `Root`) obeys the leak-guard rule: cleared/nulled on play-mode exit,
  null-pruned on scene unload ([[Core System]]).
- **`AdvancedPopupInstantiate` was removed** (a NoOp stub) in favor of `SpawnAsync`/`Despawn` + pooling — a **breaking**
  public-API change; record it in `CHANGELOG` on the next (user-gated) version bump ([[Shipped Docs]]).

## Depends on

- [[Code Style]] (paired mandatory reading), [[Project Map]] (assemblies & namespaces), [[Shipped Docs]] (public sync).
