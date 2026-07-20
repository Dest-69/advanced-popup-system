namespace AdvancedPS.Core
{
    /// <summary>
    /// Which rectangle a draggable/resizable popup is clamped inside.
    /// </summary>
    public enum BoundsMode
    {
        /// <summary> The root Canvas rect (equals the screen for Screen Space - Overlay). Default. </summary>
        Canvas = 0,
        /// <summary> The device safe area (notch / rounded-corner inset), mapped into canvas space. </summary>
        SafeArea = 1,
        /// <summary> A custom RectTransform supplied per feature (the config's CustomBounds field). </summary>
        Custom = 2,
        /// <summary> No clamping — the popup can leave the screen. </summary>
        None = 3,
    }
}
