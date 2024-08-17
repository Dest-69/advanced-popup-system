using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedPS.Core
{
    [RequireComponent(typeof(CanvasGroup))]
    public class AdvancedPopup : IAdvancedPopup
    {
        #region Public
        /// <summary>
        /// For public events.
        /// </summary>
        public Action OnShowing;
        /// <summary>
        /// For public events.
        /// </summary>
        public Action OnHided;
        /// <summary>
        /// This field can be null.
        /// </summary>
        [Header("REF's")]
        [Tooltip("This field can be null")]
        public Button closeButton;
        #endregion
        
        private bool _isSubscribed;

        /// <summary>
        /// Handler for close button press event.
        /// </summary>
        public virtual void OnCloseButtonPress()
        {
            Hide();
        }

        /// <summary>
        /// Subscribe for local events.
        /// </summary>
        protected virtual void Subscribe()
        {
            if (_isSubscribed) return;
            _isSubscribed = true;
            
            if (closeButton) closeButton.onClick.AddListener(OnCloseButtonPress);
            OnShowing?.Invoke();
            
            AdvancedPopupSystem.ActivePopups.Add(this);
        }

        /// <summary>
        /// Unsubscribe for local events.
        /// </summary>
        protected virtual void Unsubscribe()
        {
            if (!_isSubscribed) return;
            _isSubscribed = false;
            
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseButtonPress);
            OnHided?.Invoke();
            
            AdvancedPopupSystem.ActivePopups.Remove(this);
        }

        #region SHOW
        /// <summary>
        /// Show popup command, mostly used for UnityEvent attachments in inspector.
        /// </summary>
        public override void Cmd_Show()
        {
            Show();
        }
        /// <summary>
        /// Show popup by CachedDisplay type without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override Operation Show(BaseSettings settings = null)
        {
            if (IsBeVisible) return new Operation();
            
            return new Operation(async token =>
            {
                await ShowAsync(token, settings);
            }, UpdateCancellationTokenSource());
        }
        /// <summary>
        /// Show popup by CachedDisplay type.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override async Task ShowAsync(CancellationToken token = default, BaseSettings settings = null)
        {
            if (IsBeVisible) return;
            IsBeVisible = true;

            if (token == default)
                token = UpdateCancellationTokenSource().Token;
            
            Subscribe();
            
            List<Task> tasks = new List<Task>
            {
                CachedShowDisplay.ShowMethod(RootTransform, settings ??= CachedShowSettings, token)
            };
            tasks.AddRange(DeepPopups.Select(popup => popup.ShowAsync(token)));

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (token.IsCancellationRequested)
            {
                IsBeVisible = false;
                return;
            }
            
            IsVisible = true;
        }
        /// <summary>
        /// Show popup by IAdvancedPopupDisplay generic T type for all popup's without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override Operation Show<T>(BaseSettings settings = null)
        {
            if (IsBeVisible) return new Operation();
            
            return new Operation(async token =>
            {
                await ShowAsync<T>(token, settings);
            }, UpdateCancellationTokenSource());
        }
        /// <summary>
        /// Show popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override async Task ShowAsync<T>(CancellationToken token = default, BaseSettings settings = null)
        {
            if (IsBeVisible) return;
            IsBeVisible = true;
                
            if (token == default)
                token = UpdateCancellationTokenSource().Token;
            
            Subscribe();

            List<Task> tasks = new List<Task>();

            IDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
            tasks.Add(popupDisplay.ShowMethod(RootTransform, settings ??= CachedShowSettings, token));

            tasks.AddRange(DeepPopups.Select(popup => popup.ShowAsync<T>(token)));

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (token.IsCancellationRequested)
            {
                IsBeVisible = false;
                return;
            }
            
            IsVisible = true;
        }
        #endregion
        
        #region HIDE
        /// <summary>
        /// Hide popup command, mostly used for UnityEvent attachments in inspector.
        /// </summary>
        public override void Cmd_Hide()
        {
            Hide();
        }
        /// <summary>
        /// Hide popup by CachedDisplay type without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override Operation Hide(BaseSettings settings = null)
        {
            if (!IsBeVisible) return new Operation();
            
            return new Operation(async token =>
            {
                await HideAsync(token, settings);
            }, UpdateCancellationTokenSource());
        }
        /// <summary>
        /// Hide popup by CachedDisplay type.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override async Task HideAsync(CancellationToken token = default, BaseSettings settings = null)
        {
            if (!IsBeVisible) return;
            IsBeVisible = false;
            
            if (token == default)
                token = UpdateCancellationTokenSource().Token;
            
            Unsubscribe();
            
            List<Task> tasks = new List<Task>
            {
                CachedHideDisplay.HideMethod(RootTransform, settings ??= CachedHideSettings, token)
            };
            
            tasks.AddRange(DeepPopups.Select(popup => popup.HideAsync(token)));
            
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
            
            if (token.IsCancellationRequested)
            {
                IsBeVisible = true;
                return;
            }
            
            IsVisible = false;
        }
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for all popup's without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override Operation Hide<T>(BaseSettings settings = null)
        {
            if (!IsBeVisible) return new Operation();
            
            return new Operation(async token =>
            {
                await HideAsync<T>(token, settings);
            }, UpdateCancellationTokenSource());
        }
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override async Task HideAsync<T>(CancellationToken token = default, BaseSettings settings = null)
        {
            if (!IsBeVisible) return;
            IsBeVisible = false;
            
            if (token == default)
                token = UpdateCancellationTokenSource().Token;
            
            Unsubscribe();

            List<Task> tasks = new List<Task>();

            IDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
            tasks.Add(popupDisplay.HideMethod(RootTransform, settings ??= CachedHideSettings, token));

            tasks.AddRange(DeepPopups.Select(popup => popup.HideAsync<T>(token)));

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);

            if (token.IsCancellationRequested)
            {
                IsBeVisible = true;
                return;
            }
            
            IsVisible = false;
        }
        #endregion
    }
}