using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class AdvancedPopup : IAdvancedPopup
{
    public CanvasGroup _canvasGroup;

    public override void Init()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        base.Init();
    }
    
    public override async Task Show<T>(bool deepShow = false, CancellationToken cancellationToken = default)
    {
        if (cancellationToken == default)
            cancellationToken = UpdateCancellationTokenSource();
        
        List<Task> tasks = new List<Task>();
        
        IAdcencedPopupDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
        tasks.Add(popupDisplay.Show(RootTransform, cancellationToken));

        if (deepShow)
        {
            foreach (IAdvancedPopup popup in DeepPopups)
            {
                tasks.Add(popup.Show<T>(deepShow, cancellationToken));
            }
        }
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    public override async Task Show<T, J>(bool deepShow = false, CancellationToken cancellationToken = default)
    {
        if (cancellationToken == default)
            cancellationToken = UpdateCancellationTokenSource();
        
        List<Task> tasks = new List<Task>();
        
        IAdcencedPopupDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
        tasks.Add(popupDisplay.Show(RootTransform, cancellationToken));

        if (deepShow)
        {
            foreach (IAdvancedPopup popup in DeepPopups)
            {
                tasks.Add(popup.Show<J>(deepShow, cancellationToken));
            }
        }
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    public override async Task Hide<T>(bool deepHide = false, CancellationToken cancellationToken = default)
    {
        if (cancellationToken == default)
            cancellationToken = UpdateCancellationTokenSource();
        
        List<Task> tasks = new List<Task>();
        
        IAdcencedPopupDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
        tasks.Add(popupDisplay.Hide(RootTransform, cancellationToken));

        if (deepHide)
        {
            foreach (IAdvancedPopup popup in DeepPopups)
            {
                tasks.Add(popup.Hide<T>(deepHide, cancellationToken));
            }
        }
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    public override async Task Hide<T, J>(bool deepHide = false, CancellationToken cancellationToken = default)
    {
        if (cancellationToken == default)
            cancellationToken = UpdateCancellationTokenSource();
        
        List<Task> tasks = new List<Task>();
        
        IAdcencedPopupDisplay popupDisplay = AdvancedPopupSystem.GetDisplay<T>();
        tasks.Add(popupDisplay.Hide(RootTransform, cancellationToken));

        if (deepHide)
        {
            foreach (IAdvancedPopup popup in DeepPopups)
            {
                tasks.Add(popup.Hide<J>(deepHide, cancellationToken));
            }
        }
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }
}
