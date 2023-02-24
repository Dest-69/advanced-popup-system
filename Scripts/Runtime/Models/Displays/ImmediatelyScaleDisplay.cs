using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AdvancedPS.Core
{
    public class ImmediatelyScaleDisplay : IAdvancedPopupDisplay
    {
        private Vector3 _showScale;
        private Vector3 _hideScale;
        public override void Init()
        {
            _showScale = Vector3.one;
            _hideScale = Vector3.zero;
        }

        public override async Task Show(Transform transform, CancellationToken cancellationToken = default)
        {
            CanvasGroup canvasGroup = transform.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        
            transform.localScale = _showScale;
        }

        public override async Task Hide(Transform transform, CancellationToken cancellationToken = default)
        {
            transform.localScale = _hideScale;
        
            CanvasGroup canvasGroup = transform.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
