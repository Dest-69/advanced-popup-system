using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class SmoothFadeDisplay : IAdcencedPopupDisplay
{
    private int maxValue;
    private int minValue;
    private float _animationSpeed;
    
    public override void Init()
    {
        maxValue = 1;
        minValue = 0;
        _animationSpeed = 0.017f;
    }
    
    public override async Task Show(Transform transform, CancellationToken cancellationToken = default)
    {
        CanvasGroup canvasGroup = transform.gameObject.GetComponent<CanvasGroup>();
        float step = _animationSpeed;
        float targetProgress = maxValue;
        float progress = canvasGroup.alpha;
        
        while (progress < targetProgress)
        {
            canvasGroup.alpha += step;
            progress += step;
            
            
            if (cancellationToken.IsCancellationRequested)
                return;
            
            await Task.Yield();
        }

        canvasGroup.alpha = targetProgress;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public override async Task Hide(Transform transform, CancellationToken cancellationToken = default)
    {
        CanvasGroup canvasGroup = transform.gameObject.GetComponent<CanvasGroup>();
        float step = -_animationSpeed;
        float targetProgress = minValue;
        float progress = canvasGroup.alpha;
        
        while (progress > targetProgress)
        {
            canvasGroup.alpha += step;
            progress += step;
            
            
            if (cancellationToken.IsCancellationRequested)
                return;
            
            await Task.Yield();
        }

        canvasGroup.alpha = targetProgress;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}