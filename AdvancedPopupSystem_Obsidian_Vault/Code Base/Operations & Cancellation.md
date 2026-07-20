---
type: code
status: active
description: Operation (fire-and-forget wrapper), the per-popup linked CancellationTokenSource flow, TaskUtils helpers, and APSStats counters. Read when touching async control flow, cancellation, or the returned Operation.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/APSystem/Operation.cs
  - Assets/advanced-popup-system/Runtime/Utils/TaskUtils.cs
  - Assets/advanced-popup-system/Runtime/Core/APSystem/APSStats.cs
---

# Operations & Cancellation

Every non-`Async` public call (`Show`, `Hide`, `LayerShow`, `HideAll`, …) returns an **`Operation`**; the awaitable
`*Async` variants are what it wraps.

## Operation

`Operation` (`AdvancedPS.Core.System`) takes a `Func<CancellationToken, Task>` and **starts it immediately** (fire-and-
forget) under its own `CancellationTokenSource`.

- **`.OnComplete(action)`** — chainable; the action runs **only if the operation was not cancelled** (checked in the
  `finally`). Returns `this`.
- **`.Cancel()`** — cancels the source (idempotent; guards `IsCancellationRequested`).
- Exceptions inside the operation are swallowed to `APLogger.LogException` (they don't crash the caller). Register/
  unregister with `APSStats` bracket the run; the source is disposed in `finally`.

## Per-popup linked CTS (the cancellation contract)

Each popup holds one `Source` (`CancellationTokenSource`). At the top of `ShowAsync`/`HideAsync` it is refreshed with
`TaskUtils.UpdateCancellationTokenSource(Source, dispose:true, token)` — which **cancels and disposes the previous**
source and creates a new one, **linked** to the caller's `token` if one was passed. Effect: **starting a new transition
on a popup cancels the one in flight** ([[Popup Lifecycle]], [[Invariants]]). A cancelled transition rolls back
`IsBeVisible`/`IsVisible` and skips finalization. On `OnDestroy` the popup calls `TaskUtils.CancelAndDispose(Source)` so
an in-flight animation stops touching the destroyed transform (and the last, otherwise-undisposed source is released).

## TaskUtils

- `OperationCancelled(token)` = `token.IsCancellationRequested || !Application.isPlaying` — so **exiting play mode
  counts as cancelled**; every animation loop checks it.
- `UpdateCancellationTokenSource(source=null, dispose=true, linkedToken=default)` — cancel (+optionally dispose) the
  old, return a new source (`CreateLinkedTokenSource` when a token is given). The **only** sanctioned way to manage a
  CTS here ([[Code Style]]).
- `CancelAndDispose(source)` — terminal teardown (cancel + dispose, **no** replacement created; null-safe). For
  `OnDestroy`-style cleanup where `UpdateCancellationTokenSource` would leak a fresh throwaway source.

## APSStats

Thread-safe counters (`Interlocked`): `ActiveOperationsCount` (live `Operation`s) and `ActiveTasksCount` (live show/hide
tasks), plus `Reset()`. Surfaced by the samples' stats panels and useful for spotting stuck/leaked transitions
([[Samples]]). **Both register/unregister pairs are `finally`-guarded** — `Operation.ExecuteAsync` for operations,
`ShowAsync`/`HideAsync` for tasks — so a throwing display can't permanently skew `ActiveTasksCount`.

## Depends on

- [[Popup Lifecycle]] (owns the per-popup `Source`), [[Core System]] (returns `Operation` from layer calls)
