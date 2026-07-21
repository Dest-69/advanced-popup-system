# Advanced Popup System — Documentation

The **Advanced Popup System (APS)** is a modular, high-performance UI popup framework for Unity. It provides
automatic registration, layer-based grouping, a pluggable animation pipeline (custom easing, built-in Fade / Scale /
Slide, and optional DOTween), and `Task`-based async/await transitions with first-class cancellation.

This is the complete technical guide: how to set popups up and how to drive the public API.

---

## 📋 Table of Contents

1. [Core Building Blocks](#1-core-building-blocks)
2. [Step-by-Step Setup Guide](#2-step-by-step-setup-guide)
3. [API Reference & Code Examples](#3-api-reference--code-examples)
4. [Animations & Custom Transitions](#4-animations--custom-transitions)
5. [The APS Editor Window](#5-the-aps-editor-window)
6. [Advanced Configuration](#6-advanced-configuration)
7. [Settings & Logging](#7-settings--logging)
8. [Troubleshooting Checklist](#8-troubleshooting-checklist)
9. [On-Demand Loading with Addressables](#9-on-demand-loading-with-addressables)

---

## 1. Core Building Blocks

### 1.1 Core types

- **`AdvancedPopup`** — the `MonoBehaviour` you attach to a popup. It manages the lifecycle (`Init` → `Subscribe` /
  `Unsubscribe`), wires an optional close button, plays show/hide animations, and propagates to child ("deep") popups.
  Your popups inherit from it.
- **`IAdvancedPopup`** — the **abstract base class** `AdvancedPopup` derives from (namespace `AdvancedPS.Core.System`;
  the `I` prefix is historical — it is not an interface). It holds every inspector field, the cached display/settings,
  and `Init()`.
- **`AdvancedPopupSystem`** — the static manager. It registers/looks up popups and orchestrates layer-level show / hide.
- **`Operation`** — the lightweight object returned by every non-`async` show/hide call. Chain a callback with
  `.OnComplete(...)` or abort the running transition with `.Cancel()`.
- **`PopupLayerEnum`** — a generated `[Flags]` enum (see `PopupLayerEnum.generated.cs`) that groups popups into logical
  screens (e.g. `GUI`, `GAME`, `MENU`). Several flags can be active at once. Edit it from the APS **Layers** panel.
- **Displays & Settings** — a *display* runs an animation, a *settings* object holds its tunables. Built in:
  `FadeDisplay`/`FadeSettings`, `ScaleDisplay`/`ScaleSettings`, `SlideDisplay`/`SlideSettings`, and (optional)
  `DoTweenDisplay`/`DoTweenSettings`. `EasingType` provides 30 easing curves.
- **`APSStats`** — runtime counters (`ActiveOperationsCount`, `ActiveTasksCount`) for monitoring live transitions.

### 1.2 Global collections (on `AdvancedPopupSystem`)

- **`AllPopups`** — every popup present in loaded scenes (visible or not).
- **`ActivePopups`** — only the popups currently visible.
- **`ActiveLayer`** — a bitmask of the layers currently shown. **Changed only by the `Layer*` / `HideAll` APIs** — a
  manual `popup.Show()` / `Hide()` updates `ActivePopups` but not `ActiveLayer`.

---

## 2. Step-by-Step Setup Guide

### Step 1: Create the popup GameObject

Fastest path: **`GameObject ▸ UI ▸ Advanced Popup`** — this creates a stretched popup under a `Canvas` (making the
Canvas if needed) with an `AdvancedPopup` component already attached.

Manually: under your UI `Canvas`, add an empty GameObject (e.g. `SettingsPopup`), then your visuals and buttons as
children. Optionally add a close button in the hierarchy and link it under **Modules ▸ Close** (tick `Closable`) — see
[§6.5](#65-modules-drag-resize--close).

> [!NOTE]
> `RectTransform` and `CanvasGroup` are required, but APS **adds them automatically** during `Init()` if missing.

### Step 2: Write your popup script

Create a class inheriting from `AdvancedPopup` (namespace `AdvancedPS.Core`). Override `Subscribe` / `Unsubscribe` to
wire local UI events — always call `base`, and keep them symmetric:

```csharp
using AdvancedPS.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame.UI
{
    public class MainMenuPopup : AdvancedPopup
    {
        [Header("Main Menu UI")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _settingsButton;

        protected override void Subscribe()
        {
            base.Subscribe(); // wires the Close module button, fires OnShowing, adds to ActivePopups

            _playButton.onClick.AddListener(OnPlayPressed);
            _settingsButton.onClick.AddListener(OnSettingsPressed);
        }

        protected override void Unsubscribe()
        {
            base.Unsubscribe(); // unwires the Close module button, fires OnHided, removes from ActivePopups

            _playButton.onClick.RemoveListener(OnPlayPressed);
            _settingsButton.onClick.RemoveListener(OnSettingsPressed);
        }

        private void OnPlayPressed() => Hide();

        private void OnSettingsPressed() =>
            AdvancedPopupSystem.LayerShow(PopupLayerEnum.OVERLAY, autohide: false);
    }
}
```

`Subscribe` runs at the start of a show, `Unsubscribe` at the start of a hide. If you don't override `Init`, the popup
uses the default **Scale** transition (see [§4](#4-animations--custom-transitions) to pick another).

### Step 3: Configure in the Inspector

| Field | Meaning |
| :--- | :--- |
| **Popup Layer** | One or more layer flags this popup belongs to (used by `LayerShow` / `LayerHide`). |
| **Modules** | Optional per-popup features — **Draggable**, **Resizable**, and **Closable** (a close button that calls `Hide()` when clicked). See [§6.5](#65-modules-drag-resize--close). |
| **Auto Hide On Init** | Keep `true` so the popup starts hidden. Set `false` only for UI shown immediately on scene start. |
| **Manual Init** | Keep `false` for scene popups. Set `true` if you instantiate at runtime and want to call `Init()` yourself. |
| **Inactive** | `true` prevents the popup from ever showing (a hard gate on `Show`). |
| **Escape Policy** | How the popup reacts to the escape close key: `Hide`, `Ignore`, or `Block` (see [§6.3](#63-escape-close-stack)). |
| **Deep Popups** | Child/dependent popups that mirror this popup's show/hide (see [§6.1](#61-deep-popups)). |
| **Key Binding Show / Hide Settings** | Hotkeys that toggle the popup (see [§6.2](#62-hotkey-bindings)). |

---

## 3. API Reference & Code Examples

### 3.1 Finding popups

```csharp
using AdvancedPS.Core;
using UnityEngine;

public class GameFlowController : MonoBehaviour
{
    public void OpenMenu()
    {
        // By type (active or inactive). The non-activeOnly path is O(1) via an internal type cache.
        if (AdvancedPopupSystem.TryGetPopup<MainMenuPopup>(out var menu, activeOnly: false))
            menu.Show();
        else
            Debug.LogError("MainMenuPopup was not found in the scene!");
    }

    public void Inspect()
    {
        // By layer (first match) …
        IAdvancedPopup settings = AdvancedPopupSystem.GetPopupByLayer(PopupLayerEnum.OVERLAY, activeOnly: true);
        // … or by GameObject name (case-sensitive).
        IAdvancedPopup byName = AdvancedPopupSystem.GetPopupByName("SettingsPopup", activeOnly: false);
    }
}
```

### 3.2 Showing & hiding

Every non-`async` call returns an **`Operation`**. Two workflows:

**A. Callback-driven (`Operation`)**

```csharp
_infoPopup.Show().OnComplete(() => Debug.Log("Fully visible!"));
_infoPopup.Hide().OnComplete(() => Debug.Log("Fully closed."));

// Abort a running transition:
Operation op = _infoPopup.Show();
op.Cancel();
```

> [!NOTE]
> `.OnComplete` fires only if the operation was **not** cancelled. Starting a new show/hide on the same popup
> automatically cancels the one in flight.

**B. Async-first (`async/await`)**

`ShowAsync` / `HideAsync` accept a `CancellationToken`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core;
using UnityEngine;

public class AsyncTrigger : MonoBehaviour
{
    [SerializeField] private AdvancedPopup _dialog;
    private CancellationTokenSource _cts;

    public async void OpenDialogAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            await _dialog.ShowAsync(_cts.Token);
            Debug.Log("Dialog fully open!");
        }
        catch (System.OperationCanceledException)
        {
            Debug.LogWarning("Show cancelled.");
        }
    }

    private void OnDestroy() { _cts?.Cancel(); _cts?.Dispose(); }
}
```

**Toggle & Inspector commands**

- `SwitchShowHide()` (and `SwitchShowHideAsync`, plus `<T>` variants) — show if hidden, hide if visible.
- `Cmd_Show()`, `Cmd_Hide()`, `Cmd_SwitchShowHide()` — `void` methods designed for wiring to `Button.onClick` /
  `UnityEvent` fields directly in the Inspector.
- `OnShowing` / `OnHided` (`Action` on `AdvancedPopup`) — fire from `Subscribe` / `Unsubscribe`; subscribe to react to
  visibility changes.

### 3.3 Layer controls

```csharp
using AdvancedPS.Core;

// Switch screens: show MENU and hide every other active layer.
AdvancedPopupSystem.LayerShow(PopupLayerEnum.MENU, autohide: true);

// Overlay: show OVERLAY on top without closing the current screen.
AdvancedPopupSystem.LayerShow(PopupLayerEnum.OVERLAY, autohide: false);

// Hide one layer, or everything.
AdvancedPopupSystem.LayerHide(PopupLayerEnum.OVERLAY);
AdvancedPopupSystem.HideAll();
```

### 3.4 Overriding the animation per call (generics)

Any show/hide can run a **specific display type** for that call instead of the popup's cached one — handy for a
one-off transition or a screen-wide effect:

```csharp
// One popup, this call only, with explicit Fade settings:
myPopup.Show<FadeDisplay>(new FadeSettings { Duration = 0.25f });

// A whole layer with a chosen display / settings:
AdvancedPopupSystem.LayerShow<ScaleDisplay>(PopupLayerEnum.MENU, new ScaleSettings { Duration = 0.4f });

// Different displays for show vs hide across a transition:
AdvancedPopupSystem.LayerShow<SlideDisplay, FadeDisplay>(PopupLayerEnum.MENU, slideIn, fadeOut);

// Layer hide / hide-all with a chosen display:
AdvancedPopupSystem.LayerHide<FadeDisplay>(PopupLayerEnum.OVERLAY);
AdvancedPopupSystem.HideAll<FadeDisplay>();
```

---

## 4. Animations & Custom Transitions

### 4.1 Built-in displays

Each popup has a **show** display and a **hide** display. Ship-included:

| Display | Settings | Animates | Key fields |
| :--- | :--- | :--- | :--- |
| **`ScaleDisplay`** *(default)* | `ScaleSettings` | `transform.localScale` | `Duration`, `Easing`, `ShowScale` (`one`), `HideScale` (`zero`) |
| **`FadeDisplay`** | `FadeSettings` | `CanvasGroup.alpha` (+ scale one/zero) | `Duration`, `Easing`, `MaxValue` (`1`), `MinValue` (`0`) |
| **`SlideDisplay`** | `SlideSettings` | `RectTransform.anchoredPosition` + `sizeDelta` | `Duration`, `Easing`, `TargetRectPosition`, `TargetRectSize` |
| **`DoTweenDisplay`** | `DoTweenSettings` | any DOTween `Sequence` | `Factory` (build callback), `Recyclable`, `AutoKill`, `Link` |

> [!NOTE]
> All settings expose `OnAnimationStart` / `OnAnimationEnd` actions and an `UnscaledTime` flag — set it `true` so the
> popup keeps animating while the game is paused (`Time.timeScale == 0`), e.g. a pause menu; otherwise the transition
> waits for time to resume. `Easing` accepts any of the 30 `EasingType` curves (`Linear`, `EaseInOutQuad`,
> `EaseOutBack`, `EaseInOutElastic`, `EaseOutBounce`, …).

> [!NOTE]
> **`SlideDisplay`** is pivot/anchor-based (for min–max stretch anchors, wrap the popup in an empty parent and slide
> that). Both show and hide lerp toward the same `TargetRectPosition`/`TargetRectSize`, and hide then collapses scale —
> so it reads as a slide-**in**. For a distinct slide-**out**, pair it with another hide display via
> `SetCachedDisplay<SlideDisplay, T>(...)`.

### 4.2 Choosing a transition on your popup

Override `Init()`, call `SetCachedDisplay(...)`, then call `base.Init()` **last**:

```csharp
public override void Init()
{
    // Same display for show and hide, custom settings:
    SetCachedDisplay<FadeDisplay>(new FadeSettings { Duration = 0.3f, Easing = EasingType.EaseOutQuad });

    // Or different displays per direction:
    // SetCachedDisplay<SlideDisplay, FadeDisplay>(slideInSettings, fadeOutSettings);

    base.Init(); // finalizes cache, ensures components, applies auto-hide, registers with APS
}
```

> [!IMPORTANT]
> Call `SetCachedDisplay(...)` **before** `base.Init()`. `base.Init()` reads the cached display to apply the initial
> auto-hide; if the cache is still empty it falls back to **Scale**. (Setting it first is what every example does.)

### 4.3 DOTween transitions

`DoTweenDisplay` (compiled only when DOTween is present) runs an arbitrary `Sequence` built per run:

```csharp
using AdvancedPS.Core;
using DG.Tweening;
using UnityEngine;

public class CustomTweenPopup : AdvancedPopup
{
    public override void Init()
    {
        SetCachedDisplay(
            // Show
            DoTweenSettings.Create((rect, seq) =>
            {
                seq.Append(rect.DOScale(Vector3.one, 0.5f).From(Vector3.zero).SetEase(Ease.OutBack))
                   .Join(rect.DORotate(new Vector3(0, 0, 360), 0.5f, RotateMode.FastBeyond360));
            }),
            // Hide
            DoTweenSettings.Create((rect, seq) =>
            {
                seq.Append(rect.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
            })
        );

        base.Init();
    }
}
```

The display kills the sequence on cancellation and cleans it up (`AutoKill` / `Link`), so aborted transitions don't
leak tweens. Fluent knobs: `.WithUnscaledTime(true)`, `.WithAutoKill(false)`, etc.

### 4.4 Writing a custom display

Two ways — the **Displays** panel scaffolds the files for you ([§5](#5-the-aps-editor-window)), or write them by hand.
A display is **stateless** (one instance is shared and reused): keep per-run state in locals or on the transform.

**1) Settings** — inherit `BaseSettings<TDisplay>`:

```csharp
using System;
using AdvancedPS.Core.System;

[Serializable]
public class RotateSettings : BaseSettings<RotateDisplay>
{
    public float Duration = 0.5f;
    public EasingType Easing = EasingType.EaseInOutQuad;
    public float TargetAngle = 180f;
}
```

**2) Display** — inherit `DisplayBase<TSettings>` and implement the four methods:

```csharp
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;

public class RotateDisplay : DisplayBase<RotateSettings>
{
    public override void ShowInstantlyMethod(RectTransform t, RotateSettings s) =>
        t.localRotation = Quaternion.Euler(0, 0, s.TargetAngle);

    public override void HideInstantlyMethod(RectTransform t, RotateSettings s) =>
        t.localRotation = Quaternion.identity;

    public override async Task ShowMethod(RectTransform t, RotateSettings s, CancellationToken token)
    {
        s.OnAnimationStart?.Invoke();
        float elapsed = 0f;
        Quaternion from = t.localRotation, to = Quaternion.Euler(0, 0, s.TargetAngle);
        while (elapsed < s.Duration)
        {
            if (TaskUtils.OperationCancelled(token)) return;
            elapsed += Time.deltaTime;
            float eased = EasingFunctions.Get(s.Easing, elapsed / s.Duration);
            t.localRotation = Quaternion.LerpUnclamped(from, to, eased);
            await Task.Yield();
        }
        t.localRotation = to;
        s.OnAnimationEnd?.Invoke();
    }

    public override async Task HideMethod(RectTransform t, RotateSettings s, CancellationToken token)
    {
        // mirror of ShowMethod, lerping back to Quaternion.identity
    }
}
```

**3) Cache it** on the popup:

```csharp
public override void Init()
{
    SetCachedDisplay<RotateDisplay>(new RotateSettings { Duration = 0.8f, TargetAngle = 360f });
    base.Init();
}
```

> [!TIP]
> For factory defaults, add a `public static TSettings Default()` to your settings class — APS prefers it over the
> parameterless constructor when producing default settings.

---

## 5. The APS Editor Window

Open from the top **`APS`** menu — one window, three tabs:

- **`APS ▸ Layers`** — add / rename / delete `PopupLayerEnum` flags. Names are normalized to `UPPER_CASE` and may use
  Latin letters, digits and underscore (a name can't start with a digit); the enum file is **regenerated** on save
  (up to 31 flags). Do not hand-edit `PopupLayerEnum.generated.cs` — your edits are
  overwritten here. Your layer set is also saved **outside** the package, in `ProjectSettings/APS_Layers.json`, so
  **updating APS never wipes your custom layers** — the enum is automatically restored from that file when the new
  version is imported.
- **`APS ▸ Displays`** — add a new display: APS generates `<Name>Display/<Name>Display.generated.cs` +
  `<Name>Settings.generated.cs` with ready-to-fill method stubs. Generation never overwrites an existing display, and
  delete removes the pair. The `Display` / `Settings` suffixes and folder name are required by the tooling — keep them.
- **`APS ▸ Settings`** — see [§7](#7-settings--logging).

Both Layers and Displays have an **Auto-Save** toggle; with it off, use the **Save** button to apply changes.

---

## 6. Advanced Configuration

### 6.1 Deep popups

Add child/dependent popups to a parent's **Deep Popups** list. When the parent shows or hides:

- the operation propagates to each deep popup,
- all animations run **in parallel**,
- the parent's `ShowAsync` / `HideAsync` resolves only after **all** child animations complete.

Cycles are safe — APS traverses with a visited-set DFS (`ContainsDeepPopup`).

### 6.2 Hotkey bindings

Configure `Key Binding Show Settings` / `Key Binding Hide Settings` on the popup to toggle it via keys (handled by
`KeyEventSystemAPS`):

- **`Any Hot Key`** — trigger on any key.
- **`Hot Keys`** — specific keys (e.g. `Escape`, `Tab`).
- **`Layers`** — only when one of these layers is active (empty = no layer restriction).
- **`Popups`** — only when these popups are visible (empty = no restriction).
- **`On Trigger`** — a `UnityEvent` fired when the key triggers.

A key only fires when the popup's ancestor popups are all visible, so nested popups' keys are context-aware. APS works
with **both** the legacy Input Manager and the new Input System (auto-selected). With the new Input System, enable
**Auto Switch Input Module** (see [§7](#7-settings--logging)) to have APS replace `StandaloneInputModule` with
`InputSystemUIInputModule` automatically.

### 6.3 Escape close stack

One key (default `Escape`) steps back through open popups — each press closes the **most recently shown** popup, like
the Android back button. Opt in via `APS ▸ Settings ▸ Escape Close Stack` (see [§7](#7-settings--logging)); the key
also requires **Key Event Tracking** to be on.

On a key press APS walks the visible popups from the most recently shown to the oldest and applies the first relevant
popup's **Escape Policy** (an inspector field on every popup):

| Escape Policy | Behavior |
| :--- | :--- |
| `Hide` (default) | The popup closes (together with its deep popups) and the press is consumed. |
| `Ignore` | The popup is transparent — the press falls through to the popup shown before it. |
| `Block` | The press is consumed but nothing closes — for modal dialogs that must not be escaped. |

**Grouping.** Popups shown by their parent's cascade (via **Deep Popups**) don't get their own step — closing the
parent hides the whole group at once. A deep popup you later show **individually** (a nested dialog on top of its
parent) gets its own step: the key closes it first, then its parent.

Notes:

- A consumed press eats the whole frame — it can't also trigger a hotkey binding from [§6.2](#62-hotkey-bindings).
- The walk skips popups that are already hiding, so pressing repeatedly during animations steps on responsively.
- `AdvancedPopupSystem.EscapeStep()` runs one step programmatically (returns `false` if nothing consumed it) — wire it
  to a UI "Back" button for the same behavior without the keyboard. It works even with the key/toggle disabled.
- With the legacy Input Manager on Android the hardware Back button arrives as `Escape`, so the stack can double as
  back-button navigation.

### 6.4 Instantiating popups at runtime

Set **Manual Init** on the prefab, instantiate it, inject any data, then call `Init()` yourself before showing:

```csharp
var popup = Instantiate(_popupPrefab, _canvasRoot);
// popup.SetData(...);
popup.Init();   // registers with APS and applies auto-hide
popup.Show();
```

For loading popups from **Addressables** on demand (lazy / preload) and spawning many pooled copies, see
[§9](#9-on-demand-loading-with-addressables).

### 6.5 Modules (drag, resize & close)

A popup's inspector has a **Modules** box with a `Features` flag field: tick a feature and its config block appears
below. Features are **data on the popup**, not extra components. Three are built in — **Draggable**, **Resizable** and
**Closable**.

**Draggable** and **Resizable** are runtime **pointer interactions**: a single central pointer system drives every
popup (nothing is added to the scene at runtime), works with both the legacy Input Manager and the new Input System, and
both **clamp the popup to a bounds rect for any anchors / pivot** — center-anchored, corner-anchored or stretched popups
all stay on screen. **Closable** is simpler — a button that hides the popup (see below).

**Draggable** (`Modules ▸ Drag`):

- **Drag Zone** — the RectTransform that starts a drag (e.g. a title bar). Leave empty to drag by the whole popup.
- **Bounds** — where the popup is kept: `Canvas` (the canvas rect ≈ the screen, default), `SafeArea` (notch-safe),
  `Custom` (a RectTransform you assign in **Custom Bounds**), or `None` (no clamping).
- **Padding** — extra inset (px) on each side of the bounds.

**Resizable** (`Modules ▸ Resize`):

- **Grips** — the handles the pointer grabs, each with a direction (edge/corner). Click **Generate Grips** to create a
  default 8-handle set under a dedicated `[Grips]` child (added last so it sits on top); then move/scale the handles to
  taste. Grips are plain RectTransforms — add your own `Image` if you want them visible in play mode.
- **Min Size / Max Size** — size limits in px (0 on an axis = unlimited).
- **Bounds / Custom Bounds / Padding** — same clamping options as drag.
- **Change Cursor** — while the pointer is over a grip, the OS cursor switches to a directional arrow
  (↔ / ↕ for edges, ╲ / ╱ for corners). Untick to turn the feedback off for this popup.
- **Cursors** — the icon set used for that feedback. Leave empty to use the built-in arrows, or assign your own
  `ResizeCursorSet` to re-skin them in one place — create one via **Assets ▸ Create ▸ Advanced Popup System ▸ Resize
  Cursor Set** and drop four cursor textures in. The cursor is drawn at the texture's **exact pixel size** (so the
  texture size *is* the on-screen size — the built-ins are 24×24; keep yours small), set **Hotspot** to the texture's
  center, and import each with **Read/Write enabled + uncompressed** (Unity's **Cursor** texture type does this).
  (Turn the feature off everywhere with `PopupInteractionSystem.CursorFeedbackEnabled = false`.)

Resizing keeps the edge opposite the grabbed grip fixed and honors the pivot; it targets popups with **fixed
(non-stretched) anchors** on the resized axis.

**Closable** (`Modules ▸ Close`):

- **Close Button** — a `Button` that hides the popup when clicked (APS calls `Hide()` for you). Leave empty for none.
  It is wired only while the popup is shown and unwired on hide, so it never fires on a hidden popup. Unlike drag and
  resize this is a plain button click, not a pointer gesture, so it has no handler to register.

**Extending.** The pointer features (drag/resize) are each a stateless `IPopupFeatureHandler` resolved by flag from
`PopupFeatureRegistry`. Register your own to add or override behavior:

```csharp
PopupFeatureRegistry.Register(PopupFeatureEnum.Draggable, new MyDragHandler(), prepend: true);
```

**Notes:** interaction is active only while a popup is fully shown, and a gesture cancels if the popup hides mid-drag.
Hit-testing uses the popup's own zone/grip rects (not the EventSystem raycaster), so it doesn't account for unrelated UI
drawn on top; the topmost popup under the pointer wins.

---

## 7. Settings & Logging

`APS ▸ Settings` (persisted to `Assets/Resources/AP_Settings.json` in your project):

- **Key Event Tracking** — enable/disable the hotkey system globally.
- **Auto Switch Input Module** — (new Input System) auto-swap the EventSystem's input module at startup.
- **Escape Close Stack** — one key steps back through open popups (see [§6.3](#63-escape-close-stack)). Off by default;
  needs Key Event Tracking on.
- **Escape Close Key** — the key driving the escape close stack (default `Escape`).
- **Inspector View** — `APSInspector` (full custom), `APSOptimized` (lighter), or `UnityInspector` (default Unity view).
- **Log Type** — verbosity filter for APS logs, routed through `APLogger`:

| Log Type | Emits |
| :--- | :--- |
| `Info` | info + warnings + errors (most verbose) |
| `Warning` | warnings + errors (default) |
| `Error` | errors only |
| `None` | nothing (exceptions still surface) |

---

## 8. Troubleshooting Checklist

> [!WARNING]
> If a popup misbehaves, check these first.

- **Popup never appears / stays invisible**
  - Confirm `Init()` ran — `Manual Init` must be `false`, or you must call `Init()` after instantiating.
  - Ensure the GameObject and its parent Canvas are active.
  - Verify it's registered: it should be in `AdvancedPopupSystem.AllPopups`.
  - A missing `CanvasGroup` logs a warning — APS adds one in `Init()`, but a display run before init can warn.

- **Transition sticks / `OnComplete` or `await` never resolves**
  - A custom display must exit its loop (respect `Duration` and `TaskUtils.OperationCancelled`) and not throw silently.
  - Don't cancel a transition immediately after starting it (a new show/hide cancels the previous one).

- **Wrong popups open/close with `LayerShow` / `LayerHide`**
  - Check each popup's **Popup Layer** flags in the Inspector.
  - Remember `LayerShow(..., autohide: true)` hides all other layers; use `autohide: false` to overlay.
  - Mixing manual `Show()/Hide()` with layer calls can desync `ActiveLayer` from what's actually visible.

- **DOTween display missing** — the `DoTweenDisplay` compiles only when DOTween is installed (behind the `DOTWEEN`
  define).

- **Hotkeys/clicks not registering (new Input System)** — enable **Auto Switch Input Module**, or make sure your
  EventSystem uses `InputSystemUIInputModule`.

---

## 9. On-Demand Loading with Addressables

Popups can load from **Addressables** on demand instead of living in every scene — lazily on first show, preloaded on
boot, or spawned in many pooled copies. It's **optional**: install Unity's **Addressables** package to enable it;
without it APS works exactly as before (scene-only). APS never hard-depends on Addressables.

### 9.1 Make a popup Addressable

On the popup **prefab**, open the **Addressable** box in the inspector and tick **Addressable**. A few options appear:

- **Load Mode** — `Lazy` (default: loaded on first show) or `Preload` (also loaded up-front — see §9.3).
- **Preload Scenes** *(Preload only)* — a scene checklist. Leave it **empty** (the default) and the popup preloads on
  the very first scene; **check specific scenes** to preload it only when those load. Shown only for Preload popups.
- **Unload Scenes** — a checklist of scenes on **entering** which this popup is released from memory. Works for any
  Addressable popup (Lazy or Preload). Empty (default) = never unloads.

> Scene choices are stored by the scene's **asset identity, not its Build Settings position** — so reordering,
> adding, or removing scenes in Build Settings never shifts what a popup preloads or unloads. (Renaming or moving a
> scene file is handled automatically too.)
- **Pool Capacity** (Pool box) — one value for what happens to the popup's instances when hidden or despawned:
  `-1` keep unlimited (default: stays in memory like a scene popup), `0` despawn on hide (free its memory; reloads next
  show), `1` a single on/off instance (kept for reuse, no pool spun up), or `N` (2+) a pool of up to N idle copies. An
  info box in the inspector spells this out; a slider (−1…64) sets common values while the field still takes larger ones
  (see [§9.4](#94-spawn-many-copies-toasts-list-rows)).

The editor then **auto-adds the prefab to an "Advanced Popup System" Addressables group** and regenerates an internal
index — no manual Addressables wiring. Flag it on the *prefab asset* (not a scene instance), and give each Addressable
popup a **distinct `AdvancedPopup` subclass** — the index is keyed by type.

### 9.2 Show it — same API

Nothing changes at the call site. `LayerShow` loads the layer's Addressable popups if they aren't present yet, then
shows them:

```csharp
// MENU has an Addressable popup that isn't in the scene → it loads, then shows.
await AdvancedPopupSystem.LayerShow(PopupLayerEnum.MENU);
```

**Testing one screen stays trivial:** if a popup of that type is already in the loaded scene, APS uses it and skips
Addressables entirely — drop the prefab into a test scene and press Play, exactly like a normal popup.

> `TryGetPopup<T>` is synchronous, so it returns a popup only once it's resident (in a scene, preloaded, or already
> loaded). Use `Preload` (with its scene reached) for popups you want to fetch synchronously.

### 9.3 Preload & unload per scene

A **Preload** popup loads eagerly so its first show is instant. **Preload Scenes** decides *when*: on each scene load,
a Preload popup that wants that scene is loaded if it isn't already. Leave the checklist **empty** (the default) and it
loads on the very first scene — the classic boot preload. **Check** specific scenes to spread loading across your game
(e.g. preload the shop popups only when the Hub scene loads).

A popup requested **before** its preload scene is reached simply **loads on demand** at that moment — it never fails or
falls back to nothing. Preload is only a "load it earlier when possible" hint, never a gate.

**Unload Scenes** is the mirror: on entering a checked scene, the popup is **released from memory** (its Addressables
handle freed so the asset can unload) — for any Addressable popup, Lazy or Preload. Empty (the default) keeps it
resident; a currently-visible popup is left alone. Use it to drop a screen's popups when you leave it (e.g. unload the
main-menu popups once the Game scene loads).

Scenes are remembered by their **asset identity**, so this configuration is safe to keep across project changes:
reordering, adding, or removing scenes in Build Settings never re-points a popup's preload/unload choices.

To gate a loading screen yourself, await the manual preload (loads every Preload popup now, regardless of scene):

```csharp
await AdvancedPopupSystem.PreloadAll();                       // every Preload-flagged popup
await AdvancedPopupSystem.PreloadLayer(PopupLayerEnum.MENU);   // just one screen
```

### 9.4 Spawn many copies (toasts, list rows)

For popups you need in multiple instances, spawn and manage them yourself. Spawned popups are pooled and are **not**
part of layer batches, but a visible one still closes on Escape / `HideAll`:

```csharp
var toast = await AdvancedPopupSystem.SpawnAsync<ToastPopup>();
toast.Show();
// …later:
toast.Hide();
AdvancedPopupSystem.Despawn(toast);               // back to the pool for reuse
// …or fully release it (destroy + free the handle):
AdvancedPopupSystem.Despawn(toast, release: true);
```

**Bounding the pool.** The popup's **Pool Capacity** (Pool box) caps reuse: `-1` pools every despawned copy (default),
`0` releases each one immediately (no pooling), `1` keeps a single idle instance for reuse (no pool list is allocated —
it's just one on/off object), and `N` (2+) keeps at most N idle copies — past that, `Despawn` frees the extra instead of
retaining it. So you get fast reuse with a ceiling on how much memory idle copies hold. (`Despawn(…, release: true)`
always frees, whatever the capacity.)

### 9.5 Where loaded popups live — canvas per layer

Loaded and spawned popups are parented to `AdvancedPopupSystem.Root` — an auto-created persistent (DontDestroyOnLoad)
overlay Canvas, so they survive scene changes like global UI. Assign your own `Root` (or pass a `parent` to
`SpawnAsync`) to control the canvas, sort order, or render mode.

**Give each layer its own canvas from the Layers tool.** In **`APS ▸ Layers`** every layer has a **Sorting Order** and
an optional **Canvas** prefab. APS puts each layer's loaded/spawned popups on their own canvas at that sort order — so a
HUD layer, a dialog layer, and a tooltip layer stack independently instead of piling onto one overlay:

- **Sorting Order** — the `sortingOrder` of that layer's canvas (higher = in front). It also orders the layer list in
  the editor. It does **not** renumber the layer enum — the ordering there is display only, so your existing popups keep
  their layers.
- **Canvas** *(optional)* — assign a canvas prefab to route the layer onto your own canvas (with its scaler, render
  mode, extra children…). Leave it empty and APS creates a plain overlay canvas for the layer. Either way the **Sorting
  Order from the tool wins** over any value baked into the prefab.

Canvases are created lazily — a layer's canvas appears the first time one of its popups is loaded or spawned, so unused
layers cost nothing.

**Override in code.** You can still map a layer to a specific canvas at runtime; it takes precedence over the tool:

```csharp
// Each canvas is yours — set its own sortingOrder / render mode.
AdvancedPopupSystem.RegisterLayerCanvas(PopupLayerEnum.MENU, hudCanvas.transform);
AdvancedPopupSystem.UnregisterLayerCanvas(PopupLayerEnum.MENU); // back to the tool config / Root
```

- Only popups **APS instantiates** (Addressable lazy/preload loads and `SpawnAsync`) are routed. Popups you place in a
  scene keep their own canvas — position them where you want.
- A layer with no tool config and no runtime mapping falls back to `Root`.
- If a popup carries several mapped layers, the lowest-declared one wins — give a popup a single layer to keep it
  unambiguous.
- A `parent` passed explicitly to `SpawnAsync` still wins over the layer canvas.
- A runtime `RegisterLayerCanvas` must run before a preloaded popup loads if that popup must start on it — otherwise it
  uses the tool config (or `Root`).

> The manual `Instantiate` + `Init()` pattern in [§6.4](#64-instantiating-popups-at-runtime) still works for popups you
> load yourself without Addressables.
