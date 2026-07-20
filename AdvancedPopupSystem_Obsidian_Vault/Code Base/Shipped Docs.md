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
- **`documentation.md`** — the deep guide: building blocks, step-by-step setup, API reference, animations & custom
  displays, advanced config, troubleshooting, planned features.
- **`CHANGELOG.md`** — versioned history (current `1.18.0`). **User-gated** — don't add entries or bump on your own.
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
- **Dynamic spawning is "planned"** — `AdvancedPopupInstantiate` is a NoOp stub; documentation.md §7 correctly marks it
  unreleased. Keep it flagged until the feature actually lands.

## Depends on

- [[Invariants]] (version/API gates), [[Popup Lifecycle]] (the Init-order fact docs must reflect)
