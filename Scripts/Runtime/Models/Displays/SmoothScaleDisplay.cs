using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class SmoothScaleDisplay : IAdcencedPopupDisplay
{
    private Vector3 _showScale;
    private Vector3 _hideScale;
    private float _animationSpeed;
    
    public override void Init()
    {
        _animationSpeed = 0.022f;
        _showScale = Vector3.one;
        _hideScale = Vector3.zero;
    }

    public override async Task Show(Transform transform, CancellationToken cancellationToken = default)
    {
        CanvasGroup canvasGroup = transform.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        
        Vector3 step = (_showScale - transform.localScale) * _animationSpeed;
        float stepProgress = Vector3.Distance(step, Vector3.zero);
        float targetProgress = Vector3.Distance(_showScale, transform.localScale);
        float progress = 0;
        
        while (progress < targetProgress)
        {
            transform.localScale += step;
            progress += stepProgress;
            
            if (cancellationToken.IsCancellationRequested)
                return;
            
            await Task.Yield();
        }
        
        transform.localScale = _showScale;
    }

    public override async Task Hide(Transform transform, CancellationToken cancellationToken = default)
    {
        Vector3 step = (_hideScale - transform.localScale) * _animationSpeed;
        float stepProgress = Vector3.Distance(step, Vector3.zero);
        float targetProgress = Vector3.Distance(_hideScale, transform.localScale);
        float progress = 0;
        
        while (progress < targetProgress)
        {
            transform.localScale += step;
            progress += stepProgress;
            
            
            if (cancellationToken.IsCancellationRequested)
                return;
            
            await Task.Yield();
        }
        
        transform.localScale = _hideScale;
        CanvasGroup canvasGroup = transform.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}