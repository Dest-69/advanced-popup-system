using System;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Seam that lets an <b>optional</b> editor integration surface its actions inside the APS window, without the main
    /// editor assembly referencing an assembly that may not exist in the project. Mirrors the runtime
    /// <c>AdvancedPopupSystem.Resolver</c> seam for Addressables: the optional assembly assigns its entry point at load,
    /// and the panel shows the control only while the delegate is set (so a project without the integration simply has no
    /// such button, instead of a dead menu item).
    /// </summary>
    public static class APSEditorTools
    {
        /// <summary>
        /// Rebuilds the Addressable popup index from a full project scan. Assigned by the APS Addressables editor
        /// assembly (<c>APS_ADDRESSABLES</c>); null when the integration is absent. Surfaced by the Settings tab.
        /// </summary>
        public static Action RegenerateAddressableIndex;
    }
}
