using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    public abstract partial class IAdvancedPopup : MonoBehaviour
    {
        #region Public
        /// <summary>
        /// Layer of popup, check all layers where you expect this popup can be shown.
        /// </summary>
        [Tooltip("Layer of popup, check all layers where you expect this popup can be shown.")]
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
        /// Child or dependent popups of the current one, use if you need more control via Show/Hide.
        /// </summary>
        [Tooltip("Child or dependent popups of the current one, use if you need more control via Show/Hide.")] [Space]
        public List<IAdvancedPopup> DeepPopups = new List<IAdvancedPopup>();
        /// <summary>
        /// Settings for showing popup by key binding.
        /// </summary>
        [Tooltip("Settings for showing popup by key binding.")]
        public PopupKeyBinding KeyBindingShowSettings;
        /// <summary>
        /// Settings for hiding popup by key binding.
        /// </summary>
        [Tooltip("Settings for hiding popup by key binding.")]
        public PopupKeyBinding KeyBindingHideSettings;
        #endregion
        
        #region Protected
        /// <summary>
        /// Cached method for the showing animation. To change it by call 'AdvancedPopupSystem.SetCachedDisplay'.
        /// </summary>
        protected IDisplay CachedShowDisplay { get; private set; }
        /// <summary>
        /// Cached method for the hiding animation. To change it by call 'AdvancedPopupSystem.SetCachedDisplay'.
        /// </summary>
        protected IDisplay CachedHideDisplay { get; private set; }
        
        [SerializeField] private BaseSettings cachedShowSettings = new ScaleSettings();
        /// <summary>
        /// Cached settings for the showing animation. To change it by call 'AdvancedPopupSystem.SetCachedDisplay'.
        /// </summary>
        public BaseSettings CachedShowSettings
        {
            get => cachedShowSettings;
            private set => cachedShowSettings = value;
        }
        
        [SerializeField] private BaseSettings cachedHideSettings = new ScaleSettings();
        /// <summary>
        /// Cached settings for the hiding animation. To change it by call 'AdvancedPopupSystem.SetCachedDisplay'.
        /// </summary>
        public BaseSettings CachedHideSettings
        {
            get => cachedHideSettings;
            private set => cachedHideSettings = value;
        }
        #endregion
        
        #region Private
        //[SerializeField] private string inspectorShowDisplay;
        //[SerializeField] private string inspectorHideDisplay;
        #endregion

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
        
        private void Awake()
        {
            if (!ManualInit)
                Init();
        }
        private void OnDestroy()
        {
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
            {
                transform.localScale = Vector3.zero;
                
                canvasGroup.alpha = 0;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                IsBeVisible = false;
                IsVisible = false;
                
                if (gameObject.activeSelf)
                    gameObject.SetActive(false);
            }

            if (transform.localScale != Vector3.zero && canvasGroup.alpha != 0)
            {
                IsBeVisible = true;
                IsVisible = true;
                
                if (!gameObject.activeSelf)
                    gameObject.SetActive(true);
            }
            
            AdvancedPopupSystem.InitAdvancedPopup(this);
        }

        /// <summary>
        /// Initializes null caches of displays with appropriate types.
        /// </summary>
        private void SetupCache()
        {
            if (CachedShowDisplay == null && CachedHideDisplay == null) 
            {
                SetCachedDisplayInternal<ScaleDisplay>();
                return;
            }
            if (CachedShowDisplay == null) 
            {
                var hideType = CachedHideDisplay.GetType();
                SetCachedDisplayMixed(typeof(ScaleDisplay), hideType, 
                    CachedShowSettings, CachedHideSettings);
                return;
            }
            if (CachedHideDisplay == null) 
            {
                var showType = CachedShowDisplay.GetType();
                SetCachedDisplayMixed(showType, typeof(ScaleDisplay), 
                    CachedShowSettings, CachedHideSettings);
            }
        }
        
        private void SetCachedDisplayMixed(Type showT, Type hideT, BaseSettings show, BaseSettings hide) {
            var m = typeof(IAdvancedPopup)
                .GetMethod("SetCachedDisplayInternal", BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new[]{ typeof(BaseSettings), typeof(BaseSettings) }, null)!
                .MakeGenericMethod(showT, hideT);
            m.Invoke(this, new object[]{ show, hide });
        }
        
        /// <summary>
        /// Sets the cached display to an instance of the specified advanced popup display type,
        /// initialized with the provided settings.
        /// </summary>
        /// <typeparam name="T">The type of advanced popup display to create and show cache.</typeparam>
        /// <param name="showSettings">The settings for the showing animation. If not provided, the default settings will be used.</param>
        private void SetCachedDisplayInternal<T>(BaseSettings showSettings = null) where T : IDisplay, new()
        {
            CachedShowDisplay = AdvancedPopupSystem.GetDisplay<T>();
            CachedShowSettings = showSettings;
            CachedHideDisplay = AdvancedPopupSystem.GetDisplay<T>();
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
        private void SetCachedDisplayInternal<T,J>(BaseSettings showSettings = null, BaseSettings hideSettings = null) where T : IDisplay, new() where J : IDisplay, new()
        {
            CachedShowDisplay = AdvancedPopupSystem.GetDisplay<T>();
            CachedShowSettings = showSettings;
            CachedHideDisplay = AdvancedPopupSystem.GetDisplay<J>();
            CachedHideSettings = hideSettings;
        }

        /// <summary>
        /// Check if popup exist in deep of this popup. (infinite loop safe by Dfs)
        /// </summary>
        /// <param name="popup"> popup what we are searching </param>
        public virtual bool ContainsDeepPopup(IAdvancedPopup popup)
        {
            var st = new HashSet<IAdvancedPopup>();
            return Dfs(this);

            bool Dfs(IAdvancedPopup n)
            {
                if (n == null || !st.Add(n)) return false;
                return DeepPopups.Any(d => d == popup || d.ContainsDeepPopup(popup));
            }
        }

        #region SHOW
        /// <summary>
        /// Show popup command, mostly used for UnityEvent attachments in inspector.
        /// </summary>
        public abstract void Cmd_Show();
        /// <summary>
        /// Show popup by CachedDisplay type without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Operation Show(BaseSettings settings = null);
        /// <summary>
        /// Show popup by CachedDisplay type.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Task ShowAsync(CancellationToken token = default, BaseSettings settings = null);

        /// <summary>
        /// Show popup by IAdvancedPopupDisplay generic T type for all popup's without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Operation Show<T>(BaseSettings settings = null)
            where T : IDisplay, new();
        /// <summary>
        /// Show popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Task ShowAsync<T>(CancellationToken token = default, BaseSettings settings = null)
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
        public abstract Operation Hide(BaseSettings settings = null);
        /// <summary>
        /// Hide popup by CachedDisplay type.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Task HideAsync(CancellationToken token = default, BaseSettings settings = null);
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for all popup's without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Operation Hide<T>(BaseSettings settings = null)
            where T : IDisplay, new();
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public abstract Task HideAsync<T>(CancellationToken token = default, BaseSettings settings = null)
            where T : IDisplay, new();
        #endregion
    }
}
