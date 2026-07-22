namespace AdvancedPS.Core.Examples
{
    /// <summary>
    /// Addressable popup for the DoTween showcase, spawned in many pooled copies via
    /// <c>AdvancedPopupSystem.SpawnAsync</c> (Lane B) — each copy caches its own per-instance <c>DoTweenSettings</c>.
    /// A distinct type per sample so the type-keyed Addressable index can tell the samples' popups apart.
    /// </summary>
    public class TweenPopupDemo : AdvancedPopup
    {
    }
}
