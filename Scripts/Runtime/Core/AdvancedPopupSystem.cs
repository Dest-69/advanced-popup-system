using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class AdvancedPopupSystem
{
    private static readonly List<IAdvancedPopup> _popups = new List<IAdvancedPopup>();
    private static readonly List<IAdcencedPopupDisplay> _displays = new List<IAdcencedPopupDisplay>();
    
    private static CancellationTokenSource _source;

    public static void InitAdcencedPopup<T>(T popup) where T : IAdvancedPopup
    {
        if (!_popups.Contains(popup))
            _popups.Add(popup);
    }
    
    public static IAdcencedPopupDisplay GetDisplay<T>() where T : IAdcencedPopupDisplay, new()
    {
        IAdcencedPopupDisplay display = _displays.FirstOrDefault(popupDisplay => popupDisplay is T);
        if (display == default)
        {
            display = new T();
            display.Init();
            
            _displays.Add(display);
        }

        return display;
    }

    #region FORCE SHOW
    public static async void ForceShow<T>(string popupName, int layer = 0, bool deepShow = false, bool deepHide = false) where T : IAdcencedPopupDisplay, new()
    {
        IAdvancedPopup _popup = _popups.FirstOrDefault(popup => popup.PopupName == popupName);
        if (_popup == default)
        {
            Debug.LogError($"AdvancedPopupSystem not found popup with name '{popupName}' not found!");
            return;
        }

        CancellationToken cToken = UpdateCancellationTokenSource();

        List<Task> tasks = new List<Task>();
        foreach (IAdvancedPopup popup in _popups.Where(popup => popup != _popup && popup.Layer >= layer))
        {
            if (_popup.ContainsDeepPopup(popup))
                continue;
            
            tasks.Add(popup.Hide<T>(deepHide, cancellationToken: cToken));
        }
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
        
        if (cToken.IsCancellationRequested)
            return;
        
        await _popup.Show<T>(deepShow, cancellationToken: cToken);
    }

    public static async void ForceShow<T, J>(string popupName, int layer = 0, bool deepShow = true, bool deepHide = true)
        where T : IAdcencedPopupDisplay, new() where J : IAdcencedPopupDisplay, new()
    {
        IAdvancedPopup _popup = _popups.FirstOrDefault(popup => popup.PopupName == popupName);
        if (_popup == default)
        {
            Debug.LogError($"AdvancedPopupSystem not found popup with name '{popupName}' not found!");
            return;
        }
        
        CancellationToken cToken = UpdateCancellationTokenSource();
        
        List<Task> tasks = new List<Task>();
        foreach (IAdvancedPopup popup in _popups.Where(popup => popup != _popup && popup.Layer >= layer))
        {
            if (_popup.ContainsDeepPopup(popup))
                continue;
            
            tasks.Add(popup.Hide<T, J>(deepHide, cancellationToken: cToken));
        }
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
        
        if (cToken.IsCancellationRequested)
            return;

        await _popup.Show<T, J>(deepShow, cancellationToken: cToken);
    }
    #endregion

    #region HIDE ALL
    public static async void HideAll<T>(bool deepHide = false) where T : IAdcencedPopupDisplay, new()
    {
        CancellationToken cToken = UpdateCancellationTokenSource();
        
        List<Task> tasks = new List<Task>();
        foreach (IAdvancedPopup popup in _popups)
        {
            tasks.Add(popup.Hide<T>(deepHide, cancellationToken: cToken));
        }
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }
    
    public static async void HideAll<T, J>(bool deepHide = true) where T : IAdcencedPopupDisplay, new()
        where J : IAdcencedPopupDisplay, new()
    {
        CancellationToken cToken = UpdateCancellationTokenSource();
        
        List<Task> tasks = new List<Task>();
        foreach (IAdvancedPopup popup in _popups)
        {
            tasks.Add(popup.Hide<T, J>(deepHide, cancellationToken: cToken));
        }
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }
    #endregion
    
    private static CancellationToken UpdateCancellationTokenSource()
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
