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
9. [Upcoming: Dynamic Spawning & Pooling (Planned)](#9-upcoming-dynamic-spawning--pooling-planned-)

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
  screens (e.g. `LOGIN`, `HUB`, `SETTINGS`). Several flags can be active at once. Edit it from the APS **Layers** panel.
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
children. Optionally add a **Close Button** inside the hierarchy.

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
            base.Subscribe(); // wires closeButton, fires OnShowing, adds to ActivePopups

            _playButton.onClick.AddListener(OnPlayPressed);
            _settingsButton.onClick.AddListener(OnSettingsPressed);
        }

        protected override void Unsubscribe()
        {
            base.Unsubscribe(); // unwires closeButton, fires OnHided, removes from ActivePopups

            _playButton.onClick.RemoveListener(OnPlayPressed);
            _settingsButton.onClick.RemoveListener(OnSettingsPressed);
        }

        private void OnPlayPressed() => Hide();

        private void OnSettingsPressed() =>
            AdvancedPopupSystem.LayerShow(PopupLayerEnum.SETTINGS, autohide: false);
    }
}
```

`Subscribe` runs at the start of a show, `Unsubscribe` at the start of a hide. If you don't override `Init`, the popup
uses the default **Scale** transition (see [§4](#4-animations--custom-transitions) to pick another).

### Step 3: Configure in the Inspector

| Field | Meaning |
| :--- | :--- |
| **Popup Layer** | One or more layer flags this popup belongs to (used by `LayerShow` / `LayerHide`). |
| **Close Button** | Optional. When clicked, APS calls `Hide()` automatically. |
| **Auto Hide On Init** | Keep `true` so the popup starts hidden. Set `false` only for UI shown immediately on scene start. |
| **Manual Init** | Keep `false` for scene popups. Set `true` if you instantiate at runtime and want to call `Init()` yourself. |
| **Inactive** | `true` prevents the popup from ever showing (a hard gate on `Show`). |
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
        IAdvancedPopup settings = AdvancedPopupSystem.GetPopupByLayer(PopupLayerEnum.SETTINGS, activeOnly: true);
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

// Switch screens: show HUB and hide every other active layer.
AdvancedPopupSystem.LayerShow(PopupLayerEnum.HUB, autohide: true);

// Overlay: show SETTINGS on top without closing the current screen.
AdvancedPopupSystem.LayerShow(PopupLayerEnum.SETTINGS, autohide: false);

// Hide one layer, or everything.
AdvancedPopupSystem.LayerHide(PopupLayerEnum.SETTINGS);
AdvancedPopupSystem.HideAll();
```

### 3.4 Overriding the animation per call (generics)

Any show/hide can run a **specific display type** for that call instead of the popup's cached one — handy for a
one-off transition or a screen-wide effect:

```csharp
// One popup, this call only, with explicit Fade settings:
myPopup.Show<FadeDisplay>(new FadeSettings { Duration = 0.25f });

// A whole layer with a chosen display / settings:
AdvancedPopupSystem.LayerShow<ScaleDisplay>(PopupLayerEnum.HUB, new ScaleSettings { Duration = 0.4f });

// Different displays for show vs hide across a transition:
AdvancedPopupSystem.LayerShow<SlideDisplay, FadeDisplay>(PopupLayerEnum.HUB, slideIn, fadeOut);

// Layer hide / hide-all with a chosen display:
AdvancedPopupSystem.LayerHide<FadeDisplay>(PopupLayerEnum.SETTINGS);
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

- **`APS ▸ Layers`** — add / rename / delete `PopupLayerEnum` flags. Names are normalized to `UPPER_CASE`, and the enum
  file is **regenerated** on save (up to 31 flags). Do not hand-edit `PopupLayerEnum.generated.cs` — your edits are
  overwritten here.
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

### 6.3 Instantiating popups at runtime

Set **Manual Init** on the prefab, instantiate it, inject any data, then call `Init()` yourself before showing:

```csharp
var popup = Instantiate(_popupPrefab, _canvasRoot);
// popup.SetData(...);
popup.Init();   // registers with APS and applies auto-hide
popup.Show();
```

---

## 7. Settings & Logging

`APS ▸ Settings` (persisted to `Assets/Resources/AP_Settings.json` in your project):

- **Key Event Tracking** — enable/disable the hotkey system globally.
- **Auto Switch Input Module** — (new Input System) auto-swap the EventSystem's input module at startup.
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

## 9. Upcoming: Dynamic Spawning & Pooling (Planned ⏳)

> [!CAUTION]
> **UNDER DEVELOPMENT.** Real-time instantiation and pool-based spawning are planned. `AdvancedPopupInstantiate`
> currently exists only as a no-op stub — the API below is a conceptual preview and is not active yet.

```csharp
/*
// PREVIEW OF AN UNRELEASED FEATURE
public class DynamicSpawner : MonoBehaviour
{
    [SerializeField] private AdvancedPopup _popupPrefab;
    [SerializeField] private Transform _canvasRoot;

    public void SpawnAndOpen()
    {
        AdvancedPopup popup = Instantiate(_popupPrefab, _canvasRoot); // 'Manual Init' checked on the prefab
        // popup.SetData(...);
        popup.Init(); // registers, applies auto-hide
        popup.Show();
    }
}
*/
```

Until then, the manual pattern in [§6.3](#63-instantiating-popups-at-runtime) is the supported way to spawn popups at
runtime.
