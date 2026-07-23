using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Provides the consumer-owned <b>default canvas prefab</b> the Layers panel assigns to every layer (the canvas
    /// field is mandatory — see <see cref="PopupLayerEditorPanel"/>). The prefab is created once into the consumer
    /// project at <see cref="AssetPath"/> — <b>outside</b> the package, so a <c>.unitypackage</c>/UPM update never
    /// clobbers the user's edits (the same non-destructive-update rule as <c>APS_LayerCanvasConfig.asset</c> and the
    /// generated displays). It ships sensible responsive defaults — on the built-in <b>UI</b> layer, full-screen,
    /// ScreenSpaceOverlay, a <see cref="CanvasScaler"/> set to Scale-With-Screen-Size at 1920×1080, and a
    /// <see cref="GraphicRaycaster"/> — which the user tunes on this asset to control the default for all layers.
    /// </summary>
    internal static class DefaultCanvasFactory
    {
        /// <summary>Consumer-side folder for APS assets the user owns (sibling of the generated-code root).</summary>
        private const string Folder = "Assets/AdvancedPopupSystem";
        private const string AssetPath = Folder + "/APS_DefaultCanvas.prefab";

        /// <summary>
        /// The default canvas prefab's <see cref="Canvas"/>, creating the prefab asset on first use. Null only when the
        /// asset could not be created (logged) — callers keep the runtime fallback for that case.
        /// </summary>
        internal static Canvas EnsureDefault()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Canvas>(AssetPath);
            if (existing != null)
                return existing;

            GameObject go = null;
            try
            {
                if (!AssetDatabase.IsValidFolder(Folder))
                    AssetDatabase.CreateFolder("Assets", "AdvancedPopupSystem");

                // A responsive, full-screen overlay canvas the consumer owns and tunes. name / sortingOrder /
                // DontDestroyOnLoad are applied per layer at instantiation (AdvancedPopupSystem.CreateLayerCanvas), so
                // they are intentionally left at prefab defaults here.
                go = new GameObject("APS_DefaultCanvas");
                go.layer = LayerMask.NameToLayer("UI");
                go.transform.localScale = Vector3.one;

                Canvas canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                go.AddComponent<GraphicRaycaster>();

                // Stretch the canvas rect to fill the screen.
                RectTransform rect = canvas.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, AssetPath, out bool success);
                if (success && saved != null)
                    return saved.GetComponent<Canvas>();

                Debug.LogError($"[APS] Failed to save the default canvas prefab at {AssetPath}.");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[APS] Could not create the default canvas prefab: {ex.Message}");
                return null;
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }
    }
}
