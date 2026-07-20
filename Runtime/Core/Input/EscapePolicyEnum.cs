namespace AdvancedPS.Core
{
    /// <summary>
    /// How a popup participates in the escape close stack (see AdvancedPopupSystem.EscapeStep).
    /// </summary>
    public enum EscapePolicyEnum : byte
    {
        /// <summary>
        /// The popup closes when the escape step reaches it, consuming the key press.
        /// </summary>
        Hide = 0,
        /// <summary>
        /// The popup is transparent for the escape step — the key press falls through to the popup shown before it.
        /// </summary>
        Ignore = 1,
        /// <summary>
        /// The popup consumes the key press without closing (modal) — popups behind it can't be escape-closed while it is visible.
        /// </summary>
        Block = 2
    }
}
