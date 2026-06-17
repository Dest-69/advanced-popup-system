using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AdvancedPS.Core
{
    /// <summary>
    /// Core manager for "Advanced Popup System"
    /// </summary>
    public static class AdvancedPopupSystem
    {
        #region VARIABLES
        /// <summary>
        /// All popups. (In scene)
        /// </summary>
        public static readonly List<IAdvancedPopup> AllPopups = new List<IAdvancedPopup>();
        /// <summary>
        /// All isVisible popups. (In scene)
        /// </summary>
        public static readonly List<IAdvancedPopup> ActivePopups = new List<IAdvancedPopup>();
        /// <summary>
        /// All popups (In scene) which cached by type for O(1) access
        /// </summary>
        private static readonly Dictionary<Type, IAdvancedPopup> PopupCacheByType = new();
        /// <summary>
        /// Changed flags only by AdvancedPopupSystem. (if you show/hide popups manually it will effect only at 'ActivePopups' field)
        /// </summary>
        public static PopupLayerEnum ActiveLayer;
        #endregion

        #region INIT
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

#if UNITY_EDITOR
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                AllPopups.Clear();
                ActivePopups.Clear();
                PopupCacheByType.Clear();
                ActiveLayer = 0;
            }
        }
#endif

        /// <summary>
        /// Removes destroyed popups when a scene is unloaded.
        /// </summary>
        private static void OnSceneUnloaded(Scene scene)
        {
            for (int i = AllPopups.Count - 1; i >= 0; i--)
            {
                if (AllPopups[i] == null)
                    AllPopups.RemoveAt(i);
            }
            for (int i = ActivePopups.Count - 1; i >= 0; i--)
            {
                if (ActivePopups[i] == null)
                    ActivePopups.RemoveAt(i);
            }
            
            // Rebuild type cache
            PopupCacheByType.Clear();
            for (int i = 0; i < AllPopups.Count; i++)
            {
                var popup = AllPopups[i];
                if (popup != null)
                    PopupCacheByType[popup.GetType()] = popup;
            }
        }
        #endregion

        #region GET POPUP
        /// <summary>
        /// Returns first popup of type P. False if not found.
        /// </summary>
        public static bool TryGetPopup<T>(out T popup, bool activeOnly = false) where T : IAdvancedPopup
        {
            if (!activeOnly && PopupCacheByType.TryGetValue(typeof(T), out var cached))
            {
                popup = (T)cached;
                return true;
            }
            
            popup = null;
            var list = activeOnly ? ActivePopups : AllPopups;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is T p) { popup = p; return true; }
            }

            return false;
        }
        
        /// <summary>
        /// First popup in a specific layer. Null if not found.
        /// </summary>
        public static IAdvancedPopup GetPopupByLayer(PopupLayerEnum layer, bool activeOnly = true)
        {
            var list = activeOnly ? ActivePopups : AllPopups;
            for (int i = 0; i < list.Count; i++)
                if (list[i].PopupLayer.HasFlag(layer)) return list[i];
            return null;
        }
        
        /// <summary>
        /// First popup by component name (GameObject.name). Case-sensitive.
        /// </summary>
        public static IAdvancedPopup GetPopupByName(string name, bool activeOnly = true)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var list = activeOnly ? ActivePopups : AllPopups;
            for (int i = 0; i < list.Count; i++)
                if (list[i].name == name) return list[i];
            return null;
        }
        #endregion
        
        #region LAYER SHOW

        /// <summary>
        /// Show all popups with the specified layer.
        /// </summary>
        /// <param name="layer">The layer to show popups for.</param>
        /// <param name="autohide">true= hide the rest of layers, false= only open new layer</param>
        public static Operation LayerShow(PopupLayerEnum layer, bool autohide = true)
        {
            return new Operation(async token =>
            {
                if (autohide)
                {
                    if (ActiveLayer == layer) return;
                    try
                    {
                        ActiveLayer = layer;
                        await HidePopupsAsync(token, GetPopupsExcludingLayer(layer), null);
                        await ShowPopupsAsync(token, GetPopupsByLayer(layer), null);
                    }
                    catch (Exception ex)
                    {
                        APLogger.LogError($"Exception occurred: {ex.Message}");
                    }
                }
                else
                {
                    if (ActiveLayer.HasFlag(layer)) return;
                    try
                    {
                        ActiveLayer |= layer;
                        await ShowPopupsAsync(token, GetPopupsByLayer(layer), null);
                    }
                    catch (Exception ex)
                    {
                        APLogger.LogError($"Exception occurred: {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Show all popups with the specified layer by 'Display' type.
        /// </summary>
        /// <param name="layer">The layer to show popups for.</param>
        /// <param name="settings"> The settings for the open & hide popup animation. If not provided, the default settings will be used. </param>
        /// <param name="autohide">true= hide the rest of layers, false= only open new layer</param>
        public static Operation LayerShow<T>(PopupLayerEnum layer, IDisplaySettings<T> settings = null, bool autohide = true) 
            where T : IDisplay, new()
        {
            return new Operation(async token =>
            {
                if (autohide)
                {
                    if (ActiveLayer == layer) return;
                    try
                    {
                        ActiveLayer = layer;
                        await HidePopupsAsync<T>(token, GetPopupsExcludingLayer(layer), settings);
                        await ShowPopupsAsync<T>(token, GetPopupsByLayer(layer), settings);
                    }
                    catch (Exception ex)
                    {
                        APLogger.LogError($"Exception occurred: {ex.Message}");
                    }
                }
                else
                {
                    if (ActiveLayer.HasFlag(layer)) return;
                    try
                    {
                        ActiveLayer |= layer;
                        await ShowPopupsAsync<T>(token, GetPopupsByLayer(layer), settings);
                    }
                    catch (Exception ex)
                    {
                        APLogger.LogError($"Exception occurred: {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Show all popups with the specified layer by 'Display' type && Hide the rest of layers by 'Display' type.
        /// </summary>
        /// <param name="layer">The layer to show popups for.</param>
        /// <param name="showSettings"> The settings for the open popup animation. If not provided, the default settings will be used. </param>
        /// <param name="hideSettings"> The settings for the hide animation. If not provided, the default settings will be used. </param>
        public static Operation LayerShow<T, J>(PopupLayerEnum layer, IDisplaySettings<T> showSettings = null, IDisplaySettings<J> hideSettings = null)
            where T : IDisplay, new() where J : IDisplay, new()
        {
            return new Operation(async token =>
            {
                if (ActiveLayer == layer) return;
                try
                {
                    ActiveLayer = layer;
                    await HidePopupsAsync<J>(token, GetPopupsExcludingLayer(layer), hideSettings);
                    await ShowPopupsAsync<T>(token, GetPopupsByLayer(layer), showSettings);
                }
                catch (Exception ex)
                {
                    APLogger.LogError($"Exception occurred: {ex.Message}");
                }
            });
        }
        #endregion
        
        #region LAYER HIDE
        /// <summary>
        /// Hide all popups with the specified layer.
        /// </summary>
        /// <param name="layer">The layer to hide popup for.</param>
        public static Operation LayerHide(PopupLayerEnum layer)
        {
            return new Operation(async token =>
            {
                try
                {
                    ActiveLayer &= ~layer;
                    await HidePopupsAsync(token, GetPopupsByLayer(layer), null);
                }
                catch (Exception ex)
                {
                    APLogger.LogError($"Exception occurred: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Hide all popups with the specified layer by 'Display' type.
        /// </summary>
        /// <param name="layer">The layer to show popup for.</param>
        /// <param name="settings"> The settings for the hide animation. If not provided, the default settings will be used. </param>
        public static Operation LayerHide<T>(PopupLayerEnum layer, IDisplaySettings<T> settings = null) 
            where T : IDisplay, new()
        {
            return new Operation(async token =>
            {
                if (!ActiveLayer.HasFlag(layer)) return;
                try
                {
                    ActiveLayer &= ~layer;
                    await HidePopupsAsync<T>(token, GetPopupsByLayer(layer), settings);
                }
                catch (Exception ex)
                {
                    APLogger.LogError($"Exception occurred: {ex.Message}");
                }
            });
        }
        #endregion

        #region HIDE ALL

        /// <summary>
        /// Hide all popups by CachedDisplay type.
        /// </summary>
        public static Operation HideAll()
        {
            return new Operation(async token =>
            {
                if (ActiveLayer == 0) return;
                try
                {
                    ActiveLayer = 0;
                    await HideAllPopupsAsync(token, null);
                }
                catch (Exception ex)
                {
                    APLogger.LogError($"Exception occurred: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Hide all popups by IAdvancedPopupDisplay generic T type for all popups.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public static Operation HideAll<T>(IDisplaySettings<T> settings = null) 
            where T : IDisplay, new()
        {
            return new Operation(async token =>
            {
                if (ActiveLayer == 0) return;
                try
                {
                    ActiveLayer = 0;
                    await HideAllPopupsAsync<T>(token, settings);
                }
                catch (Exception ex)
                {
                    APLogger.LogError($"Exception occurred: {ex.Message}");
                }
            });
        }
        #endregion
        
        #region Helpers
        /// <summary>
        /// Get popups by layer in any loaded scene.
        /// </summary>
        private static IEnumerable<IAdvancedPopup> GetPopupsByLayer(PopupLayerEnum layer)
        {
            List<IAdvancedPopup> popups = AllPopups.Where(popup => popup != null && popup.PopupLayer.HasFlag(layer)).ToList();
            if (popups.Count == 0)
                APLogger.Log($"AdvancedPopupSystem not found popup/s by '{layer}' layer!");

            return popups;
        }
        
        /// <summary>
        /// Get popups excluding a specific layer in any loaded scene.
        /// </summary>
        private static IEnumerable<IAdvancedPopup> GetPopupsExcludingLayer(PopupLayerEnum layer)
        {
            List<IAdvancedPopup> popups = AllPopups.Where(popup => popup != null && !popup.PopupLayer.HasFlag(layer)).ToList();
            if (popups.Count == 0)
                APLogger.Log($"AdvancedPopupSystem not found popup/s excluding '{layer}' layer!");

            return popups;
        }

        /// <summary>
        /// Show popups with specified display type.
        /// </summary>
        private static async Task ShowPopupsAsync(CancellationToken token, IEnumerable<IAdvancedPopup> popups, IDisplaySettings settings)
        {
            if (TaskUtils.OperationCancelled(token)) return;

            List<Task> tasks = popups.Select(popup => popup.ShowAsync(token, settings)).ToList();
            if (tasks.Count > 0) 
                await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Show popups with specified display type.
        /// </summary>
        private static async Task ShowPopupsAsync<T>(CancellationToken token, IEnumerable<IAdvancedPopup> popups, IDisplaySettings<T> settings) 
            where T : IDisplay, new()
        {
            if (TaskUtils.OperationCancelled(token)) return;
            
            List<Task> tasks = popups.Select(popup =>popup.ShowAsync<T>(token, settings)).ToList();
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Hide popups with specified display type.
        /// </summary>
        private static async Task HidePopupsAsync(CancellationToken token, IEnumerable<IAdvancedPopup> popups, IDisplaySettings settings)
        {
            if (TaskUtils.OperationCancelled(token)) return;
            
            List<Task> tasks = popups.Select(popup => popup.HideAsync(token, settings)).ToList();
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Hide popups with specified display type.
        /// </summary>
        private static async Task HidePopupsAsync<T>(CancellationToken token, IEnumerable<IAdvancedPopup> popups, IDisplaySettings<T> settings) 
            where T : IDisplay, new()
        {
            if (TaskUtils.OperationCancelled(token)) return;
            
            List<Task> tasks = popups.Select(popup => popup.HideAsync<T>(token, settings)).ToList();
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Hide all popups.
        /// </summary>
        private static async Task HideAllPopupsAsync(CancellationToken token, IDisplaySettings settings)
        {
            if (TaskUtils.OperationCancelled(token)) return;
            
            List<Task> tasks = AllPopups.Select(popup => popup.HideAsync(token, settings)).ToList();
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Hide all popups.
        /// </summary>
        private static async Task HideAllPopupsAsync<T>(CancellationToken token, IDisplaySettings<T> settings) 
            where T : IDisplay, new()
        {
            if (TaskUtils.OperationCancelled(token)) return;
            
            List<Task> tasks = AllPopups.Select(popup => popup.HideAsync<T>(token, settings)).ToList();
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }
        #endregion
        
        #region Other
        /// <summary>
        /// Push popup entity into AdvancedPopupSystem for caching, without it AdvancedPopupSystem will not use popup in our logic.
        /// </summary>
        public static void InitAdvancedPopup(IAdvancedPopup popup)
        {
            if (!AllPopups.Contains(popup))
            {
                AllPopups.Add(popup);
                PopupCacheByType[popup.GetType()] = popup;
                SortPopups();
            }
        }
        /// <summary>
        /// Remove popup entity from AdvancedPopupSystem cache, now GC can clean object at all.
        /// </summary>
        public static void DeactivateAdvancedPopup(IAdvancedPopup popup)
        {
            AllPopups.Remove(popup);
            ActivePopups.Remove(popup);
            PopupCacheByType.Remove(popup.GetType());
        }
        
        private static void SortPopups()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            
            // Sort popups in active scene
            List<IAdvancedPopup> activeScenePopups = AllPopups
                .Where(popup => popup != null && popup.gameObject.scene == activeScene)
                .OrderByDescending(popup => GetHierarchyDepth(popup.transform))
                .ToList();

            // Sort popups in background scenes
            List<IAdvancedPopup> otherScenesPopups = AllPopups
                .Where(popup => popup != null && popup.gameObject.scene != activeScene)
                .OrderByDescending(popup => GetHierarchyDepth(popup.transform))
                .ToList();

            AllPopups.Clear();
            AllPopups.AddRange(activeScenePopups);
            AllPopups.AddRange(otherScenesPopups);
        }
        private static int GetHierarchyDepth(Transform transform)
        {
            int depth = 0;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }
            return depth;
        }
        
        #endregion
    }
}