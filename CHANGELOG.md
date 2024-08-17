# Changelog
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