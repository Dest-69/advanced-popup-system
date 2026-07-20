using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>
        /// This field can be null.
        /// </summary>
        [Header("REF's")]
        [Tooltip("This field can be null")]
        public Button closeButton;
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

            ShownByCascade = false;
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

                Subscribe();

                List<Task> tasks = new List<Task>
                {
                    cachedShowDisplay.ShowMethod(RootTransform, settings ??= CachedShowSettings, token)
                };
                foreach (IAdvancedPopup deepPopup in DeepPopups)
                {
                    MarkCascadeShow(deepPopup);
                    tasks.Add(deepPopup.ShowAsync(token));
                }

                if (tasks.Count > 0)
                    await Task.WhenAll(tasks);
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

                Subscribe();

                List<Task> tasks = new List<Task>();

                IDisplay popupDisplay = DisplayRegistry.Get<T>();
                tasks.Add(popupDisplay.ShowMethod(RootTransform, settings ??= CachedShowSettings as IDisplaySettings<T>, token));

                foreach (IAdvancedPopup deepPopup in DeepPopups)
                {
                    MarkCascadeShow(deepPopup);
                    tasks.Add(deepPopup.ShowAsync<T>(token));
                }

                if (tasks.Count > 0)
                    await Task.WhenAll(tasks);
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
        /// Marks a deep popup whose show is about to start as part of this popup's cascade, so the escape
        /// stack treats the group as one step (see AdvancedPopupSystem.EscapeStep). Popups already visible
        /// (shown independently before) keep their own stack entry.
        /// </summary>
        private static void MarkCascadeShow(IAdvancedPopup deepPopup)
        {
            if (deepPopup != null && !deepPopup.Inactive && !deepPopup.IsBeVisible)
                deepPopup.ShownByCascade = true;
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

                List<Task> tasks = new List<Task>
                {
                    cachedHideDisplay.HideMethod(RootTransform, settings ??= CachedHideSettings, token)
                };

                tasks.AddRange(DeepPopups.Select(popup => popup.HideAsync(token)));

                if (tasks.Count > 0)
                    await Task.WhenAll(tasks);
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
            gameObject.SetActive(false);
            IsVisible = false;
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

                List<Task> tasks = new List<Task>();

                IDisplay popupDisplay = DisplayRegistry.Get<T>();
                tasks.Add(popupDisplay.HideMethod(RootTransform, settings ??= CachedHideSettings as IDisplaySettings<T>, token));

                tasks.AddRange(DeepPopups.Select(popup => popup.HideAsync<T>(token)));

                if (tasks.Count > 0)
                    await Task.WhenAll(tasks);
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
            gameObject.SetActive(false);
            IsVisible = false;
        }
        #endregion
    }
}