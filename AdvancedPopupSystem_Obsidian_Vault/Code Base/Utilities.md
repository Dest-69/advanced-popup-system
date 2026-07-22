---
type: code
status: active
description: Utils registry — APLogger, FileSearcher, TaskUtils, TypeHelper. Where each helper lives and who owns its detail. Read when looking for a shared helper before writing a new one.
code_paths:
  - Assets/advanced-popup-system/Runtime/Utils/
---

# Utilities

`AdvancedPS.Core.Utils` — shared, stateless helpers. Detail lives in the owning subsystem note; this is the registry so
they get reused, not re-written ([[Code Style]]).

- **`APLogger`** — level-filtered logging (`Log`/`LogWarning`/`LogError`/`LogException`). Detail: [[Settings & Logging]].
- **`FileSearcher`** — UPM-aware package resolution (`PackageInfo.FindForAssembly`, folder-name fallback) for read-only
  image/built-in-display paths, and the **consumer-side** generated-code paths (`LayersEnumFilePath`,
  `CustomDisplaysFolderPath` under `Assets/AdvancedPopupSystem/Generated/`), plus `ToAssetPath`/`ToFsPath`. Lazy,
  non-throwing. Detail: [[Editor & Codegen]].
- **`TaskUtils`** — `OperationCancelled(token)` and `UpdateCancellationTokenSource(...)`. Detail:
  [[Operations & Cancellation]].
- **`TypeHelper`** — display/settings type resolution by name: `GetDisplay`/`GetDisplaySettings` (base name + suffix,
  resolved through `GetTypeByName`, an **assembly scan** — a bare `Type.GetType(shortName)` can't find APS types in
  their own assemblies), `GetTypeByName`/`GetTypeByFullName` (scan loaded assemblies), and **`RemoveDisplaySuffix`**
  (strips a **single trailing** `Display`/`Settings` suffix, case-insensitive — not a replace-anywhere) — the naming
  convention the codegen relies on ([[Editor & Codegen]], [[Invariants]]).

## Depends on

- [[Project Map]] (namespace/placement of helpers)
