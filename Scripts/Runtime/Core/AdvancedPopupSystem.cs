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

        public static void InitAdcencedPopup<T>(T popup) where T : IAdvancedPopup
        {
            if (!_popups.Contains(popup))
                _popups.Add(popup);
        }
        
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

        #region FORCE SHOW
        public static async void ForceShow<T>(string popupName, bool deepShow = false, bool deepHide = true) where T : IAdvancedPopupDisplay, new()
        {
            IAdvancedPopup _popup = _popups.FirstOrDefault(popup => popup.PopupName == popupName);
            if (_popup == default)
            {
                Debug.LogError($"AdvancedPopupSystem not found popup with name '{popupName}'!");
                return;
            }

            CancellationToken cToken = UpdateCancellationTokenSource();

            List<Task> tasks = new List<Task>();
            foreach (IAdvancedPopup popup in _popups.Where(popup => popup != _popup))
            {
                if (_popup.ContainsDeepPopup(popup))
                    continue;
                
                tasks.Add(popup.Hide<T>(deepHide, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (cToken.IsCancellationRequested)
                return;
            
            await _popup.Show<T>(deepShow, cancellationToken: cToken);
        }

        public static async void ForceShow<T, J>(string popupName, bool deepShow = false, bool deepHide = true)
            where T : IAdvancedPopupDisplay, new() where J : IAdvancedPopupDisplay, new()
        {
            IAdvancedPopup _popup = _popups.FirstOrDefault(popup => popup.PopupName == popupName);
            if (_popup == default)
            {
                Debug.LogError($"AdvancedPopupSystem not found popup with name '{popupName}'!");
                return;
            }
            
            CancellationToken cToken = UpdateCancellationTokenSource();
            
            List<Task> tasks = new List<Task>();
            foreach (IAdvancedPopup popup in _popups.Where(popup => popup != _popup))
            {
                if (_popup.ContainsDeepPopup(popup))
                    continue;
                
                tasks.Add(popup.Hide<T, J>(deepHide, cancellationToken: cToken));
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (cToken.IsCancellationRequested)
                return;

            await _popup.Show<T, J>(deepShow, cancellationToken: cToken);
        }
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
