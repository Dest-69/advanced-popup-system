---
type: code
status: active
description: Sample showcases (DoTween / Easing / Performance / Addressables) and the InputSwitcher util — what each demonstrates and its asmdef/define. Read when editing samples or reproducing usage patterns.
code_paths:
  - Assets/advanced-popup-system/Samples~/
  - Assets/advanced-popup-system/Samples/Utils/
---

# Samples

Showcase scenes + scripts under `Samples~/` (hidden from Unity — see **Delivery** below), each with its own
`AdvancedPS.Core.Examples` assembly ([[Project Map]]). They are also the most faithful **usage references** for the
public API.

**All showcases run on the Addressables pipeline** ([[Addressables]]). Each has its **own distinct `AdvancedPopup`
subclass** (the index is type-keyed — no sharing between samples), flagged Addressable on its prefab, and materializes
copies via `AdvancedPopupSystem.SpawnAsync<T>` (Lane B) instead of `Instantiate`. Consequently they now **require the
Addressables package** — every sample below is gated on `APS_ADDRESSABLES` (the DoTween one on `DOTWEEN` **and**
`APS_ADDRESSABLES`), mirroring the AddressablesShowcase asmdef. Their prefabs keep authored visuals (unlike the bare,
Init-built AddressablesShowcase demos), so the subclasses are empty type-markers. `GeneratePopups` is `async` and the
action buttons stay disabled until spawning finishes.

- **DoTweenShowcase** (`DOTWEEN` + `APS_ADDRESSABLES`) — spawns N `TweenPopupDemo` via `SpawnAsync`, each caching a
  per-instance `DoTweenSettings` (`DOLocalJump`/`DOLocalMove`), and loops `Show().OnComplete(() => Hide().OnComplete(...))`.
  Its stats panel reads `ActivePopups`, `APSStats.ActiveOperationsCount/ActiveTasksCount`, and DOTween totals — the
  canonical [[Operations & Cancellation]] monitor.
- **EasingShowcase** (`APS_ADDRESSABLES`) — spawns one `EasingPopupDemo` per `EasingType` (with `InfoBlock`), each caching
  a `SlideSettings` for that curve. Shows/hides the grid by **iterating the spawned list** — Lane B popups aren't in layer
  batches, so it no longer uses `LayerShow`/`LayerHide`.
- **PerformanceShowcase** (`APS_ADDRESSABLES`) — stress test of many simultaneous show/hide operations on
  `SpawnAsync`-ed `PerformancePopupDemo` copies.
- **AddressablesShowcase** (`APS_ADDRESSABLES` define) — demos on-demand loading ([[Addressables]]): a scene popup
  (immediate), a lazy Addressable popup (MENU), a preloaded-at-boot Addressable popup (GUI), and pooled `SpawnAsync`
  toasts, driven by a code-built button bar; all draggable. The demo popups build their own visuals in `Init`, so the
  prefabs are bare (one distinct `AdvancedPopup` subclass per type — the index is type-keyed).
- **InputSwitcher** (`Samples/Utils`) — auto-swaps the EventSystem input module under `ENABLE_INPUT_SYSTEM` when
  `AutoSwitchInputModule` is on ([[Input & Hotkeys]]); `[RuntimeInitializeOnLoadMethod]`, no scene placement needed.

**Gotcha:** flagging a prefab Addressable only takes effect once the editor postprocessor regenerates the index asset +
Addressables group ([[Addressables]] "Editor tooling"). After a fresh checkout or a **hand-edited** `Addressable` flag,
open the project once so the postprocessor fires — until then `SpawnAsync` finds no index entry and the samples spawn
nothing (they log and degrade gracefully). The consumer-side index asset/group are not shipped (regenerated on import).

## Delivery (opt-in, both channels)

Sources live in the hidden **`Samples~/`** folder (Unity ignores `~` folders everywhere — including under `Assets/`, so
they're invisible in the dev project too) so they **never auto-compile in a consumer**. UPM installs get them as
on-demand **Package Manager samples** (`package.json` `"samples"` → `Samples~/<Showcase>`); the `.unitypackage` ships them
as nested `Samples/<Showcase>.unitypackage` (opt-in, imported by double-click), **rebuilt from `Samples~/`** by the
exporter's staging step ([[Build & Packaging]]). `Samples/Utils/` (InputSwitcher) stays visible and ships raw. Samples are
**not** part of the public API — patterns here can change with the demo without a version concern.

**Editing sources in the dev project** — the "Sample sources" buttons in the exporter window
(`APS ▸ Build ▸ Export Package…`), backed by `APSSampleDevMode` (dev-only, lives in the exporter's deny-listed
`Editor/Build/`; no menu items of its own). "Edit Sample Sources" **moves** each showcase
`Samples~/ → Samples/` so Unity imports it (edit/playtest as usual); "Finish Editing" moves it back and **parks the showcase's
folder `.meta` next to the source in `Samples~/`**, so folder GUIDs stay stable across round-trips and exporter stagings
(the four original folder GUIDs were restored from the pre-refactor commit). While anything is checked out the exporter
**refuses to export** and a reminder logs on every domain reload — finish before committing or exporting. Detection is
by folder presence: any dir under `Samples/` except the raw-shipped set (`RawShippedFolders` = `Utils`) counts as
checked out — add new raw-shipped folders to that set.

**Why hidden (the bug this fixed):** sample code hard-references specific layers (e.g. `PopupLayerEnum.MENU` in
`AddressablesShowcase`). `PopupLayerEnum` is a **projection of the consumer's** layer set ([[Layers]]), so left visible in
a UPM package the sample auto-compiled in *every* consumer and hard-failed the build (`CS0117`) wherever their layer set
lacked the sample's layers. `Samples~` makes compilation opt-in, so a differing layer set can no longer break an install.

## Depends on

- [[Popup Lifecycle]], [[Displays & Animations]], [[Operations & Cancellation]] (what the demos exercise)
