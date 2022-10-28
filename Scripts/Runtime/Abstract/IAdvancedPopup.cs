using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public abstract class IAdvancedPopup : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Popup unique ID")]
    public string PopupName;
    public int Layer;
    
    [Header("REF's")][Space(5)]
    [Tooltip("Root transform")]
    public Transform RootTransform;
    [Tooltip("Popups for deep show/hide")]
    public List<IAdvancedPopup> DeepPopups;

    private CancellationTokenSource _source;

    private void Awake()
    {
        Init();
    }

    public virtual void Init()
    {
        AdvancedPopupSystem.InitAdcencedPopup(this);

        if (RootTransform == null)
            RootTransform = transform;

        if (string.IsNullOrEmpty(PopupName))
        {
            Debug.LogWarning($"IAdvancedPopup on '{gameObject.name}' object not set manual variable PopupName");
            PopupName = gameObject.name;
        }
    }

    public virtual bool ContainsDeepPopup(IAdvancedPopup popup)
    {
        return DeepPopups.Any(_deepPopup => popup == _deepPopup || _deepPopup.ContainsDeepPopup(popup));
    }

    #region SHOW
    public abstract Task Show<T>(bool deepShow = false, CancellationToken cancellationToken = default)
        where T : IAdcencedPopupDisplay, new();

    public abstract Task Show<T, J>(bool deepShow = false, CancellationToken cancellationToken = default)
        where T : IAdcencedPopupDisplay, new()
        where J : IAdcencedPopupDisplay, new();
    #endregion


    #region HIDE
    public abstract Task Hide<T>(bool deepHide = false, CancellationToken cancellationToken = default)
        where T : IAdcencedPopupDisplay, new();

    public abstract Task Hide<T, J>(bool deepHide = false, CancellationToken cancellationToken = default)
        where T : IAdcencedPopupDisplay, new()
        where J : IAdcencedPopupDisplay, new();
    #endregion

    protected virtual CancellationToken UpdateCancellationTokenSource()
    {
        if (_source != null)
        {
            _source.Cancel();
            _source.Dispose();
        }
        _source = new CancellationTokenSource();
        return _source.Token;
    }
}
