# Advanced Popup System Documentation

The **Advanced Popup System** is a modular, high-performance UI popup management framework for Unity. It provides automatic registration, layer-based management, custom easing animations, built-in DOTween integration, and task-based async/await support with cancellation tokens.

This document serves as a complete technical guide for setting up popups in your project and interacting with the system's public API surface.

---

## 📋 Table of Contents
1. [Core Building Blocks](#1-core-building-blocks)
2. [Step-by-Step Setup Guide](#2-step-by-step-setup-guide)
3. [API Reference & Code Examples](#3-api-reference--code-examples)
4. [Animations & Custom Transitions](#4-animations--custom-transitions)
5. [Advanced Configuration](#5-advanced-configuration)
6. [Troubleshooting Checklist](#6-troubleshooting-checklist)
7. [Upcoming Feature: Dynamic Spawning & Pool Instancing (Planned)](#7-upcoming-feature-dynamic-spawning--pool-instancing-planned-)

---

## 1. Core Building Blocks

### 1.1 Core Classes & Structs
- **`AdvancedPopup`**: The base `MonoBehaviour` component (implementing `IAdvancedPopup`) that you attach to your UI popups. It manages lifecycle events, subscribes to key bindings, handles close buttons, and implements animation triggers.
- **`AdvancedPopupSystem`**: The main static manager that coordinates showing, hiding, tracking, and lookup operations.
- **`Operation`**: A lightweight wrapper class returned by synchronous show/hide calls. It supports chaining callbacks via `.OnComplete()` and can cancel running transitions using `.Cancel()`.
- **`PopupLayerEnum`**: A generated flags enum (defined in `PopupLayerEnum.generated.cs`) used to group popups into logic layers (e.g. `LOGIN`, `HUB`, `SETTINGS`). Multiple layers can be active simultaneously.
- **`APSStats`**: A debugging tool that tracks current active operations, tasks, and registers/unregisters animations to help monitor allocations and lifecycle issues.

### 1.2 Global Collections
- **`AdvancedPopupSystem.AllPopups`**: Keeps track of all loaded/instantiated popups in active scenes (whether visible or hidden).
- **`AdvancedPopupSystem.ActivePopups`**: A list containing only the popups that are currently visible/active.
- **`AdvancedPopupSystem.ActiveLayer`**: A bitmask value representing the combined flags of all currently active `PopupLayerEnum` layers.

---

## 2. Step-by-Step Setup Guide

Follow these steps to create a new popup and configure it in Unity:

### Step 1: GameObject Setup in Hierarchy
1. Under your UI `Canvas`, create a new empty GameObject and name it (e.g., `SettingsPopup`).
2. Add your visual UI content, graphics, and interactive buttons as children of this GameObject.
3. (Optional) Create a **Close Button** inside the popup's hierarchy.

> [!NOTE]
> `RectTransform` and `CanvasGroup` components are required by the system. However, they are automatically added during initialization if they are missing from the GameObject.

### Step 2: Create a Custom Popup Script
Create a C# script for your popup (e.g., `MainMenuPopup.cs`) that inherits from `AdvancedPopup` (namespace `AdvancedPS.Core`). Override `Subscribe` and `Unsubscribe` to wire up local button events:

```csharp
using AdvancedPS.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame.UI
{
    public class MainMenuPopup : AdvancedPopup
    {
        [Header("Main Menu UI References")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _settingsButton;

        /// <summary>
        /// Subscribes to UI events when the popup is shown.
        /// Ensure you call base.Subscribe()!
        /// </summary>
        protected override void Subscribe()
        {
            base.Subscribe(); // Hooks the closeButton click and registers with ActivePopups
            
            _playButton.onClick.AddListener(OnPlayPressed);
            _settingsButton.onClick.AddListener(OnSettingsPressed);
        }

        /// <summary>
        /// Unsubscribes from UI events when the popup is hidden.
        /// Ensure you call base.Unsubscribe()!
        /// </summary>
        protected override void Unsubscribe()
        {
            base.Unsubscribe(); // Unhooks the closeButton and removes from ActivePopups
            
            _playButton.onClick.RemoveListener(OnPlayPressed);
            _settingsButton.onClick.RemoveListener(OnSettingsPressed);
        }

        private void OnPlayPressed()
        {
            Debug.Log("Starting game...");
            Hide(); // Hide this popup
        }

        private void OnSettingsPressed()
        {
            // Open the settings popup by showing the SETTINGS layer
            AdvancedPopupSystem.LayerShow(PopupLayerEnum.SETTINGS, autohide: false);
        }
    }
}
```

### Step 3: Inspector Configuration
1. Attach your script (e.g. `MainMenuPopup`) to the root of your popup GameObject.
2. Fill the references in the inspector:
   - **Popup Layer**: Select the appropriate flag (e.g., `HUB`).
   - **Close Button**: Drag and drop the close button component. The system will automatically call `Hide()` when it is clicked.
   - **Auto Hide On Init**: Keep checked (`true`) so it starts hidden. Uncheck only for UI that must be shown immediately on scene startup.
   - **Manual Init**: Leave unchecked (`false`) for pre-placed scene popups. Check only if you instantiate popups dynamically via code and want to manually run `.Init()`.
   - **Deep Popups**: Drag child popups here if you want them to automatically mirror the parent's show/hide operations.

---

## 3. API Reference & Code Examples

### 3.1 Finding Popups
Use `AdvancedPopupSystem` lookup helpers to find registered popups at runtime:

```csharp
using AdvancedPS.Core;
using UnityEngine;

public class GameFlowController : MonoBehaviour
{
    public void OpenMenu()
    {
        // Find a registered popup of type MainMenuPopup (active or inactive in scene)
        if (AdvancedPopupSystem.TryGetPopup<MainMenuPopup>(out var mainMenu, activeOnly: false))
        {
            mainMenu.Show();
        }
        else
        {
            Debug.LogError("MainMenuPopup was not found in the scene!");
        }
    }

    public void CheckActiveLayer()
    {
        // Check if there is an active popup in the SETTINGS layer
        IAdvancedPopup settings = AdvancedPopupSystem.GetPopupByLayer(PopupLayerEnum.SETTINGS, activeOnly: true);
        if (settings != null)
        {
            Debug.Log($"Active settings popup found: {settings.gameObject.name}");
        }
    }
}
```

---

### 3.2 Showing and Hiding

#### A. Callback-Driven Workflow (`Operation`)
`.Show()` and `.Hide()` run asynchronously but allow you to chain operations cleanly without using async/await keywords:

```csharp
using AdvancedPS.Core;
using UnityEngine;

public class SimpleTrigger : MonoBehaviour
{
    [SerializeField] private AdvancedPopup _infoPopup;

    public void OnButtonClick()
    {
        // 1. Show the popup and hook a callback upon animation completion
        _infoPopup.Show().OnComplete(() =>
        {
            Debug.Log("Popup is now fully visible!");
        });
    }

    public void ClosePopup()
    {
        // 2. Hide the popup
        _infoPopup.Hide().OnComplete(() =>
        {
            Debug.Log("Popup has finished closing.");
        });
    }
}
```

You can cancel a running transition operation:
```csharp
Operation activeOperation = _infoPopup.Show();

// Aborts the current show transition mid-way
activeOperation.Cancel(); 
```

#### B. Async-First Workflow (`async/await`)
`.ShowAsync()` and `.HideAsync()` are fully compatible with async/await and support cancellations via `CancellationToken`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core;
using UnityEngine;

public class AsyncTrigger : MonoBehaviour
{
    [SerializeField] private AdvancedPopup _dialogPopup;
    private CancellationTokenSource _cts;

    public async void OpenDialogAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            Debug.Log("Starting animation...");
            await _dialogPopup.ShowAsync(_cts.Token);
            Debug.Log("Animation complete, dialog is fully open!");
        }
        catch (System.OperationCanceledException)
        {
            Debug.LogWarning("Show transition was cancelled.");
        }
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
```

---

### 3.3 Layer-based Popup Controls
Instead of managing individual popups, control multiple popups grouped under specific layers (e.g. `LOGIN`, `HUB`, `SETTINGS`).

```csharp
using AdvancedPS.Core;
using UnityEngine;

public class MenuManager : MonoBehaviour
{
    // Transitioning from one screen to another
    public void NavigateToHub()
    {
        // Shows all popups on the HUB layer AND automatically hides other active layers
        AdvancedPopupSystem.LayerShow(PopupLayerEnum.HUB, autohide: true);
    }

    // Overlaying a popup on top of the current screen
    public void OpenSettingsOverlay()
    {
        // Shows all settings popups without closing the currently visible popups
        AdvancedPopupSystem.LayerShow(PopupLayerEnum.SETTINGS, autohide: false);
    }

    public void CloseSettingsOnly()
    {
        // Hides all popups belonging to the SETTINGS layer
        AdvancedPopupSystem.LayerHide(PopupLayerEnum.SETTINGS);
    }

    public void CloseAllPopups()
    {
        // Closes every active popup in the system
        AdvancedPopupSystem.HideAll();
    }
}
```
---

## 4. Animations & Custom Transitions

### 4.1 Built-in Displays Reference
Every popup is associated with a show and hide animation. The system ships with four built-in animation displays:

| Display Name | Associated Settings Class | Modifies | Key Settings Fields |
| :--- | :--- | :--- | :--- |
| **`FadeDisplay`** | `FadeSettings` | `CanvasGroup.alpha` | `Duration`, `Easing`, `MaxValue` (default 1), `MinValue` (default 0) |
| **`ScaleDisplay`** | `ScaleSettings` | `transform.localScale` | `Duration`, `Easing`, `ShowScale` (Vector3.one), `HideScale` (Vector3.zero) |
| **`SlideDisplay`** | `SlideSettings` | `RectTransform.anchoredPosition` | `Duration`, `Easing`, `TargetRectPosition` (Vector3), `TargetRectSize` (Vector2) |
| **`DoTweenDisplay`**| `DoTweenSettings` | Custom DOTween Tween | `Factory` (build callback), `UnscaledTime`, `AutoKill`, `Link` |

> [!NOTE]
> All built-in settings support `OnAnimationStart` and `OnAnimationEnd` action events.

---

### 4.2 Using DOTween Transitions
The `DoTweenDisplay` allows you to create custom complex animations programmatically using DOTween:

```csharp
using AdvancedPS.Core;
using DG.Tweening;
using UnityEngine;

public class CustomTweenPopup : AdvancedPopup
{
    public override void Init()
    {
        // Configure custom show and hide transitions via DOTween sequences
        SetCachedDisplay(
            // Show Transition
            DoTweenSettings.Create((rectTransform, sequence) =>
            {
                sequence.Append(rectTransform.DOScale(Vector3.one, 0.5f).From(Vector3.zero).SetEase(Ease.OutBack))
                        .Join(rectTransform.DORotate(new Vector3(0, 0, 360), 0.5f, RotateMode.FastBeyond360));
            }),
            // Hide Transition
            DoTweenSettings.Create((rectTransform, sequence) =>
            {
                sequence.Append(rectTransform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
            })
        );

        base.Init(); // Finalizes registration
    }
}
```

---

### 4.3 Implementing Custom Displays
You can implement completely custom animation engines (e.g. relying on your own script loops or other animation packages) by writing custom classes:

#### Step 1: Create a Custom Settings Class
Create a settings class inheriting from `BaseSettings<T>` where `T` is your display runner class:

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

#### Step 2: Create a Custom Display Runner Class
Create a display runner class inheriting from `DisplayBase<TSettings>`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;

public class RotateDisplay : DisplayBase<RotateSettings>
{
    public override void ShowInstantlyMethod(RectTransform transform, RotateSettings settings)
    {
        transform.localRotation = Quaternion.Euler(0, 0, settings.TargetAngle);
    }

    public override void HideInstantlyMethod(RectTransform transform, RotateSettings settings)
    {
        transform.localRotation = Quaternion.identity;
    }

    public override async Task ShowMethod(RectTransform transform, RotateSettings settings, CancellationToken cancellationToken)
    {
        float elapsedTime = 0;
        Quaternion startRot = transform.localRotation;
        Quaternion targetRot = Quaternion.Euler(0, 0, settings.TargetAngle);

        while (elapsedTime < settings.Duration)
        {
            if (TaskUtils.OperationCancelled(cancellationToken)) return;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / settings.Duration;
            float easedT = EasingFunctions.Get(settings.Easing, t);
            transform.localRotation = Quaternion.Lerp(startRot, targetRot, easedT);

            await Task.Yield();
        }

        transform.localRotation = targetRot;
    }

    public override async Task HideMethod(RectTransform transform, RotateSettings settings, CancellationToken cancellationToken)
    {
        float elapsedTime = 0;
        Quaternion startRot = transform.localRotation;
        Quaternion targetRot = Quaternion.identity;

        while (elapsedTime < settings.Duration)
        {
            if (TaskUtils.OperationCancelled(cancellationToken)) return;

            elapsedTime += Time.deltaTime;
            float t = elapsedTime / settings.Duration;
            float easedT = EasingFunctions.Get(settings.Easing, t);
            transform.localRotation = Quaternion.Lerp(startRot, targetRot, easedT);

            await Task.Yield();
        }

        transform.localRotation = targetRot;
    }
}
```

#### Step 3: Cache the Custom Transition on Your Popup
In your popup's script, register it by calling `SetCachedDisplay()` before calling `base.Init()`:

```csharp
using AdvancedPS.Core;

public class SpinPopup : AdvancedPopup
{
    public override void Init()
    {
        // Cache Custom Rotate transition for both Show and Hide
        SetCachedDisplay<RotateDisplay>(new RotateSettings 
        { 
            Duration = 0.8f, 
            TargetAngle = 360f 
        });

        base.Init();
    }
}
```

---

## 5. Advanced Configuration

### 5.1 Deep Popups Hierarchy
If your popup contains child sub-popups, add them to the **`Deep Popups`** list in the parent's inspector. 
- When the parent's `Show()` / `Hide()` method is invoked, it propagates down.
- All showing or hiding animations run in parallel.
- The parent popup's await tasks (`ShowAsync` / `HideAsync`) will wait until **all** child animations have completed before resolving.

### 5.2 Hotkey Bindings
You can configure popups to toggle visibility automatically in response to key presses (wired up via `KeyEventSystemAPS`):
- Locate `KeyBindingShowSettings` / `KeyBindingHideSettings` on your popup component.
- **`AnyHotKey`**: Toggles popup if any key is pressed.
- **`HotKeys`**: A list of specific keys (e.g. `KeyCode.Escape`, `KeyCode.Tab`) mapped to the popup.
- **`Layers`**: Restricts key activation to times when specific layers are active.
- **`Popups`**: Restricts key activation to times when other listed popups are visible.
- **`OnTrigger`**: A `UnityEvent` callback that fires when the key is successfully triggered.

---

## 6. Troubleshooting Checklist

> [!WARNING]
> If a popup is not functioning as expected, verify the following points:

- **Popup does not appear or remains invisible:**
  - Verify that the component's `Init()` function has executed (check that `ManualInit` is `false` or that you are calling `Init()` manually after dynamic instantiation).
  - Ensure that the GameObject is active or that the parent Canvas is active.
  - Verify that the popup exists in `AdvancedPopupSystem.AllPopups`.

- **Popup transitions are stuck or OnComplete/Async tasks never finish:**
  - Ensure that custom displays are not throwing silent exceptions.
  - Verify that your custom display logic completes and does not loop infinitely. If your transition relies on `Duration`, ensure it updates correctly and exits the loop.
  - Ensure you are not canceling the transition immediately after starting it.

- **Incorrect popups open or close when calling LayerShow/LayerHide:**
  - Inspect the bitmask flags assigned in the inspector under `Popup Layer` for each popup. Ensure flags are properly separated (e.g., bit values like `1`, `2`, `4`, `8`, etc.).
  - Remember that `LayerShow` with `autohide = true` hides all other layers automatically. Use `autohide = false` if you want to display multiple overlay layers concurrently.

---

## 7. Upcoming Feature: Dynamic Spawning & Pool Instancing (Planned ⏳)

> [!CAUTION]
> **UNDER DEVELOPMENT** — Real-time dynamic instantiation and pool-based spawning are planned for future versions. The API described below is a conceptual preview and is currently not active in the codebase.

<span style="color: gray;">
When instantiating popups dynamically (e.g., from Resources, prefabs, or Addressables), the planned flow will allow manual initialization and spawning as shown below:
</span>

```csharp
/*
using AdvancedPS.Core;
using UnityEngine;

// THIS IS A PREVIEW OF AN UNRELEASED FEATURE
public class DynamicSpawner : MonoBehaviour
{
    [SerializeField] private AdvancedPopup _popupPrefab;
    [SerializeField] private Transform _canvasRoot;

    public void SpawnAndOpen()
    {
        // 1. Instantiate the prefab (Make sure 'Manual Init' is checked in the prefab)
        AdvancedPopup popupInstance = Instantiate(_popupPrefab, _canvasRoot);

        // 2. Inject parameters/data if required
        // popupInstance.SetData(...);

        // 3. Call Init() manually. This registers it to AllPopups and hides it instantly if AutoHideOnInit is true
        popupInstance.Init();

        // 4. Play show transition
        popupInstance.Show();
    }
}
*/
```
