---
type: code
status: active
description: C# style conventions of the APS asset — comments/XML docs, naming, regions, logging, async, files. Mandatory reading before any task together with "Invariants".
code_paths:
  - Assets/advanced-popup-system/Runtime/
---

# Code Style

Hard prohibitions live in [[Invariants]]. Here — how to write what you may write. Match the surrounding code; APS has
its own conventions that **differ from a typical game project** (it is a public library).

## Comments & XML docs

- **Public API is documented with XML `///` comments** — this is a library; keep summaries on public types/members
  (and `<param>` where non-obvious). Match the existing density; don't strip existing XML docs.
- Generated display stubs carry `/* Your code here */` markers at the fill-in points — leave them until you implement.

## Naming & members

- **`_camelCase` for private fields is the norm** (`_isSubscribed`, `_cts`, `_activeTasks`, `_creators`). Serialized
  inspector refs in samples/popups follow the same (`_playButton`). (This is the opposite of comment-free game code —
  don't "fix" it.)
- **`#region` grouping is used heavily** — keep members inside the matching region (`VARIABLES`, `INIT`, `SHOW`,
  `HIDE`, `Helpers`, `Editor`, …) and preserve the file's region layout when editing.
- Display type names end with **`Display`**, their settings with **`Settings`** — codegen and `TypeHelper` depend on it
  ([[Invariants]]).
- Enum flags (`PopupLayerEnum`) are `UPPER_CASE`; the Layers panel enforces this on save.

## Logging

- **Route runtime logs through `APLogger`** (`Utils/APLogger.cs`), not raw `Debug.Log*` — it honors the Settings
  `LogType` filter (`Error` < `Warning` < `Info`, plus `None`). Direct `Debug.LogError` is used only in low-level
  `SettingsManager`/`InputSwitcher` where settings may not be loaded yet.
- Messages are **English**, usually tagged and colored: `APLogger.Log("<color=green>[PointerEventSystemAPS]</color> …")`.
  See [[Settings & Logging]].

## Async

- **`Task`-based async/await** (System.Threading.Tasks) — **not** UniTask, to keep the runtime dependency-light.
- Guard every animation/polling loop with `TaskUtils.OperationCancelled(token)`; yield with `await Task.Yield()` inside
  per-frame loops. Fan-out (deep popups, layer batches) is `Task.WhenAll(...)`. See [[Operations & Cancellation]].
- Do not manage `CancellationTokenSource` lifetime by hand — use `TaskUtils.UpdateCancellationTokenSource` (cancels +
  disposes the old source and returns a fresh, optionally linked one).

## Files & namespaces

- One top-level type per file (enums may group a small related set, e.g. `PopupSettings` + `InspectorEnum`).
- Namespaces by role: `AdvancedPS.Core` (public surface), `AdvancedPS.Core.System` (infrastructure/contracts),
  `AdvancedPS.Core.Utils` (helpers), `AdvancedPS.Core.Input`, `AdvancedPS.Editor`. Full table — [[Project Map]].
- Editor-only code sits under `#if UNITY_EDITOR` or in the Editor assembly; input-variant code under
  `HAS_NEWINPUT` / `!HAS_NEWINPUT`; DoTween code under `DOTWEEN`.

## Git (asset repo)

- The asset repo (`Assets/advanced-popup-system/`) commits in **English**, short imperative subject (`fix:`, `feat:`).
- Do not commit unless the user asks; version/CHANGELOG changes are user-gated ([[Invariants]], [[Shipped Docs]]).

## Depends on

- [[Invariants]] (hard prohibitions)
