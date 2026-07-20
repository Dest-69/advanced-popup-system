namespace AdvancedPS.Core
{
    /// <summary>
    /// What Hide does with an Addressable popup instance once its hide animation finishes. Ignored for
    /// non-Addressable (scene-authored) popups, which always just deactivate.
    /// </summary>
    public enum HideBehavior : byte
    {
        /// <summary>
        /// Deactivate and keep resident (gameObject.SetActive(false)); the Addressables handle stays loaded, so
        /// the next show is instant. The default — matches how scene-authored popups behave.
        /// </summary>
        Deactivate = 0,
        /// <summary>
        /// Release the instance and its Addressables handle so the assets can unload; the popup is reloaded on the
        /// next show. Frees memory at the cost of a load on each open.
        /// </summary>
        Despawn = 1
    }
}
