---
tags: [tree/displays]
type: leaf
parent: "[[Displays & Animations]]"
status: active
description: How to author a new display — via the Displays codegen panel or by hand (DisplayBase<TSettings> + BaseSettings<TDisplay>), and how to cache it on a popup.
code_paths:
  - Assets/advanced-popup-system/Runtime/Generated/Displays/
---

# Display — Custom

Adding your own animation driver; contract & pipeline in [[Displays & Animations]].

## Two ways to create it

- **Codegen (preferred):** APS window → **Displays** tab → `+` → name it → it generates
  `Runtime/Generated/Displays/<Name>Display/<Name>Display.generated.cs` + `<Name>Settings.generated.cs` with the four
  method stubs (`/* Your code here */`). Details & guards in [[Editor & Codegen]].
- **By hand:** a `[Serializable]` settings class `: BaseSettings<TDisplay>` (add your tunable fields) and a display
  class `: DisplayBase<TSettings>` implementing `ShowInstantlyMethod`/`HideInstantlyMethod`/`ShowMethod`/`HideMethod`.

## Rules to honor

- **Keep the display stateless** — one shared instance is reused via `DisplayRegistry` ([[Displays & Animations]]);
  keep per-run state in locals / on the `RectTransform`.
- Fire `settings.OnAnimationStart` / `OnAnimationEnd`; guard async loops with `TaskUtils.OperationCancelled(token)` and
  `await Task.Yield()`; write the exact final state after the loop.
- Optional: a public static `TSettings Default()` on the settings — `DisplaySettingsFactory` prefers it over the
  parameterless ctor for defaults.
- **Naming is mandatory** ([[Invariants]]): `<Name>Display` + `<Name>Settings` inside a `<Name>Display` folder.

## Use it

In the popup's `Init()`: `SetCachedDisplay<MyDisplay>(new MySettings { … })` (same for show+hide) or
`SetCachedDisplay<MyShow, MyHide>(showSettings, hideSettings)`, then `base.Init()` ([[Popup Lifecycle]]). Or per call:
`popup.Show<MyDisplay>(settings)` / `AdvancedPopupSystem.LayerShow<MyDisplay>(layer, settings)`.
