namespace AdvancedPS.Core.Examples
{
    /// <summary>
    /// Addressable popup with <c>LoadMode.Preload</c>: loaded in the background at boot (after the first scene), so
    /// <c>LayerShow(GUI)</c> is instant with no load hitch.
    /// </summary>
    public class PreloadPopupDemo : AdvancedPopup
    {
    }
}
