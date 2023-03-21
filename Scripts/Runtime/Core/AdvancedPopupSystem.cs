using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.Enum;
using UnityEngine;

namespace AdvancedPS.Core
{
    public static class AdvancedPopupSystem
    {
        private static readonly List<IAdvancedPopup> _popups = new List<IAdvancedPopup>();
        private static readonly List<IAdvancedPopupDisplay> _displays = new List<IAdvancedPopupDisplay>();
        
        private static CancellationTokenSource _source;

        /// <summary>
        /// Push popup entity into AdvancedPopupSystem for caching, without it AdvancedPopupSystem will not use popup in our logic.
        /// </summary>
        public static void InitAdvancedPopup<T>(T popup) where T : IAdvancedPopup
        {
            if (!_popups.Contains(popup))
                _popups.Add(popup);
        }
        
        /// <summary>
        /// Get display entity with caching in AdvancedPopupSystem for better performance, because we don't need duplicate entity.
        /// </summary>
        public static IAdvancedPopupDisplay GetDisplay<T>() where T : IAdvancedPopupDisplay, new()
        {
            IAdvancedPopupDisplay display = _displays.FirstOrDefault(popupDisplay => popupDisplay is T);
            if (display == default)
            {
                display = new T();
                display.Init();
                
                _displays.Add(display);
            }

            return display;
        }

        /// <summary>
        /// Show all popup's with layer by CachedDisplay type.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="deepShow"> true - show all "DeepPopups" with layer </param>
        /// <param name="deepHide"> true - hide all "DeepPopups" without layer </param>

        #region Layer systems
        public static async void LayerShow(PopupLayerEnum layer, bool deepShow = false, bool deepHide = true)
        {
            List<IAdvancedPopup> _popup = _popups.FindAll(popup => popup.PopupLayer.HasFlag(layer));
            if (_popup.Count <= 0)
            {
                Debug.LogError($"AdvancedPopupSystem not found popup/s by '{layer}' layer!");
                return;
            }

            CancellationToken cToken = UpdateCancellationTokenSource();

            List<Task> tasks = new List<Task>();
            foreach (IAdvancedPopup popup in _popups.Where(popup => !popup.PopupLayer.HasFlag(layer)))
            {
                tasks.Add(popup.Hide(deepHide, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (cToken.IsCancellationRequested)
                return;
            
            tasks.Clear();
            foreach (IAdvancedPopup popup in _popup)
            {
                tasks.Add(popup.Show(deepShow, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (cToken.IsCancellationRequested)
                return;
        }
        
        /// <summary>
        /// Show all popup's with layer by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="deepShow"> true - show all "DeepPopups" with layer </param>
        /// <param name="deepHide"> true - hide all "DeepPopups" without layer </param>
        public static async void LayerShow<T>(PopupLayerEnum layer, bool deepShow = false, bool deepHide = true) where T : IAdvancedPopupDisplay, new()
        {
            List<IAdvancedPopup> _popup = _popups.FindAll(popup => popup.PopupLayer.HasFlag(layer));
            if (_popup.Count <= 0)
            {
                Debug.LogError($"AdvancedPopupSystem not found popup/s by '{layer}' layer!");
                return;
            }

            CancellationToken cToken = UpdateCancellationTokenSource();

            List<Task> tasks = new List<Task>();
            foreach (IAdvancedPopup popup in _popups.Where(popup => !popup.PopupLayer.HasFlag(layer)))
            {
                tasks.Add(popup.Hide<T>(deepHide, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (cToken.IsCancellationRequested)
                return;
            
            tasks.Clear();
            foreach (IAdvancedPopup popup in _popup)
            {
                tasks.Add(popup.Show<T>(deepShow, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (cToken.IsCancellationRequested)
                return;
        }

        /// <summary>
        /// Show all popup's with layer by IAdvancedPopupDisplay generic T type for this popup and J type for "DeepPopups".
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="deepShow"> true - show all "DeepPopups" with layer </param>
        /// <param name="deepHide"> true - hide all "DeepPopups" without layer </param>
        public static async void LayerShow<T, J>(PopupLayerEnum layer, bool deepShow = false, bool deepHide = true)
            where T : IAdvancedPopupDisplay, new() where J : IAdvancedPopupDisplay, new()
        {
            List<IAdvancedPopup> _popup = _popups.FindAll(popup => popup.PopupLayer.HasFlag(layer));
            if (_popup.Count <= 0)
            {
                Debug.LogError($"AdvancedPopupSystem not found popup/s by '{layer}' layer!");
                return;
            }

            CancellationToken cToken = UpdateCancellationTokenSource();

            List<Task> tasks = new List<Task>();
            foreach (IAdvancedPopup popup in _popups.Where(popup => !popup.PopupLayer.HasFlag(layer)))
            {
                tasks.Add(popup.Hide<T, J>(deepHide, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (cToken.IsCancellationRequested)
                return;
            
            tasks.Clear();
            foreach (IAdvancedPopup popup in _popup)
            {
                tasks.Add(popup.Show<T, J>(deepShow, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (cToken.IsCancellationRequested)
                return;
        }
        #endregion

        #region HIDE ALL
        /// <summary>
        /// Hide all popup's by CachedDisplay type.
        /// </summary>
        /// <param name="deepHide"> true - hide all "DeepPopups" without layer </param>
        public static async void HideAll(bool deepHide = false)
        {
            CancellationToken cToken = UpdateCancellationTokenSource();
            
            List<Task> tasks = new List<Task>();
            foreach (IAdvancedPopup popup in _popups)
            {
                tasks.Add(popup.Hide(deepHide, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }
        
        /// <summary>
        /// Hide all popup's by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="deepHide"> true - hide all "DeepPopups" </param>
        public static async void HideAll<T>(bool deepHide = false) where T : IAdvancedPopupDisplay, new()
        {
            CancellationToken cToken = UpdateCancellationTokenSource();
            
            List<Task> tasks = new List<Task>();
            foreach (IAdvancedPopup popup in _popups)
            {
                tasks.Add(popup.Hide<T>(deepHide, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }
        
        /// <summary>
        /// Hide all popup's by IAdvancedPopupDisplay generic T type for this popup and J type for "DeepPopups".
        /// </summary>
        /// <param name="deepHide"> true - hide all "DeepPopups" </param>
        public static async void HideAll<T, J>(bool deepHide = true) where T : IAdvancedPopupDisplay, new()
            where J : IAdvancedPopupDisplay, new()
        {
            CancellationToken cToken = UpdateCancellationTokenSource();
            
            List<Task> tasks = new List<Task>();
            foreach (IAdvancedPopup popup in _popups)
            {
                tasks.Add(popup.Hide<T, J>(deepHide, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }
        #endregion
        
        /// <summary>
        /// Stop previous task's and start new.
        /// </summary>
        private static CancellationToken UpdateCancellationTokenSource()
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
