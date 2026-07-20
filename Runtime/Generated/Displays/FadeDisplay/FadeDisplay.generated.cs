using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace AdvancedPS.Core
{
    public class FadeDisplay : DisplayBase<FadeSettings>
    {
        /// <summary>
        /// Logic for instant popup show.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <returns></returns>
        public override void ShowInstantlyMethod(RectTransform transform, FadeSettings settings)
        {
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            settings.OnAnimationStart?.Invoke();
            
            transform.localScale = Vector3.one;
            SetCanvasGroupState(canvasGroup, settings, true);
            
            settings.OnAnimationEnd?.Invoke();
        }

        /// <summary>
        /// Logic for instant popup hide.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <returns></returns>
        public override void HideInstantlyMethod(RectTransform transform, FadeSettings settings)
        {
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            settings.OnAnimationStart?.Invoke();
            
            transform.localScale = Vector3.zero;
            SetCanvasGroupState(canvasGroup, settings, false);
            
            settings.OnAnimationEnd?.Invoke();
        }

        /// <summary>
        /// Logic for popup showing animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ShowMethod(RectTransform transform, FadeSettings settings, CancellationToken cancellationToken)
        {
            if (TaskUtils.OperationCancelled(cancellationToken))
                return;
            
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            settings.OnAnimationStart?.Invoke();
            
            transform.localScale = Vector3.one;

            float initialAlpha = canvasGroup.alpha;
            float elapsedTime = 0;

            while (true)
            {
                elapsedTime += settings.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsedTime / settings.Duration;
                float easedT = EasingFunctions.Get(settings.Easing, t);

                canvasGroup.alpha = Mathf.LerpUnclamped(initialAlpha, settings.MaxValue, easedT);

                if (elapsedTime < settings.Duration)
                {
                    await Task.Yield();
                    if (TaskUtils.OperationCancelled(cancellationToken))
                        return;
                }
                else
                    break;
            }

            // Ensure the final alpha is set correctly
            SetCanvasGroupState(canvasGroup, settings, true);
            settings.OnAnimationEnd?.Invoke();
        }

        /// <summary>
        /// Logic for popup hiding animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task HideMethod(RectTransform transform, FadeSettings settings, CancellationToken cancellationToken)
        {
            if (TaskUtils.OperationCancelled(cancellationToken))
                return;
            
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            settings.OnAnimationStart?.Invoke();

            float initialAlpha = canvasGroup.alpha;
            float elapsedTime = 0;

            while (true)
            {
                elapsedTime += settings.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = elapsedTime / settings.Duration;
                float easedT = EasingFunctions.Get(settings.Easing, t);

                canvasGroup.alpha = Mathf.LerpUnclamped(initialAlpha, settings.MinValue, easedT);

                if (elapsedTime < settings.Duration)
                {
                    await Task.Yield();
                    if (TaskUtils.OperationCancelled(cancellationToken))
                        return;
                }
                else
                    break;
            }

            // Ensure the final alpha is set correctly
            transform.localScale = Vector3.zero;
            SetCanvasGroupState(canvasGroup, settings, false);
            settings.OnAnimationEnd?.Invoke();
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
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        /// <param name="state">The desired state (true for visible, false for hidden).</param>
        private void SetCanvasGroupState(CanvasGroup canvasGroup, FadeSettings settings, bool state)
        {
            canvasGroup.alpha = state ? settings.MaxValue : settings.MinValue;
            canvasGroup.interactable = state;
            canvasGroup.blocksRaycasts = state;
        }
    }
}