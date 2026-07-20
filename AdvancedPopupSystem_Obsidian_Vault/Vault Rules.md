---
type: meta
status: active
description: Rules and workflow for this Obsidian vault — the working protocol for AI agents and humans on the Advanced Popup System asset.
color: var(--mk-color-blue)
sticker: emoji//2754
---

# Vault Rules

Plain-text database. Each note is a row; frontmatter properties are columns.
[Obsidian Bases](https://help.obsidian.md/bases) turns them into live tables that maintain themselves.

This vault is **internal tooling for developing the APS asset** — it lives **inside** the asset's git repo
(`Assets/advanced-popup-system/`, next to `.git`), versioned together with the code it documents. The public,
user-facing docs are a **separate** artifact (see [[Shipped Docs]]).

## Protocol for the AI agent

- **ALWAYS, before any task,** read these rules (`Vault Rules`), then [[Invariants]] (hard rules) and
  [[Code Style]] (C# conventions). The "task → note" router is `.agents/rules/advanced-popup-system.md`.
- **Orientation** (where code lives, assemblies, folder → namespace, define constraints): [[Project Map]].
- **Features, bugs, refactors → read `Code Base/`.** Open the subsystem note via the router **before** changing it;
  after significant changes — **update it**. A new subsystem → new note in `Code Base/` with `type: code` and a
  `tree/<subsystem>` tag — it lands in `Code.base`/[[_Code_Map]] on its own.
- **Public API or observable behavior changed → sync the shipped docs** (`README.md`, `documentation.md`,
  `CHANGELOG.md` inside the asset) — that is what the git repo publishes to humans. See [[Shipped Docs]].
  **Never bump `package.json` version yourself** — always ask the user (a bump affects every consumer via UPM).

## Principles

1. **Small linked notes beat one giant manual** — one note per subsystem/entity, connected with `[[ ]]`.
2. **Shallow PARA folders + MOC** (`_..._Map`) for navigation.
3. **Properties drive Bases** — consistent frontmatter powers the dashboards.
4. **Bases for tables, Dataview (optional) for complex queries.**
5. **Density:** no filler, maximally token-efficient — these notes are read by both humans and AI agents.

## Code Base style — implementation docs

Rules for `Code Base/` notes (`type: code`/`leaf`): they describe the **implementation** and are tied to the code.

### What to document (altitude & splitting)

A note is a **map of decisions**, not a retelling of the code. Write the important and non-obvious; local details
visible from the code — skip them (reading them wastes context and time).

- **Write:** contracts/invariants · cross-system wiring (who calls whom, where data comes from) · non-obvious
  decisions and *why* · gotchas (timing, order, authority, allocations) · code entry points (`code_paths`,
  class/method names — so they can be found).
- **Don't write** (obvious from code): line-by-line method retelling, parameter values, local variables · trivial
  getters/checks · details that will rot together with the code first.
- **Test for a line:** describes what the method already does line-by-line → cut it; encodes a
  decision/relationship/invariant/gotcha → keep it. **Abstract > detailed.**
- **Split into parts:** once a self-contained sub-capability appears (read/changed on its own — e.g. one specific
  display) → extract it into a **leaf** (see "Mechanic trees"), don't grow the hub. Over-detail/bloat = a signal to
  split. Many small files > one big file.

### Links (link discipline)

Links are coupling. To avoid dragging in the irrelevant while working a feature, distinguish two kinds:

- **Dependency** (what the note actually needs) — a "## Depends on" section + inline `(see [[X]])` at the point of
  use. Read by both human and AI.
- **Navigation** (overview, "where is what") — only in the MOC/`Code.base`, **one-directional**: the map links to
  notes, notes do **not** link back to the map. AI does not read it.

"Depends on" rules:

- only real dependencies, minimal;
- **down the stability gradient** — toward the foundation ([[Invariants]]/[[Code Style]]/[[Project Map]]), core
  infrastructure ([[Operations & Cancellation]], [[Displays & Animations]]); **not toward consumers** (whoever uses
  this note is not its dependency);
- sibling feature↔feature — avoid; if needed, link its **contract**, one link;
- cross-cutting (`IDisplay`, `Operation`) — keep in one stable note that many reference;
- **don't backlink [[_Code_Map]]** — navigation is one-directional.

### Mechanic trees (hub → leaves)

Split a large mechanic into a **tree**: a hub note (`type: code`, top level of `Code Base/`) holds the shared
machinery and **all shared dependencies**; concrete variants are **leaves** (`type: leaf`, `parent: "[[Hub]]"`) in a
subfolder `Code Base/<Mechanic>/`. A leaf links **only to its hub + its own unique** deps — the shared already lives
in the hub. The hub lists its leaves in a "## Variants (tree)" section. Current tree: [[Displays & Animations]] →
`Displays/` (Fade, Scale, Slide, DoTween, Custom).

- The `Code.base` table (`type == code`) shows **only hubs**; leaves are reached via the hub tree and the graph.
- `[[ ]]` links resolve by name — subfolders don't break them; keep leaf names unique.
- Granularity is a **hybrid**: a separate leaf where behavior is unique, group the trivial (don't spawn 5-line leaves).

## Shipped Docs style — public docs

Rules for the user-facing docs in the asset (`README.md`, `documentation.md`, `CHANGELOG.md`). Separate ruleset — do
not confuse with implementation docs. Details and the sync procedure live in [[Shipped Docs]]:

- **For humans/users, English.** Readable, informative, deep, current — usage-first, not a source dump.
- **No `[[ ]]` wiki-links, no `code_paths`, no references to vault notes** — these files live in the public repo.
- **Kept in sync on public API / behavior change**; version bumps and `CHANGELOG` entries — **only with the user**.

## Structure

- `Code Base/` — live docs per code subsystem. Entry: [[_Code_Map]], table — `Code.base`.
- `Code Base/Displays/` — leaves of the [[Displays & Animations]] tree.

Hard rules and style live **in the vault**: `Vault Rules` (protocol), [[Invariants]] & [[Code Style]] (mandatory
reading before any task). `.agents/rules/advanced-popup-system.md` at the asset root is only the **router** with the
"task → note" table: it holds no rules and requires reading `Vault Rules` first. `CLAUDE.md` beside it points here.

## Language

Everything in this vault is **English** — note text, headings, and file names (so `[[ ]]` links and H1 match). This
matches the asset's English codebase and its English shipped docs. Frontmatter keys/values, folder names, and
`code_paths` are English too (Bases filters depend on them: `file.inFolder(...)`, property comparisons).

## Sources

- [Obsidian Bases docs](https://help.obsidian.md/bases) · [syntax](https://help.obsidian.md/bases/syntax)
- [Folder-structure best practices](https://studio-obsidian.com/obsidian-folder-structure/)
- [Maps of Content — Obsidian Rocks](https://obsidian.rocks/maps-of-content-effortless-organization-for-notes/)
