---
type: code
status: active
description: The public, user-facing docs inside the asset (README, documentation.md, CHANGELOG, LICENSE, package.json) — what each is and the rule for keeping them in sync with the code. Read before changing public API or the docs.
code_paths:
  - Assets/advanced-popup-system/README.md
  - Assets/advanced-popup-system/documentation.md
  - Assets/advanced-popup-system/CHANGELOG.md
  - Assets/advanced-popup-system/package.json
---

# Shipped Docs

The **user-facing** docs live inside the asset (the git repo / UPM package) and are written for **humans**, in English.
They are a **separate artifact** from this vault — no wiki-links, no `code_paths`, no vault references
([[Vault Rules]] → "Shipped Docs style").

- **`README.md`** — landing page: requirements/compatibility, install (Newtonsoft), features, quick start, showcase.
- **`documentation.md`** — the deep guide: the consumer contract (§0 — "want → call" table + the lazy-residency
  rules), building blocks, step-by-step setup, API reference (§3.2 documents `Operation` as the public primitive for
  consumer async — status/outcome, both `OnComplete` overloads — and the data-before-show ladder:
  `AdvancedPopup<TData>`/`Bind` → configure callback → manual `Operation`), animations & custom displays, advanced
  config, troubleshooting, Addressables (§9, with the §9.6 what-loads cheat sheet), and pitfalls/anti-patterns with
  symptoms (§10 — from real consumer incidents).
- **`CHANGELOG.md`** — versioned history (current `2.0.1`). **User-gated** — don't add entries or bump on your own.
- **`package.json`** — UPM manifest: `version`, `unity` min, `dependencies` (Newtonsoft). **Never bump without
  asking** ([[Invariants]]).
- **`LICENSE.md`**, `.github/FUNDING.yml` — legal/funding.

## Sync rule

When a task changes **public API or observable behavior**, update `README.md` + `documentation.md` in the same task so
the repo stays accurate for users. Keep them **usage-first, deep, and current** — not a source dump. Version bumps and
`CHANGELOG` entries are the **user's** call.

## Known accuracy points to preserve

- **`Init()` override order:** shipped examples call `SetCachedDisplay(...)` **then** `base.Init()` last — that's the
  correct order ([[Popup Lifecycle]]). The `IAdvancedPopup.Init` XML summary ("keep base.Init() first") is misleading;
  if docs are rewritten, follow the examples, not that summary.
- **`OnComplete(Action)` is success-only** (since the Operation outcome API, shipped `2.0.1` 2026-07-23; before that it
  fired on any not-cancelled finish, faults included). The docs lean on this (§3.2 table, the §10.5 race pattern) —
  keep them consistent. The behavior change is called out in the `2.0.1` CHANGELOG *Upgrading* note
  ([[Operations & Cancellation]]).
- **The consumer contract (§0) + Pitfalls (§10) encode the lazy-residency model** — primary open =
  `Show<T>`/`GetPopupAsync` (custom async wrapped in `Operation`), `TryGetPopup` secondary-only, no bulk layer
  preloads. New doc examples must not contradict it; the same rules sit normatively in the asset `CLAUDE.md` for
  agents writing consumer code. README's quick start deliberately opens with `Show<T>()`, not a `TryGetPopup` gate —
  don't "restore" the old lookup-first snippet.

## Depends on

- [[Invariants]] (version/API gates), [[Popup Lifecycle]] (the Init-order fact docs must reflect)
