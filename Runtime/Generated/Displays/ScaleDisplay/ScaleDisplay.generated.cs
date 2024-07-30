using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core
{
    public class ScaleDisplay : IDisplay
    {
        /// <summary>
        /// Logic for popup showing animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ShowMethod(RectTransform transform, BaseSettings settings, CancellationToken cancellationToken = default)
        {
            ScaleSettings settingsLocal = settings as ScaleSettings; 
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            SetCanvasGroupState(canvasGroup, true);

            Vector3 initialScale = transform.localScale;
            float elapsedTime = 0;

            while (elapsedTime < settingsLocal.Duration)
            {
                if (OperationCancelled(cancellationToken))
                    return;
                
                float t = elapsedTime / settingsLocal.Duration;
                float easedT = EasingFunctions.GetEasingValue(settingsLocal.Easing, t);
                
                transform.localScale = Vector3.LerpUnclamped(initialScale, settingsLocal.ShowScale, easedT);

                elapsedTime += Time.deltaTime;
                await Task.Yield();
            }

            if (OperationCancelled(cancellationToken))
                return;

            // Ensure the final scale is set correctly
            transform.localScale = settingsLocal.ShowScale;
            settingsLocal.OnAnimationEnd?.Invoke();
        }

        /// <summary>
        /// Logic for popup hiding animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task HideMethod(RectTransform transform, BaseSettings settings, CancellationToken cancellationToken = default)
        {
            ScaleSettings settingsLocal = settings as ScaleSettings; 
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            Vector3 initialScale = transform.localScale;
            float elapsedTime = 0;

            while (elapsedTime < settingsLocal.Duration)
            {
                if (OperationCancelled(cancellationToken))
                    return;
                
                float t = elapsedTime / settingsLocal.Duration;
                float easedT = EasingFunctions.GetEasingValue(settingsLocal.Easing, t);
                
                transform.localScale = Vector3.LerpUnclamped(initialScale, settingsLocal.HideScale, easedT);

                elapsedTime += Time.deltaTime;
                await Task.Yield();
            }

            if (OperationCancelled(cancellationToken))
                return;

            // Ensure the final scale is set correctly
            transform.localScale = settingsLocal.HideScale;

            // Set CanvasGroup state to hidden
            SetCanvasGroupState(canvasGroup, false);
            settingsLocal.OnAnimationEnd?.Invoke();
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
        /// Checks if operation already cancelled.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private bool OperationCancelled(CancellationToken cancellationToken) => cancellationToken.IsCancellationRequested || !Application.isPlaying;

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