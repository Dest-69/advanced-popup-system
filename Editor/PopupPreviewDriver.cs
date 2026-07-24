using System;
using System.Collections.Generic;
using System.Reflection;
using AdvancedPS.Core;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Edit-mode preview of a popup's show → hold → hide cycle, driven from <see cref="EditorApplication.update"/>.
    /// The runtime animation path cannot run outside play mode (<c>TaskUtils.OperationCancelled</c> treats
    /// <c>!Application.isPlaying</c> as cancelled, and <c>Time.deltaTime</c> is frozen in edit mode), so the built-in
    /// displays are re-sampled here from <see cref="EditorApplication.timeSinceStartup"/> with the same lerp + easing
    /// math their generated ShowMethod/HideMethod bodies use; any other display type (custom, DoTween) has opaque
    /// animation code and degrades to its instant methods. The popup's visual state is snapshotted before the preview
    /// and restored however the preview ends — nothing is dirtied, and the runtime Show/Hide path
    /// (Subscribe, ActivePopups, CancellationTokenSource) is never invoked.
    /// </summary>
    internal static class PopupPreviewDriver
    {
        private enum Phase { Show, Hold, Hide }

        /// <summary> How long the popup stays fully shown between the show and hide animations. </summary>
        private const float HoldSeconds = 1f;

        /// <summary>
        /// One animation phase re-expressed for editor time: Begin applies the display's start-of-method side effects
        /// and captures the "from" values, Sample applies the frame state for a 0..1 progress, Finish applies the
        /// exact end-of-method state. Duration 0 (custom/DoTween fallback, or a bad Duration value) = instant.
        /// </summary>
        private sealed class PhaseProgram
        {
            public float Duration;
            public Action Begin;
            public Action<float> Sample;
            public Action Finish;
        }

        private static IAdvancedPopup _popup;
        private static RectTransform _rt;
        private static CanvasGroup _group;

        private static Phase _phase;
        private static double _phaseStart;
        private static PhaseProgram _show;
        private static PhaseProgram _hide;

        // Pre-preview state, put back whatever way the preview ends.
        private static bool _hadActiveSelf;
        private static Vector3 _hadScale;
        private static Vector3 _hadAnchoredPos;
        private static Vector2 _hadSizeDelta;
        private static float _hadAlpha;
        private static bool _hadInteractable;
        private static bool _hadBlocksRaycasts;

        // User OnAnimationStart/End delegates detached for the duration of the preview (instant methods fire them,
        // and game callbacks must not run from an editor preview); restored in Cleanup.
        private static readonly List<(object settings, FieldInfo field, object value)> SwappedCallbacks = new();

        /// <summary> True while THIS popup is being previewed (drives the inspector's Preview/Stop button). </summary>
        public static bool IsPreviewing(IAdvancedPopup popup) => _popup != null && ReferenceEquals(_popup, popup);

        /// <summary>
        /// Starts a preview on the given scene / prefab-stage popup. A running preview (on any popup) is stopped and
        /// restored first. No-op in play mode and on persistent assets (an asset can't be animated).
        /// </summary>
        public static void Start(IAdvancedPopup popup)
        {
            Stop();

            if (popup == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(popup))
                return;

            RectTransform rt = popup.transform as RectTransform;
            CanvasGroup group = popup.GetComponent<CanvasGroup>();
            if (rt == null || group == null)
            {
                APLogger.LogWarning("<color=orange>[APS]</color> Preview needs a RectTransform and a CanvasGroup on the popup root.");
                return;
            }

            _popup = popup;
            _rt = rt;
            _group = group;
            TakeSnapshot();

            try
            {
                if (!popup.gameObject.activeSelf)
                    popup.gameObject.SetActive(true);

                // The show/hide choice lives in user Init() overrides (SetCachedDisplay) and the caches are
                // runtime-only, so the first preview must run Init() to learn them. Its scene edits are covered by
                // the snapshot; the registration it performs is undone in Cleanup (DeactivateAdvancedPopup).
                if (popup.CachedShowSettings == null || popup.CachedHideSettings == null)
                    popup.Init();

                if (popup.CachedShowSettings == null || popup.CachedHideSettings == null)
                {
                    APLogger.LogWarning("<color=orange>[APS]</color> Preview aborted — Init() cached no displays (does the override call base.Init()?).");
                    RestoreAndCleanup();
                    return;
                }

                IDisplay showDisplay = DisplayRegistry.Get(popup.CachedShowSettings.DisplayType);
                IDisplay hideDisplay = DisplayRegistry.Get(popup.CachedHideSettings.DisplayType);

                SwapOutCallbacks(popup.CachedShowSettings);
                SwapOutCallbacks(popup.CachedHideSettings);

                // Start from the true hidden endpoint so the show animation covers its real distance.
                hideDisplay.HideInstantlyMethod(_rt, popup.CachedHideSettings);

                _show = BuildShowProgram(showDisplay, popup.CachedShowSettings);
                _hide = BuildHideProgram(hideDisplay, popup.CachedHideSettings);
            }
            catch (Exception e) // user Init() or a custom display's instant method threw — put everything back
            {
                Debug.LogException(e);
                RestoreAndCleanup();
                return;
            }

            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;

            BeginPhase(Phase.Show);
        }

        /// <summary> Stops the running preview (if any) and restores the popup's pre-preview state. </summary>
        public static void Stop()
        {
            // _show is a plain C# object, so it can't turn fake-null the way a destroyed _popup/_rt can —
            // it is the reliable "a preview is live, hooks are attached" marker.
            if (_show == null && _popup == null) return;
            RestoreAndCleanup();
        }

        #region Phases
        private static void BeginPhase(Phase phase)
        {
            _phase = phase;
            _phaseStart = EditorApplication.timeSinceStartup;

            if (phase == Phase.Show) _show.Begin?.Invoke();
            else if (phase == Phase.Hide) _hide.Begin?.Invoke();
        }

        private static void Tick()
        {
            if (_popup == null) // destroyed mid-preview — nothing left to restore
            {
                Cleanup();
                return;
            }

            try
            {
                float elapsed = (float)(EditorApplication.timeSinceStartup - _phaseStart);
                switch (_phase)
                {
                    case Phase.Show:
                        if (RunPhase(_show, elapsed)) BeginPhase(Phase.Hold);
                        break;
                    case Phase.Hold:
                        if (elapsed >= HoldSeconds) BeginPhase(Phase.Hide);
                        break;
                    case Phase.Hide:
                        if (RunPhase(_hide, elapsed))
                        {
                            RestoreAndCleanup();
                            return;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                RestoreAndCleanup();
                return;
            }

            // Edit mode repaints only on demand — without this the animation would only advance when the mouse moves.
            InternalEditorUtility.RepaintAllViews();
        }

        /// <summary> Advances one phase; true when it finished this tick. </summary>
        private static bool RunPhase(PhaseProgram program, float elapsed)
        {
            if (program.Duration > 0f && elapsed < program.Duration)
            {
                program.Sample?.Invoke(elapsed / program.Duration);
                return false;
            }

            program.Finish?.Invoke();
            return true;
        }
        #endregion

        #region Phase programs (mirrors of the generated display bodies)
        /// <summary>
        /// Rebuilds a display's ShowMethod for editor time. Built-ins replicate the exact lerp + easing of their
        /// generated sources — keep in sync when those bodies change. Unknown display types degrade to the instant method.
        /// </summary>
        private static PhaseProgram BuildShowProgram(IDisplay display, IDisplaySettings settings)
        {
            Type type = display.GetType();

            if (type == typeof(ScaleDisplay))
            {
                ScaleSettings s = settings as ScaleSettings ?? new ScaleSettings();
                Vector3 from = default;
                return new PhaseProgram
                {
                    Duration = SafeDuration(s.Duration),
                    Begin = () => { SetGroupVisible(true, 1f); from = _rt.localScale; },
                    Sample = t => _rt.localScale = Vector3.LerpUnclamped(from, s.ShowScale, EasingFunctions.Get(s.Easing, t)),
                    Finish = () => { SetGroupVisible(true, 1f); _rt.localScale = s.ShowScale; }
                };
            }

            if (type == typeof(FadeDisplay))
            {
                FadeSettings s = settings as FadeSettings ?? new FadeSettings();
                float from = 0f;
                return new PhaseProgram
                {
                    Duration = SafeDuration(s.Duration),
                    // FadeDisplay.ShowMethod enables interactable/blocksRaycasts only at the END — mirrored here.
                    Begin = () => { _rt.localScale = Vector3.one; from = _group.alpha; },
                    Sample = t => _group.alpha = Mathf.LerpUnclamped(from, s.MaxValue, EasingFunctions.Get(s.Easing, t)),
                    Finish = () => { _rt.localScale = Vector3.one; SetGroupVisible(true, s.MaxValue); }
                };
            }

            if (type == typeof(SlideDisplay))
            {
                SlideSettings s = settings as SlideSettings ?? new SlideSettings();
                Vector3 fromPos = default;
                Vector2 fromSize = default;
                return new PhaseProgram
                {
                    Duration = SafeDuration(s.Duration),
                    Begin = () =>
                    {
                        SetGroupVisible(true, 1f);
                        _rt.localScale = Vector3.one;
                        fromPos = _rt.anchoredPosition3D;
                        fromSize = _rt.sizeDelta;
                    },
                    // SlideDisplay lerps clamped (Vector3.Lerp), unlike Scale/Fade — mirrored here.
                    Sample = t =>
                    {
                        float eased = EasingFunctions.Get(s.Easing, t);
                        _rt.anchoredPosition3D = Vector3.Lerp(fromPos, s.TargetRectPosition, eased);
                        _rt.sizeDelta = Vector2.Lerp(fromSize, s.TargetRectSize, eased);
                    },
                    Finish = () =>
                    {
                        SetGroupVisible(true, 1f);
                        _rt.localScale = Vector3.one;
                        _rt.anchoredPosition3D = s.TargetRectPosition;
                        _rt.sizeDelta = s.TargetRectSize;
                    }
                };
            }

            return new PhaseProgram { Duration = 0f, Finish = () => display.ShowInstantlyMethod(_rt, settings) };
        }

        /// <summary> Hide-side twin of <see cref="BuildShowProgram"/>. </summary>
        private static PhaseProgram BuildHideProgram(IDisplay display, IDisplaySettings settings)
        {
            Type type = display.GetType();

            if (type == typeof(ScaleDisplay))
            {
                ScaleSettings s = settings as ScaleSettings ?? new ScaleSettings();
                Vector3 from = default;
                return new PhaseProgram
                {
                    Duration = SafeDuration(s.Duration),
                    Begin = () => from = _rt.localScale,
                    Sample = t => _rt.localScale = Vector3.LerpUnclamped(from, s.HideScale, EasingFunctions.Get(s.Easing, t)),
                    Finish = () => { _rt.localScale = s.HideScale; SetGroupVisible(false, 0f); }
                };
            }

            if (type == typeof(FadeDisplay))
            {
                FadeSettings s = settings as FadeSettings ?? new FadeSettings();
                float from = 0f;
                return new PhaseProgram
                {
                    Duration = SafeDuration(s.Duration),
                    Begin = () => from = _group.alpha,
                    Sample = t => _group.alpha = Mathf.LerpUnclamped(from, s.MinValue, EasingFunctions.Get(s.Easing, t)),
                    Finish = () => { _rt.localScale = Vector3.zero; SetGroupVisible(false, s.MinValue); }
                };
            }

            if (type == typeof(SlideDisplay))
            {
                SlideSettings s = settings as SlideSettings ?? new SlideSettings();
                Vector3 fromPos = default;
                Vector2 fromSize = default;
                return new PhaseProgram
                {
                    Duration = SafeDuration(s.Duration),
                    Begin = () => { fromPos = _rt.anchoredPosition3D; fromSize = _rt.sizeDelta; },
                    Sample = t =>
                    {
                        float eased = EasingFunctions.Get(s.Easing, t);
                        _rt.anchoredPosition3D = Vector3.Lerp(fromPos, s.TargetRectPosition, eased);
                        _rt.sizeDelta = Vector2.Lerp(fromSize, s.TargetRectSize, eased);
                    },
                    Finish = () =>
                    {
                        _rt.anchoredPosition3D = s.TargetRectPosition;
                        _rt.sizeDelta = s.TargetRectSize;
                        SetGroupVisible(false, 0f);
                        _rt.localScale = Vector3.zero;
                    }
                };
            }

            return new PhaseProgram { Duration = 0f, Finish = () => display.HideInstantlyMethod(_rt, settings) };
        }

        /// <summary> NaN / negative / zero Duration means "instant" — keeps a bad value from stalling a phase forever. </summary>
        private static float SafeDuration(float duration) => duration > 0f ? duration : 0f;

        private static void SetGroupVisible(bool visible, float alpha)
        {
            _group.alpha = alpha;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }
        #endregion

        #region Snapshot & cleanup
        private static void TakeSnapshot()
        {
            _hadActiveSelf = _popup.gameObject.activeSelf;
            _hadScale = _rt.localScale;
            _hadAnchoredPos = _rt.anchoredPosition3D;
            _hadSizeDelta = _rt.sizeDelta;
            _hadAlpha = _group.alpha;
            _hadInteractable = _group.interactable;
            _hadBlocksRaycasts = _group.blocksRaycasts;
        }

        /// <summary>
        /// Detaches OnAnimationStart/OnAnimationEnd from a settings object for the whole preview. Reflection because
        /// the fields live on the generic BaseSettings&lt;TDisplay&gt;. Skips an instance already swapped —
        /// SetCachedDisplay&lt;T&gt; caches the same settings object for show and hide.
        /// </summary>
        private static void SwapOutCallbacks(IDisplaySettings settings)
        {
            if (settings == null) return;
            foreach ((object swapped, _, _) in SwappedCallbacks)
                if (ReferenceEquals(swapped, settings))
                    return;

            foreach (string name in new[] { "OnAnimationStart", "OnAnimationEnd" })
            {
                FieldInfo field = settings.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
                if (field == null || field.FieldType != typeof(Action)) continue;
                SwappedCallbacks.Add((settings, field, field.GetValue(settings)));
                field.SetValue(settings, null);
            }
        }

        private static void RestoreAndCleanup()
        {
            if (_popup != null && _rt != null && _group != null)
            {
                _rt.localScale = _hadScale;
                _rt.anchoredPosition3D = _hadAnchoredPos;
                _rt.sizeDelta = _hadSizeDelta;
                _group.alpha = _hadAlpha;
                _group.interactable = _hadInteractable;
                _group.blocksRaycasts = _hadBlocksRaycasts;
                if (_popup.gameObject.activeSelf != _hadActiveSelf)
                    _popup.gameObject.SetActive(_hadActiveSelf);
            }

            Cleanup();
        }

        private static void Cleanup()
        {
            // Undo the registration Init() may have done; harmless when it never ran.
            if (_popup != null)
                AdvancedPopupSystem.DeactivateAdvancedPopup(_popup);

            foreach ((object settings, FieldInfo field, object value) in SwappedCallbacks)
                field.SetValue(settings, value);
            SwappedCallbacks.Clear();

            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            PrefabStage.prefabSaving -= OnPrefabSaving;

            _popup = null;
            _rt = null;
            _group = null;
            _show = null;
            _hide = null;

            InternalEditorUtility.RepaintAllViews();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) Stop();
        }

        private static void OnSceneSaving(Scene scene, string path) => Stop();

        private static void OnPrefabSaving(GameObject prefab) => Stop();
        #endregion
    }
}
