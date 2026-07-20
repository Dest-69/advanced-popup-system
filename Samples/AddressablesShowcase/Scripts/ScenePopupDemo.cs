namespace AdvancedPS.Core.Examples
{
    /// <summary>
    /// In-scene popup (NOT Addressable): placed directly in the showcase scene, so it shows instantly. A distinct
    /// AdvancedPopup subclass — the Addressable index is keyed by type, so each demo popup gets its own type.
    /// Visuals are authored on the prefab/scene object, not built in code.
    /// </summary>
    public class ScenePopupDemo : AdvancedPopup
    {
    }
}
