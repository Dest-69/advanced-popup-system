using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    /// <summary>
    /// Core popup abstraction.
    /// </summary>
    public abstract class IAdvancedPopup : MonoBehaviour
    {
        #region Public
        /// <summary>
        /// The single layer this popup belongs to. Layers are canvas-bound (each layer routes to its own canvas), so a
        /// popup carries exactly one layer — or <see cref="PopupLayerEnum.None"/> to keep it out of layer control.
        /// </summary>
        [Tooltip("The single layer this popup belongs to. Layers are canvas-bound, so pick exactly one (or None to keep the popup out of layer control).")]
        public PopupLayerEnum PopupLayer;
        /// <summary>
        /// true - if need manual initialize popup via Init() func for better resources control.
        /// </summary>
        [Tooltip("true - if need manual initialize popup via Init() func for better resources control.")]
        public bool ManualInit;
        /// <summary>
        /// Recommended 'False' only for UI what should face player on startup scene.
        /// </summary>
        [Tooltip("Recommended 'False' only for UI what should face player on startup scene.")]
        public bool AutoHideOnInit = true;
        /// <summary>
        /// Set 'true' to prevent showing this popup.
        /// </summary>
        [Tooltip("Set 'true' to prevent showing this popup.")]
        public bool Inactive;
        /// <summary>
        /// How this popup takes part in the escape close stack (see AdvancedPopupSystem.EscapeStep):
        /// Hide — closes and consumes the step; Ignore (default) — the step falls through to the popup below;
        /// Block — consumes the step without closing (modal). Settable at runtime — that is how a popup joins or
        /// leaves the stack (see AdvancedPopupSystem.AddToEscapeStack / RemoveFromEscapeStack).
        /// </summary>
        [Tooltip("How this popup takes part in the escape close stack:\n" +
                 "Hide — closes and consumes the step.\n" +
                 "Ignore (default) — the step falls through to the popup below.\n" +
                 "Block — consumes the step without closing (modal).")]
        public EscapePolicyEnum EscapePolicy = EscapePolicyEnum.Ignore;
        /// <summary>
        /// Root transform.
        /// </summary>
        [HideInInspector] public RectTransform RootTransform;
        /// <summary>
        /// Canvas group of root popup.
        /// </summary>
        [HideInInspector] public CanvasGroup canvasGroup;
        /// <summary>
        /// State changed after animation started.
        /// </summary>
        [HideInInspector] public bool IsBeVisible;
        /// <summary>
        /// State changed after animation ended.
        /// </summary>
        [HideInInspector] public bool IsVisible;
        /// <summary>
        /// Data-driven interactive features (drag, resize, …) for this popup and their per-feature config.
        /// Enable features via the <see cref="PopupFeatureEnum"/> flags; the central PointerEventSystemAPS reads this —
        /// no extra components are added at runtime. Configure in the inspector's "Modules" box.
        /// </summary>
        public PopupModules Modules = new PopupModules();
        /// <summary>
        /// Load this popup's prefab from Addressables on demand instead of requiring it in the scene. The editor adds
        /// the prefab to the APS Addressables group and lists it in the generated index (see AddressablePopupIndex);
        /// at runtime the system loads it lazily on show (or up-front when <see cref="AddressableLoadMode"/> is Preload).
        /// </summary>
        [Space]
        [Header("Addressable")]
        [Tooltip("Load this popup's prefab from Addressables on demand instead of placing it in the scene.")]
        public bool Addressable;
        /// <summary>
        /// When the Addressable asset is brought into memory: Lazy (on first show) or Preload (up-front on boot).
        /// </summary>
        [Tooltip("When the Addressable asset is loaded: Lazy (on first show) or Preload (up-front on boot).")]
        public LoadMode AddressableLoadMode = LoadMode.Lazy;
        /// <summary>
        /// How many idle copies of this popup to keep alive for reuse instead of releasing them — the unified pool /
        /// on-hide control for Addressable popups. Applies both to a hidden unique popup (Lane A) and to the spawn pool
        /// (Lane B — <see cref="AdvancedPopupSystem.SpawnAsync{T}"/> / <see cref="AdvancedPopupSystem.Despawn"/>):
        /// <list type="bullet">
        /// <item><c>-1</c> — keep unlimited (never released, like a resident scene popup).</item>
        /// <item><c>0</c> — despawn on hide: release the Addressables handle so memory can unload (reloads next show).</item>
        /// <item><c>1</c> — a single on/off instance (the default): keep one idle copy for reuse, without allocating a pool.</item>
        /// <item><c>N</c> (≥2) — pool up to N idle copies; releasing beyond that frees the extras.</item>
        /// </list>
        /// </summary>
        [Tooltip("Idle copies kept for reuse:\n-1 = unlimited (never released)\n0 = despawn on hide (free memory)\n1 = single on/off instance (no pool)\n2+ = pool up to N.")]
        [Min(-1)]
        public int PoolCapacity = 1;
        /// <summary>
        /// Scenes in which a <see cref="LoadMode.Preload"/> popup is <b>preloaded up-front</b>, stored as their
        /// <b>asset GUIDs</b> (stable identity — reordering Build Settings never remaps them). <b>Empty (the default) =
        /// "Everyone"</b>: preloaded on every scene, so it loads on the very first scene (the classic boot-preload); list
        /// specific scenes to preload only when those load. Ignored for Lazy popups (they load purely on demand). On each
        /// scene load a matching popup is materialized if not already live — a popup shown before its scene arrives still
        /// loads on demand. The index bakes these GUIDs to scene paths for runtime matching (see AddressablePopupIndex).
        /// </summary>
        public List<string> PreloadSceneGuids = new List<string>();
        /// <summary>
        /// Scenes on entering which this popup is <b>released from memory</b>, stored as their <b>asset GUIDs</b> (stable
        /// identity). Applies to any Addressable popup, Lazy or Preload. When a listed scene loads, the resolver releases
        /// the resident instance (and any pooled idle copies of its type) so the asset can unload; a currently-visible
        /// instance is skipped. Empty (the default) = "None": never unloads.
        /// </summary>
        public List<string> UnloadSceneGuids = new List<string>();
        /// <summary>
        /// Identity of the <b>prefab asset</b> this popup came from — its GUID — so the draw-order catalog can rank two
        /// prefabs of the same class independently (see PopupOrderConfig). Stamped once per prefab by the APS editor
        /// tooling and never edited by hand; an instance carries whatever its prefab has, so every copy of one prefab
        /// shares a slot. Empty for a popup authored straight into a scene: those fall back to their type's slot.
        /// <para>
        /// Hidden in the inspector on purpose — it is identity, not a setting. A serialized field is the only way the
        /// runtime can know which asset an instance came from (there is no AssetDatabase in a build), the same reason
        /// <see cref="PreloadSceneGuids"/> stores GUIDs.
        /// </para>
        /// </summary>
        public string OrderKey => _orderKey;
        // Initialized, not left null: only the editor tooling ever writes it, and an unassigned serialized field is a
        // CS0649 warning in every consumer's console.
        [SerializeField, HideInInspector] private string _orderKey = string.Empty;
        #endregion
        
        #region Protected
        /// <summary>
        /// Cached method for the showing animation. To change it by call 'AdvancedPopupSystem.SetCachedDisplay'.
        /// </summary>
        protected IDisplay cachedShowDisplay { get; private set; }
        /// <summary>
        /// Cached method for the hiding animation. To change it by call 'AdvancedPopupSystem.SetCachedDisplay'.
        /// </summary>
        protected IDisplay cachedHideDisplay { get; private set; }
        
        protected CancellationTokenSource Source;
        
        /// <summary>
        /// Cached settings for the showing animation — set at runtime via 'SetCachedDisplay'. Runtime-only: it is an
        /// interface reference, which Unity's serializer ignores (real inspector persistence would need
        /// [SerializeReference] + a custom drawer), so a plain [SerializeField] here was a no-op.
        /// </summary>
        public IDisplaySettings CachedShowSettings { get; private set; }

        /// <summary>
        /// Cached settings for the hiding animation — set at runtime via 'SetCachedDisplay'. Runtime-only (see
        /// <see cref="CachedShowSettings"/>).
        /// </summary>
        public IDisplaySettings CachedHideSettings { get; private set; }
        #endregion

        #region Init
        private void Awake()
        {
            // Addressable popups are instantiated on demand by the resolver, which never calls Init() itself — Awake is
            // their only init trigger. ManualInit must never suppress it for an Addressable popup, or the loaded instance
            // would never register (SpawnAsync/preload/lazy all rely on Init() having run). ManualInit stays meaningful
            // only for scene-placed popups. See AddressablesPopupResolver / AdvancedPopupSystem.SpawnAsync.
            if (!ManualInit || Addressable)
                Init();
        }
        private void OnDestroy()
        {
            // Cancel any in-flight show/hide so its animation loop stops touching this destroyed transform,
            // and release the per-popup source (the last one is otherwise never disposed).
            TaskUtils.CancelAndDispose(Source);
            Source = null;

            AdvancedPopupSystem.DeactivateAdvancedPopup(this);
        }

        /// <summary>
        /// Method invoking manual or from Awake if "ManualInit" - false. Please keep base.Init() first of all when override.
        /// To define the show or/and hide animation - invoke SetCachedDisplay method here. 
        /// </summary>
        public virtual void Init()
        {
            SetupCache();
            
            if (!TryGetComponent(out RootTransform)) RootTransform = gameObject.AddComponent<RectTransform>();
            if (!TryGetComponent(out canvasGroup)) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
            if (AutoHideOnInit)
                cachedHideDisplay.HideInstantlyMethod(RootTransform, CachedHideSettings);
            else if (gameObject.activeSelf)
                cachedShowDisplay.ShowInstantlyMethod(RootTransform, CachedShowSettings);
            
            AdvancedPopupSystem.InitAdvancedPopup(this);
        }
        #endregion

        #region Cache
        /// <summary>
        /// Initializes null caches of displays with appropriate types.
        /// </summary>
        private void SetupCache()
        {
            if (cachedShowDisplay == null && cachedHideDisplay == null) 
            {
                SetCachedDisplay<ScaleDisplay>();
                return;
            }
            if (cachedShowDisplay == null) 
            {
                var hideType = cachedHideDisplay.GetType();
                SetCachedDisplayMixed(typeof(ScaleDisplay), hideType, 
                    CachedShowSettings, CachedHideSettings);
                return;
            }
            if (cachedHideDisplay == null) 
            {
                var showType = cachedShowDisplay.GetType();
                SetCachedDisplayMixed(showType, typeof(ScaleDisplay), 
                    CachedShowSettings, CachedHideSettings);
            }
        }
        
        private void SetCachedDisplayMixed(Type showT, Type hideT, IDisplaySettings show, IDisplaySettings hide) {
            var m = typeof(IAdvancedPopup)
                .GetMethod("SetCachedDisplay", BindingFlags.Instance | BindingFlags.Public, null,
                    new[]{ typeof(IDisplaySettings), typeof(IDisplaySettings) }, null)!
                .MakeGenericMethod(showT, hideT);
            m.Invoke(this, new object[]{ show, hide });
        }
        
        /// <summary>
        /// Sets the cached display to an instance of the specified advanced popup display type,
        /// initialized with the provided settings.
        /// </summary>
        /// <typeparam name="T">The type of advanced popup display to create and show cache.</typeparam>
        /// <param name="showSettings">The settings for the showing animation. If not provided, the default settings will be used.</param>
        public void SetCachedDisplay<T>(IDisplaySettings<T> showSettings = null)
            where T : IDisplay, new()
        {
            var display = DisplayRegistry.Get<T>();
            showSettings ??= DisplaySettingsFactory.GetDefaultSettings<T>();
            
            cachedShowDisplay = display;
            CachedShowSettings = showSettings;
            cachedHideDisplay = display;
            CachedHideSettings = showSettings;
        }

        /// <summary>
        /// Sets the cached display to an instance of the specified advanced popup display type,
        /// initialized with the provided settings.
        /// </summary>
        /// <typeparam name="T">The type of advanced popup display to create and show cache.</typeparam>
        /// <typeparam name="J">The type of advanced popup display to create and hide cache.</typeparam>
        /// <param name="showSettings">The settings for the showing animation. If not provided, the default settings will be used.</param>
        /// <param name="hideSettings">The settings for the hiding animation. If not provided, the default settings will be used.</param>
        public void SetCachedDisplay<T,J>(IDisplaySettings<T> showSettings = null, IDisplaySettings<J> hideSettings = null) 
            where T : IDisplay, new() where J : IDisplay, new()
        {
            cachedShowDisplay = DisplayRegistry.Get<T>();
            CachedShowSettings = showSettings ?? DisplaySettingsFactory.GetDefaultSettings<T>();
            cachedHideDisplay = DisplayRegistry.Get<J>();
            CachedHideSettings = hideSettings ?? DisplaySettingsFactory.GetDefaultSettings<J>();
        }
        #endregion

        #region Switch between Show/Hide
        public abstract void Cmd_SwitchShowHide();
        public abstract Operation SwitchShowHide(IDisplaySettings settings = null);
        public abstract Task SwitchShowHideAsync(CancellationToken token = default, IDisplaySettings settings = null);
        public abstract Operation SwitchShowHide<T>(IDisplaySettings<T> settings = null)
            where T : IDisplay, new();
        public abstract Task SwitchShowHideAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
            where T : IDisplay, new();
        #endregion

        #region SHOW
        /// <summary>
        /// Show popup command, mostly used for UnityEvent attachments in inspector.
        /// </summary>
        public abstract void Cmd_Show();
        /// <summary>
        /// Show popup by CachedDisplay type without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Operation Show(IDisplaySettings settings = null);
        /// <summary>
        /// Show popup by CachedDisplay type.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Task ShowAsync(CancellationToken token = default, IDisplaySettings settings = null);

        /// <summary>
        /// Show popup by IAdvancedPopupDisplay generic T type for all popup's without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Operation Show<T>(IDisplaySettings<T> settings = null)
            where T : IDisplay, new();
        /// <summary>
        /// Show popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Task ShowAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
            where T : IDisplay, new();
        #endregion

        #region HIDE
        /// <summary>
        /// Hide popup command, mostly used for UnityEvent attachments in inspector.
        /// </summary>
        public abstract void Cmd_Hide();
        /// <summary>
        /// Hide popup by CachedDisplay type without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Operation Hide(IDisplaySettings settings = null);
        /// <summary>
        /// Hide popup by CachedDisplay type.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Task HideAsync(CancellationToken token = default, IDisplaySettings settings = null);
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for all popup's without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Operation Hide<T>(IDisplaySettings<T> settings = null)
            where T : IDisplay, new();
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Task HideAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
            where T : IDisplay, new();
        #endregion
        
        #region Editor
#if UNITY_EDITOR
        private void Reset()
        {
            MoveComponentToTop(this);
        }

        private static void MoveComponentToTop(Component component)
        {
            Component[] components = component.gameObject.GetComponents<Component>();
            int index = Array.IndexOf(components, component);

            if (index > 1) 
            {
                for (int i = index; i > 1; i--)
                {
                    UnityEditorInternal.ComponentUtility.MoveComponentUp(component);
                }
            }
        }
#endif
        #endregion
    }
}
