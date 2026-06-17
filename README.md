# Advanced Popup System

<p align="center">
  <img src="https://github.com/Dest-69/advanced-popup-system/assets/41786105/fd1cc56f-fca8-427a-af25-2267462d223e" alt="Advanced Popup System Banner" width="100%">
</p>

<p align="center">
  <b>A highly flexible, performance-oriented UI popup management framework for Unity.</b>
</p>

<p align="center">
  <a href="documentation.md"><b>📖 Documentation</b></a> |
  <a href="#-quick-start"><b>🚀 Quick Start</b></a> |
  <a href="#-installation"><b>📦 Installation</b></a> |
  <a href="#-features"><b>📋 Features</b></a> |
  <a href="CHANGELOG.md"><b>📜 Changelog</b></a>
</p>

---

## 🛠️ Requirements & Compatibility

| Feature | Supported / Requirement |
| :--- | :--- |
| **Unity Version** | `2021.3 LTS` or higher |
| **Input System** | Both **Legacy Input Manager** & **New Input System** (with automatic switching) |
| **Render Pipelines** | Built-in, Universal RP (URP), High Definition RP (HDRP) |
| **UI Frameworks** | Unity UI (uGUI) |
| **Platforms** | All platforms supported by Unity (iOS, Android, Standalone, WebGL, Consoles) |
| **Dependencies** | Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) |

---

## 📦 Installation

### 1. Install Newtonsoft.Json (Required)
Advanced Popup System requires the `Newtonsoft.Json` package.
Go to **Window** -> **Package Manager**, click the **"+"** button, select **Install package by name...**, and enter:
```
com.unity.nuget.newtonsoft-json
```

### 2. Import Advanced Popup System
Clone or copy this package into your Unity project's `Assets` folder, or add it via the Unity Package Manager (UPM) if configured as a package.

---

## ✨ Features

*   🗂️ **Layer-based Management** — Group and control popups by custom layer bitmasks (e.g., `LOGIN`, `HUB`, `SETTINGS`). Show or hide entire layers with a single call.
*   🎭 **Extensible Animation Pipeline** — Out-of-the-box support for **Fade**, **Scale**, and **Slide** transitions using custom easing curves, plus native [DOTween](https://github.com/Demigiant/dotween) integration.
*   ⚡ **Async-First Execution** — Fully Task-based async/await transitions with automatic cancellation support via `CancellationToken`s.
*   🎹 **Input System Binding** — Easily bind popups to keyboard hotkeys or controllers. Works seamlessly with both Legacy Input Manager and the New Input System.
*   🌲 **Nested Popup Hierarchies** — Support for deep child popups that automatically animate and manage their states in alignment with their parent popups.
*   🛠️ **Tailored Inspector Window** — Dedicated custom inspectors and a global APS Manager Window to debug active popups and operations in real-time.

---

## 🚀 Quick Start

### 1. Define Your Popup & Configure Transitions
Create a script that inherits from `AdvancedPopup` and attach it to your popup's `GameObject`. 

To configure how the popup enters and exits the screen, override the `Init()` method and call `SetCachedDisplay()`. The system supports built-in transitions (Fade, Scale, Slide) or native [DOTween](https://github.com/Demigiant/dotween) sequences:

```csharp
using AdvancedPS.Core;
using DG.Tweening; // Import DOTween namespace
using UnityEngine;

public class MySettingsPopup : AdvancedPopup
{
    public override void Init()
    {
        // --- Option A: Cache built-in transitions ---
        // SetCachedDisplay<ScaleDisplay>(); // Uses default Scale transition rules
        
        // --- Option B: Cache custom DOTween sequences ---
        SetCachedDisplay(
            // Show Sequence
            DoTweenSettings.Create((rectTransform, sequence) =>
            {
                sequence.Append(rectTransform.DOScale(Vector3.one, 0.4f)
                            .From(Vector3.zero)
                            .SetEase(Ease.OutBack));
            }),
            // Hide Sequence
            DoTweenSettings.Create((rectTransform, sequence) =>
            {
                sequence.Append(rectTransform.DOScale(Vector3.zero, 0.3f)
                            .SetEase(Ease.InBack));
            })
        );

        // ALWAYS call base.Init() at the end to auto-register the popup with APS
        base.Init();
    }
}
```

### 2. Open / Close the Popup from Code
You can find and animate your popups easily using the `AdvancedPopupSystem` API:

```csharp
using AdvancedPS.Core;

// Find the popup and display it using its cached transition
if (AdvancedPopupSystem.TryGetPopup<MySettingsPopup>(out var settingsPopup, activeOnly: false))
{
    // Show popup and run code when the animation is fully complete
    settingsPopup.Show().OnComplete(() => 
    {
        Debug.Log("Settings popup finished opening!");
    });
}

// Show/hide popups by Layer
// This will automatically open all HUB popups and hide other active layers
AdvancedPopupSystem.LayerShow(PopupLayerEnum.HUB, autohide: true);
```
---

## 📸 Showcase & Gallery

### Editor Tools

The **APS Editor Window** allows you to see all registered popups and trace active operations or tasks at runtime.

<p align="center">
  <img width="32%" alt="Editor View 1" src="https://github.com/user-attachments/assets/e2dff5a4-a9ba-42b1-864d-9c1542a8f464" />
  <img width="32%" alt="Editor View 2" src="https://github.com/user-attachments/assets/e3aa166e-9ec5-42db-870c-1641192fc5ff" />
  <img width="32%" alt="Editor View 3" src="https://github.com/user-attachments/assets/0af6ae6c-4b52-4f80-b53b-e96861a661a3" />
</p>

### Popup Inspector Configuration

Configure custom animations, keys, child popups, and layer masks directly in the inspector:

<p align="center">
  <img width="60%" alt="Inspector Settings" src="https://github.com/user-attachments/assets/bfbb330a-639e-4d9b-9aaa-fb431146e640" />
</p>

### Animation Showcases

Here are some real-time examples of popup animations in action:

<p align="center">
  <img width="32%" alt="Showcase 1" src="https://github.com/user-attachments/assets/2ff31071-2874-480b-bc69-3c5d1ca26164" />
  <img width="32%" alt="Showcase 2" src="https://github.com/user-attachments/assets/dd874890-5cd0-4188-b173-14c1610d6d8a" />
  <img width="32%" alt="Showcase 3" src="https://github.com/user-attachments/assets/e8e6678c-e278-4ccc-a368-9dd90af49dd8" />
</p>

---

_Developed with the support of the Hyperfactory._
