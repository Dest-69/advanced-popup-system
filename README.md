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
| **Input System** | **Legacy Input Manager** & **New Input System** (auto-switching) |
| **Render Pipelines** | Built-in, URP, HDRP |
| **UI Frameworks** | Unity UI (uGUI) |
| **Platforms** | All Unity platforms (iOS, Android, Standalone, WebGL, Consoles) |
| **Dependencies** | Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) |

---

## 📦 Installation

### Option A — Package Manager (Git URL) *(recommended, stays updatable)*
**Window ▸ Package Manager ▸ "+" ▸ Install package from git URL…**, paste the link and click **Add**:
```
https://github.com/Dest-69/advanced-popup-system.git
```
Newtonsoft.Json is pulled in automatically. Requires [Git](https://git-scm.com/); read-only installs are fully supported.

### Option B — Import a `.unitypackage`
1. Install **Newtonsoft.Json** first (manual imports don't auto-resolve dependencies): **Package Manager ▸ "+" ▸ Install package by name…** →
   ```
   com.unity.nuget.newtonsoft-json
   ```
2. Import the latest release from [**Releases**](https://github.com/Dest-69/advanced-popup-system/releases) via **Assets ▸ Import Package ▸ Custom Package…** (or copy the package folder into `Assets/`).

> Custom displays are generated into an `Assets/AdvancedPopupSystem/` folder in your project — commit it like the rest of your code. Editing layers is behind a **Customization** toggle in **APS ▸ Layers** (see the [documentation](documentation.md#5-the-aps-editor-window)).

---

## ✨ Features

*   🗂️ **Layer-based Management** — Group popups into layer bitmasks (`GUI`, `GAME`, `MENU`…) and show or hide whole layers in one call. Each layer gets its own **canvas** and sort order in **`APS ▸ Layers`**, so HUD, dialogs and tooltips stack independently.
*   🎭 **Extensible Animations** — Built-in **Fade**, **Scale** and **Slide** with 30 easing curves, or scaffold your own display straight from the editor.
*   🎬 **DOTween Integration** *(optional)* — Drive show/hide with hand-built [DOTween](https://github.com/Demigiant/dotween) `Sequence`s. Lives behind a `DOTWEEN` define in its own assembly; the core compiles fine without it.
*   ⚡ **Async-First** — Fully `Task`-based transitions with `CancellationToken` cancellation throughout.
*   ⬅️ **Escape Close Stack** — One key steps back through open popups like the Android back button, with a per-popup policy (`Hide` / `Ignore` / `Block`) for modals and pass-through popups. Each popup can override the key with its own **Close Key**; works with both the Legacy Input Manager and the new Input System.
*   🖱️ **Drag & Resize Modules** — Tick `Draggable` / `Resizable` to move or resize a popup at runtime, clamped to the screen (or a safe-area / custom rect) for any anchors. Data-driven flags, no extra components; resize grips show a re-skinnable directional cursor.
*   📦 **Addressables Loading** *(optional)* — Flag a popup **Addressable** to load its prefab on demand — lazily, preloaded on the scenes you choose, or as pooled copies — instead of placing it in every scene. Scene instances still win for effortless testing. Requires the Addressables package.
*   🧩 **Typed Data Popups** — Declare a popup's data (`class RewardPopup : AdvancedPopup<RewardData>`), implement one `Bind(data)`, and open it with `Show<RewardPopup, RewardData>(data)` — the data binds **before** the popup is visible, with no empty-popup flash.
*   🌲 **Nested Popups** — Deep child popups animate and manage their state together with their parent.
*   ⏱️ **Cancellation-Aware Operations** — Every transition returns a self-starting `Operation` you can `.Cancel()`, chain with `.OnComplete()`, and inspect via `Status` / `Error`. Starting a new show/hide auto-cancels the previous one.
*   🛠️ **Editor Tooling** — An **APS** window with **Layers**, **Displays** and **Settings** tabs, plus a live view of registered popups and running operations.

---

## 🚀 Quick Start

### 1. Build a popup — the right architecture

Create the object with **`GameObject ▸ UI ▸ Advanced Popup`** (it makes a stretched popup under a `Canvas`, adding the Canvas if needed, with an `AdvancedPopup` already attached). Add your visuals as children, then set the **Popup Layer** and tick any **Modules** (Draggable / Resizable / Closable) in the inspector.

The script that goes on it is the whole architecture — transition, data, and UI wiring in one place:

```csharp
using AdvancedPS.Core;
using UnityEngine;
using UnityEngine.UI;

// The data this popup opens with. Omit it (and Bind) for a popup that needs no per-open data.
[System.Serializable]
public class RewardData
{
    public string Title;
    public int Amount;
}

// Inherit AdvancedPopup<T> for a data popup, or plain AdvancedPopup if it takes no data.
public class RewardPopup : AdvancedPopup<RewardData>
{
    [SerializeField] private Text _title;
    [SerializeField] private Text _amount;
    [SerializeField] private Button _claimButton;

    // 1) Transition — how the popup enters/exits. Set the display, then call base.Init() LAST.
    public override void Init()
    {
        SetCachedDisplay<FadeDisplay>(new FadeSettings { Duration = 0.25f, Easing = EasingType.EaseOutQuad });
        base.Init();
    }

    // 2) Data → UI. The single place data is applied, always BEFORE the popup becomes visible.
    protected override void Bind(RewardData data)
    {
        _title.text  = data.Title;
        _amount.text = $"+{data.Amount}";
    }

    // 3) Local UI wiring — keep Subscribe/Unsubscribe symmetric and always call base.
    protected override void Subscribe()
    {
        base.Subscribe();
        _claimButton.onClick.AddListener(OnClaim);
    }

    protected override void Unsubscribe()
    {
        base.Unsubscribe();
        _claimButton.onClick.RemoveListener(OnClaim);
    }

    private void OnClaim() => Hide();
}
```

### 2. Show, hide, toggle — from anywhere

```csharp
using AdvancedPS.Core;
using UnityEngine;

// By type — one line each. Loads from Addressables on demand if the popup isn't in the scene yet.
AdvancedPopupSystem.Show<RewardPopup, RewardData>(reward);   // open WITH data (bound before it's visible)
AdvancedPopupSystem.Show<SettingsPopup>();                   // open a popup that needs no data
AdvancedPopupSystem.Hide<SettingsPopup>();                   // close (safe no-op if it never loaded)
AdvancedPopupSystem.SwitchShowHide<SettingsPopup>();         // toggle: shown → hide, hidden → show

// Run code once the animation finishes (fires on success only):
AdvancedPopupSystem.Show<SettingsPopup>().OnComplete(() => Debug.Log("Opened!"));

// By layer — switch whole screens at once:
AdvancedPopupSystem.LayerShow(PopupLayerEnum.MENU, autohide: true);      // show MENU, hide other active layers
AdvancedPopupSystem.LayerShow(PopupLayerEnum.OVERLAY, autohide: false);  // overlay on top of the current screen
AdvancedPopupSystem.LayerHide(PopupLayerEnum.OVERLAY);                   // one layer
AdvancedPopupSystem.HideAll();                                          // everything

// Need the instance itself first? Await the loader (null if neither in-scene nor Addressable):
SettingsPopup popup = await AdvancedPopupSystem.GetPopupAsync<SettingsPopup>();
```

> **Rule of thumb:** open with `Show<T>()` / `GetPopupAsync<T>()` — they load on demand. `TryGetPopup<T>` is for secondary actions on a popup that's already open; it never loads. The full contract is at the top of the [documentation](documentation.md).

---

## ⚙️ Editor Workflow

Open the **APS** window from the top menu bar:

| Menu | Purpose |
| :--- | :--- |
| **`APS ▸ Layers`** | Add / rename / delete `PopupLayerEnum` flags (the enum is code-generated for you) and set each layer's canvas + sort order. |
| **`APS ▸ Displays`** | Scaffold a custom display — APS generates the display + settings scripts with ready-to-fill stubs. |
| **`APS ▸ Settings`** | Toggle the escape close stack and its key, auto input-module switching, inspector view, and log verbosity. |

See the [full documentation](documentation.md) for the complete API, custom-display authoring, and troubleshooting.

---

## 📸 Showcase & Gallery

### Editor Tools

The **APS Editor Window** lists every registered popup and traces active operations and tasks at runtime.

<p align="center">
  <img width="32%" alt="Editor View 1" src="https://github.com/user-attachments/assets/897b5471-2a7a-4c0a-8b07-b762a8ac9026" />
  <img width="32%" alt="Editor View 2" src="https://github.com/user-attachments/assets/6af2907b-7347-4381-9f8f-f50e44021ece" />
  <img width="32%" alt="Editor View 3" src="https://github.com/user-attachments/assets/84540c94-f3b6-4871-9595-fb5b2696cba0" />
</p>

### Popup Inspector

Configure animations, keys, child popups, and layer masks directly in the inspector. The **Preview** button plays the
show → hide cycle right in edit mode (no play mode needed) and restores the popup's state afterwards:

<p align="center">
  <img width="60%" alt="Inspector Settings" src="https://github.com/user-attachments/assets/9be62d64-fa84-47cd-a7e9-5febf067230b" />
  <img width="60%" alt="Inspector Settings" src="https://github.com/user-attachments/assets/74f790eb-2b85-490d-a217-2fc0b87e4d3e" />
</p>

### Animation Showcases

<p align="center">
  <img width="32%" alt="Showcase 1" src="https://github.com/user-attachments/assets/2ff31071-2874-480b-bc69-3c5d1ca26164" />
  <img width="32%" alt="Showcase 2" src="https://github.com/user-attachments/assets/dd874890-5cd0-4188-b173-14c1610d6d8a" />
  <img width="32%" alt="Showcase 3" src="https://github.com/user-attachments/assets/e8e6678c-e278-4ccc-a368-9dd90af49dd8" />
</p>

---

_Developed with the support of the Hyperfactory._
