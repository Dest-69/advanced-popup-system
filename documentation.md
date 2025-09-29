# Advanced Popup System Documentation

The Advanced Popup System is a small framework for managing UI popups in Unity. This document keeps the
public surface simple and focuses on the parts you will touch most often when wiring the package into a
project.

---

## 1. Core Building Blocks

### 1.1 Types
- **PopupLayerEnum** – Flags enum that groups popups by purpose (login, settings, etc.). Multiple flags can be
  combined to describe complex layouts.
- **AdvancedPopup** – Base MonoBehaviour that implements `IAdvancedPopup`. It handles lifecycle events,
  optional close buttons, and the async helpers described below.
- **Operation** – Lightweight wrapper returned by show/hide calls. Await `OnComplete` to run code after the
  animation, or call `Cancel()` to stop a transition in progress.

### 1.2 Global collections
- **AllPopups** – Every registered popup, whether visible or not.
- **ActivePopups** – Only the popups that are currently shown.
- **ActiveLayer** – Bitmask that stores which `PopupLayerEnum` values are currently active.

---

## 2. Everyday API Calls

### 2.1 Lookup helpers
- `TryGetPopup<T>(out T popup, bool activeOnly = true)` – Returns the first popup of type `T`. Pass
  `activeOnly = false` to include hidden popups.
- `GetPopupByLayer(PopupLayerEnum layer, bool activeOnly = true)` – Finds the first popup that matches a
  specific layer mask.
- `GetPopupByName(string name, bool activeOnly = true)` – Searches by the GameObject name.

### 2.2 Showing and hiding
- `Show()`, `Hide()`, `ShowAsync()`, `HideAsync()` – Instance methods on `AdvancedPopup`. Async variants return
  an `Operation` you can await.
- `LayerShow(...)` – Static helper on `AdvancedPopupSystem` that reveals every popup matching the layer mask.
  Optional parameters let you hide other layers, apply custom `IDisplaySettings`, or choose a display type.
- `LayerHide(...)` – Static helper that hides popups in the provided layer. You can pass custom animation
  settings here as well.
- `HideAll()` / `HideAll<T>()` – Close every registered popup (optionally filtered by type) using the chosen
  display settings.

### 2.3 Manual registration
- `InitAdvancedPopup(AdvancedPopup popup)` – Adds a popup to the global collections. Normally called during
  `Init()`.
- `DeactivateAdvancedPopup(AdvancedPopup popup)` – Removes a popup from tracking. Automatically invoked from
  `OnDestroy`.

---

## 3. Typical Workflow

1. Add a component that derives from `AdvancedPopup` to the popup root `RectTransform`.
2. Configure the inspector fields:
   - `PopupLayer` – Assign the relevant `PopupLayerEnum` value(s).
   - `ManualInit` – Enable if the popup is spawned at runtime and you plan to call `Init()` yourself.
   - `AutoHideOnInit` – Automatically hides the popup after initialization.
   - `DeepPopups` – Allows child popups to follow the parent show/hide calls.
   - `KeyBinding*` – Optional shortcuts that trigger `Show()` or `Hide()`.
3. Call `Show()` / `Hide()` (or their async variants). Use `AdvancedPopupSystem.LayerShow/LayerHide` when you
   want to operate on groups of popups by layer.
4. Subscribe to the `Operation.OnComplete` callback to run post-animation logic. Cancel long transitions by
   calling `Operation.Cancel()` if the popup is no longer needed.

---

## 4. Animation and Display Settings

- Use `SetCachedDisplay` to assign default `IDisplay` + `IDisplaySettings` pairs to a popup. Generic overloads
  let you pick different settings for showing vs. hiding.
- `DisplayRegistry` caches display instances so that repeated show/hide calls reuse the same objects instead of
  allocating new ones.
- You can swap the display type at runtime by passing custom settings directly into `Show()`/`Hide()` or the
  layer helpers.

---

## 5. Practical Tips

- Keep layer assignments accurate. `LayerShow` with `autoHide = true` hides other layers and keeps
  `ActiveLayer` in sync. Leave `autoHide` false when you want multiple layers to remain visible.
- Wire `Cmd_Show` / `Cmd_Hide` to `UnityEvent` buttons if you need inspector-only configuration.
- When instantiating popups through Addressables or other factories, set `ManualInit = true`, call `Init()`
  manually, then show the popup.
- Depth hierarchies are handled for you: `DeepPopups` propagates show/hide calls to children, and the async
  helpers already wait for nested popups to finish.

---

## 6. Troubleshooting Checklist

- **Popup never appears** – Confirm `Init()` has been called and the popup is registered (`AllPopups` contains
  it). Also check that the GameObject is active in the hierarchy.
- **Wrong popup hides** – Inspect the layer flags. Bitwise combinations are easy to misconfigure.
- **Animation plays twice** – Ensure only one of `Show()` or `LayerShow()` is called for the same popup.
- **Async flow stuck** – Verify that every custom display invokes its completion callback; otherwise
  `Operation.OnComplete` will never fire.

This cheat-sheet should cover the 90% path. Extend or specialise it as your project requires.
