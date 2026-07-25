namespace AdvancedPS.Core
{
    /// <summary>
    /// How a popup participates in the escape close stack (see AdvancedPopupSystem.EscapeStep).
    /// </summary>
    public enum EscapePolicyEnum : byte
    {
        /// <summary>
        /// The popup closes when the escape step reaches it, consuming the step.
        /// </summary>
        Hide = 0,
        /// <summary>
        /// The popup is transparent for the escape step — the step falls through to the popup shown before it.
        /// </summary>
        Ignore = 1,
        /// <summary>
        /// The popup consumes the step without closing (modal) — popups behind it can't be escape-closed while it is visible.
        /// </summary>
        Block = 2
    }
}
