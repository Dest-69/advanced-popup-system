using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core
{
    public class SlideDisplay : IDisplay
    {
        /// <summary>
        /// Logic for popup showing animation.
        /// SUPPORTED ONLY ANCHORS PIVOT
        /// If you need anchors linking (min-max), use empty prent object with it.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ShowMethod(RectTransform transform, BaseSettings settings, CancellationToken cancellationToken)
        {
            if (OperationCancelled(cancellationToken))
                return;
            
            SlideSettings settingsLocal = settings as SlideSettings;
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            settingsLocal.OnAnimationStart?.Invoke();

            SetCanvasGroupState(canvasGroup, true);
            transform.localScale = Vector3.one;

            await Slide(transform, settingsLocal, cancellationToken);

            if (OperationCancelled(cancellationToken))
                return;

            settingsLocal.OnAnimationEnd?.Invoke();
        }

        /// <summary>
        /// Logic for popup hiding animation.
        /// SUPPORTED ONLY ANCHORS PIVOT
        /// If you need anchors linking (min-max), use empty prent object with it.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task HideMethod(RectTransform transform, BaseSettings settings, CancellationToken cancellationToken)
        {
            if (OperationCancelled(cancellationToken))
                return;
            
            SlideSettings settingsLocal = settings as SlideSettings;
            CanvasGroup canvasGroup = GetCanvasGroup(transform);

            settingsLocal.OnAnimationStart?.Invoke();

            await Slide(transform, settingsLocal, cancellationToken);

            if (OperationCancelled(cancellationToken))
                return;

            // Set CanvasGroup state to hidden
            SetCanvasGroupState(canvasGroup, false);

            transform.localScale = Vector3.zero;
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

        /// <summary>
        /// Checks if operation already cancelled.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private bool OperationCancelled(CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested || !Application.isPlaying;

        private async Task Slide(RectTransform transform, SlideSettings settingsLocal, CancellationToken cancellationToken)
        {
            Vector3 startPos = transform.anchoredPosition3D;
            Vector2 startSize = transform.sizeDelta;

            Vector3 targetPos = settingsLocal.TargetRectPosition;
            Vector2 targetSize = settingsLocal.TargetRectSize;

            float elapsedTime = 0;
            while (elapsedTime < settingsLocal.Duration)
            {
                if (OperationCancelled(cancellationToken))
                    return;

                elapsedTime += Time.deltaTime;
                float t = elapsedTime / settingsLocal.Duration;
                float easedT = EasingFunctions.GetEasingValue(settingsLocal.Easing, t);

                Vector3 lerpedPosition = Vector3.Lerp(startPos, targetPos, easedT);
                Vector2 lerpedSize = Vector2.Lerp(startSize, targetSize, easedT);

                transform.sizeDelta = lerpedSize;
                transform.anchoredPosition3D = lerpedPosition;

                await Task.Yield();
            }
            
            if (OperationCancelled(cancellationToken))
                return;

            // Ensure the final position is set correctly
            transform.sizeDelta = targetSize;
            transform.anchoredPosition3D = targetPos;
        }
    }
}