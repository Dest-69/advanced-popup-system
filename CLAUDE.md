# Advanced Popup System (APS) — rules for AI agents

**MANDATORY before any task** — read `.agents/rules/advanced-popup-system.md`, the router to the knowledge base
(`AdvancedPopupSystem_Obsidian_Vault/`). Order: `Vault Rules.md` → `Code Base/Invariants.md` →
`Code Base/Code Style.md` → the subsystem note via the router's "task → note" table.
After significant changes to a subsystem — update its note.

The vault + this router are internal tooling **inside** the asset's git repo (`Assets/advanced-popup-system/`, next to
`.git`), versioned with the code. When public API or behavior changes, sync the shipped docs
(`README.md`/`documentation.md`) — see `Code Base/Shipped Docs.md` — and **never bump `package.json` version without
asking the user**.

**Consumer model (normative — for any agent writing code that USES APS):** consumers never assume residency — popups
may be lazy-loaded and released again. Primary open = `Show<T>()` / `GetPopupAsync<T>()`; wrap custom async around
popups in an `Operation`, never in `async void` / UniTask `.Forget()`. `TryGetPopup` is **secondary-only** (it never
loads — it must not be a popup's primary open path). Never bulk-preload layers to make sync access work. Per-open data
goes through `AdvancedPopup<TData>` + `Bind` (open with `Show<TPopup, TData>(data)`) or the configure overload
`Show<T>(p => …)` — data binds **before** the show; never configure content in `.OnComplete` after `Show`. Full
contract: `documentation.md` §0 "The Consumer Contract".
