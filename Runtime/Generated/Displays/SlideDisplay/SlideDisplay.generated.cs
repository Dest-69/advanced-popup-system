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
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ShowMethod(RectTransform transform, BaseSettings settings, CancellationToken cancellationToken = default)
        {
            SlideSettings settingsLocal = settings as SlideSettings; 
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            SetCanvasGroupState(canvasGroup, true);
            transform.localScale = Vector3.one;

            Vector3 startPos = GetStartPosition(transform, settingsLocal);
            Vector3 targetPos = settingsLocal.TargetPosition;

            float elapsedTime = 0;

            while (elapsedTime < settingsLocal.Duration)
            {
                if (OperationCancelled(cancellationToken))
                    return;

                float t = elapsedTime / settingsLocal.Duration;
                float easedT = EasingFunctions.GetEasingValue(settingsLocal.Easing, t);
                
                transform.localPosition = Vector3.LerpUnclamped(startPos, targetPos, easedT);

                elapsedTime += Time.deltaTime;
                await Task.Yield();
            }

            if (OperationCancelled(cancellationToken))
                return;

            // Ensure the final position is set correctly
            transform.localPosition = targetPos;
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
            SlideSettings settingsLocal = settings as SlideSettings; 
            CanvasGroup canvasGroup = GetCanvasGroup(transform);
            
            Vector3 startPos = transform.localPosition;
            Vector3 targetPos = GetStartPosition(transform, settingsLocal);

            float elapsedTime = 0;

            while (elapsedTime < settingsLocal.Duration)
            {
                if (OperationCancelled(cancellationToken))
                    return;

                float t = elapsedTime / settingsLocal.Duration;
                float easedT = EasingFunctions.GetEasingValue(settingsLocal.Easing, t);
                transform.localPosition = Vector3.LerpUnclamped(startPos, targetPos, easedT);

                elapsedTime += Time.deltaTime;
                await Task.Yield();
            }

            if (OperationCancelled(cancellationToken))
                return;

            // Ensure the final position is set correctly
            transform.localPosition = targetPos;

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
        private bool OperationCancelled(CancellationToken cancellationToken) => cancellationToken.IsCancellationRequested || !Application.isPlaying;
        
        private static Vector3 GetStartPosition(RectTransform transform, SlideSettings settings)
        {
            Canvas canvas = transform.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError($"SlideDisplay not found Canvas in parent of {transform.name} popup");
                return Vector3.zero;
            }
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                Debug.LogError("SlideDisplay is not working with Canvas RenderMode.WorldSpace");
                return Vector3.zero;
            }
            
            Rect rect = transform.rect;
            Rect canvasRect = canvas.GetComponent<RectTransform>().rect;
            Vector2 size = new Vector2(rect.width / 2f + canvasRect.width / 2f, rect.height / 2f + canvasRect.height / 2f);

            var localPosition = transform.localPosition;
            Vector3 startPos = settings.SlideEnum switch
            {
                SlideEnum.Up => new Vector3(localPosition.x, size.y, localPosition.z),
                SlideEnum.Down => new Vector3(localPosition.x, -size.y, localPosition.z),
                SlideEnum.Left => new Vector3(-size.x, localPosition.y, localPosition.z),
                SlideEnum.Right => new Vector3(size.x, localPosition.y, localPosition.z),
                _ => Vector3.zero,
            };
            
            return startPos;
        }
    }
}