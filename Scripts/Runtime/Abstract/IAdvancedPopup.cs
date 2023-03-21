using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.Enum;
using UnityEngine;

namespace AdvancedPS.Core
{
    public abstract class IAdvancedPopup : MonoBehaviour
    {
        /// <summary>
        /// Popup unique ID.
        /// </summary>
        [Header("Settings")]
        [Tooltip("Popup unique ID.")]
        public string PopupName;
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
        /// Root transform.
        /// </summary>
        [Header("REF's")][Space(5)]
        [Tooltip("Root transform.")]
        public Transform RootTransform;
        /// <summary>
        /// Child or dependent popups of the current one, use if you need more control via Show/Hide.
        /// </summary>
        [Tooltip("Child or dependent popups of the current one, use if you need more control via Show/Hide.")]
        public List<IAdvancedPopup> DeepPopups;
        /// <summary>
        /// Change CachedDisplay value if need it by "AdvancedPopupSystem.GetDisplay" - for better performance.
        /// </summary>
        protected IAdvancedPopupDisplay CachedDisplay { get; private set; }
        
        private CancellationTokenSource _source;

        private void Awake()
        {
            // Change CachedDisplay value if need it, like below - for better performance
            SetCachedDisplay<ImmediatelyScaleDisplay>();
            
            if (!ManualInit)
                Init();
        }

        /// <summary>
        /// Change CachedDisplay value by "AdvancedPopupSystem.GetDisplay" inside - for better performance.
        /// </summary>
        public void SetCachedDisplay<T>() where T : IAdvancedPopupDisplay, new()
        {
            CachedDisplay = AdvancedPopupSystem.GetDisplay<T>();
        }

        /// <summary>
        /// Method invoking manual or from Awake if "ManualInit" - false. Please keep base.Init() first of all when override.
        /// </summary>
        public virtual void Init()
        {
            AdvancedPopupSystem.InitAdcencedPopup(this);

            if (RootTransform == null)
                RootTransform = transform;

            if (string.IsNullOrEmpty(PopupName))
            {
                Debug.LogWarning($"IAdvancedPopup on '{gameObject.name}' object not set manual variable PopupName");
                PopupName = gameObject.name;
            }
        }

        /// <summary>
        /// Check if popup exist in deep of this popup.
        /// </summary>
        /// <param name="popup"> popup what we are searching </param>
        public virtual bool ContainsDeepPopup(IAdvancedPopup popup)
        {
            return DeepPopups.Any(_deepPopup => popup == _deepPopup || _deepPopup.ContainsDeepPopup(popup));
        }

        #region SHOW

        /// <summary>
        /// Show popup by CachedDisplay type.
        /// </summary>
        /// <param name="deepShow"> true - show all "DeepPopups" of this popup </param>
        /// <param name="cancellationToken"></param>
        public abstract Task Show(bool deepShow = false, CancellationToken cancellationToken = default);
        /// <summary>
        /// Show popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="deepShow"> true - show all "DeepPopups" of this popup </param>
        /// <param name="cancellationToken"></param>
        public abstract Task Show<T>(bool deepShow = false, CancellationToken cancellationToken = default)
            where T : IAdvancedPopupDisplay, new();
        /// <summary>
        /// Show popup by IAdvancedPopupDisplay generic T type for this popup and J type for "DeepPopups".
        /// </summary>
        /// <param name="deepShow"> true - show all "DeepPopups" of this popup </param>
        /// <param name="cancellationToken"></param>
        public abstract Task Show<T, J>(bool deepShow = false, CancellationToken cancellationToken = default)
            where T : IAdvancedPopupDisplay, new()
            where J : IAdvancedPopupDisplay, new();
        #endregion


        #region HIDE
        /// <summary>
        /// Hide popup by CachedDisplay type.
        /// </summary>
        /// <param name="deepHide"> true - hide all "DeepPopups" of this popup </param>
        /// <param name="cancellationToken"></param>
        public abstract Task Hide(bool deepHide = false, CancellationToken cancellationToken = default);
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="deepHide"> true - hide all "DeepPopups" of this popup </param>
        /// <param name="cancellationToken"></param>
        public abstract Task Hide<T>(bool deepHide = false, CancellationToken cancellationToken = default)
            where T : IAdvancedPopupDisplay, new();
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for this popup and J type for "DeepPopups".
        /// </summary>
        /// <param name="deepHide"> true - hide all "DeepPopups" of this popup </param>
        /// <param name="cancellationToken"></param>
        public abstract Task Hide<T, J>(bool deepHide = false, CancellationToken cancellationToken = default)
            where T : IAdvancedPopupDisplay, new()
            where J : IAdvancedPopupDisplay, new();
        #endregion

        /// <summary>
        /// Stop previous task's and start new.
        /// </summary>
        protected virtual CancellationToken UpdateCancellationTokenSource()
        {
            if (_source != null)
            {
                _source.Cancel();
                _source.Dispose();
            }
            _source = new CancellationTokenSource();
            return _source.Token;
        }
    }
}
