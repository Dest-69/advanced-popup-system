using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

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

        private bool isSubscribed;
        #endregion

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
            if (isSubscribed) return;
            isSubscribed = true;
            
            if (closeButton) closeButton.onClick.AddListener(OnCloseButtonPress);
            OnShowing?.Invoke();
        }

        /// <summary>
        /// Unsubscribe for local events.
        /// </summary>
        protected virtual void Unsubscribe()
        {
            if (!isSubscribed) return;
            isSubscribed = false;
            
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseButtonPress);
            OnHided?.Invoke();
        }

        /// <summary>
        /// Show the popup.
        /// </summary>
        public override Operation Show(BaseSettings settings = null)
        {
            if (IsBeVisible) return new Operation();
            
            return new Operation(async token =>
            {
                await ShowAsync(token, settings);
            }, UpdateCancellationTokenSource());
        }
        public override async Task ShowAsync(CancellationToken token = default, BaseSettings settings = null)
        {
            if (IsBeVisible) return;
            IsBeVisible = true;

            if (token == default)
                UpdateCancellationTokenSource();
            
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
        /// Show the popup with a specific display type.
        /// </summary>
        public override Operation Show<T>(BaseSettings settings = null)
        {
            if (IsBeVisible) return new Operation();
            
            return new Operation(async token =>
            {
                await ShowAsync<T>(token, settings);
            }, UpdateCancellationTokenSource());
        }
        public override async Task ShowAsync<T>(CancellationToken token = default, BaseSettings settings = null)
        {
            if (IsBeVisible) return;
            IsBeVisible = true;
                
            if (token == default)
                UpdateCancellationTokenSource();
            
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

        /// <summary>
        /// Hide the popup.
        /// </summary>
        public override Operation Hide(BaseSettings settings = null)
        {
            if (!IsBeVisible) return new Operation();
            
            return new Operation(async token =>
            {
                await HideAsync(token, settings);
            }, UpdateCancellationTokenSource());
        }
        public override async Task HideAsync(CancellationToken token = default, BaseSettings settings = null)
        {
            if (!IsBeVisible) return;
            IsBeVisible = false;
            
            if (token == default)
                UpdateCancellationTokenSource();
            
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

            Unsubscribe();
            IsVisible = false;
        }

        /// <summary>
        /// Hide the popup with a specific display type.
        /// </summary>
        public override Operation Hide<T>(BaseSettings settings = null)
        {
            if (!IsBeVisible) return new Operation();
            
            return new Operation(async token =>
            {
                await HideAsync<T>(token, settings);
            }, UpdateCancellationTokenSource());
        }
        public override async Task HideAsync<T>(CancellationToken token = default, BaseSettings settings = null)
        {
            if (!IsBeVisible) return;
            IsBeVisible = false;
            
            if (token == default)
                UpdateCancellationTokenSource();

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

            Unsubscribe();
            IsVisible = false;
        }
    }
}