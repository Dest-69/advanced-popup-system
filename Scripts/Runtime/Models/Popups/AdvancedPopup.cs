using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedPS.Core.Popup
{
    [RequireComponent(typeof(CanvasGroup))]
    public class AdvancedPopup : IAdvancedPopup
    {
        public CanvasGroup _canvasGroup;
        public Button _closeButton;
    
        private bool _subscribed;

        public override void Init()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            base.Init();
        }

        public virtual void OnCloseButtonPress()
        {
            Hide<SmoothScaleDisplay>(true).ConfigureAwait(false);
        }
    
        public virtual void Subscribe()
        {
            _closeButton.onClick.AddListener(OnCloseButtonPress);
            _subscribed = true;
        }

        public virtual void Unsubscribe()
        {
            _closeButton.onClick.RemoveListener(OnCloseButtonPress);
            _subscribed = false;
        }

        public override async Task Show(bool deepShow = false, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
                cancellationToken = UpdateCancellationTokenSource();
        
            if (!_subscribed)
                Subscribe();
        
            List<Task> tasks = new List<Task>();
        
            IAdvancedPopupDisplay popupDisplay = base.CachedDisplay;
            tasks.Add(popupDisplay.Show(RootTransform, cancellationToken));

            if (deepShow)
            {
                foreach (IAdvancedPopup popup in DeepPopups)
                {
                    tasks.Add(popup.Show(true, cancellationToken));
                }
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        public override async Task Show<T>(bool deepShow = false, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
                cancellationToken = UpdateCancellationTokenSource();
        
            if (!_subscribed)
                Subscribe();
        
            List<Task> tasks = new List<Task>();
        
            IAdvancedPopupDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
            tasks.Add(popupDisplay.Show(RootTransform, cancellationToken));

            if (deepShow)
            {
                foreach (IAdvancedPopup popup in DeepPopups)
                {
                    tasks.Add(popup.Show<T>(true, cancellationToken));
                }
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        public override async Task Show<T, J>(bool deepShow = false, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
                cancellationToken = UpdateCancellationTokenSource();
        
            if (!_subscribed)
                Subscribe();
        
            List<Task> tasks = new List<Task>();
        
            IAdvancedPopupDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
            tasks.Add(popupDisplay.Show(RootTransform, cancellationToken));

            if (deepShow)
            {
                foreach (IAdvancedPopup popup in DeepPopups)
                {
                    tasks.Add(popup.Show<J>(true, cancellationToken));
                }
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        public override async Task Hide(bool deepHide = false, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
                cancellationToken = UpdateCancellationTokenSource();
        
            List<Task> tasks = new List<Task>();
        
            IAdvancedPopupDisplay popupDisplay = base.CachedDisplay;
            tasks.Add(popupDisplay.Hide(RootTransform, cancellationToken));

            if (deepHide)
            {
                foreach (IAdvancedPopup popup in DeepPopups)
                {
                    tasks.Add(popup.Hide(true, cancellationToken));
                }
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        
            if (_subscribed)
                Unsubscribe();
        }

        public override async Task Hide<T>(bool deepHide = false, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
                cancellationToken = UpdateCancellationTokenSource();
        
            List<Task> tasks = new List<Task>();
        
            IAdvancedPopupDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
            tasks.Add(popupDisplay.Hide(RootTransform, cancellationToken));

            if (deepHide)
            {
                foreach (IAdvancedPopup popup in DeepPopups)
                {
                    tasks.Add(popup.Hide<T>(true, cancellationToken));
                }
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        
            if (_subscribed)
                Unsubscribe();
        }

        public override async Task Hide<T, J>(bool deepHide = false, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
                cancellationToken = UpdateCancellationTokenSource();
        
            List<Task> tasks = new List<Task>();
        
            IAdvancedPopupDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
            tasks.Add(popupDisplay.Hide(RootTransform, cancellationToken));

            if (deepHide)
            {
                foreach (IAdvancedPopup popup in DeepPopups)
                {
                    tasks.Add(popup.Hide<J>(true, cancellationToken));
                }
            }
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        
            if (_subscribed)
                Unsubscribe();
        }
    }
}
