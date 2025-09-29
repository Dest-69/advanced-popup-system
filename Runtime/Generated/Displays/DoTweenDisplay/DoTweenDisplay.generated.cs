using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using DG.Tweening;
using UnityEngine;

namespace AdvancedPS.Core
{
    public class DoTweenDisplay : DisplayBase<DoTweenSettings>
    {
        /// <summary>
        /// Logic for popup showing animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ShowMethod(RectTransform transform, DoTweenSettings settings, CancellationToken cancellationToken)
        {
            if (settings.Factory == null)
            {
                APLogger.LogError("DoTweenSettings.Factory value can't be null. Exit from DoTweenDisplay.");
                return;
            }
            
            CanvasGroup canvasGroup = GetCanvasGroup(transform);

            SetCanvasGroupState(canvasGroup, true);
            transform.localScale = Vector3.one;
            
            await ProceedAnimation(transform, settings, cancellationToken);
        }

        /// <summary>
        /// Logic for popup hiding animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns> 
        public override async Task HideMethod(RectTransform transform, DoTweenSettings settings, CancellationToken cancellationToken)
        {
            if (settings.Factory == null)
            {
                APLogger.LogError("DoTweenSettings.Factory value can't be null. Exit from DoTweenDisplay.");
                return;
            }
            
            CanvasGroup canvasGroup = GetCanvasGroup(transform);

            bool sucessful = await ProceedAnimation(transform, settings, cancellationToken);

            if (sucessful)
            {
                SetCanvasGroupState(canvasGroup, false);
                transform.localScale = Vector3.zero;
            }
        }
        
        private async Task<bool> ProceedAnimation(RectTransform transform, DoTweenSettings settings, CancellationToken cancellationToken)
        {
            // User builds pure DOTween Sequence
            var seq = settings.Factory(transform);
            if (seq == null) return false;

            // Standardize defaults (no reuse, no leaks)
            seq.SetUpdate(settings.UnscaledTime)
                .SetRecyclable(settings.Recyclable)
                .SetAutoKill(settings.AutoKill)
                .SetLink(transform.gameObject, settings.Link);

            settings.OnAnimationStart?.Invoke();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TweenCallback onComplete = null, onKill = null;
            void Unsub(){ seq.onComplete -= onComplete; seq.onKill -= onKill; }
            onComplete = () => { Unsub(); tcs.TrySetResult(true);  };
            onKill     = () => { Unsub(); tcs.TrySetResult(false); };
            seq.onComplete += onComplete;
            seq.onKill     += onKill;

            using (cancellationToken.Register(() => { if (seq.IsActive()) seq.Kill(); tcs.TrySetResult(false); }))
            {
                seq.Restart(); // play from start
                var ok = await tcs.Task;
                if (!ok || TaskUtils.OperationCancelled(cancellationToken)) return false;
                settings.OnAnimationEnd?.Invoke();
                return true;
            }
        }
        
        /// <summary>
        /// Get the CanvasGroup component from the transform.
        /// </summary>
        /// <param name="transform">The transform of the popup.</param>
        /// <returns>The CanvasGroup component if it exists, null otherwise.</returns>
        private static CanvasGroup GetCanvasGroup(Component transform)
        {
            CanvasGroup canvasGroup = transform.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                APLogger.LogWarning($"CanvasGroup component missing on {transform.name}");
            }
            return canvasGroup;
        }
        
        /// <summary>
        /// Set the state of the CanvasGroup.
        /// </summary>
        /// <param name="canvasGroup">The CanvasGroup component of the popup.</param>
        /// <param name="state">The desired state (true for visible, false for hidden).</param>
        private static void SetCanvasGroupState(CanvasGroup canvasGroup, bool state)
        {
            canvasGroup.alpha = state ? 1 : 0;
            canvasGroup.interactable = state;
            canvasGroup.blocksRaycasts = state;
        }
    }
}
