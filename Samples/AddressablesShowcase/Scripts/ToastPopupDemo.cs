namespace AdvancedPS.Core.Examples
{
    /// <summary>
    /// Addressable popup spawned in many pooled copies via <c>AdvancedPopupSystem.SpawnAsync</c> (Lane B). It is not
    /// part of any layer — you own its lifetime (<c>Despawn</c> returns it to the pool or releases it).
    /// </summary>
    public class ToastPopupDemo : AdvancedPopup
    {
    }
}
