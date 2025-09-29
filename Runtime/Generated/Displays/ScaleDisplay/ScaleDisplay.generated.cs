using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core
{
    public class ScaleDisplay : DisplayBase<ScaleSettings>
    {
        /// <summary>
        /// Logic for popup showing animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ShowMethod(RectTransform transform, ScaleSettings settings, CancellationToken cancellationToken)
        {
            if (TaskUtils.OperationCancelled(cancellationToken))
                return;
            
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            settings.OnAnimationStart?.Invoke();
            
            SetCanvasGroupState(canvasGroup, true);

            Vector3 initialScale = transform.localScale;
            float elapsedTime = 0;

            while (true)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / settings.Duration;
                float easedT = EasingFunctions.Get(settings.Easing, t);
                
                transform.localScale = Vector3.LerpUnclamped(initialScale, settings.ShowScale, easedT);
                
                if (elapsedTime < settings.Duration)
                {
                    await Task.Yield();
                    if (TaskUtils.OperationCancelled(cancellationToken))
                        return;
                }
                else
                    break;
            }

            // Ensure the final scale is set correctly
            transform.localScale = settings.ShowScale;
            settings.OnAnimationEnd?.Invoke();
        }

        /// <summary>
        /// Logic for popup hiding animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task HideMethod(RectTransform transform, ScaleSettings settings, CancellationToken cancellationToken)
        {
            if (TaskUtils.OperationCancelled(cancellationToken))
                return;
            
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            settings.OnAnimationStart?.Invoke();
            
            Vector3 initialScale = transform.localScale;
            float elapsedTime = 0;

            while (true)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / settings.Duration;
                float easedT = EasingFunctions.Get(settings.Easing, t);

                transform.localScale = Vector3.LerpUnclamped(initialScale, settings.HideScale, easedT);

                if (elapsedTime < settings.Duration)
                {
                    await Task.Yield();
                    if (TaskUtils.OperationCancelled(cancellationToken))
                        return;
                }
                else
                    break;
            }

            // Ensure the final scale is set correctly
            transform.localScale = settings.HideScale;

            // Set CanvasGroup state to hidden
            SetCanvasGroupState(canvasGroup, false);
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
        /// <param name="state">The desired state (true for visible, false for hidden).</param>
        private static void SetCanvasGroupState(CanvasGroup canvasGroup, bool state)
        {
            canvasGroup.alpha = state ? 1 : 0;
            canvasGroup.interactable = state;
            canvasGroup.blocksRaycasts = state;
        }
    }
}