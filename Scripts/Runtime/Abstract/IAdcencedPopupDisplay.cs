using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public abstract class IAdcencedPopupDisplay
{
    public abstract void Init();
    public abstract Task Show(Transform transform, CancellationToken cancellationToken = default);
    public abstract Task Hide(Transform transform, CancellationToken cancellationToken = default);
}
