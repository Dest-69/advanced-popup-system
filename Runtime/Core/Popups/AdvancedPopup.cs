using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
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
        #endregion

        #region Private
        private bool _isSubscribed;
        #endregion
       
        #region Sub/Unsub
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

            Button closeButton = Modules.CloseButton;
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

            Button closeButton = Modules.CloseButton;
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseButtonPress);
            OnHided?.Invoke();

            AdvancedPopupSystem.ActivePopups.Remove(this);
        }
        #endregion
        
        #region Switch between Show/Hide
        public override void Cmd_SwitchShowHide()
        {
            if ((!IsVisible || !IsBeVisible) && !Inactive)
                Show();
            else
                Hide();
        }
        public override Operation SwitchShowHide(IDisplaySettings settings = null)
        {
            if ((!IsVisible || !IsBeVisible) && !Inactive)
                return Show(settings);
            
            return Hide(settings);
        }
        public override Task SwitchShowHideAsync(CancellationToken token = default, IDisplaySettings settings = null)
        {
            if ((!IsVisible || !IsBeVisible) && !Inactive)
                return ShowAsync(token, settings);

            return HideAsync(token, settings);
        }

        public override Operation SwitchShowHide<T>(IDisplaySettings<T> settings = null)
        {
            if ((!IsVisible || !IsBeVisible) && !Inactive)
                return Show<T>(settings);

            return Hide<T>(settings);
        }

        public override Task SwitchShowHideAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
        {
            if ((!IsVisible || !IsBeVisible) && !Inactive)
                return ShowAsync<T>(token, settings);

            return HideAsync<T>(token, settings);
        }
        #endregion

        #region SHOW
        /// <summary>
        /// Show popup command, mostly used for UnityEvent attachments in inspector.
        /// </summary>
        public override void Cmd_Show()
        {
            if (Inactive || IsBeVisible) return;
            Show();
        }
        /// <summary>
        /// Show popup by CachedDisplay type without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override Operation Show(IDisplaySettings settings = null)
        {
            return new Operation(async token =>
            {
                if (Inactive || IsBeVisible) return;
                await ShowAsync(token, settings);
            });
        }

        /// <summary>
        /// Show popup by CachedDisplay type.
        /// </summary>
        /// <param name="token"> (Optional) For control Task life-cycle. </param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override async Task ShowAsync(CancellationToken token = default, IDisplaySettings settings = null)
        {
            if (this == null) return;
            if (Inactive || IsBeVisible) return;
            IsBeVisible = true;
            
            Source = TaskUtils.UpdateCancellationTokenSource(Source, true, token);
            token = Source.Token;
            APSStats.RegisterTask();

            try
            {
                gameObject.SetActive(true);

                // Claim the sibling slot the Order catalog gives this popup inside its canvas — a show, not the load
                // that happened at some point in the past, is what decides who is in front (see
                // AdvancedPopupSystem.ApplyOrder). No-op outside an APS-routed canvas.
                AdvancedPopupSystem.ApplyOrder(this);

                Subscribe();

                await cachedShowDisplay.ShowMethod(RootTransform, settings ??= CachedShowSettings, token);
            }
            finally
            {
                APSStats.UnregisterTask();
            }

            if (TaskUtils.OperationCancelled(token))
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
        public override Operation Show<T>(IDisplaySettings<T> settings = null)
        {
            return new Operation(async token =>
            {
                if (Inactive || IsBeVisible) return;
                await ShowAsync<T>(token, settings);
            });
        }
        /// <summary>
        /// Show popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="token"> (Optional) For control Task life-cycle. </param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override async Task ShowAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
        {
            if (this == null) return;
            if (Inactive || IsBeVisible) return;
            IsBeVisible = true;

            Source = TaskUtils.UpdateCancellationTokenSource(Source, true, token);
            token = Source.Token;
            APSStats.RegisterTask();

            try
            {
                gameObject.SetActive(true);

                // Same as the cached-display path: the show claims this popup's ordered slot in its canvas.
                AdvancedPopupSystem.ApplyOrder(this);

                Subscribe();

                IDisplay popupDisplay = DisplayRegistry.Get<T>();
                await popupDisplay.ShowMethod(RootTransform, settings ??= CachedShowSettings as IDisplaySettings<T>, token);
            }
            finally
            {
                APSStats.UnregisterTask();
            }

            if (TaskUtils.OperationCancelled(token))
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
        public override Operation Hide(IDisplaySettings settings = null)
        {
            return new Operation(async token =>
            {
                if (!IsBeVisible) return;
                await HideAsync(token, settings);
            });
        }
        /// <summary>
        /// Hide popup by CachedDisplay type.
        /// </summary>
        /// <param name="token"> (Optional) For control Task life-cycle. </param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override async Task HideAsync(CancellationToken token = default, IDisplaySettings settings = null)
        {
            if (!IsBeVisible) return;
            IsBeVisible = false;
            
            Source = TaskUtils.UpdateCancellationTokenSource(Source, true, token);
            token = Source.Token;
            APSStats.RegisterTask();

            try
            {
                Unsubscribe();

                await cachedHideDisplay.HideMethod(RootTransform, settings ??= CachedHideSettings, token);
            }
            finally
            {
                APSStats.UnregisterTask();
            }

            if (TaskUtils.OperationCancelled(token))
            {
                IsBeVisible = true;
                return;
            }

            if (this == null) return;
            IsVisible = false;
            // Addressable Lane-A popup with PoolCapacity 0 ("despawn on hide"): release the handle so memory can unload;
            // any other capacity keeps the instance resident. Spawned Lane-B instances apply PoolCapacity in
            // AdvancedPopupSystem.Despawn(), not here.
            if (Addressable && PoolCapacity == 0
                && AdvancedPopupSystem.Resolver != null && !AdvancedPopupSystem.SpawnedPopups.Contains(this))
                AdvancedPopupSystem.Resolver.Release(this);
            else
                gameObject.SetActive(false);
        }
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for all popup's without await.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override Operation Hide<T>(IDisplaySettings<T> settings = null)
        {
            return new Operation(async token =>
            {
                if (!IsBeVisible) return;
                await HideAsync<T>(token, settings);
            });
        }
        /// <summary>
        /// Hide popup by IAdvancedPopupDisplay generic T type for all popup's.
        /// </summary>
        /// <param name="token"> (Optional) For control Task life-cycle. </param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public override async Task HideAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
        {
            if (!IsBeVisible) return;
            IsBeVisible = false;
            
            Source = TaskUtils.UpdateCancellationTokenSource(Source, true, token);
            token = Source.Token;
            APSStats.RegisterTask();

            try
            {
                Unsubscribe();

                IDisplay popupDisplay = DisplayRegistry.Get<T>();
                await popupDisplay.HideMethod(RootTransform, settings ??= CachedHideSettings as IDisplaySettings<T>, token);
            }
            finally
            {
                APSStats.UnregisterTask();
            }

            if (TaskUtils.OperationCancelled(token))
            {
                IsBeVisible = true;
                return;
            }

            if (this == null) return;
            IsVisible = false;
            // Addressable Lane-A popup with PoolCapacity 0 ("despawn on hide"): release the handle so memory can unload;
            // any other capacity keeps the instance resident. Spawned Lane-B instances apply PoolCapacity in
            // AdvancedPopupSystem.Despawn(), not here.
            if (Addressable && PoolCapacity == 0
                && AdvancedPopupSystem.Resolver != null && !AdvancedPopupSystem.SpawnedPopups.Contains(this))
                AdvancedPopupSystem.Resolver.Release(this);
            else
                gameObject.SetActive(false);
        }
        #endregion
    }
}