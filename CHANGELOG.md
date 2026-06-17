# Changelog
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