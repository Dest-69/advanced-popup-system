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
        /// Despawned-but-retained instances (<see cref="IAdvancedPopup.PoolCapacity"/> ≥ 2 or unlimited), keyed by type
        /// full name, reused by <see cref="SpawnAsync{T}"/>.
        /// </summary>
        private static readonly Dictionary<string, List<IAdvancedPopup>> _pool = new();
        /// <summary>
        /// Single retained instance per type for <see cref="IAdvancedPopup.PoolCapacity"/> == 1 — the "on/off" case:
        /// keep one idle copy for reuse without allocating a whole pool <see cref="List{T}"/>. Reused by
        /// <see cref="SpawnAsync{T}"/> ahead of <see cref="_pool"/>.
        /// </summary>
        private static readonly Dictionary<string, IAdvancedPopup> _singlePool = new();
        /// <summary> Scratch reused by <see cref="OnSceneUnloaded"/> to prune dead single-slot entries without allocating. </summary>
        private static readonly List<string> _deadPoolKeys = new();
        /// <summary>
        /// Loads currently in flight for a Lane-A unique type, keyed by popup <c>Type.FullName</c> — the same currency
        /// the index and <see cref="IsLive"/> speak, which is what lets the by-type and the preload paths share one map
        /// (the preload side only ever has the index entry's name, never a <see cref="Type"/>). Every Lane-A load goes
        /// through <see cref="SharedLoadAsync"/>, so concurrent <see cref="GetPopupAsync{T}"/> / <see cref="Show{T}"/>
        /// calls and the preload passes (<see cref="PreloadAll"/>, <see cref="EnsureLayerLoadedAsync"/>, the per-scene
        /// pass) for the same not-yet-loaded type share one load instead of each instantiating a duplicate. An entry
        /// lives only for its load — <see cref="RemoveInFlightWhenComplete"/> drops it on completion. Cleared on
        /// play-mode exit; needs no scene-unload prune (entries are self-removing and hold a Task, never a destroyable
        /// Unity reference — unlike the instance pools).
        /// </summary>
        private static readonly Dictionary<string, Task<IAdvancedPopup>> _inFlightLoads = new();
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
                _singlePool.Clear();
                _inFlightLoads.Clear();
                _layerCanvases.Clear();
                // The runtime APS_Root GameObject is destroyed by Unity on play-mode exit; drop the reference so a
                // fresh one is created next play (new static state → same leak-guard treatment, see Invariants).
                _root = null;
                // Drop the cached config assets so play-mode edits to APS_LayerCanvasConfig / APS_PopupOrderConfig /
                // the Addressable index are picked up next run.
                LayerCanvasConfig.ClearCache();
                PopupOrderConfig.ClearCache();
                AddressablePopupIndexAsset.ClearCache();
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
            // Single-slot pool (PoolCapacity == 1): drop entries whose instance was destroyed. Collect first, then
            // remove — can't mutate the dictionary mid-enumeration.
            if (_singlePool.Count > 0)
            {
                _deadPoolKeys.Clear();
                foreach (var kv in _singlePool)
                    if (kv.Value == null)
                        _deadPoolKeys.Add(kv.Key);
                for (int i = 0; i < _deadPoolKeys.Count; i++)
                    _singlePool.Remove(_deadPoolKeys[i]);
            }
            // A registered layer canvas may be a scene object destroyed on unload — drop dead mappings (those layers
            // fall back to Root). Collect first, then remove: can't mutate the dictionary mid-enumeration.
            if (_layerCanvases.Count > 0)
            {
                _deadCanvasKeys.Clear();
                foreach (var kv in _layerCanvases)
                    if (kv.Value == null)
                        _deadCanvasKeys.Add(kv.Key);
                for (int i = 0; i < _deadCanvasKeys.Count; i++)
                    _layerCanvases.Remove(_deadCanvasKeys[i]);
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
                    _root = CreateCanvas("APS_Root", short.MaxValue);
                return _root;
            }
            set => _root = value;
        }

        /// <summary>
        /// Builds a persistent (DontDestroyOnLoad) ScreenSpaceOverlay Canvas with a scaler + raycaster at
        /// <paramref name="sortingOrder"/>. Backs the default <see cref="Root"/> (kept on top) and the auto-created
        /// per-layer canvases for layers that have canvas config but no assigned prefab (see <see cref="GetCanvasForLayer"/>).
        /// </summary>
        private static Transform CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            return go.transform;
        }
        #endregion

        #region LAYER CANVAS
        /// <summary>
        /// Per-layer canvas routing. Maps a single <see cref="PopupLayerEnum"/> flag to the Transform (usually a
        /// Canvas) that popups of that layer are parented under when the system instantiates them — Addressable
        /// lazy/preload loads and <see cref="SpawnAsync{T}"/>. Lets you split popups across canvases with independent
        /// sort orders (a HUD canvas below, a dialog canvas above, …). Unmapped layers fall back to <see cref="Root"/>.
        /// Scene-authored popups are unaffected — they keep their own hierarchy. Cleared on play-mode exit; entries
        /// whose canvas was destroyed are pruned on scene unload (leak-guard rule, see Invariants).
        /// </summary>
        private static readonly Dictionary<PopupLayerEnum, Transform> _layerCanvases = new();
        /// <summary> Scratch reused by <see cref="OnSceneUnloaded"/> to collect dead mappings without allocating. </summary>
        private static readonly List<PopupLayerEnum> _deadCanvasKeys = new();

        /// <summary>
        /// Route popups of <paramref name="layer"/> under <paramref name="canvas"/> when the system instantiates them
        /// (Addressable load / <see cref="SpawnAsync{T}"/>). Pass a mask with several flags to map them all to the same
        /// canvas. Re-registering a layer overwrites it; a null <paramref name="canvas"/> clears those flags (same as
        /// <see cref="UnregisterLayerCanvas"/>). You own the canvas' lifetime, sort order and render mode — APS only
        /// parents to it. Scene-authored popups keep their own hierarchy and ignore this.
        /// </summary>
        public static void RegisterLayerCanvas(PopupLayerEnum layer, Transform canvas)
        {
            for (int bit = 0; bit < 31; bit++)
            {
                var flag = (PopupLayerEnum)(1 << bit);
                if ((layer & flag) == 0) continue;
                if (canvas == null) _layerCanvases.Remove(flag);
                else _layerCanvases[flag] = canvas;
            }
        }

        /// <summary>
        /// Remove the canvas mapping for every flag in <paramref name="layer"/>; those popups fall back to <see cref="Root"/>.
        /// </summary>
        public static void UnregisterLayerCanvas(PopupLayerEnum layer)
        {
            for (int bit = 0; bit < 31; bit++)
            {
                var flag = (PopupLayerEnum)(1 << bit);
                if ((layer & flag) != 0) _layerCanvases.Remove(flag);
            }
        }

        /// <summary>
        /// The canvas a popup with <paramref name="popupLayer"/> is parented under, or <see cref="Root"/> when none of
        /// its layers are mapped. Mappings come from two sources, checked together: a manual <see cref="RegisterLayerCanvas"/>
        /// (runtime override) and the APS Layers tool's <see cref="LayerCanvasConfig"/> — the latter's canvases are
        /// created lazily on first use here. A popup belongs to exactly one layer; legacy multi-flag data still
        /// resolves deterministically — the lowest-bit mapped layer wins. Used by the load/spawn paths; scene-authored
        /// popups don't call it.
        /// </summary>
        public static Transform GetCanvasForLayer(PopupLayerEnum popupLayer)
        {
            EnsureLayerCanvases(popupLayer);

            if (_layerCanvases.Count > 0)
            {
                Transform best = null;
                int bestBit = int.MaxValue;
                foreach (var kv in _layerCanvases)
                {
                    if (kv.Value == null) continue;
                    int key = (int)kv.Key;
                    if (((int)popupLayer & key) != 0 && key < bestBit)
                    {
                        bestBit = key;
                        best = kv.Value;
                    }
                }
                if (best != null) return best;
            }
            return Root;
        }

        /// <summary>
        /// Lazily materialize the canvases configured via the Layers tool (<see cref="LayerCanvasConfig"/>) for the
        /// flags set in <paramref name="popupLayer"/> that aren't mapped yet. A layer already mapped — by a manual
        /// <see cref="RegisterLayerCanvas"/> or an earlier call — is left untouched, so the manual override wins.
        /// No-op when no config asset exists (scene-only projects pay nothing but the load path).
        /// </summary>
        private static void EnsureLayerCanvases(PopupLayerEnum popupLayer)
        {
            LayerCanvasConfig config = LayerCanvasConfig.Loaded;
            if (config == null || config.Entries == null) return;

            for (int i = 0; i < config.Entries.Count; i++)
            {
                LayerCanvasConfig.Entry entry = config.Entries[i];
                if (entry == null) continue;
                if (!LayerCanvasConfig.TryParseLayer(entry.Layer, out PopupLayerEnum flag)) continue;
                if (((int)popupLayer & (int)flag) == 0) continue;
                if (_layerCanvases.TryGetValue(flag, out Transform existing) && existing != null) continue;

                _layerCanvases[flag] = CreateLayerCanvas(entry);
            }
        }

        /// <summary>
        /// Builds the canvas for one configured layer: the assigned <see cref="LayerCanvasConfig.Entry.CanvasPrefab"/>
        /// (instantiated), or a plain overlay canvas when none is assigned. Either way its <c>sortingOrder</c> is forced
        /// to the entry's value — the tool always wins over a value baked into the prefab — and the instance is renamed
        /// "<c>&lt;Layer&gt; - APS Canvas</c>" so each layer's canvas is identifiable in the hierarchy (and the default
        /// prefab's "(Clone)" suffix never shows). Kept persistent so it survives scene loads like <see cref="Root"/>.
        /// </summary>
        private static Transform CreateLayerCanvas(LayerCanvasConfig.Entry entry)
        {
            if (entry.CanvasPrefab != null)
            {
                Canvas canvas = UnityEngine.Object.Instantiate(entry.CanvasPrefab);
                canvas.gameObject.name = $"{entry.Layer} - APS Canvas";
                canvas.sortingOrder = entry.SortingOrder;
                UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
                return canvas.transform;
            }
            return CreateCanvas($"{entry.Layer} - APS Canvas", entry.SortingOrder);
        }
        #endregion

        #region HIERARCHY ORDER
        /// <summary>
        /// Order <paramref name="popup"/>'s canvas by the <see cref="PopupOrderConfig"/> catalog, so the popups listed
        /// front-most in the APS <b>Order</b> tool are drawn above the rest of that canvas (this popup taking the front of
        /// its own band). Called automatically when APS shows a popup and when it loads / spawns / re-parents one — call it
        /// yourself only after re-parenting a popup by hand.
        /// <para>
        /// Popups of the same rank (and every type the catalog does not list) keep <b>show order</b> — the one shown last
        /// is on top. Between layers nothing changes: each layer has its own canvas and <c>sortingOrder</c>
        /// (<see cref="GetCanvasForLayer"/>), and this only orders siblings within one of them.
        /// </para>
        /// </summary>
        public static void ApplyOrder(IAdvancedPopup popup)
        {
            Place(popup, true);
        }

        /// <summary>
        /// Raise <paramref name="popup"/> to the front of its rank band — above its equals, still below anything the
        /// Order catalog ranks in front of it. It also becomes the top of the recency stack, so the pointer system and
        /// <see cref="EscapeStep"/> agree with what is visually on top. No-op for a popup outside an APS-routed canvas.
        /// </summary>
        public static void BringToFront(IAdvancedPopup popup)
        {
            Place(popup, true);
            Restack(popup, true);
        }

        /// <summary>
        /// Push <paramref name="popup"/> behind its equals (still in front of lower-ranked popups) and to the bottom of
        /// the recency stack, so escape / pointer input reach it last. No-op for a popup outside an APS-routed canvas.
        /// </summary>
        public static void SendToBack(IAdvancedPopup popup)
        {
            Place(popup, false);
            Restack(popup, false);
        }

        /// <summary>
        /// Re-sorts the whole canvas by order rank and gives <paramref name="popup"/> the front (<paramref name="front"/>)
        /// or back edge of its own band. Ordering is applied only inside canvases APS routes popups to —
        /// <see cref="Root"/>, the per-layer canvases and any canvas mapped through <see cref="RegisterLayerCanvas"/> —
        /// so a scene-authored hierarchy is never rearranged behind the author's back.
        /// </summary>
        private static void Place(IAdvancedPopup popup, bool front)
        {
            if (popup == null) return;
            Transform self = popup.transform;
            Transform parent = self.parent;
            if (parent == null || !IsRoutedCanvas(parent)) return;

            // Sort the WHOLE canvas rather than inserting this one popup: a single insertion lands correctly only when
            // the existing children are already ordered, which a canvas APS did not build itself can't be assumed to be
            // (a scene canvas handed to RegisterLayerCanvas with popups already under it, a hand-made SetSiblingIndex).
            // Sorting makes the outcome independent of the order the children arrived in — the point of the feature.
            _orderScratch.Clear();
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                // The popup being placed claims the front/back edge of its band; the others keep show order.
                int index = child == self ? (front ? int.MaxValue : int.MinValue) : i;
                _orderScratch.Add(new OrderSortKey(child, RankOf(child), index));
            }

            _orderScratch.Sort(OrderComparison);

            // Ascending target order: each SetSiblingIndex only shifts children after the slot just filled, so the
            // already-placed prefix stays valid and an equal index means "already there" (no needless hierarchy event).
            for (int i = 0; i < _orderScratch.Count; i++)
            {
                Transform child = _orderScratch[i].Child;
                if (child.GetSiblingIndex() != i)
                    child.SetSiblingIndex(i);
            }

            // Never hold Transform references between calls (static-state rule, see Invariants).
            _orderScratch.Clear();
        }

        /// <summary> Scratch reused by <see cref="Place"/>, so ordering a canvas allocates nothing per show. </summary>
        private static readonly List<OrderSortKey> _orderScratch = new List<OrderSortKey>();

        /// <summary> Sort key for <see cref="Place"/>: order rank + the child's current index as the stability tiebreaker. </summary>
        private readonly struct OrderSortKey
        {
            public readonly Transform Child;
            public readonly int Rank;
            public readonly int Index;

            public OrderSortKey(Transform child, int rank, int index)
            {
                Child = child;
                Rank = rank;
                Index = index;
            }
        }

        // Cached so Place doesn't allocate a delegate per call.
        private static readonly Comparison<OrderSortKey> OrderComparison = (a, b) =>
        {
            // Higher rank = further back = earlier child. CompareTo, not subtraction: UnrankedRank is int.MaxValue and
            // b.Rank - a.Rank would overflow into the wrong sign.
            if (a.Rank != b.Rank) return b.Rank.CompareTo(a.Rank);
            return a.Index.CompareTo(b.Index); // stable — keeps show order inside a band
        };

        /// <summary>
        /// Moves <paramref name="popup"/> to the top / bottom of <see cref="ActivePopups"/> — the recency stack the
        /// pointer system and the escape stack read as "topmost". Membership stays owned by the popup's
        /// Subscribe/Unsubscribe (see Popup Lifecycle); this only reorders what is already there, and ignores a popup
        /// that is not visible.
        /// </summary>
        private static void Restack(IAdvancedPopup popup, bool top)
        {
            int index = ActivePopups.IndexOf(popup);
            if (index < 0) return;

            ActivePopups.RemoveAt(index);
            if (top) ActivePopups.Add(popup);
            else ActivePopups.Insert(0, popup);
        }

        /// <summary> True when <paramref name="parent"/> is a canvas APS parents popups to (see <see cref="GetCanvasForLayer"/>). </summary>
        private static bool IsRoutedCanvas(Transform parent)
        {
            // _root, not the Root property: a mere ordering check must never create the fallback canvas as a side effect.
            if (_root != null && parent == _root) return true;
            foreach (var kv in _layerCanvases)
                if (kv.Value == parent) return true;
            return false;
        }

        /// <summary>
        /// Order rank of a canvas child: the popup's catalog rank, or <see cref="PopupOrderConfig.UnrankedRank"/> for a
        /// child that is not a popup — decorations authored into a canvas prefab stay at the back.
        /// </summary>
        private static int RankOf(Transform child)
        {
            return child.TryGetComponent(out IAdvancedPopup popup) ? RankOf(popup) : PopupOrderConfig.UnrankedRank;
        }

        private static int RankOf(IAdvancedPopup popup)
        {
            PopupOrderConfig config = PopupOrderConfig.Loaded;
            // Prefab key first (two prefabs of one class rank apart), type name as the fallback for a scene-authored
            // popup that has no prefab to key on — see PopupOrderConfig.GetRank.
            return config == null
                ? PopupOrderConfig.UnrankedRank
                : config.GetRank(popup.OrderKey, popup.GetType().FullName);
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
        /// First popup whose layer is in <paramref name="layer"/> (a single flag, or a mask to match any of several
        /// layers). Null if not found.
        /// </summary>
        public static IAdvancedPopup GetPopupByLayer(PopupLayerEnum layer, bool activeOnly = true)
        {
            var list = activeOnly ? ActivePopups : AllPopups;
            for (int i = 0; i < list.Count; i++)
                if ((list[i].PopupLayer & layer) != 0) return list[i];
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

        /// <summary>
        /// Get the unique (Lane-A) popup of type <typeparamref name="T"/>, loading it from Addressables first when it is
        /// not already present. The async counterpart to <see cref="TryGetPopup{T}"/>: a resident popup (scene-authored,
        /// preloaded, or loaded earlier) is returned right away — so the scene-wins / dedup rule is honored — otherwise
        /// the type is resolved in the Addressable index and instantiated via <see cref="Resolver"/> under its layer
        /// canvas, awaiting the load. The new instance self-registers as the unique instance (unlike
        /// <see cref="SpawnAsync{T}"/>, which pulls its copy back out of the registries). Concurrent requests for the
        /// same not-yet-loaded type <b>share one load</b> — including a preload pass that got there first
        /// (<see cref="SharedLoadAsync"/>) — so no duplicate instance is created. Returns null when the popup is
        /// neither in a loaded scene nor flagged Addressable, the Addressables integration is absent (logged), or
        /// <paramref name="token"/> was cancelled by the time the load finished.
        /// </summary>
        /// <param name="token">Cancels this caller's get. The shared load itself always runs to completion (the unique
        /// instance stays resident for other callers); a cancelled caller simply receives null.</param>
        public static async Task<T> GetPopupAsync<T>(CancellationToken token = default) where T : IAdvancedPopup
        {
            // Already resident (scene / preloaded / loaded earlier) — hand it back; this is also the scene-wins/dedup guard.
            if (TryGetPopup<T>(out T resident))
                return resident;

            string typeName = typeof(T).FullName;
            // Reuse a load already in flight for this type — started by another get, a layer show, or a preload pass —
            // instead of starting a second one (which would create a duplicate instance). Entries are published
            // synchronously, before the first await, so a concurrent caller running while this one is suspended finds
            // it here. Null FullName can't be a dictionary key; it fails the index lookup below anyway.
            Task<IAdvancedPopup> load = null;
            if (typeName != null)
                _inFlightLoads.TryGetValue(typeName, out load);

            if (load == null)
            {
                if (Resolver == null || typeName == null || !AddressablePopupIndex.TryGetByTypeName(typeName, out var entry))
                {
                    APLogger.LogError($"<color=red>[AdvancedPopupSystem]</color> GetPopupAsync<{typeName}> needs the popup present in a loaded scene, or flagged Addressable with the Addressables integration present.");
                    return default;
                }

                load = SharedLoadAsync(entry);
            }

            IAdvancedPopup popup;
            try
            {
                popup = await load;
            }
            catch (Exception ex)
            {
                APLogger.LogError($"<color=red>[AdvancedPopupSystem]</color> GetPopupAsync<{typeName}> load failed: {ex.Message}");
                return default;
            }

            if (token.IsCancellationRequested)
                return default;
            return popup is T typed ? typed : default;
        }

        /// <summary>
        /// The one gate every Lane-A load passes through: returns the load already in flight for <paramref name="entry"/>'s
        /// type, or starts one and publishes it in <see cref="_inFlightLoads"/> <b>before the first await</b>, so whoever
        /// asks next — a by-type get (<see cref="GetPopupAsync{T}"/> / <see cref="Show{T}"/>), a layer show, or a preload
        /// pass (<see cref="EnsureEntryLoadedAsync"/>) — awaits the same instance instead of instantiating a second one.
        /// It exists because the residency guards those paths run first (<see cref="TryGetPopup{T}"/> /
        /// <see cref="IsLive"/>) cannot see a load in flight: a popup registers itself from its <c>Awake</c>, i.e. only
        /// once <c>InstantiateAsync</c> has finished, so between two calls in the same frame both guards read "not
        /// present" and each would start its own load.
        /// </summary>
        private static Task<IAdvancedPopup> SharedLoadAsync(AddressablePopupIndexAsset.Entry entry)
        {
            if (_inFlightLoads.TryGetValue(entry.TypeName, out Task<IAdvancedPopup> load))
                return load;

            load = LoadAndOrderAsync(entry);
            _inFlightLoads[entry.TypeName] = load;
            RemoveInFlightWhenComplete(entry.TypeName, load);
            return load;
        }

        /// <summary>
        /// The shared load itself: materialize the entry under its layer canvas and order it. Runs on
        /// <see cref="CancellationToken.None"/> on purpose, never a caller's token — a unique popup is a singleton, so
        /// one caller cancelling (a superseded <see cref="LayerShow(PopupLayerEnum, bool)"/>, an abandoned
        /// <see cref="PreloadAll"/>) must not release the shared instance out from under the others; each caller honors
        /// its own token after the await instead. The instance's <c>Init()</c> self-registers it as the unique instance.
        /// <see cref="ApplyOrder"/> lands here — once per load rather than once per caller — so a preloaded or
        /// just-loaded popup already sits where the Order catalog wants it (the resolver appends it last), even before
        /// its first Show.
        /// </summary>
        private static async Task<IAdvancedPopup> LoadAndOrderAsync(AddressablePopupIndexAsset.Entry entry)
        {
            IAdvancedPopup popup = await Resolver.LoadAsync(entry.Address, GetCanvasForLayer(entry.Layer), CancellationToken.None);
            if (popup != null)
                ApplyOrder(popup);
            return popup;
        }

        /// <summary>
        /// Drops the <see cref="_inFlightLoads"/> entry for <paramref name="typeName"/> once its shared load finishes
        /// (success or failure), so the type can load again later and a failed load never poisons the map. The reference
        /// check avoids evicting a newer entry re-registered meanwhile. The awaiting callers observe the
        /// result/exception; this fire-and-forget cleanup only swallows it (mirrors <see cref="PreloadEntryAsync"/>).
        /// </summary>
        private static async void RemoveInFlightWhenComplete(string typeName, Task<IAdvancedPopup> load)
        {
            try { await load; }
            catch { /* outcome handled by the awaiting callers; here we only clean up the map */ }

            if (_inFlightLoads.TryGetValue(typeName, out Task<IAdvancedPopup> current) && ReferenceEquals(current, load))
                _inFlightLoads.Remove(typeName);
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

        #region SHOW / HIDE (BY TYPE)
        /// <summary>
        /// Show the unique popup of type <typeparamref name="T"/> in one call — loading it from Addressables first when
        /// needed (see <see cref="GetPopupAsync{T}"/>), then playing its cached display. The by-type counterpart to
        /// <see cref="LayerShow(PopupLayerEnum, bool)"/>: summons a single known popup without holding a reference or
        /// driving a whole layer. It does not touch <see cref="ActiveLayer"/> — a by-type show is a manual show, so it
        /// never autohides other layers (mixing it with layer control follows the same rule as <c>popup.Show()</c>).
        /// No-op when the popup can neither be found nor loaded (logged by the getter).
        /// </summary>
        /// <param name="settings">Optional open-animation settings; the popup's cached display is used when null.</param>
        public static Operation Show<T>(IDisplaySettings settings = null) where T : IAdvancedPopup
        {
            return new Operation(async token =>
            {
                T popup = await GetPopupAsync<T>(token);
                if (popup != null)
                    await popup.ShowAsync(token, settings);
            });
        }

        /// <summary>
        /// Show the unique popup of type <typeparamref name="TPopup"/> with a per-call display type
        /// <typeparamref name="TDisplay"/> instead of its cached display — loading it from Addressables first when
        /// needed. See <see cref="Show{T}"/> for the semantics.
        /// </summary>
        /// <param name="settings">Optional settings for the <typeparamref name="TDisplay"/> animation; defaults when null.</param>
        public static Operation Show<TPopup, TDisplay>(IDisplaySettings<TDisplay> settings = null)
            where TPopup : IAdvancedPopup where TDisplay : IDisplay, new()
        {
            return new Operation(async token =>
            {
                TPopup popup = await GetPopupAsync<TPopup>(token);
                if (popup != null)
                    await popup.ShowAsync<TDisplay>(token, settings);
            });
        }

        /// <summary>
        /// Hide the unique popup of type <typeparamref name="T"/> if it is currently resident, playing its cached
        /// display. A no-op when no instance exists — hiding never triggers an Addressable load (nothing to hide), so
        /// this is a synchronous lookup wrapped in an <see cref="Operation"/>. Does not touch <see cref="ActiveLayer"/>.
        /// </summary>
        /// <param name="settings">Optional hide-animation settings; the popup's cached display is used when null.</param>
        public static Operation Hide<T>(IDisplaySettings settings = null) where T : IAdvancedPopup
        {
            return new Operation(async token =>
            {
                if (TryGetPopup<T>(out T popup))
                    await popup.HideAsync(token, settings);
            });
        }

        /// <summary>
        /// Hide the unique popup of type <typeparamref name="TPopup"/> with a per-call display type
        /// <typeparamref name="TDisplay"/> instead of its cached display, if an instance is resident (no load).
        /// </summary>
        /// <param name="settings">Optional settings for the <typeparamref name="TDisplay"/> animation; defaults when null.</param>
        public static Operation Hide<TPopup, TDisplay>(IDisplaySettings<TDisplay> settings = null)
            where TPopup : IAdvancedPopup where TDisplay : IDisplay, new()
        {
            return new Operation(async token =>
            {
                if (TryGetPopup<TPopup>(out TPopup popup))
                    await popup.HideAsync<TDisplay>(token, settings);
            });
        }

        /// <summary>
        /// Toggle the unique popup of type <typeparamref name="T"/> in one call: a resident instance runs its normal
        /// <see cref="IAdvancedPopup.SwitchShowHideAsync"/> (show when hidden, hide when shown), while a popup that is
        /// not loaded yet is loaded from Addressables and shown — not loaded means not shown, so its toggle is a show
        /// (see <see cref="GetPopupAsync{T}"/>). The static counterpart to the instance
        /// <see cref="IAdvancedPopup.SwitchShowHide"/>: no reference needed. Like <see cref="Show{T}"/>/<see cref="Hide{T}"/>
        /// this is a manual call — it never touches <see cref="ActiveLayer"/>.
        /// </summary>
        /// <param name="settings">Optional animation settings; the popup's cached display is used when null.</param>
        public static Operation SwitchShowHide<T>(IDisplaySettings settings = null) where T : IAdvancedPopup
        {
            return new Operation(async token =>
            {
                if (TryGetPopup<T>(out T resident))
                {
                    await resident.SwitchShowHideAsync(token, settings);
                    return;
                }

                T popup = await GetPopupAsync<T>(token);
                if (popup != null)
                    await popup.ShowAsync(token, settings);
            });
        }

        /// <summary>
        /// Toggle the unique popup of type <typeparamref name="TPopup"/> with a per-call display type
        /// <typeparamref name="TDisplay"/> instead of its cached display — loading it from Addressables first when
        /// needed. See <see cref="SwitchShowHide{T}"/> for the semantics.
        /// </summary>
        /// <param name="settings">Optional settings for the <typeparamref name="TDisplay"/> animation; defaults when null.</param>
        public static Operation SwitchShowHide<TPopup, TDisplay>(IDisplaySettings<TDisplay> settings = null)
            where TPopup : IAdvancedPopup where TDisplay : IDisplay, new()
        {
            return new Operation(async token =>
            {
                if (TryGetPopup<TPopup>(out TPopup resident))
                {
                    await resident.SwitchShowHideAsync<TDisplay>(token, settings);
                    return;
                }

                TPopup popup = await GetPopupAsync<TPopup>(token);
                if (popup != null)
                    await popup.ShowAsync<TDisplay>(token, settings);
            });
        }

        /// <summary>
        /// Show the unique popup of type <typeparamref name="T"/> after running <paramref name="configure"/> on it —
        /// load → configure → show, so per-open setup lands <b>before</b> the popup is visible (no flash of
        /// unconfigured content). The low-ceremony alternative to hand-writing that flow in an <see cref="Operation"/>;
        /// a popup with a declared data type is better served by <see cref="Show{TPopup, TData}(TData, IDisplaySettings)"/>.
        /// An exception thrown by <paramref name="configure"/> faults the operation (logged) and the popup is not shown.
        /// </summary>
        /// <param name="configure">Runs on the loaded popup right before the show.</param>
        /// <param name="settings">Optional open-animation settings; the popup's cached display is used when null.</param>
        public static Operation Show<T>(Action<T> configure, IDisplaySettings settings = null) where T : IAdvancedPopup
        {
            return new Operation(async token =>
            {
                T popup = await GetPopupAsync<T>(token);
                if (popup == null || token.IsCancellationRequested) return;

                configure?.Invoke(popup);
                await popup.ShowAsync(token, settings);
            });
        }

        /// <summary>
        /// Show the data popup of type <typeparamref name="TPopup"/> with <paramref name="data"/> — load →
        /// <see cref="AdvancedPopup{TData}.SetData"/> → show, so the data is bound before the popup is visible.
        /// On an already-visible popup this updates the content live. See <see cref="AdvancedPopup{TData}"/> for the
        /// data model (retention, <c>Bind</c>, <c>IsSameData</c>).
        /// </summary>
        /// <param name="data">The data to bind before showing.</param>
        /// <param name="settings">Optional open-animation settings; the popup's cached display is used when null.</param>
        public static Operation Show<TPopup, TData>(TData data, IDisplaySettings settings = null)
            where TPopup : AdvancedPopup<TData>
        {
            return new Operation(async token =>
            {
                TPopup popup = await GetPopupAsync<TPopup>(token);
                if (popup == null || token.IsCancellationRequested) return;

                popup.SetData(data);
                await popup.ShowAsync(token, settings);
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
        /// oldest and applies the first relevant popup's EscapePolicy — Hide closes it, Block consumes the step
        /// without closing (modal), Ignore passes it to the next popup.
        /// <para>
        /// A layer marked <b>Escape Closes Layer</b> in the Layers tool is treated as one screen: reaching any of its
        /// popups closes the whole layer in a single step (<see cref="LayerHide(PopupLayerEnum)"/>, which also clears
        /// <see cref="ActiveLayer"/>), instead of taking one press per popup.
        /// </para>
        /// APS reads no input of its own — call this from whatever drives "back" in your game: an input action,
        /// the Android back button, a UI button.
        /// </summary>
        /// <returns> True if the step was consumed — a popup was hidden or blocked it. </returns>
        public static bool EscapeStep()
        {
            for (int i = ActivePopups.Count - 1; i >= 0; i--)
            {
                IAdvancedPopup popup = ActivePopups[i];
                if (!IsEscapeCandidate(popup))
                    continue;

                switch (popup.EscapePolicy)
                {
                    case EscapePolicyEnum.Hide:
                        if (TryGetEscapeGroupLayer(popup, out PopupLayerEnum layer)) LayerHide(layer);
                        else popup.Hide();
                        return true;
                    case EscapePolicyEnum.Block:
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The layer to close as one screen when <paramref name="popup"/> owns an escape step, or false when the popup
        /// closes on its own. Reads the per-layer <see cref="LayerCanvasConfig.Entry.EscapeClosesLayer"/> flag — so the
        /// grouping is a stable property of the layer rather than of who happened to open what, and the same popup always
        /// behaves the same way. Legacy multi-flag data resolves to the lowest bit, matching the canvas tie-break.
        /// </summary>
        private static bool TryGetEscapeGroupLayer(IAdvancedPopup popup, out PopupLayerEnum layer)
        {
            layer = PopupLayerEnum.None;
            int mask = (int)popup.PopupLayer;
            if (mask == 0) return false;

            LayerCanvasConfig config = LayerCanvasConfig.Loaded;
            if (config == null) return false;

            var flag = (PopupLayerEnum)(mask & -mask);
            LayerCanvasConfig.Entry entry = config.GetEntry(flag);
            if (entry == null || !entry.EscapeClosesLayer) return false;

            layer = flag;
            return true;
        }

        /// <summary>
        /// The popup the next <see cref="EscapeStep"/> would reach — the top of the escape stack — or null when the
        /// stack is empty (a step would then be a no-op). Drives a "Back" button's visibility without stepping.
        /// </summary>
        public static IAdvancedPopup PeekEscapeStack()
        {
            for (int i = ActivePopups.Count - 1; i >= 0; i--)
            {
                IAdvancedPopup popup = ActivePopups[i];
                if (IsInEscapeStack(popup))
                    return popup;
            }

            return null;
        }

        /// <summary>
        /// Whether this popup is in the escape stack right now: visible and not <see cref="EscapePolicyEnum.Ignore"/>.
        /// Being in it doesn't mean it owns the next step — that is <see cref="PeekEscapeStack"/>.
        /// </summary>
        public static bool IsInEscapeStack(IAdvancedPopup popup)
        {
            return IsEscapeCandidate(popup) && popup.EscapePolicy != EscapePolicyEnum.Ignore;
        }

        /// <summary>
        /// Fills <paramref name="buffer"/> with the escape stack, top (most recently shown) first, and returns the
        /// count. The buffer is cleared first and stays caller-owned, so reusing one list keeps this allocation-free.
        /// A snapshot — showing or hiding afterwards doesn't update it.
        /// </summary>
        public static int GetEscapeStack(List<IAdvancedPopup> buffer)
        {
            if (buffer == null) return 0;

            buffer.Clear();
            for (int i = ActivePopups.Count - 1; i >= 0; i--)
            {
                IAdvancedPopup popup = ActivePopups[i];
                if (IsInEscapeStack(popup))
                    buffer.Add(popup);
            }

            return buffer.Count;
        }

        /// <summary>
        /// Puts this popup into the escape stack by giving it a participating policy — <see cref="EscapePolicyEnum.Hide"/>
        /// by default, <see cref="EscapePolicyEnum.Block"/> for a modal. It does NOT show the popup: the stack is the
        /// visible popups in show order, so a popup joins at the top the moment it becomes visible and leaves when it
        /// hides. Membership only decides whether a step stops at it.
        /// </summary>
        public static void AddToEscapeStack(IAdvancedPopup popup, EscapePolicyEnum policy = EscapePolicyEnum.Hide)
        {
            if (popup == null) return;

            popup.EscapePolicy = policy;
        }

        /// <summary>
        /// Takes this popup out of the escape stack (<see cref="EscapePolicyEnum.Ignore"/>) — steps pass through it to
        /// the popup below. It does NOT hide the popup; ordering rules are in <see cref="AddToEscapeStack"/>.
        /// </summary>
        public static void RemoveFromEscapeStack(IAdvancedPopup popup)
        {
            if (popup == null) return;

            popup.EscapePolicy = EscapePolicyEnum.Ignore;
        }

        /// <summary>
        /// Whether the walk reaches this popup at all: alive and not already hiding (which also shields the known
        /// cancel-rollback gap). Says nothing about the popup's policy.
        /// </summary>
        private static bool IsEscapeCandidate(IAdvancedPopup popup)
        {
            return popup != null && popup.IsBeVisible;
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
        /// <param name="parent">Parent for the instance; when null, the popup's layer canvas
        /// (<see cref="GetCanvasForLayer"/>) or <see cref="Root"/> if the layer is unmapped.</param>
        /// <param name="token">Cancels the load in flight.</param>
        public static async Task<T> SpawnAsync<T>(Transform parent = null, CancellationToken token = default)
            where T : IAdvancedPopup
        {
            string typeName = typeof(T).FullName;

            // Reuse a retained instance first (a previous Despawn without release): the single-slot (PoolCapacity == 1)
            // ahead of the multi-copy pool.
            if (typeName != null && _singlePool.TryGetValue(typeName, out var single))
            {
                _singlePool.Remove(typeName);
                if (single != null)
                {
                    single.transform.SetParent(parent != null ? parent : GetCanvasForLayer(single.PopupLayer), false);
                    ApplyOrder(single);
                    SpawnedPopups.Add(single);
                    return (T)single;
                }
            }
            if (typeName != null && _pool.TryGetValue(typeName, out var bucket))
            {
                while (bucket.Count > 0)
                {
                    IAdvancedPopup reused = bucket[bucket.Count - 1];
                    bucket.RemoveAt(bucket.Count - 1);
                    if (reused == null) continue; // destroyed straggler
                    reused.transform.SetParent(parent != null ? parent : GetCanvasForLayer(reused.PopupLayer), false);
                    ApplyOrder(reused);
                    SpawnedPopups.Add(reused);
                    return (T)reused;
                }
            }

            if (Resolver == null || typeName == null || !AddressablePopupIndex.TryGetByTypeName(typeName, out var entry))
            {
                APLogger.LogError($"<color=red>[AdvancedPopupSystem]</color> SpawnAsync<{typeName}> needs the popup flagged Addressable and the Addressables integration present.");
                return null;
            }

            Transform target = parent != null ? parent : GetCanvasForLayer(entry.Layer);
            IAdvancedPopup popup = await Resolver.LoadAsync(entry.Address, target, token);
            if (popup == null)
                return null;

            ApplyOrder(popup);

            // Lane B: pull it back out of the unique registries its Init() just joined — spawned copies are
            // user-managed and must not collide with the by-type cache or be swept up by LayerShow.
            DeactivateAdvancedPopup(popup);
            SpawnedPopups.Add(popup);
            return (T)popup;
        }

        /// <summary>
        /// Spawn a copy of the data popup <typeparamref name="TPopup"/> and bind <paramref name="data"/> before
        /// handing it back — pair with the <see cref="Despawn"/>-side data clearing so a pooled instance never
        /// carries the previous use's content into this one. See <see cref="SpawnAsync{T}"/> for the spawn
        /// semantics and <see cref="AdvancedPopup{TData}"/> for the data model.
        /// </summary>
        /// <param name="data">The data to bind on the spawned instance.</param>
        /// <param name="parent">Parent for the instance; when null, the popup's layer canvas or <see cref="Root"/>.</param>
        /// <param name="token">Cancels the load in flight.</param>
        public static async Task<TPopup> SpawnAsync<TPopup, TData>(TData data, Transform parent = null, CancellationToken token = default)
            where TPopup : AdvancedPopup<TData>
        {
            TPopup popup = await SpawnAsync<TPopup>(parent, token);
            if (popup != null)
                popup.SetData(data);
            return popup;
        }

        /// <summary>
        /// Return a spawned popup (see <see cref="SpawnAsync{T}"/>) to the pool for reuse, or release it entirely.
        /// Hide (await) the popup first if you want its close animation — Despawn itself is instant.
        /// </summary>
        /// <param name="popup">The spawned instance.</param>
        /// <param name="release">
        /// false (default) — honor the popup's <see cref="IAdvancedPopup.PoolCapacity"/>: keep the instance in the pool
        /// for the next <see cref="SpawnAsync{T}"/> of that type (capacity permitting), else release it; true — force
        /// release the instance and its Addressables handle (destroyed, memory can unload) regardless of capacity.
        /// </param>
        public static void Despawn(IAdvancedPopup popup, bool release = false)
        {
            if (popup == null) return;
            SpawnedPopups.Remove(popup);

            // A data popup's content belongs to the use that just ended — forget it so a pooled (or released)
            // instance can't leak it into its next spawn. Retention across shows is a unique-popup (Lane A) rule only.
            if (popup is IDataPopup dataPopup)
                dataPopup.ClearData();

            string typeName = popup.GetType().FullName;
            int cap = popup.PoolCapacity;

            // Force release, unknown type, or PoolCapacity 0 ("despawn") → free it now instead of retaining.
            if (release || typeName == null || cap == 0)
            {
                Resolver?.Release(popup);
                return;
            }

            // PoolCapacity 1 → "on/off": keep a single idle instance in a light single-slot, no pool list. A second
            // idle copy of the type is released (one is the whole point of capacity 1).
            if (cap == 1)
            {
                if (_singlePool.TryGetValue(typeName, out var held) && held != null && !ReferenceEquals(held, popup))
                {
                    Resolver?.Release(popup);
                    return;
                }
                popup.gameObject.SetActive(false);
                _singlePool[typeName] = popup;
                return;
            }

            // PoolCapacity -1 → unlimited pool; N ≥ 2 → keep at most N idle copies, release beyond that.
            if (!_pool.TryGetValue(typeName, out var bucket))
            {
                bucket = new List<IAdvancedPopup>();
                _pool[typeName] = bucket;
            }
            if (cap > 1 && bucket.Count >= cap)
            {
                Resolver?.Release(popup);
                return;
            }

            popup.gameObject.SetActive(false);
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
                foreach (var entry in AddressablePopupIndex.Preloads)
                {
                    if (TaskUtils.OperationCancelled(token)) return;
                    await EnsureEntryLoadedAsync(entry, token);
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
        /// Boot pass + scene-load subscription. AfterSceneLoad (not Before): scene-authored popups have Awoken and
        /// registered by now, so scene-wins can suppress a duplicate; the resolver registered earlier (BeforeSceneLoad),
        /// so it is set. Subscribes <see cref="SceneManager.sceneLoaded"/> for every later scene and runs the first pass
        /// here for the boot scene — <c>sceneLoaded</c> does not fire for the already-loaded first scene.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PreloadOnBoot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ProcessScene(SceneManager.GetActiveScene().path);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ProcessScene(scene.path);

        /// <summary>
        /// Per-scene preload/unload driver — run for the boot scene and on every <see cref="SceneManager.sceneLoaded"/>.
        /// Releases Addressable popups whose <see cref="IAdvancedPopup.UnloadSceneGuids"/> contains this scene, then
        /// eagerly loads Preload popups that want it (all-scenes, or <see cref="IAdvancedPopup.PreloadSceneGuids"/>) and
        /// are not already live or loading. Unload runs first so a scene listed in both sets ends up loaded. Matched by
        /// <see cref="Scene.path"/> against the baked scene paths in the index — identity-based, so reordering Build
        /// Settings never shifts it. No-op without the Addressables resolver.
        /// </summary>
        private static void ProcessScene(string scenePath)
        {
            if (Resolver == null || !AddressablePopupIndex.HasEntries || string.IsNullOrEmpty(scenePath)) return;

            foreach (var entry in AddressablePopupIndex.UnloadsForScene(scenePath))
                UnloadType(entry.TypeName);

            // Skip what is already resident or has a load in flight (a Show<T> in the same frame, an additive scene that
            // preloads the same popup) — SharedLoadAsync would dedup it anyway, this just avoids the wasted state machine.
            foreach (var entry in AddressablePopupIndex.PreloadsForScene(scenePath))
                if (!IsLive(entry.TypeName) && !_inFlightLoads.ContainsKey(entry.TypeName))
                    PreloadEntryAsync(entry);
        }

        /// <summary>
        /// Fire-and-forget one eager preload. Load failures are already logged and swallowed per entry inside
        /// <see cref="EnsureEntryLoadedAsync"/>; the catch here only remains as the async-void safety net.
        /// </summary>
        private static async void PreloadEntryAsync(AddressablePopupIndexAsset.Entry entry)
        {
            try { await EnsureEntryLoadedAsync(entry, CancellationToken.None); }
            catch (Exception ex) { APLogger.LogError($"Exception during APS scene preload: {ex.Message}"); }
        }

        /// <summary>
        /// Release an Addressable popup type from memory for the per-scene unload pass: the resident unique (Lane-A)
        /// instance via the <see cref="Resolver"/> (its <c>OnDestroy</c> prunes the registries) plus any idle pooled
        /// copies of the type. A currently-visible instance and user-owned spawned copies (<see cref="SpawnedPopups"/>)
        /// are left alone; releasing a scene-authored popup is a no-op (the resolver only frees what it created).
        /// No-op without a resolver.
        /// </summary>
        private static void UnloadType(string typeName)
        {
            if (Resolver == null || typeName == null) return;

            // Resident unique instance — skip if visible so we don't yank on-screen UI mid-transition.
            for (int i = AllPopups.Count - 1; i >= 0; i--)
            {
                IAdvancedPopup popup = AllPopups[i];
                if (popup != null && !popup.IsBeVisible && popup.GetType().FullName == typeName)
                    Resolver.Release(popup);
            }

            // Idle pooled copies of the type: the single-slot (PoolCapacity == 1) and the multi-copy pool.
            if (_singlePool.TryGetValue(typeName, out var single))
            {
                _singlePool.Remove(typeName);
                if (single != null) Resolver.Release(single);
            }
            if (_pool.TryGetValue(typeName, out var bucket))
            {
                for (int i = bucket.Count - 1; i >= 0; i--)
                    if (bucket[i] != null) Resolver.Release(bucket[i]);
                bucket.Clear();
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// True when a popup of <paramref name="typeName"/> (Type.FullName) is already present in AllPopups — a live
        /// scene instance or one loaded earlier. Drives the "scene wins the index" rule. It sees only <b>finished</b>
        /// loads (a popup registers itself from its Awake), so it is not on its own a double-load guard — that is
        /// <see cref="SharedLoadAsync"/>'s job.
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
        /// that Resolver is non-null. A failed load is logged (naming the popup type) and <b>swallowed</b> so the
        /// sequential batch loops above it (<see cref="PreloadAll"/>, <see cref="EnsureLayerLoadedAsync"/>, the
        /// per-scene pass) continue with their remaining entries — one popup whose load or Awake throws must not
        /// silently block every popup after it. Cancellation is not treated as a failure: the loops' own
        /// cancellation checks exit instead.
        /// <para>
        /// The load goes through <see cref="SharedLoadAsync"/>, not straight to the resolver: a preload and a
        /// <see cref="Show{T}"/> of the same popup in the same frame must end up awaiting <b>one</b> instance
        /// (<c>IsLive</c> alone cannot see a load in flight). One consequence of that sharing: cancelling the batch no
        /// longer aborts the entry already in flight — it runs to completion and stays resident, because another caller
        /// may be waiting on it. The loop simply stops before the next entry.
        /// </para>
        /// </summary>
        private static async Task EnsureEntryLoadedAsync(AddressablePopupIndexAsset.Entry entry, CancellationToken token)
        {
            if (IsLive(entry.TypeName)) return;
            try
            {
                await SharedLoadAsync(entry);
            }
            catch (Exception ex)
            {
                if (TaskUtils.OperationCancelled(token)) return;
                APLogger.LogError($"<color=red>[AdvancedPopupSystem]</color> Failed to load Addressable popup '{entry.TypeName}' — skipping it, the remaining popups keep loading: {ex}");
            }
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
        /// Get popups whose layer is in <paramref name="layer"/> (any-of for a mask) in any loaded scene.
        /// </summary>
        private static List<IAdvancedPopup> GetPopupsByLayer(PopupLayerEnum layer)
        {
            var popups = new List<IAdvancedPopup>();
            for (int i = 0; i < AllPopups.Count; i++)
            {
                IAdvancedPopup popup = AllPopups[i];
                if (popup != null && (popup.PopupLayer & layer) != 0)
                    popups.Add(popup);
            }
            if (popups.Count == 0)
                APLogger.Log($"AdvancedPopupSystem not found popup/s by '{layer}' layer!");

            return popups;
        }

        /// <summary>
        /// Get popups whose layer is NOT in <paramref name="layer"/> (any-of for a mask) in any loaded scene.
        /// </summary>
        private static List<IAdvancedPopup> GetPopupsExcludingLayer(PopupLayerEnum layer)
        {
            var popups = new List<IAdvancedPopup>();
            for (int i = 0; i < AllPopups.Count; i++)
            {
                IAdvancedPopup popup = AllPopups[i];
                if (popup != null && (popup.PopupLayer & layer) == 0)
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
                // Layers are canvas-bound: a popup belongs to exactly one layer. Legacy multi-flag data still works
                // (any-of matching; canvas = lowest bit) but should be re-authored — warn once, at registration.
                int layerMask = (int)popup.PopupLayer;
                if ((layerMask & (layerMask - 1)) != 0)
                    APLogger.LogWarning($"<color=yellow>[AdvancedPopupSystem]</color> Popup '{popup.name}' carries several layers ({popup.PopupLayer}) — layers are canvas-bound, one layer per popup. It matches any of them, its canvas comes from the lowest bit; pick a single layer in the inspector.");
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