namespace AdvancedPS.Core.Examples
{
    /// <summary>
    /// Addressable popup for the Easing showcase, spawned in one pooled copy per <c>EasingType</c> via
    /// <c>AdvancedPopupSystem.SpawnAsync</c> (Lane B) — each copy caches its own per-instance <c>SlideSettings</c>.
    /// A distinct type per sample so the type-keyed Addressable index can tell the samples' popups apart.
    /// </summary>
    public class EasingPopupDemo : AdvancedPopup
    {
    }
}
