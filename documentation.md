# Advanced Popup System — Documentation

The **Advanced Popup System (APS)** is a modular, high-performance UI popup framework for Unity: automatic
registration, layer-based grouping, a pluggable animation pipeline (custom easing, built-in Fade / Scale / Slide, and
optional DOTween), and `Task`-based async/await transitions with first-class cancellation. This is the complete guide —
how to set popups up and how to drive the public API.

---

## 📋 Table of Contents

0. [The Consumer Contract (Start Here)](#0-the-consumer-contract-start-here)
1. [Core Building Blocks](#1-core-building-blocks)
2. [Step-by-Step Setup Guide](#2-step-by-step-setup-guide)
3. [API Reference & Code Examples](#3-api-reference--code-examples)
4. [Animations & Custom Transitions](#4-animations--custom-transitions)
5. [The APS Editor Window](#5-the-aps-editor-window)
6. [Advanced Configuration](#6-advanced-configuration)
7. [Settings & Logging](#7-settings--logging)
8. [Troubleshooting Checklist](#8-troubleshooting-checklist)
9. [On-Demand Loading with Addressables](#9-on-demand-loading-with-addressables)
10. [Pitfalls — Anti-Patterns That Look Right](#10-pitfalls--anti-patterns-that-look-right)

---

## 0. The Consumer Contract (Start Here)

APS popups may be **lazy**: with the Addressables integration ([§9](#9-on-demand-loading-with-addressables)) a popup
often **does not exist in memory** until something loads it. Every rule below follows from that one fact — pick the
call by what you want, and let APS do the loading:

| I want to… | Call | Why this one |
| :--- | :--- | :--- |
| **Show a popup** | `AdvancedPopupSystem.Show<MyPopup>()` | Primary open path — loads the popup (lazily, if Addressable), then shows it. |
| **Hide a popup** | `AdvancedPopupSystem.Hide<MyPopup>()` | Safe no-op when not loaded; hiding **never** loads. |
| **Toggle a popup** | `AdvancedPopupSystem.SwitchShowHide<MyPopup>()` | Shown → hides, hidden → shows, not-loaded → loads & shows. |
| **Feed it data *before* it appears** | `AdvancedPopupSystem.Show<MyPopup, MyData>(data)` | Declare `AdvancedPopup<MyData>` + a `Bind` override; the data binds before the popup is visible. One-off setup instead: `Show<MyPopup>(p => …)` — both in [§3.2](#32-showing--hiding). |
| **Act only if it is shown** | `TryGetPopup<MyPopup>(out var p, activeOnly: true)` | The one legitimate *synchronous* case — a **secondary** op on an already-visible popup (refresh, read state). |

The rules behind the table — they keep lazy loading working:

- **Never assume residency.** A lazy popup isn't there until first use, and may be released again (unload scenes,
  `PoolCapacity 0`). Resolve at call time; don't cache references at boot.
- **Primary open = `Show<T>()` / `GetPopupAsync<T>()`** — the calls that load. `TryGetPopup` is **secondary-only**: it
  never loads, so a popup opened only through it never appears ([§10](#10-pitfalls--anti-patterns-that-look-right)).
- **Custom async around popups goes inside an `Operation`** — never `async void` or a fire-and-forget task. You get
  logging, cancellation and an observable outcome for free ([§3.2](#32-showing--hiding)).
- **Don't bulk-preload layers to make sync access work.** Preload is a per-popup startup-UX hint, not a way to avoid
  the async path ([§9.3](#93-preload--unload-per-scene)).

For which calls load and which don't, see the cheat sheet in [§9.6](#96-what-loads-and-what-doesnt--cheat-sheet).

---

## 1. Core Building Blocks

### 1.1 Core types

- **`AdvancedPopup`** — the `MonoBehaviour` you attach to a popup. Manages the lifecycle (`Init` → `Subscribe` /
  `Unsubscribe`), wires an optional close button, plays show/hide animations, and propagates to child ("deep") popups.
  Your popups inherit from it.
- **`AdvancedPopup<TData>`** — the base for popups that open **with data**: declare the data type once, implement
  `Bind(TData)`, and APS binds the data **before** the popup is visible ([§3.2](#32-showing--hiding)).
- **`IAdvancedPopup`** — the **abstract base class** `AdvancedPopup` derives from (namespace `AdvancedPS.Core.System`;
  the `I` prefix is historical — it is not an interface). Holds every inspector field, the cached display/settings, and
  `Init()`.
- **`AdvancedPopupSystem`** — the static manager. Registers/looks up popups and orchestrates layer-level show / hide.
- **`Operation`** — the self-starting object returned by every non-`async` show/hide call, and the sanctioned wrapper
  for your own async popup flows. Chain callbacks with `.OnComplete(...)`, abort with `.Cancel()`, inspect via
  `Status` / `Error` ([§3.2](#32-showing--hiding)).
- **`PopupLayerEnum`** — a generated `[Flags]` enum grouping popups into logical screens (`GUI`, `GAME`, `MENU`…).
  Each popup belongs to **exactly one** layer (every layer routes to its own canvas —
  [§9.5](#95-where-loaded-popups-live--canvas-per-layer)); the flags exist so several *layers* can be active at once
  and `Layer*` calls can take masks. Edit it from the APS **Layers** panel ([§5](#5-the-aps-editor-window)).
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

Fastest path: **`GameObject ▸ UI ▸ Advanced Popup`** — creates a stretched popup under a `Canvas` (making the Canvas if
needed) with an `AdvancedPopup` already attached. Manually: under your UI `Canvas`, add an empty GameObject (e.g.
`SettingsPopup`), then your visuals and buttons as children. To add a close button, link it under **Modules ▸ Close**
(tick `Closable`) — see [§6.4](#64-modules-drag-resize--close).

> [!NOTE]
> `RectTransform` and `CanvasGroup` are required, but APS **adds them automatically** during `Init()` if missing.

### Step 2: Write your popup script

Inherit from `AdvancedPopup` (namespace `AdvancedPS.Core`). Override `Subscribe` / `Unsubscribe` to wire local UI
events — always call `base`, and keep them symmetric:

```csharp
using AdvancedPS.Core;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuPopup : AdvancedPopup
{
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _settingsButton;

    protected override void Subscribe()
    {
        base.Subscribe(); // wires the Close button, fires OnShowing, adds to ActivePopups

        _playButton.onClick.AddListener(OnPlayPressed);
        _settingsButton.onClick.AddListener(OnSettingsPressed);
    }

    protected override void Unsubscribe()
    {
        base.Unsubscribe(); // unwires the Close button, fires OnHided, removes from ActivePopups

        _playButton.onClick.RemoveListener(OnPlayPressed);
        _settingsButton.onClick.RemoveListener(OnSettingsPressed);
    }

    private void OnPlayPressed() => Hide();

    private void OnSettingsPressed() =>
        AdvancedPopupSystem.LayerShow(PopupLayerEnum.OVERLAY, autohide: false);
}
```

`Subscribe` runs at the start of a show, `Unsubscribe` at the start of a hide. Without an `Init` override the popup uses
the default **Scale** transition ([§4](#4-animations--custom-transitions) to pick another).

### Step 3: Configure in the Inspector

| Field | Meaning |
| :--- | :--- |
| **Popup Layer** | The single layer this popup belongs to (used by `LayerShow` / `LayerHide`; each layer routes to its own canvas — [§9.5](#95-where-loaded-popups-live--canvas-per-layer)). `None` keeps it out of layer control. |
| **Modules** | Optional per-popup features — **Draggable**, **Resizable**, **Closable** ([§6.4](#64-modules-drag-resize--close)). |
| **Auto Hide On Init** | Keep `true` so the popup starts hidden. Set `false` only for UI shown immediately on scene start. |
| **Manual Init** | Keep `false` for scene popups. Set `true` to instantiate at runtime and call `Init()` yourself. Ignored for **Addressable** popups (they always auto-init). |
| **Inactive** | `true` prevents the popup from ever showing (a hard gate on `Show`). |
| **Escape Policy** | Reaction to the escape close key: `Hide`, `Ignore`, or `Block` ([§6.2](#62-escape-close-stack)). |
| **Close Key** | The key that closes *this* popup, overriding the project-wide escape key. Shown for the `Hide` policy; defaults to the project-wide key ([§6.2](#62-escape-close-stack)). |
| **Deep Popups** | Child/dependent popups that mirror this popup's show/hide ([§6.1](#61-deep-popups)). |

The **Preview** button at the top of the inspector plays the popup's show animation, holds it visible for a second,
plays the hide animation, and then restores the exact pre-preview state — all without entering play mode and without
dirtying the scene. It works in the Scene view and in Prefab Mode (select the prefab asset itself and it is disabled —
open Prefab Mode instead). The built-in Fade / Scale / Slide transitions animate for real; custom and DOTween displays
show their instant end states instead (their animation code needs play-mode time). While a preview is running the
button turns into **Stop**.

---

## 3. API Reference & Code Examples

### 3.1 Finding popups

```csharp
using AdvancedPS.Core;

// By type (active or inactive). The non-activeOnly path is O(1) via an internal type cache.
if (AdvancedPopupSystem.TryGetPopup<MainMenuPopup>(out var menu, activeOnly: false))
    menu.Show();

// By layer (first match) …
IAdvancedPopup settings = AdvancedPopupSystem.GetPopupByLayer(PopupLayerEnum.OVERLAY, activeOnly: true);
// … or by GameObject name (case-sensitive).
IAdvancedPopup byName = AdvancedPopupSystem.GetPopupByName("SettingsPopup", activeOnly: false);
```

### 3.2 Showing & hiding

Every non-`async` call returns an **`Operation`** — which is also the wrapper for your own async popup flows.

**A. Callback-driven (`Operation`)**

An `Operation` is **self-starting** — the moment a call returns one, the work is already running (there is no
`Start()`/`Run()`). Exceptions inside it never crash your code: they are caught, logged through `APLogger`, and surface
as the operation's outcome.

```csharp
_infoPopup.Show().OnComplete(() => Debug.Log("Fully visible!"));
_infoPopup.Hide().OnComplete(() => Debug.Log("Fully closed."));

// Abort a running transition:
Operation op = _infoPopup.Show();
op.Cancel();

// Observe the outcome — succeeded, cancelled, or faulted:
AdvancedPopupSystem.Show<SettingsPopup>().OnComplete(o =>
{
    switch (o.Status)
    {
        case OperationStatus.Succeeded: Debug.Log("Shown."); break;
        case OperationStatus.Cancelled: break; // superseded, Cancel()-ed, or play mode exited
        case OperationStatus.Faulted:   Debug.LogWarning($"Show failed: {o.Error}"); break;
    }
});
```

Every `Operation` exposes:

| Member | Meaning |
| :--- | :--- |
| `Status` | `Running` → then final `Succeeded` / `Cancelled` / `Faulted`. |
| `Error` | The exception that failed it — non-null only when `Faulted`. |
| `IsCompleted` | `true` once the status is final. |
| `OnComplete(Action)` | Runs **only on success** — never fires on a failed or cancelled run. |
| `OnComplete(Action<Operation>)` | Runs on **every** outcome; read `Status` / `Error` on the operation it receives. |
| `Cancel()` | Requests cancellation. Safe at any time — a no-op after completion. |

> [!NOTE]
> Callbacks are chainable and accumulate; attaching one to an **already-finished** operation invokes it immediately
> (even when the call completed synchronously, e.g. `Hide<T>()` of a popup that isn't loaded). Starting a new show/hide
> on the same popup automatically cancels the one in flight.

**B. Data before show — never configure a popup the player is already looking at**

A popup that needs per-open data must receive it **before** it becomes visible. Three tools, most to least structured —
pick the first that fits:

**1. Data popups (`AdvancedPopup<TData>`)** — for popups that always open with data. Declare the type once and
implement `Bind`, the single place data meets the UI:

```csharp
public class RewardPopup : AdvancedPopup<RewardData>
{
    [SerializeField] private Text _title;

    // Runs whenever new data arrives — always before the popup is shown with it.
    protected override void Bind(RewardData data)
    {
        _title.text = data.Title;
    }
}

// Open it with data — loads (if Addressable), binds, then shows:
AdvancedPopupSystem.Show<RewardPopup, RewardData>(reward);
// Or on an instance you already hold:
popup.Show(reward);
```

- **`Bind` runs only when new data arrives** (`Show(data)` / `SetData(data)`). Data-less re-shows — `LayerShow`,
  `SwitchShowHide`, a plain `Show()` — reopen the popup with its last content and don't re-run `Bind`.
- **The last data is kept** (`Data` / `HasData`); calling `Show(data)` on a visible popup updates its content live.
- **`Show(data)` re-binds by default, even if the data looks unchanged** — a skipped bind on changed data would be a
  stale-UI bug, while a redundant bind is only wasted work. For a genuinely expensive bind, override
  `IsSameData(current, next)` on that popup (a version field, or `current.Equals(next)` for record-style data).
- **Spawned copies never leak data**: `SpawnAsync<ToastPopup, ToastData>(data)` binds before handing you the instance,
  and `Despawn` clears it ([§9.4](#94-spawn-many-copies-toasts-list-rows)).
- If `Bind` throws, the operation faults (logged) and the popup is **not** shown.

**2. One-off setup — the configure callback**, for a popup that doesn't warrant a declared data type:

```csharp
AdvancedPopupSystem.Show<SettingsPopup>(p => p.PreselectTab(Tab.Audio));
```

Load → configure → show, one line. A throwing callback faults the operation and the popup is not shown.

**3. Full control — your own `Operation`**, when you need relevance guards between the load and the show. `Operation` is
the public primitive for your own async popup code, not just a return type — it self-runs, logs exceptions, honors
`Cancel()`, and reports its outcome. Don't roll your own fire-and-forget (`async void`, or UniTask's `.Forget()`) —
that silently loses all four.

```csharp
public Operation OpenReward(RewardData reward)
{
    return new Operation(async token =>
    {
        // 1. Load (or fetch) the unique popup. Forward the token so Cancel() reaches the wait.
        var popup = await AdvancedPopupSystem.GetPopupAsync<RewardPopup>(token);
        if (popup == null) return;                    // not in a scene & not Addressable — already logged by APS
        if (token.IsCancellationRequested) return;    // cancelled while loading

        // 2. Your relevance guards — the world may have moved on during the load.
        if (!_rewardService.IsStillPending(reward)) return;

        // 3. Data first, show second — the popup is never visible in a default state.
        popup.SetData(reward);
        await popup.ShowAsync(token);
    });
}

// Caller side — cancel if the player leaves before the popup appears:
Operation op = OpenReward(reward);
op.Cancel();
```

**C. Async-first (`async/await`)**

`ShowAsync` / `HideAsync` accept a `CancellationToken`:

```csharp
using System.Threading;
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

- `SwitchShowHide()` / `SwitchShowHideAsync()` (plus `<T>` variants) — show if hidden, hide if visible; a not-loaded
  (Addressable) type is loaded and shown. Like `Show<T>` / `Hide<T>`, a manual call that doesn't touch `ActiveLayer`.
- `Cmd_Show()`, `Cmd_Hide()`, `Cmd_SwitchShowHide()` — `void` methods for wiring to `Button.onClick` / `UnityEvent`
  fields in the Inspector.
- `OnShowing` / `OnHided` (`Action` on `AdvancedPopup`) — fire from `Subscribe` / `Unsubscribe`; subscribe to react to
  visibility changes.

### 3.3 Layer controls

```csharp
// Switch screens: show MENU and hide every other active layer.
AdvancedPopupSystem.LayerShow(PopupLayerEnum.MENU, autohide: true);

// Overlay: show OVERLAY on top without closing the current screen.
AdvancedPopupSystem.LayerShow(PopupLayerEnum.OVERLAY, autohide: false);

// Hide one layer, or everything.
AdvancedPopupSystem.LayerHide(PopupLayerEnum.OVERLAY);
AdvancedPopupSystem.HideAll();
```

### 3.4 Overriding the animation per call (generics)

Any show/hide can run a **specific display type** for that call instead of the popup's cached one — handy for a one-off
transition or a screen-wide effect:

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
> All settings expose `OnAnimationStart` / `OnAnimationEnd` actions and an `UnscaledTime` flag — set it `true` to keep
> animating while the game is paused (`Time.timeScale == 0`), e.g. a pause menu; otherwise the transition waits for time
> to resume. `Easing` accepts any of the 30 `EasingType` curves (`Linear`, `EaseInOutQuad`, `EaseOutBack`,
> `EaseInOutElastic`, `EaseOutBounce`, …).

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
> auto-hide; if the cache is still empty it falls back to **Scale**.

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

- **`APS ▸ Layers`** — add / rename / delete `PopupLayerEnum` flags, and set each layer's canvas + sort order
  ([§9.5](#95-where-loaded-popups-live--canvas-per-layer)). Editing is behind a **Customization** toggle (locked by
  default). Names are normalized to `UPPER_CASE` (Latin letters, digits, underscore; can't start with a digit); the
  enum is **regenerated** on save (up to 31 flags), and the durable list lives in `ProjectSettings/APS_Layers.json` so
  **updating APS never wipes your custom layers**. The enum ships inside the package (assembly
  `AdvancedPS.Generated.Layers`), so editing it needs a **writable** copy: on a read-only Package Manager install,
  turning on Customization offers to **embed** the package first. Don't hand-edit the generated file — edits are
  overwritten. (If your own code is in a separate assembly definition, reference `AdvancedPS.Generated.Layers` to see
  `PopupLayerEnum`.)
- **`APS ▸ Displays`** — add a display: APS generates `<Name>Display/<Name>Display.generated.cs` +
  `<Name>Settings.generated.cs` with ready-to-fill stubs into `Assets/AdvancedPopupSystem/Generated/Displays/` (built-in
  displays are listed read-only). Generation never overwrites an existing display; delete removes the pair. The
  `Display` / `Settings` suffixes and folder name are required by the tooling — keep them.
- **`APS ▸ Settings`** — see [§7](#7-settings--logging).

Both Layers and Displays have an **Auto-Save** toggle; with it off, use the **Save** button to apply changes.

---

## 6. Advanced Configuration

### 6.1 Deep popups

Add child/dependent popups to a parent's **Deep Popups** list. When the parent shows or hides, the operation propagates
to each deep popup, all animations run **in parallel**, and the parent's `ShowAsync` / `HideAsync` resolves only after
**all** child animations complete. Cycles are safe — APS traverses with a visited-set DFS (`ContainsDeepPopup`).

### 6.2 Escape close stack

One key (default `Escape`) steps back through open popups — each press closes the **most recently shown** popup, like
the Android back button. Opt in via `APS ▸ Settings ▸ Escape Close Stack` ([§7](#7-settings--logging)); that toggle is
the whole keyboard path, so with it off APS never installs its key-polling update at all.

APS works with **both** the legacy Input Manager and the new Input System (auto-selected). With the new Input System,
enable **Auto Switch Input Module** ([§7](#7-settings--logging)) to have APS replace `StandaloneInputModule` with
`InputSystemUIInputModule` automatically.

On a key press APS walks the visible popups newest-to-oldest and applies the first relevant popup's **Escape Policy** (an
inspector field on every popup):

| Escape Policy | Behavior |
| :--- | :--- |
| `Hide` | The popup closes (with its deep popups) and the press is consumed. |
| `Ignore` (default) | The popup is transparent — the press falls through to the popup shown before it. |
| `Block` | The press is consumed but nothing closes — for modal dialogs that must not be escaped. |

Popups are **transparent by default** (`Ignore`): the stack is opt-in per popup — set the ones you want the key to close
to `Hide`, and modal ones to `Block`.

**Per-popup key.** A `Hide` popup gets a **Close Key** field in the inspector. It starts out showing the project-wide
**Escape Close Key** and keeps following it — change the setting and every popup that never overrode it follows along.
Pick a different key and *that* one closes this popup instead, so one screen can close on `Tab` and another on `Q`
without touching the global setting.

The topmost closable popup **owns** the press: if the key you pressed isn't its key, the step is dropped rather than
reaching past it — a key bound to a background popup can never close it from under the popup on screen. `Block` and
`Ignore` don't read the key at all (a modal swallows every key, a transparent popup passes every key through).

**Grouping.** Popups shown by their parent's cascade (via **Deep Popups**) don't get their own step — closing the parent
hides the whole group at once. A deep popup you later show **individually** (a nested dialog on top of its parent) gets
its own step: the key closes it first, then its parent.

Notes:

- The walk skips popups that are already hiding, so pressing repeatedly during animations steps on responsively.
- `AdvancedPopupSystem.EscapeStep()` runs one step programmatically (returns `false` if nothing consumed it) — wire it
  to a UI "Back" button for the same behavior without the keyboard. It ignores Close Key (any closable popup answers)
  and works even with the toggle off.
- With the legacy Input Manager on Android the hardware Back button arrives as `Escape`, so the stack doubles as
  back-button navigation.

### 6.3 Instantiating popups at runtime

Set **Manual Init** on the prefab, instantiate it, inject any data, then call `Init()` before showing:

```csharp
var popup = Instantiate(_popupPrefab, _canvasRoot);
// popup.SetData(...);
popup.Init();   // registers with APS and applies auto-hide
popup.Show();
```

This applies only to popups you instantiate yourself. For loading popups from **Addressables** on demand and spawning
pooled copies, see [§9](#9-on-demand-loading-with-addressables) — those auto-initialize on load, so **Manual Init** does
not apply to them.

### 6.4 Modules (drag, resize & close)

A popup's inspector has a **Modules** box with a `Features` flag field: tick a feature and its config block appears
below. Features are **data on the popup**, not extra components. Three are built in — **Draggable**, **Resizable**,
**Closable**.

**Draggable** and **Resizable** are runtime **pointer interactions**: a single central pointer system drives every
popup (nothing is added to the scene at runtime), works with both input backends, and both **clamp the popup to a bounds
rect for any anchors / pivot** — center-anchored, corner-anchored or stretched popups all stay on screen. **Closable** is
simpler — a button that hides the popup.

**Draggable** (`Modules ▸ Drag`):

- **Drag Zone** — the RectTransform that starts a drag (e.g. a title bar). Leave empty to drag by the whole popup.
- **Bounds** — where the popup is kept: `Canvas` (≈ the screen, default), `SafeArea` (notch-safe), `Custom` (a
  RectTransform in **Custom Bounds**), or `None`.
- **Padding** — extra inset (px) on each side of the bounds.

**Resizable** (`Modules ▸ Resize`):

- **Grips** — the handles the pointer grabs, each with a direction (edge/corner). Click **Generate Grips** for a default
  8-handle set under a dedicated `[Grips]` child (added last so it sits on top); then move/scale them to taste. Grips are
  plain RectTransforms — add your own `Image` to make them visible in play mode.
- **Min Size / Max Size** — size limits in px (0 on an axis = unlimited).
- **Bounds / Custom Bounds / Padding** — same clamping options as drag.
- **Change Cursor** — while the pointer is over a grip, the OS cursor switches to a directional arrow (↔ / ↕ for edges,
  ╲ / ╱ for corners). Untick to turn the feedback off for this popup.
- **Cursors** — leave empty for the built-in arrows, or assign a `ResizeCursorSet` to re-skin them (create one via
  **Assets ▸ Create ▸ Advanced Popup System ▸ Resize Cursor Set** and drop in four cursor textures). Each cursor draws
  at the texture's **exact pixel size** (built-ins are 24×24 — keep yours small), with **Hotspot** at the texture center,
  imported **Read/Write enabled + uncompressed** (Unity's **Cursor** texture type does this). Turn the feature off
  everywhere with `PopupInteractionSystem.CursorFeedbackEnabled = false`.

Resizing keeps the edge opposite the grabbed grip fixed and honors the pivot; it targets popups with **fixed
(non-stretched) anchors** on the resized axis.

**Closable** (`Modules ▸ Close`):

- **Close Button** — a `Button` that hides the popup when clicked (APS calls `Hide()` for you). Wired only while the
  popup is shown and unwired on hide, so it never fires on a hidden popup. Leave empty for none.

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

- **Auto Switch Input Module** — (new Input System) auto-swap the EventSystem's input module at startup.
- **Escape Close Stack** — one key steps back through open popups ([§6.2](#62-escape-close-stack)). Off by default.
  It is the master switch for APS's keyboard handling: off, and no key-polling update is installed at all.
- **Escape Close Key** — the project-wide key driving the escape close stack (default `Escape`). Any popup can override
  it with its own **Close Key**.
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
  - Opening it only through `TryGetPopup`? A lazy (Addressable) popup **never loads that way** — the lookup just misses
    silently. Open with `Show<T>()` / `GetPopupAsync<T>` instead ([§10.1](#101-trygetpopup-as-the-primary-way-to-open-popups)).
  - Confirm `Init()` ran — for a scene or manually-instantiated popup, `Manual Init` must be `false`, or call `Init()`
    after instantiating. (Addressable popups always auto-init.)
  - Ensure the GameObject and its parent Canvas are active, and the popup is in `AdvancedPopupSystem.AllPopups`.

- **Transition sticks / `OnComplete` or `await` never resolves**
  - `OnComplete(Action)` fires on **success only** — a cancelled or faulted run skips it. Attach the all-outcomes
    overload (`.OnComplete(o => Debug.Log($"{o.Status} {o.Error}"))`) to see how it actually ended ([§3.2](#32-showing--hiding)).
  - A custom display must exit its loop (respect `Duration` and `TaskUtils.OperationCancelled`) and not throw silently.
  - Don't cancel a transition immediately after starting it (a new show/hide cancels the previous one).

- **Wrong popups open/close with `LayerShow` / `LayerHide`**
  - Check each popup's **Popup Layer** in the Inspector — one layer per popup.
  - `LayerShow(..., autohide: true)` hides all other layers; use `autohide: false` to overlay.
  - Mixing manual `Show()/Hide()` with layer calls can desync `ActiveLayer` from what's actually visible.

- **DOTween display missing** — `DoTweenDisplay` compiles only when DOTween is installed (behind the `DOTWEEN` define).

- **Escape key / clicks not registering (new Input System)** — enable **Auto Switch Input Module**, or make sure your
  EventSystem uses `InputSystemUIInputModule`.

- **A popup's key doesn't close it** — its **Escape Policy** must be `Hide` (`Ignore` is the default and passes keys
  through), **Escape Close Stack** must be on, and no popup above it may be open — the topmost closable popup owns the
  press.

---

## 9. On-Demand Loading with Addressables

Popups can load from **Addressables** on demand instead of living in every scene — lazily on first show, preloaded on
boot, or in many pooled copies. It's **optional**: install Unity's **Addressables** package to enable it; without it
APS works exactly as before (scene-only). APS never hard-depends on Addressables.

### 9.1 Make a popup Addressable

On the popup **prefab**, open the **Addressable** box and tick **Addressable**. A few options appear:

- **Load Mode** — `Lazy` (default: loaded on first show) or `Preload` (also loaded up-front — see §9.3).
- **Preload Scenes** *(Preload only)* — a scene checklist. Leave it **empty** (default) to preload on the very first
  scene; **check specific scenes** to preload it only when those load.
- **Unload Scenes** — scenes on **entering** which this popup is released from memory. Works for any Addressable popup
  (Lazy or Preload). Empty (default) = never unloads.

> Scene choices are stored by the scene's **asset identity, not its Build Settings position** — so reordering, adding,
> or removing scenes in Build Settings never shifts what a popup preloads or unloads. (Renaming or moving a scene file
> is handled automatically too.)

- **Pool Capacity** (Pool box) — one value for what happens to the popup's instances when hidden or despawned: `-1`
  keep unlimited (stays in memory like a scene popup), `0` despawn on hide (free its memory; reloads next show), `1` a
  single on/off instance (default: kept for reuse, no pool spun up), or `N` (2+) a pool of up to N idle copies
  ([§9.4](#94-spawn-many-copies-toasts-list-rows)).

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

**Or summon one popup by its type** — `Show<T>()` loads it from Addressables if it isn't present and shows it; `Hide<T>()`
closes it. These are manual shows — they don't autohide other layers:

```csharp
AdvancedPopupSystem.Show<SettingsPopup>();                       // loads if Addressable, then shows
AdvancedPopupSystem.Show<SettingsPopup, FadeDisplay>();          // with a per-call animation type
AdvancedPopupSystem.Hide<SettingsPopup>();                       // close it again (never loads)
AdvancedPopupSystem.SwitchShowHide<SettingsPopup>();             // toggle: shown → hide, hidden/not loaded → (load &) show
```

**Need the instance itself?** `GetPopupAsync<T>()` is the async companion to `TryGetPopup<T>`: it returns a resident
popup immediately, otherwise loads it from Addressables and hands you the instance (null if the type is neither in a
scene nor Addressable):

```csharp
SettingsPopup popup = await AdvancedPopupSystem.GetPopupAsync<SettingsPopup>();
if (popup != null) popup.Show();
```

**Testing one screen stays trivial:** if a popup of that type is already in the loaded scene, APS uses it and skips
Addressables — drop the prefab into a test scene and press Play, exactly like a normal popup.

> `TryGetPopup<T>` stays synchronous and resident-only — reach for it (with `Preload`) when you want a popup with no
> `await`. When the popup may still need loading, use `GetPopupAsync<T>` (to fetch) or `Show<T>` (to fetch + show).

### 9.3 Preload & unload per scene

A **Preload** popup loads eagerly so its first show is instant. **Preload Scenes** decides *when*: on each scene load, a
Preload popup that wants that scene is loaded if it isn't already. Leave the checklist **empty** (default) to load on the
very first scene — the classic boot preload. **Check** specific scenes to spread loading across your game (e.g. preload
the shop popups only when the Hub scene loads).

A popup requested **before** its preload scene is reached simply **loads on demand** at that moment — preload is only a
"load it earlier when possible" hint, never a gate.

**Unload Scenes** is the mirror: on entering a checked scene, the popup is **released from memory** (its Addressables
handle freed). Empty (default) keeps it resident; a currently-visible popup is left alone. Use it to drop a screen's
popups when you leave it.

To gate a loading screen yourself, await the manual preload (loads every Preload popup now, regardless of scene):

```csharp
await AdvancedPopupSystem.PreloadAll();                       // every Preload-flagged popup
await AdvancedPopupSystem.PreloadLayer(PopupLayerEnum.MENU);   // just one screen
```

> [!NOTE]
> **One broken popup can't block the rest.** If a popup throws while loading (a bad asset, an exception in its
> `Awake`/injection…), APS logs an error naming that popup type, skips it, and keeps loading the remaining entries — in
> `PreloadAll`, `PreloadLayer`, the per-scene preload, and the load step of `LayerShow` alike.

### 9.4 Spawn many copies (toasts, list rows)

For popups you need in multiple instances, spawn and manage them yourself. Spawned popups are pooled and **not** part of
layer batches, but a visible one still closes on Escape / `HideAll`:

```csharp
var toast = await AdvancedPopupSystem.SpawnAsync<ToastPopup>();
toast.Show();
// …later:
toast.Hide();
AdvancedPopupSystem.Despawn(toast);               // back to the pool for reuse
// …or fully release it (destroy + free the handle):
AdvancedPopupSystem.Despawn(toast, release: true);

// A data popup (class NoticePopup : AdvancedPopup<string>, see §3.2) spawns with its content already bound:
var saved = await AdvancedPopupSystem.SpawnAsync<NoticePopup, string>("Saved!");
```

`Despawn` also **clears a data popup's bound data**, so a pooled instance never shows the previous use's content.

**Bounding the pool.** The popup's **Pool Capacity** caps reuse: `1` keeps a single idle instance (default — no pool list
allocated), `-1` pools every despawned copy, `0` releases each one immediately, and `N` (2+) keeps at most N idle
copies — past that, `Despawn` frees the extra. (`Despawn(…, release: true)` always frees, whatever the capacity.)

### 9.5 Where loaded popups live — canvas per layer

Loaded and spawned popups are parented to `AdvancedPopupSystem.Root` — an auto-created persistent (DontDestroyOnLoad)
overlay Canvas, so they survive scene changes like global UI. Assign your own `Root` (or pass a `parent` to
`SpawnAsync`) to control the canvas, sort order, or render mode.

**Give each layer its own canvas from the Layers tool.** In **`APS ▸ Layers`** every layer has a **Sorting Order** and a
**Canvas** prefab. APS puts each layer's loaded/spawned popups on their own canvas at that sort order — so a HUD layer, a
dialog layer, and a tooltip layer stack independently:

- **Sorting Order** — the `sortingOrder` of that layer's canvas (higher = in front). It also orders the layer list in the
  editor, but does **not** renumber the layer enum — the ordering is display only, so existing popups keep their layers.
- **Canvas** *(required)* — the canvas prefab this layer's popups are instantiated under. New layers are seeded with a
  **default canvas** APS creates in your project at `Assets/AdvancedPopupSystem/APS_DefaultCanvas.prefab` — **edit that
  prefab to control the default for every layer** (its scaler, render mode, extra children…). It ships full-screen on the UI layer with a **Scale With Screen Size** scaler at 1920×1080. It lives in your `Assets`,
  not the package, so a package update never overwrites your changes. Point a layer at a different prefab to give it its
  own canvas; clearing the field snaps it back to the default. Either way the **Sorting Order from the tool wins** over
  any value baked into the prefab, and the instance is named `<Layer> - APS Canvas` in the hierarchy.

Canvases are created lazily — a layer's canvas appears the first time one of its popups is loaded or spawned, so unused
layers cost nothing.

**Override in code.** You can map a layer to a specific canvas at runtime; it takes precedence over the tool:

```csharp
AdvancedPopupSystem.RegisterLayerCanvas(PopupLayerEnum.MENU, hudCanvas.transform);
AdvancedPopupSystem.UnregisterLayerCanvas(PopupLayerEnum.MENU); // back to the tool config / Root
```

- Only popups **APS instantiates** (Addressable loads and `SpawnAsync`) are routed. Popups you place in a scene keep
  their own canvas — position them where you want.
- A layer with no tool config and no runtime mapping falls back to `Root`.
- A popup belongs to exactly one layer. Legacy multi-flag data (authored before layers became canvas-bound) still
  resolves — the lowest-declared mapped layer wins and APS logs a warning at registration — but re-author it to a
  single layer (the inspector offers a one-click fix).
- A `parent` passed explicitly to `SpawnAsync` wins over the layer canvas. A runtime `RegisterLayerCanvas` must run
  before a preloaded popup loads if that popup must start on it.

> The manual `Instantiate` + `Init()` pattern in [§6.3](#63-instantiating-popups-at-runtime) still works for popups you
> load yourself without Addressables.

### 9.6 What loads and what doesn't — cheat sheet

The whole lazy model hangs on knowing which calls load:

| Call | Loads? | Notes |
| :--- | :--- | :--- |
| `Show<T>()` / `Show<T, TDisplay>()` | **Yes** | Fetches via `GetPopupAsync` under the hood, then shows. |
| `SwitchShowHide<T>()` | **Yes** | Toggles a resident popup; a not-loaded popup is loaded **and shown**. |
| `GetPopupAsync<T>(token)` | **Yes** | Resident popup returns instantly. **Concurrent calls for the same not-yet-loaded type share one in-flight load** — no duplicate instance. |
| `LayerShow(layer, …)` | **Yes** | Materializes the layer's Addressable popups (sequentially) before showing. |
| `SpawnAsync<T>(parent)` | **Yes** | The many-copies lane; reuses the pool first. |
| `PreloadAll()` / `PreloadLayer(layer)` | **Yes** | Eager and **sequential** — a long list takes the sum of its load times. Await them to gate a loading screen. |
| `TryGetPopup<T>(out p)` | **No** | Synchronous, resident-only. A miss means "not loaded", nothing more — it will *not* start a load. |
| `Hide<T>()` / `LayerHide(…)` / `HideAll()` | **No** | Nothing unloaded can be visible, so hiding never loads — a `Hide<T>` of an absent popup is a safe no-op. |

On every sequential batch path (`PreloadAll`, `PreloadLayer`, per-scene preload, `LayerShow`'s load step) a popup that
**fails** to load is logged by type and skipped ([§9.3](#93-preload--unload-per-scene)).

> [!NOTE]
> The shared in-flight load is detached from any single caller: cancelling **your** `GetPopupAsync` call abandons *your
> wait* (you get `null`), but the load still completes and the popup stays resident for everyone else — a unique popup is
> never released because one caller changed its mind.

---

## 10. Pitfalls — Anti-Patterns That Look Right

Real failure modes from production integrations. Each one compiles, runs, and quietly does the wrong thing — check here
first when a lazy popup "misbehaves".

### 10.1 `TryGetPopup` as the primary way to open popups

```csharp
// ANTI-PATTERN
if (AdvancedPopupSystem.TryGetPopup<ShopPopup>(out var shop))
    shop.Show();
```

**Symptom:** the popup **never opens**, and there are no errors — the `if` just silently falls through.

`TryGetPopup` never loads ([§9.6](#96-what-loads-and-what-doesnt--cheat-sheet)). For a lazy popup that hasn't been loaded,
the miss is the *expected* state — so an open path built on `TryGetPopup` works only if something else happened to load
the popup earlier. Primary opens go through `Show<T>()` (or `GetPopupAsync<T>`); `TryGetPopup` is for **secondary**
operations on a popup that is already there ([§0](#0-the-consumer-contract-start-here)).

### 10.2 Caching a popup reference and assuming it stays resident

```csharp
// ANTI-PATTERN: popup self-registers into a DI container / service locator on construction
protected override void Awake() { GameServices.Popups.Register(this); }
// … elsewhere, later:
GameServices.Popups.Get<ShopPopup>().Refresh();   // NullReferenceException
```

**Symptom:** `NullReferenceException` at seemingly random moments — before the popup's first show, or after a scene
switch.

A lazy popup **does not exist until first loaded**, and may be **released again** (Unload Scenes, `PoolCapacity 0`) — so
any stored reference has windows where it is null or destroyed. Resolve at call time with `Show<T>` / `GetPopupAsync<T>`
/ `TryGetPopup<T>`, which always answer from the live registries.

### 10.3 Preloading a layer so that `TryGetPopup` "starts working"

```csharp
// ANTI-PATTERN
await AdvancedPopupSystem.PreloadLayer(PopupLayerEnum.GAME);   // "now TryGetPopup works everywhere"
```

**Symptom:** boot / scene-switch time grows with every popup added; memory holds popups the player may never open.

This inverts the model: it makes *every* popup of the layer resident so that sync lookups can't miss — paying the exact
cost lazy loading exists to avoid. Preload is a **per-popup startup-UX hint** ("this one must show instantly"), set on the
popup's inspector ([§9.3](#93-preload--unload-per-scene)). If code needs the popup, let *that code* load it: `Show<T>()`
/ `GetPopupAsync<T>`.

### 10.4 Configuring content in `.OnComplete` after `Show<T>()`

```csharp
// ANTI-PATTERN
AdvancedPopupSystem.Show<QuestPopup>().OnComplete(() =>
{
    if (AdvancedPopupSystem.TryGetPopup<QuestPopup>(out var p))
        p.SetQuest(quest);          // runs AFTER the show animation finished
});
```

**Symptom:** the popup flashes empty / with stale defaults, then snaps to the right content.

`OnComplete` fires when the transition is **done** — the player has been looking at an unconfigured popup the whole time.
Data belongs **before** the show: make the popup an `AdvancedPopup<TData>` and open it with
`Show<QuestPopup, QuestData>(quest)`, or use the configure overload `Show<QuestPopup>(p => p.SetQuest(quest))` — both
bind before the popup is visible ([§3.2](#32-showing--hiding)).

### 10.5 The "closed before it loaded" race

```csharp
// ANTI-PATTERN: assumes Hide undoes an in-flight Show
AdvancedPopupSystem.Show<OfferPopup>();      // starts an Addressables load…
// … the player leaves the screen while it loads:
AdvancedPopupSystem.Hide<OfferPopup>();      // no-op! the popup isn't resident yet
// …the load finishes → the popup appears on the wrong screen.
```

**Symptom:** a popup appears "out of nowhere" moments after its context is gone.

`Hide<T>` of a not-yet-loaded popup is a no-op and **does not cancel the pending show**. Two correct shapes:

```csharp
// 1. Keep the Operation and cancel it when the context dies:
Operation op = AdvancedPopupSystem.Show<OfferPopup>();
// … context invalidated:
op.Cancel();                                  // the shared load may still finish; the SHOW won't happen

// 2. Or re-check relevance once it actually opened — OnComplete(Action) is success-only,
//    so the guard runs exactly when the popup really got shown:
AdvancedPopupSystem.Show<OfferPopup>()
    .OnComplete(() => { if (!OfferIsStillValid()) AdvancedPopupSystem.Hide<OfferPopup>(); });
```
