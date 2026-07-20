using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        /// <summary>
        /// Optional seam that materializes popups from an external source (Addressables) on demand. Null by
        /// default — then the system is scene-only, exactly as before. The APS Addressables assembly (compiled
        /// under the APS_ADDRESSABLES define) assigns this at load. See <see cref="IPopupResolver"/>.
        /// </summary>
        public static IPopupResolver Resolver { get; set; }
        /// <summary>
        /// Currently spawned multi-instance popups (Lane B — created via <see cref="SpawnAsync{T}"/>). Kept out of
        /// <see cref="AllPopups"/> so layer batches ignore them; still reachable by the escape stack / HideAll via
        /// <see cref="ActivePopups"/> while visible. Cleared on play-mode exit; null entries pruned on scene unload.
        /// </summary>
        public static readonly List<IAdvancedPopup> SpawnedPopups = new List<IAdvancedPopup>();
        /// <summary>
        /// Despawned-but-retained instances, keyed by type full name, reused by <see cref="SpawnAsync{T}"/>.
        /// </summary>
        private static readonly Dictionary<string, List<IAdvancedPopup>> _pool = new();
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
                SpawnedPopups.Clear();
                _pool.Clear();
                // The runtime APS_Root GameObject is destroyed by Unity on play-mode exit; drop the reference so a
                // fresh one is created next play (new static state → same leak-guard treatment, see Invariants).
                _root = null;
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
            // Spawned popups parented to the persistent Root survive scene unload by design; only drop entries whose
            // GameObject was destroyed (e.g. spawned under a scene parent). Pooled instances get the same prune.
            for (int i = SpawnedPopups.Count - 1; i >= 0; i--)
            {
                if (SpawnedPopups[i] == null)
                    SpawnedPopups.RemoveAt(i);
            }
            foreach (var bucket in _pool.Values)
            {
                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    if (bucket[i] == null)
                        bucket.RemoveAt(i);
                }
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

        #region ROOT
        private static Transform _root;
        /// <summary>
        /// Parent for popups the system instantiates itself — Addressable lazy/preload loads and
        /// <see cref="Resolver"/>-driven spawns. Defaults to an auto-created persistent (DontDestroyOnLoad) Canvas so
        /// loaded popups survive scene changes like global UI. Assign your own Canvas/Transform before the first load
        /// to override (e.g. to control sort order or render mode). Scene-authored popups are unaffected — they keep
        /// their own hierarchy and never touch this.
        /// </summary>
        public static Transform Root
        {
            get
            {
                if (_root == null)
                    _root = CreateDefaultRoot();
                return _root;
            }
            set => _root = value;
        }

        /// <summary>
        /// Builds the default overlay Canvas used when <see cref="Root"/> was not assigned. Kept on top by default;
        /// override <see cref="Root"/> if you need a different render mode or ordering.
        /// </summary>
        private static Transform CreateDefaultRoot()
        {
            var go = new GameObject("APS_Root");
            UnityEngine.Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            return go.transform;
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
                        // Lazily materialize this layer's Addressable popups before showing (no-op scene-only).
                        await EnsureLayerLoadedAsync(layer, token);
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
                        await EnsureLayerLoadedAsync(layer, token);
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
                        await EnsureLayerLoadedAsync(layer, token);
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
                        await EnsureLayerLoadedAsync(layer, token);
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
                    await EnsureLayerLoadedAsync(layer, token);
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

        #region ESCAPE
        /// <summary>
        /// One step of the escape close stack: walks visible popups from the most recently shown to the
        /// oldest and applies the first relevant popup's EscapePolicy — Hide closes it (together with its
        /// DeepPopups), Block consumes the step without closing (modal), Ignore passes it to the next popup.
        /// Popups shown as part of a parent's cascade (DeepPopups) don't get their own step — the cascade
        /// root represents the whole group.
        /// Invoked by KeyEventSystemAPS on the escape close key (see PopupSettings.EscapeCloseKey); call it
        /// directly to drive the same behavior from a UI "back" button.
        /// </summary>
        /// <returns> True if the step was consumed — a popup was hidden or blocked it. </returns>
        public static bool EscapeStep()
        {
            for (int i = ActivePopups.Count - 1; i >= 0; i--)
            {
                IAdvancedPopup popup = ActivePopups[i];
                if (popup == null || !popup.IsBeVisible || popup.ShownByCascade)
                    continue;

                switch (popup.EscapePolicy)
                {
                    case EscapePolicyEnum.Hide:
                        popup.Hide();
                        return true;
                    case EscapePolicyEnum.Block:
                        return true;
                }
            }

            return false;
        }
        #endregion

        #region SPAWN / POOL
        /// <summary>
        /// Spawn a fresh instance of an Addressable popup for the "many copies" case (Lane B) — toasts, floating
        /// labels, list rows. Reuses a pooled instance when one is free, otherwise loads it via <see cref="Resolver"/>.
        /// The returned popup is NOT part of the unique registries or layer batches (LayerShow ignores it); it is
        /// yours to Show/Hide and <see cref="Despawn"/>. It still participates in the escape stack and HideAll while
        /// visible (it enters <see cref="ActivePopups"/> on Show). Requires the popup type to be flagged Addressable
        /// and the Addressables integration present — returns null otherwise (logged).
        /// </summary>
        /// <param name="parent">Parent for the instance; defaults to <see cref="Root"/>.</param>
        /// <param name="token">Cancels the load in flight.</param>
        public static async Task<T> SpawnAsync<T>(Transform parent = null, CancellationToken token = default)
            where T : IAdvancedPopup
        {
            string typeName = typeof(T).FullName;
            parent = parent != null ? parent : Root;

            // Reuse a pooled instance first (a previous Despawn without release).
            if (typeName != null && _pool.TryGetValue(typeName, out var bucket))
            {
                while (bucket.Count > 0)
                {
                    IAdvancedPopup reused = bucket[bucket.Count - 1];
                    bucket.RemoveAt(bucket.Count - 1);
                    if (reused == null) continue; // destroyed straggler
                    reused.transform.SetParent(parent, false);
                    SpawnedPopups.Add(reused);
                    return (T)reused;
                }
            }

            if (Resolver == null || typeName == null || !AddressablePopupIndex.TryGetByTypeName(typeName, out var entry))
            {
                APLogger.LogError($"<color=red>[AdvancedPopupSystem]</color> SpawnAsync<{typeName}> needs the popup flagged Addressable and the Addressables integration present.");
                return null;
            }

            IAdvancedPopup popup = await Resolver.LoadAsync(entry.Address, parent, token);
            if (popup == null)
                return null;

            // Lane B: pull it back out of the unique registries its Init() just joined — spawned copies are
            // user-managed and must not collide with the by-type cache or be swept up by LayerShow.
            DeactivateAdvancedPopup(popup);
            SpawnedPopups.Add(popup);
            return (T)popup;
        }

        /// <summary>
        /// Return a spawned popup (see <see cref="SpawnAsync{T}"/>) to the pool for reuse, or release it entirely.
        /// Hide (await) the popup first if you want its close animation — Despawn itself is instant.
        /// </summary>
        /// <param name="popup">The spawned instance.</param>
        /// <param name="release">
        /// false (default) — deactivate and keep in the pool for the next <see cref="SpawnAsync{T}"/> of that type;
        /// true — release the instance and its Addressables handle (destroyed, memory can unload).
        /// </param>
        public static void Despawn(IAdvancedPopup popup, bool release = false)
        {
            if (popup == null) return;
            SpawnedPopups.Remove(popup);

            if (release)
            {
                Resolver?.Release(popup);
                return;
            }

            popup.gameObject.SetActive(false);
            string typeName = popup.GetType().FullName;
            if (typeName == null)
            {
                Resolver?.Release(popup);
                return;
            }
            if (!_pool.TryGetValue(typeName, out var bucket))
            {
                bucket = new List<IAdvancedPopup>();
                _pool[typeName] = bucket;
            }
            bucket.Add(popup);
        }
        #endregion

        #region PRELOAD
        /// <summary>
        /// Load (and instantiate) every Addressable popup flagged <see cref="LoadMode.Preload"/> that is not already
        /// live. Runs automatically once after the first scene loads; call it yourself and await the Operation to
        /// gate a loading screen. No-op without the Addressables integration.
        /// </summary>
        public static Operation PreloadAll()
        {
            return new Operation(async token =>
            {
                if (Resolver == null || !AddressablePopupIndex.HasEntries) return;
                try
                {
                    foreach (var entry in AddressablePopupIndex.Preloads)
                    {
                        if (TaskUtils.OperationCancelled(token)) return;
                        await EnsureEntryLoadedAsync(entry, token);
                    }
                }
                catch (Exception ex)
                {
                    APLogger.LogError($"Exception occurred: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Load (and instantiate) every Addressable popup of <paramref name="layer"/> that is not already live, so a
        /// following <see cref="LayerShow(PopupLayerEnum, bool)"/> is instant. No-op without the Addressables integration.
        /// </summary>
        public static Operation PreloadLayer(PopupLayerEnum layer)
        {
            return new Operation(token => EnsureLayerLoadedAsync(layer, token));
        }

        /// <summary>
        /// Boot preload pass. AfterSceneLoad (not Before): scene-authored popups have Awoken and registered by now, so
        /// the scene-wins dedup can suppress loading a second copy of a popup already placed in the scene. The optional
        /// resolver registers earlier (BeforeSceneLoad), so it is set by the time this runs.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static async void PreloadOnBoot()
        {
            if (Resolver == null || !AddressablePopupIndex.HasEntries) return;
            try
            {
                foreach (var entry in AddressablePopupIndex.Preloads)
                    await EnsureEntryLoadedAsync(entry, CancellationToken.None);
            }
            catch (Exception ex)
            {
                APLogger.LogError($"Exception occurred during APS preload: {ex.Message}");
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// True when a popup of <paramref name="typeName"/> (Type.FullName) is already present in AllPopups — a live
        /// scene instance or one loaded earlier. Drives the "scene wins the index" rule and prevents double-loads.
        /// </summary>
        private static bool IsLive(string typeName)
        {
            for (int i = 0; i < AllPopups.Count; i++)
            {
                var p = AllPopups[i];
                if (p != null && p.GetType().FullName == typeName)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Materialize one index entry via the <see cref="Resolver"/> unless it is already live (scene-wins/dedup).
        /// The new instance's Init() registers it, so the caller's next GetPopupsByLayer picks it up. Callers guard
        /// that Resolver is non-null.
        /// </summary>
        private static async Task EnsureEntryLoadedAsync(AddressablePopupIndex.Entry entry, CancellationToken token)
        {
            if (IsLive(entry.TypeName)) return;
            await Resolver.LoadAsync(entry.Address, Root, token);
        }

        /// <summary>
        /// Ensure every Addressable popup of a layer is loaded before the layer is shown. No-op without a resolver or
        /// index entries — that is the scene-only fast path, paying nothing.
        /// </summary>
        private static async Task EnsureLayerLoadedAsync(PopupLayerEnum layer, CancellationToken token)
        {
            if (Resolver == null || !AddressablePopupIndex.HasEntries) return;
            foreach (var entry in AddressablePopupIndex.ForLayer(layer))
            {
                if (TaskUtils.OperationCancelled(token)) return;
                await EnsureEntryLoadedAsync(entry, token);
            }
        }

        /// <summary>
        /// Get popups by layer in any loaded scene.
        /// </summary>
        private static List<IAdvancedPopup> GetPopupsByLayer(PopupLayerEnum layer)
        {
            var popups = new List<IAdvancedPopup>();
            for (int i = 0; i < AllPopups.Count; i++)
            {
                IAdvancedPopup popup = AllPopups[i];
                if (popup != null && popup.PopupLayer.HasFlag(layer))
                    popups.Add(popup);
            }
            if (popups.Count == 0)
                APLogger.Log($"AdvancedPopupSystem not found popup/s by '{layer}' layer!");

            return popups;
        }

        /// <summary>
        /// Get popups excluding a specific layer in any loaded scene.
        /// </summary>
        private static List<IAdvancedPopup> GetPopupsExcludingLayer(PopupLayerEnum layer)
        {
            var popups = new List<IAdvancedPopup>();
            for (int i = 0; i < AllPopups.Count; i++)
            {
                IAdvancedPopup popup = AllPopups[i];
                if (popup != null && !popup.PopupLayer.HasFlag(layer))
                    popups.Add(popup);
            }
            if (popups.Count == 0)
                APLogger.Log($"AdvancedPopupSystem not found popup/s excluding '{layer}' layer!");

            return popups;
        }

        /// <summary>
        /// Show popups with specified display type.
        /// </summary>
        private static async Task ShowPopupsAsync(CancellationToken token, IReadOnlyList<IAdvancedPopup> popups, IDisplaySettings settings)
        {
            if (TaskUtils.OperationCancelled(token) || popups.Count == 0) return;

            var tasks = new List<Task>(popups.Count);
            for (int i = 0; i < popups.Count; i++)
                tasks.Add(popups[i].ShowAsync(token, settings));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Show popups with specified display type.
        /// </summary>
        private static async Task ShowPopupsAsync<T>(CancellationToken token, IReadOnlyList<IAdvancedPopup> popups, IDisplaySettings<T> settings)
            where T : IDisplay, new()
        {
            if (TaskUtils.OperationCancelled(token) || popups.Count == 0) return;

            var tasks = new List<Task>(popups.Count);
            for (int i = 0; i < popups.Count; i++)
                tasks.Add(popups[i].ShowAsync<T>(token, settings));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Hide popups with specified display type.
        /// </summary>
        private static async Task HidePopupsAsync(CancellationToken token, IReadOnlyList<IAdvancedPopup> popups, IDisplaySettings settings)
        {
            if (TaskUtils.OperationCancelled(token) || popups.Count == 0) return;

            var tasks = new List<Task>(popups.Count);
            for (int i = 0; i < popups.Count; i++)
                tasks.Add(popups[i].HideAsync(token, settings));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Hide popups with specified display type.
        /// </summary>
        private static async Task HidePopupsAsync<T>(CancellationToken token, IReadOnlyList<IAdvancedPopup> popups, IDisplaySettings<T> settings)
            where T : IDisplay, new()
        {
            if (TaskUtils.OperationCancelled(token) || popups.Count == 0) return;

            var tasks = new List<Task>(popups.Count);
            for (int i = 0; i < popups.Count; i++)
                tasks.Add(popups[i].HideAsync<T>(token, settings));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Hide all popups.
        /// </summary>
        private static async Task HideAllPopupsAsync(CancellationToken token, IDisplaySettings settings)
        {
            if (TaskUtils.OperationCancelled(token) || ActivePopups.Count == 0) return;

            // Snapshot ActivePopups (the visible set of BOTH lanes) so spawned Lane B popups also close on HideAll;
            // copy because Unsubscribe mutates ActivePopups mid-hide while we build the batch.
            var snapshot = new List<IAdvancedPopup>(ActivePopups);
            var tasks = new List<Task>(snapshot.Count);
            for (int i = 0; i < snapshot.Count; i++)
                tasks.Add(snapshot[i].HideAsync(token, settings));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Hide all popups.
        /// </summary>
        private static async Task HideAllPopupsAsync<T>(CancellationToken token, IDisplaySettings<T> settings) 
            where T : IDisplay, new()
        {
            if (TaskUtils.OperationCancelled(token) || ActivePopups.Count == 0) return;

            // Snapshot ActivePopups (both lanes) so spawned Lane B popups also close; copy because Unsubscribe
            // mutates ActivePopups mid-hide while we build the batch.
            var snapshot = new List<IAdvancedPopup>(ActivePopups);
            var tasks = new List<Task>(snapshot.Count);
            for (int i = 0; i < snapshot.Count; i++)
                tasks.Add(snapshot[i].HideAsync<T>(token, settings));
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

            // Only touch the type cache if THIS popup is the cached representative — another live instance of the same
            // type may still exist. If it is, re-point the cache to a survivor instead of dropping the entry (which
            // would silently degrade TryGetPopup<T> to a linear scan for the rest of the session).
            Type type = popup.GetType();
            if (PopupCacheByType.TryGetValue(type, out var cached) && ReferenceEquals(cached, popup))
            {
                PopupCacheByType.Remove(type);
                for (int i = 0; i < AllPopups.Count; i++)
                {
                    if (AllPopups[i] != null && AllPopups[i].GetType() == type)
                    {
                        PopupCacheByType[type] = AllPopups[i];
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Precomputed sort key for <see cref="SortPopups"/>: scene group + hierarchy depth, plus the original
        /// index as a stability tiebreaker (so equal-depth popups keep their registration order — matters for
        /// first-match key-event resolution, see Input &amp; Hotkeys).
        /// </summary>
        private readonly struct PopupSortKey
        {
            public readonly IAdvancedPopup Popup;
            public readonly int Group; // 0 = active scene, 1 = background scenes
            public readonly int Depth;
            public readonly int Order;

            public PopupSortKey(IAdvancedPopup popup, int group, int depth, int order)
            {
                Popup = popup;
                Group = group;
                Depth = depth;
                Order = order;
            }
        }

        // Cached so SortPopups doesn't allocate a delegate on every registration.
        private static readonly Comparison<PopupSortKey> PopupSortComparison = (a, b) =>
        {
            if (a.Group != b.Group) return a.Group - b.Group; // active-scene popups first
            if (a.Depth != b.Depth) return b.Depth - a.Depth; // then hierarchy depth descending (deepest first)
            return a.Order - b.Order;                          // stable within a group
        };

        /// <summary>
        /// Reorders <see cref="AllPopups"/> in place: active-scene popups first, each group by hierarchy depth
        /// descending (deepest first — nested/child popups iterate before their parents). Destroyed (null) entries
        /// are dropped. Depth is computed once per popup (not on every comparison) and the sort is stabilized by the
        /// original index. Called on every registration, so kept allocation-light — one scratch list, no LINQ.
        /// </summary>
        private static void SortPopups()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            var keys = new List<PopupSortKey>(AllPopups.Count);
            for (int i = 0; i < AllPopups.Count; i++)
            {
                IAdvancedPopup popup = AllPopups[i];
                if (popup == null) continue;
                int group = popup.gameObject.scene == activeScene ? 0 : 1;
                keys.Add(new PopupSortKey(popup, group, GetHierarchyDepth(popup.transform), keys.Count));
            }

            keys.Sort(PopupSortComparison);

            AllPopups.Clear();
            for (int i = 0; i < keys.Count; i++)
                AllPopups.Add(keys[i].Popup);
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