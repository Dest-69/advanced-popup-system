using UnityEngine;
using UnityEngine.UI;

namespace AdvancedPS.Core.Examples
{
    /// <summary>
    /// Addressables feature demo. Builds a small button bar at runtime and drives popups through the public API:
    /// a SCENE popup (immediate), a LAZY Addressable HUB popup (loads on first show), a PRELOAD Addressable LOGIN popup
    /// (loaded at boot → instant), and pooled TOASTs via SpawnAsync. Every popup is draggable. Intentionally small —
    /// see documentation §9 "On-Demand Loading with Addressables".
    /// </summary>
    public class AddressablesShowcase : MonoBehaviour
    {
        [Tooltip("Canvas the demo UI is added under. Found or created automatically if left empty.")]
        [SerializeField] private Canvas _canvas;

        private Text _status;
        private int _toastCount;

        private void Start()
        {
            if (_canvas == null) _canvas = FindOrCreateCanvas();

            RectTransform bar = BuildColumn();
            // LayerShow returns an Operation; fire-and-forget from a button is fine (autohide:false so they stack).
            ShowcaseUI.Button(bar, "Show HUB (lazy load)", () => AdvancedPopupSystem.LayerShow(PopupLayerEnum.HUB, autohide: false));
            ShowcaseUI.Button(bar, "Show LOGIN (preloaded)", () => AdvancedPopupSystem.LayerShow(PopupLayerEnum.LOGIN, autohide: false));
            ShowcaseUI.Button(bar, "Toggle SCENE popup", ToggleScene);
            ShowcaseUI.Button(bar, "Spawn toast", SpawnToast);
            ShowcaseUI.Button(bar, "Hide all", () => AdvancedPopupSystem.HideAll());

            _status = ShowcaseUI.Label(bar, string.Empty, 13, TextAnchor.UpperLeft, Color.white);
            ((RectTransform)_status.transform).sizeDelta = new Vector2(220f, 64f);
        }

        private void Update()
        {
            if (_status == null) return;
            string resolver = AdvancedPopupSystem.Resolver != null ? "Addressables" : "none (install package)";
            _status.text = $"Active popups: {AdvancedPopupSystem.ActivePopups.Count}\n" +
                           $"Spawned toasts: {_toastCount}\n" +
                           $"Resolver: {resolver}";
        }

        /// <summary> Toggle the in-scene popup (it is already registered, so a synchronous lookup finds it). </summary>
        private void ToggleScene()
        {
            if (AdvancedPopupSystem.TryGetPopup<ScenePopupDemo>(out ScenePopupDemo scene))
                scene.SwitchShowHide();
        }

        /// <summary> Spawn a pooled toast at a random spot. Returns null (and does nothing) without the Addressables package. </summary>
        private async void SpawnToast()
        {
            ToastPopupDemo toast = await AdvancedPopupSystem.SpawnAsync<ToastPopupDemo>();
            if (toast == null) return;
            toast.RootTransform.anchoredPosition = new Vector2(Random.Range(-220f, 220f), Random.Range(-160f, 160f));
            toast.Show();
            _toastCount++;
        }

        private RectTransform BuildColumn()
        {
            var go = new GameObject("Demo Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -16f);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        private static Canvas FindOrCreateCanvas()
        {
            Canvas existing = FindObjectOfType<Canvas>();
            if (existing != null) return existing;

            var go = new GameObject("Showcase Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }
    }
}
