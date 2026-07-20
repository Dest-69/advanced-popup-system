namespace AdvancedPS.Core
{
    /// <summary>
    /// When an Addressable popup's asset is brought into memory (see AdvancedPopupSystem.Resolver).
    /// </summary>
    public enum LoadMode : byte
    {
        /// <summary>
        /// Loaded and instantiated on first show — the default. Nothing is in memory until the popup is needed.
        /// </summary>
        Lazy = 0,
        /// <summary>
        /// Loaded up-front by the boot preload pass (after the first scene loads), so the first show is instant.
        /// </summary>
        Preload = 1
    }
}
