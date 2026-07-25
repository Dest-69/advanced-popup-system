# Changelog
## [2.1.0] - 2026-07-25
### Added
- **Control the close stack from code** — put a popup in or out of the back stack at runtime, or ask what "back" would close next.
### Changed
- **"Back" is now yours to trigger** — one call closes the top popup, from whatever key or button you like. *Upgrading: add `AdvancedPopupSystem.EscapeStep()` to your own input handling; each popup's **Escape Policy** keeps working as before.*
### Removed
- **The escape key settings and the per-popup Close Key** — APS no longer reads the keyboard at all. *Upgrading: pick the key in your own input code.*

## [2.0.3] - 2026-07-24
### Added
- **Each popup can have its own close key** — a popup set to close on the escape key now has a **Close Key** field right in its inspector. It starts on the project-wide key and keeps following it, so changing that one setting still reaches every popup you left alone; pick another key and only this popup changes. The popup on top always owns the key press, so a key you gave a background popup can never close it from underneath.
- **Preview animations without pressing Play** — the **Preview** button in the popup inspector now works: it plays the popup's show animation, holds it for a second, then plays the hide — right in edit mode, in the scene or in Prefab Mode. When it finishes (or you press **Stop**), the popup snaps back to exactly how it was, so nothing in your scene changes. The built-in Fade, Scale and Slide transitions play for real; custom and DOTween transitions show their end states instead.
### Removed
- **Hotkeys that open and close popups** — the *Show / Hide Key Settings* block is gone from the popup inspector, along with its any-key, layer and popup conditions and its UnityEvent. Closing by key now lives entirely in the escape close stack, where it's one setting instead of two parallel systems. *Upgrading: for closing, set the popup's **Escape Policy** to `Hide` and pick its **Close Key**. For opening by key, call `Show<YourPopup>()` from your own input handling.*
### Changed
- **One switch for keyboard handling** — the separate **Key Event Tracking** toggle is gone; **Escape Close Stack** is now the single switch, and with it off APS installs no per-frame key polling at all. *Upgrading: if you had Key Event Tracking on, make sure **Escape Close Stack** is on too.*

## [2.0.2] - 2026-07-23
### Added
- **Customizable default canvas per layer** — edit one prefab to restyle every layer's popup canvas; saved in your project, so updates keep your changes.
### Changed
- **A layer's canvas is now always set** — filled with the default (override per layer anytime), named `<Layer> - APS Canvas` in the hierarchy.

## [2.0.1] - 2026-07-23
### Added
- **Popups that open with data** — declare the data a popup needs and fill it in one place; APS applies it *before* the popup appears, so it never flashes empty and then fills in. The content is remembered when you reopen the popup, and cleared automatically for pooled copies. There's also a one-liner to tweak a popup right before it opens.
- **One-line toggle by type** — flip a popup open or closed straight by its type, without holding a reference; if it isn't loaded yet it loads and opens.
### Changed
- **One failed popup no longer blocks the rest** — if a popup errors while loading (bad asset, a failed dependency…), APS logs it, names it, and keeps loading the others instead of silently stopping — so a single bad popup can't leave half your UI missing.
- **Clearer show/hide results** — the object returned by show/hide now tells you how it ended (finished, cancelled, or failed) and lets you react to *any* outcome, not only success. *Upgrading: the success callback (`.OnComplete`) now runs on success only — it used to also run after a failed transition. If you relied on that, use the new any-outcome callback instead.*
### Fixed
- **Callbacks never silently vanish** — a completion callback added to a show/hide that already finished now runs right away instead of being dropped, and stacking several callbacks keeps all of them.
- **Safer cancel** — cancelling a show/hide that has already finished is now a harmless no-op.

## [2.0.0] - 2026-07-22
### Changed
- **Works as a Package Manager package** — install APS straight from a Git URL or registry, not just by copying it into your project. Its icons and editor tools now find everything wherever the package lives, and it compiles the moment it's imported.
- **Editing layers is now an explicit choice** — the **Layers** tab has a **Customization** toggle (locked by default). Turn it on to add, rename, or delete layers. On a read-only Package Manager install, turning it on offers to embed the package into your project so your changes can be saved.
### Fixed
- **Your layers can't get corrupted on import** — layer data is saved safely and is never reset by an interrupted or unreadable read, so deleting, re-importing, or updating APS always keeps your layers.
### Upgrading from 1.x
- Your custom layers carry over automatically — they're restored from `ProjectSettings/APS_Layers.json` (on a read-only Package Manager install, turn on **Customization** in the Layers tab to edit them again).
- Custom displays now live in your project under `Assets/AdvancedPopupSystem/`, not inside the package — commit that folder to version control.
- If your popup code lives in its **own** assembly definition, add a reference to **`AdvancedPS.Generated.Layers`** so it can see `PopupLayerEnum` (code in the default `Assembly-CSharp` needs nothing).
- If you previously copied APS into your `Assets` folder, after updating delete the leftover `Assets/advanced-popup-system/Runtime/Generated/PopupLayerEnum.generated.cs` (the enum now lives beside it in `Runtime/Generated/Layers/`, and the stray copy would double-define it).

## [1.25.0] - 2026-07-22
### Added
- **Open a popup in one line** — call any popup straight by its type: it loads from Addressables on the spot if it isn't in the scene yet, then shows (and a matching one-liner hides it). Need the popup object itself? A new await-able getter returns it, loading it first when necessary. No more grabbing a reference or opening a whole layer just to show one screen.

## [1.24.0] - 2026-07-21
### Added
- **Per-scene preload & unload** — each Addressable popup can now pick *which scenes* preload it and *which scenes* free it from memory, right in the inspector. Preload a screen's popups only when their scene loads (default: on the first scene), and drop them again when you move on — so memory follows the player through your game. A popup you open before its preload scene still loads on the spot, no setup needed. Choices are remembered per scene, so reordering your Build Settings never disturbs them.
### Changed
- **Close button is now a module** — the popup's close button moved into the **Modules** box next to Draggable and Resizable: tick **Closable** and drop your button in. One tidy place for every per-popup feature instead of a stray field at the bottom of the inspector. *Upgrading: re-assign your close button under **Modules ▸ Close** and enable **Closable** — the old Close Button field no longer carries over.*

## [1.23.0] - 2026-07-21
### Added
- **Popups on separate canvases** — in **APS ▸ Layers**, give each layer its own sort order and, optionally, its own canvas prefab; APS then puts that layer's on-demand and spawned popups on their own canvas, so your HUD, dialogs, and tooltips sit on independent sort orders instead of stacking on a single overlay. Popups you place in a scene keep their own canvas.
### Changed
- **Pool control in one setting** — a single **Pool Capacity** per popup now decides what happens to its copies when hidden: keep them all for reuse, free their memory immediately, keep just one, or cap how many stay around. This replaces the earlier On-Hide option. *Upgrading: existing Addressable popups read Pool Capacity as `0` (free on hide) — set it to `-1` to keep them resident as before.*

## [1.22.0] - 2026-07-22
### Changed
- **Layers survive updates** — your popup layer set is now saved in your project, so importing a new APS version keeps your custom layers instead of wiping them. *(Upgrading to 1.22.0 may reset them one last time — re-add them in `APS ▸ Layers`; every update after this preserves them.)*
### Fixed
- **Always compiles after import** — a missing generated file is now re-created as valid code instead of an empty file.

## [1.21.0] - 2026-07-19
### Added
- **Addressables support** *(optional)* — mark a popup as Addressable and it loads only when needed: lazily on first show, or quietly preloaded in the background, instead of living in every scene. Setup is automatic, and popups you keep in the scene still work as before — so testing stays easy.
- **Runtime spawning & pooling** — open many copies of the same popup (toasts, list rows) with automatic reuse, plus one-line preloading for a hitch-free first open.
- **Addressables showcase** — a sample scene to try it out: scene, lazy, preloaded, and spawned popups, all draggable.
### Removed
- The unused `AdvancedPopupInstantiate` placeholder — real runtime spawning takes its place.

## [1.19.0] - 2026-07-19
### Added
- **Interaction modules**: per-popup `Draggable` / `Resizable` features via a bit-flag `Modules` field — drag zone, resize grips, and screen/safe-area/custom bounds clamping that works with **any anchors & pivot**.
- Central `PointerEventSystemAPS` (New/Old input) driving interactions through the PlayerLoop — no runtime components; stateless handlers with an extensible `PopupFeatureRegistry`.
- Inspector "Modules" box with conditional per-feature config and a **Generate Grips** button.

## [1.18.0] - 2026-06-17
### Added
- Auto-initialization for `InputSwitcher` using `[RuntimeInitializeOnLoadMethod]` (no longer requires manual placement in the scene).
- "Auto Switch Input Module" toggle in settings to enable/disable automated input switching.
- Standard warning/information boxes (`EditorGUILayout.HelpBox`) in the settings UI.

### Fixed
- Completely rebuilt `KeyEventSystemAPS` key mapping for New Input System using high-performance enum-name caching and manual overrides, resolving reflection/GC allocation overhead and key name mismatch issues.
- Fixed `InvalidOperationException` (collection modified) in both New and Old `KeyEventSystemAPS` instances by switching to `for` loops.
- Fixed memory leaks on scene unloaded and play mode exit in editor by introducing static collection cleanups.
- Fixed double-disposal issue in `Operation.Cancel()` causing `ObjectDisposedException`.
- Corrected logic bug in `APLogger` where message severity filtering did not prioritize log levels properly.
- Resolved potential recursion stack overflow in `ContainsDeepPopup` DFS logic.
- Prevented potential `NullReferenceException` when invoking `.OnComplete()` on popup transitions by returning a proper `NoOp` operation instead of `null`.

## [1.17.0] - 2025-09-29 
### Improved
- Core refactor to **generics** instead of reflection. Faster calls and compile-time safety for displays/settings.
- New **Display Registry** for caching display instances. Cuts lookups and allocations.
- Cleaner folder structure for APS core. Easier navigation and maintenance.
- Better cancellation handling in async show/hide. Fewer stuck operations.
### Added
- **Input System support**: separate “New” and “Old” input assemblies with centralized key tracking (`Input/New` and `Input/Old`, `PopupKeyBinding`). 
- **DoTween integration example** with generated display/settings, asmdef, scene, and prefabs. Shows how to plug custom anim drivers.
- Editor tooling: **PopupSystemEditor**, **PopupDisplaysEditorPanel**, **BaseSettingsDrawer**, **PackageVersionHelper** to streamline setup and inspection.
- Auto-generated stubs for **Fade/Scale/Slide** displays and settings to match the new typed model. 
### Fixed
- Reduced GC pressure and spikes by removing delegate/reflection paths and avoiding redundant allocations.
- More stable show/hide flow across layers due to unified typed pipeline.

## [1.11.2] - 2025-09-20
### Improved
- Core popup system flow reworked for more stable show/hide logic.
- Popup operations made more consistent across layers and nested stacks.
- Inspector settings improved: clearer control of popup layers, deep-stack options, and key tracking states.
- Code generation process more reliable (auto-creates required folders).
- General stability and utility improvements.
- Minor optimizations in core and utilities to reduce overhead and improve editor responsiveness.
### Added
- Support for a new **Input System**: centralized key event tracking for controlling popups.
- Editor helper for managing package version info.
### Fixed
- Popup banner icon now displays correctly in the inspector.
- Generated popup enums and interfaces updated to match the new layering model, preventing mismatches at runtime.

## [1.9.8] - 2024-10-31
### Improved
- SlideDisplay anchor(pivot only) support
- SlideDisplay now can controll size too.
- Key Event Tracnking hide dependent settings in popup inspector when disabled.
- Incode documentation
### Added
- Script field in inspector
- Now you can disable custom APS inspector view by settings panel.
### Fixed
- Display lag in the first frame has been noticed and corrected.

## [1.9.71] - 2024-10-30
### Improved
- Popup Inspector View

## [1.9.7] - 2024-08-17
### Rework
- Incode documentation
### Added
- Deep controls for popup show/hide key bindings.
- CMD show/hide functions.
- Some global states and info in AdvancedPopupSystem.cs

## [1.9.5] - 2024-08-13
### Fixed
- Settings json loading in build
- Freeze when hidding same poup at same frame multiple times
- Refactoring

## [1.9.4] - 2024-08-5
### Fixed
- Editor bugs
- Operation bug
- Editor optimization
- Refactoring

## [1.9.3] - 2024-08-2
### Rework
- Smoother animation by increasing pre-calculated easing steps
### Added
- Logging operation errors
- Some settings for configuring popups
- Start animation event for display settings
### Fixed
- Caching settings and displays
- Incorrect executing `DeepPopups`

## [1.9.2] - 2024-07-30
### Fixed
- Newtonsoft Json dependency
- Inpector for unity white theme
- Validation of Popup Layers and Displays
- Bug with show/hide popup every frame
- Subscribtion/unsubscription

## [1.9.1] - 2024-07-30
### Fixed
- APS Key Event System initialization
- Platform compatibility

## [1.9.0] - 2024-07-30
### Fixed
- Some incorrect behaviors
### Rework
- Optimization
### Added
- New popup settings UI in inspector
- Code generation for Displays, Popups, APS
- Configuring keys for show/hide popup
- APS Displays Editor
### Removed
- Manual displays & settings classes
### Updated
- Documentation in code (tips)

## [1.2.1] - 2024-7-28
### Fixed
- Animations lineary
- Incorrect events behavior
### Rework
- Popup Layer Editor
- Settings for animations (duration for ex.)
### Added
- Bunner in AP Layer Editor
- Incorrect enum names validator
- Icon for Popups in inspector & hierarchy
- Manual control of popups (PopupShow/PopupHide)
- Auto-save checkbox & Save button in AP Layer Editor
### Remove
- Obsolete initialization for displays
### Updated
- Documentation in code (tips)

## [1.1.0] - 2024-7-19
### Fixed
- Working with Treads
- Depending animations speed from FPS
- Rare bug with editing popups in scene after stopping play mode in editor
### Rework
- Documentation in scripts
- Popup view in inspector
- Access to Displays parameters
### Added
- Layers Editor
### Remove
- Obsolete fields

## [1.0.5] - 2024-7-19
### Fixed
- Reopening popups

## [1.0.4] - 2023-4-21
### Remove
- Show/hide popups by name
- Old demo's
### Added
- Caching IAdvancedPopupDisplay for popups
- Show/hide popup without generic methods by cached IAdvancedPopupDisplay type
### Rework
- Tooltip's $ summary's updated

## [1.0.3] - 2023-2-24
### Rework
- Improvement and polishing of systems after combat use in projects

## [1.0.2] - 2022-11-14
### Fixed
- Fixed layers~

## [1.0.1] - 2022-11-10
### Fixed
- Fixed description~
### Added
- Functionality for managing popups by layer (enumeration)

## [1.0.0] - 2022-10-28
### Fixed
- Fixed Samples~
- Fixed bug with missing .meta files
### Added
- LICENSE.md
- CHANGELOG.md