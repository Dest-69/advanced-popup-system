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

Pick either method — both are fully supported.

### Option A — Package Manager (Git URL) *(recommended — stays updatable from the Package Manager)*
Go to **Window** -> **Package Manager**, click the **"+"** button, select **Install package from git URL...**, paste the link below, and click **Add**:
```
https://github.com/Dest-69/advanced-popup-system.git
```
The required **Newtonsoft.Json** dependency is pulled in automatically — no separate step needed. *(Requires [Git](https://git-scm.com/) installed. Installing read-only this way is fully supported — see the note below.)*

### Option B — Import into your project *(as before)*
1. **Install Newtonsoft.Json first** — manual imports don't auto-resolve dependencies, so add it yourself: **Window** -> **Package Manager** -> **"+"** -> **Install package by name...**, and enter:
```
com.unity.nuget.newtonsoft-json
```
2. Download the latest `.unitypackage` from the [**Releases**](https://github.com/Dest-69/advanced-popup-system/releases) page and import it via **Assets -> Import Package -> Custom Package...**, or simply copy the package folder into your project's `Assets/`.

> Note: When you author custom displays, APS generates them into an `Assets/AdvancedPopupSystem/` folder in your project — commit it to version control like the rest of your code. Editing layers is behind a **Customization** toggle in the APS **Layers** tab (details in the [documentation](documentation.md#5-the-aps-editor-window)).

---

## ✨ Features

*   🗂️ **Layer-based Management** — Group and control popups by custom layer bitmasks (e.g., `GUI`, `GAME`, `MENU`). Show or hide entire layers with a single call, and give each layer its own **canvas** and sort order right in **`APS ▸ Layers`** (HUD, dialogs, tooltips…) so on-demand popups stack independently.
*   🎭 **Extensible Animation Pipeline** — Out-of-the-box support for **Fade**, **Scale**, and **Slide** transitions using custom easing curves, or scaffold your own custom display straight from the editor.
*   🎬 **DOTween Integration** *(optional)* — Drive a popup's show/hide with hand-built [DOTween](https://github.com/Demigiant/dotween) `Sequence`s via `DoTweenSettings.Create(...)` for full easing, timing, and chaining control — an alternative to the built-in transitions. Enabled by a `DOTWEEN` scripting define and shipped in its own assembly; the core runtime stays dependency-light and compiles fine without DOTween installed.
*   ⚡ **Async-First Execution** — Fully Task-based async/await transitions with automatic cancellation support via `CancellationToken`s.
*   🎹 **Input System Binding** — Easily bind popups to keyboard hotkeys or controllers. Works seamlessly with both Legacy Input Manager and the New Input System.
*   ⬅️ **Escape Close Stack** — One key (default `Escape`) steps back through open popups like the Android back button, closing the most recent one first. Per-popup policy (`Hide` / `Ignore` / `Block`) covers modals and pass-through popups.
*   🖱️ **Drag & Resize Modules** — Tick `Draggable` / `Resizable` on a popup to move or resize it at runtime, clamped to the screen (or a custom / safe-area rect) and correct for **any anchors**. Resize grips show a directional cursor (re-skinnable via a `ResizeCursorSet` asset). Data-driven flags — no extra components — with a stateless, registry-based handler pipeline you can extend.
*   📦 **Addressables Loading** *(optional)* — Flag a popup **Addressable** to load its prefab on demand — lazily on first show, preloaded on the scenes you choose (and freed again on the scenes you pick), or spawned as many pooled copies (with a single **Pool Capacity** knob — keep unlimited, despawn on hide, or cap idle copies) — instead of placing it in every scene. The editor auto-manages the Addressables group and a generated index; scene instances still win for effortless testing. Requires the Addressables package; the core runtime stays dependency-light.
*   🌲 **Nested Popup Hierarchies** — Support for deep child popups that automatically animate and manage their states in alignment with their parent popups.
*   ⏱️ **Cancellation-Aware** — Every transition returns an `Operation` you can `.Cancel()` or chain with `.OnComplete()`; starting a new show/hide auto-cancels the previous one.
*   🛠️ **Editor Tooling** — A dedicated **APS** window with **Layers**, **Displays**, and **Settings** tabs: generate layer flags and custom display scripts, tune settings, and inspect active popups/operations in real-time.

---

## 🚀 Quick Start

### 1. Define Your Popup & Configure Transitions
> 💡 Tip: `GameObject ▸ UI ▸ Advanced Popup` creates a ready-to-use popup (and a Canvas if needed) in one click.

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
// This will automatically open all MENU popups and hide other active layers
AdvancedPopupSystem.LayerShow(PopupLayerEnum.MENU, autohide: true);

// Or summon a single popup by type in one line — loads it from Addressables if it isn't in the scene yet
AdvancedPopupSystem.Show<MySettingsPopup>();
// Need the instance and it might still be loading? Await the async companion to TryGetPopup:
MySettingsPopup popup = await AdvancedPopupSystem.GetPopupAsync<MySettingsPopup>();
```
---

## ⚙️ Editor Workflow

Open the **APS** window from the top menu bar:

| Menu | Purpose |
| :--- | :--- |
| **`APS ▸ Layers`** | Add / rename / delete `PopupLayerEnum` flags (the enum is code-generated for you). |
| **`APS ▸ Displays`** | Scaffold a new custom display — APS generates the display + settings scripts with ready-to-fill stubs. |
| **`APS ▸ Settings`** | Toggle key-event tracking, the escape close stack, auto input-module switching, inspector view, and log verbosity. |

See the [full documentation](documentation.md) for the complete API, custom-display authoring, and troubleshooting.

---

## 📸 Showcase & Gallery

### Editor Tools

The **APS Editor Window** allows you to see all registered popups and trace active operations or tasks at runtime.

<p align="center">
  <img width="32%" alt="Editor View 1" src="https://github.com/user-attachments/assets/f484cfa7-addc-4af8-bbd2-5b9b18427674" />
  <img width="32%" alt="Editor View 2" src="https://github.com/user-attachments/assets/d7663a35-f90a-4745-9255-9c9b67468293" />
  <img width="32%" alt="Editor View 3" src="https://github.com/user-attachments/assets/8b8f56a8-543e-48ca-a61e-ed56e7cb5128" />
</p>

### Popup Inspector Configuration

Configure custom animations, keys, child popups, and layer masks directly in the inspector:

<p align="center">
  <img width="60%" alt="Inspector Settings" src="https://github.com/user-attachments/assets/54e29df7-a352-4394-8304-b058e8342b22" />
  <img width="60%" alt="Inspector Settings" src="https://github.com/user-attachments/assets/e29c4014-9be0-4f9e-9f53-7d21746e1ec0" />
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
