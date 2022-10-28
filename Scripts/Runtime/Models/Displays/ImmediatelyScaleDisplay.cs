using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ImmediatelyScaleDisplay : IAdcencedPopupDisplay
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
        transform.localScale = _showScale;
    }

    public override async Task Hide(Transform transform, CancellationToken cancellationToken = default)
    {
        transform.localScale = _hideScale;
    }
}
