---
type: code
status: active
description: Operation (self-starting wrapper with an observable outcome — Status/Error/OnComplete), the per-popup linked CancellationTokenSource flow, TaskUtils helpers, and APSStats counters. Read when touching async control flow, cancellation, or the returned Operation.
code_paths:
  - Assets/advanced-popup-system/Runtime/Core/APSystem/Operation.cs
  - Assets/advanced-popup-system/Runtime/Utils/TaskUtils.cs
  - Assets/advanced-popup-system/Runtime/Core/APSystem/APSStats.cs
---

# Operations & Cancellation

Every non-`Async` public call (`Show`, `Hide`, `LayerShow`, `HideAll`, …) returns an **`Operation`**; the awaitable
`*Async` variants are what it wraps.

## Operation

`Operation` (`AdvancedPS.Core.System`) takes a `Func<CancellationToken, Task>` and **starts it immediately** in the
constructor (fire-and-forget) under its own `CancellationTokenSource`. It is the **public consumer primitive**: shipped
docs (§0 contract, §3.2) direct users to wrap any custom async popup flow in an `Operation` instead of `async void` /
UniTask `.Forget()` — its guarantees (logging, cancellation, observable outcome) are contract, not detail.

- **Outcome surface:** `Status` (`OperationStatus`: `Running` → final `Succeeded` / `Cancelled` / `Faulted`), `Error`
  (non-null **iff** `Faulted`), `IsCompleted`. Classified once after the run's `finally`: **cancelled wins over fault**
  — an exception escaping an already-cancelled run is teardown noise (still logged, except an
  `OperationCanceledException`, which own-token cancellation makes normal control flow — not logged). Play-mode exit
  counts as cancelled (`TaskUtils.OperationCancelled`).
- **`.OnComplete(Action)`** — runs on **success only**. *Behavior change* from the old "not cancelled" rule, which also
  fired after a logged exception — running success continuations on a failed load is the consumer-NRE class the
  shipped Pitfalls section documents. **`.OnComplete(Action<Operation>)`** — runs on **every** outcome; the callback
  reads `Status`/`Error` off the operation it receives. Both chainable and **accumulating** (`+=` — the old single-field
  assignment dropped earlier callbacks). **Late attach invokes immediately** under the same rules — fixes the silently
  lost callback on operations that complete synchronously inside the constructor (guard early-outs: `Hide<T>` miss,
  `IsBeVisible`, idempotent `ActiveLayer` no-ops…). Callback exceptions are isolated per callback (`InvokeSafely` →
  `APLogger`) so a throwing continuation can't kill the chain or surface as an unobserved task exception.
- **`.Cancel()`** — no-op once completed **or** already cancelled; the completed-guard closes the
  `ObjectDisposedException` of cancel-after-finish (the CTS is disposed in the run's `finally`).
- Exceptions inside the operation are swallowed to `APLogger.LogException` (they don't crash the caller). Register/
  unregister with `APSStats` bracket the run.

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
