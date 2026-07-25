---
trigger: always_on
---

# Advanced Popup System (APS) — rules for AI agents

`.agents/rules/advanced-popup-system.md` (this file) is a **router** to the knowledge base: it holds no rules, it
points into `AdvancedPopupSystem_Obsidian_Vault/` via the "task → note" table. Notation `[[Name]]` =
`AdvancedPopupSystem_Obsidian_Vault/Code Base/<Name>.md`.

**ALWAYS, before any task, first read `AdvancedPopupSystem_Obsidian_Vault/Vault Rules.md`** — the protocol for working
with the vault (what to read when, what/how to document, the style of each section). It also requires reading
[[Invariants]] and [[Code Style]] before any task. Open the rest by the table **on demand**; after significant changes
to a subsystem, update its note (how — in `Vault Rules`).

The vault and this router live **inside the asset's git repo** (`Assets/advanced-popup-system/`, next to `.git`),
versioned with the code. The public user docs are separate — see [[Shipped Docs]]; sync them when public API
or behavior changes, and **never bump `package.json` version without asking the user**.

| Task | Note |
|------|------|
| Any (always) | [[Invariants]], [[Code Style]] |
| Orientation: paths, assemblies, `folder → namespace`, defines (`HAS_NEWINPUT`/`DOTWEEN`) | [[Project Map]] |
| The static manager, registries, lookup, layer batches, scene/play-mode cleanup | [[Core System]] |
| Create/change a popup: `Init`/`Subscribe`/`Show`/`Hide`/`Cmd_`/`Switch`, relationships between popups | [[Popup Lifecycle]] |
| Animations, `IDisplay`/`DisplayBase`, settings, `DisplayRegistry`, cached vs per-call | [[Displays & Animations]] |
| A built-in display — fade | [[Display — Fade]] |
| A built-in display — scale (the default) | [[Display — Scale]] |
| A built-in display — slide | [[Display — Slide]] |
| A DoTween-driven display (`DOTWEEN`) | [[Display — DoTween]] |
| Authoring a new/custom display + settings | [[Display — Custom]] |
| Drag/resize a popup, `Modules`/`PopupFeatureEnum`, pointer system, grips, bounds/anchors, new interactive feature | [[Interaction Modules]] |
| `Operation`, `OnComplete`/`Cancel`, `CancellationToken` flow, `APSStats` | [[Operations & Cancellation]] |
| Layers, `PopupLayerEnum`, `ActiveLayer`, autohide | [[Layers]] |
| Who is drawn above whom inside a canvas: the Order catalog, `ApplyOrder`/`BringToFront`, `Focusable` | [[Hierarchy Order]] |
| On-demand loading: Addressables, lazy/preload, `SpawnAsync`/pool, the resolver seam & index asset | [[Addressables]] |
| Input backends (New vs Old), pointer polling, input-module switch, why APS reads no keys | [[Input Backends]] |
| Settings, `AP_Settings.json`, log levels / `APLogger` | [[Settings & Logging]] |
| APS editor window, generating layers/displays, inspectors, create-popup menu | [[Editor & Codegen]] |
| Shared helpers (`APLogger`/`FileSearcher`/`TaskUtils`/`TypeHelper`) | [[Utilities]] |
| Sample scenes / faithful usage references | [[Samples]] |
| Public docs (`README`/`documentation.md`/`CHANGELOG`), package version | [[Shipped Docs]] |
| Building/exporting the `.unitypackage`, what ships vs. self-generates, non-destructive updates, the export tool | [[Build & Packaging]] |
