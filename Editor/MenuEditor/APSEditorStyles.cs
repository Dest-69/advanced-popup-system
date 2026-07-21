using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor.Styles
{
    /// <summary>
    /// Shared editor GUI styles for the APS inspectors and windows.
    ///
    /// Styles are built lazily and self-heal. The 1×1 background textures they rely on are plain
    /// <see cref="Texture2D"/> objects, which Unity destroys during memory cleanup (entering/exiting play mode, scene
    /// loads, <c>Resources.UnloadUnusedAssets</c>). A destroyed background renders as nothing — that is why the dark
    /// box groups occasionally lost their background. Two guards: the textures are flagged
    /// <see cref="HideFlags.HideAndDontSave"/> so cleanup skips them, and every accessor rebuilds its style if the
    /// backing texture died anyway. Building lazily (inside the getter, first hit during OnGUI) also avoids touching
    /// <see cref="GUI.skin"/> from a static constructor that may run outside a GUI context.
    /// </summary>
    public static class APSEditorStyles
    {
        // Simple, skin-derived styles (no owned texture → never culled, built once).
        private static GUIStyle _toggle;
        private static GUIStyle _warpedText;
        private static GUIStyle _warningText;
        private static GUIStyle _boldButton;
        private static GUIStyle _experimentalButton;
        private static GUIStyle _scrollView;
        private static GUIStyle _line;
        private static GUIStyle _header;

        // Texture-backed styles (rebuilt when their background texture is culled).
        private static GUIStyle _selectedTab;
        private static GUIStyle _normalTab;
        private static GUIStyle _darkBackground;
        private static GUIStyle _background;

        private static bool IsDark => EditorGUIUtility.isProSkin;

        public static GUIStyle ToggleStyle => _toggle ??= new GUIStyle(GUI.skin.toggle)
        {
            fontSize = 13,
            contentOffset = new Vector2(-10, 0)
        };

        public static GUIStyle WarpedTextStyle => _warpedText ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

        public static GUIStyle WarningTextStyle => _warningText ??= new GUIStyle(GUI.skin.label)
        {
            normal = { textColor = new Color(0.7f, 0.6f, 0f, 1f) }
        };

        public static GUIStyle BoldButtonStyle => _boldButton ??= new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold
        };

        public static GUIStyle ExperimentalButtonStyle => _experimentalButton ??= new GUIStyle(GUI.skin.button)
        {
            padding = new RectOffset(10, 10, 0, 0),
            fixedHeight = 18
        };

        public static GUIStyle ScrollViewStyle => _scrollView ??= new GUIStyle(GUI.skin.scrollView)
        {
            stretchHeight = true,
            stretchWidth = true
        };

        public static GUIStyle LineStyle => _line ??= new GUIStyle
        {
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(-25, -25, 0, 0)
        };

        /// <summary> Bold, centered label for the section headers inside the inspector boxes. </summary>
        public static GUIStyle HeaderLabelStyle => _header ??= new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };

        public static GUIStyle SelectedTabStyle
        {
            get
            {
                if (Culled(_selectedTab))
                    _selectedTab = new GUIStyle(GUI.skin.button)
                    {
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(4, 4, 4, 4),
                        normal = { background = MakeTex(Color.white), textColor = Color.white }
                    };
                return _selectedTab;
            }
        }

        public static GUIStyle NormalTabStyle
        {
            get
            {
                if (Culled(_normalTab))
                    _normalTab = new GUIStyle(GUI.skin.button)
                    {
                        border = new RectOffset(2, 2, 2, 2),
                        normal = { background = MakeTex(Color.gray), textColor = Color.gray }
                    };
                return _normalTab;
            }
        }

        public static GUIStyle DarkBackgroundStyle
        {
            get
            {
                if (Culled(_darkBackground))
                    _darkBackground = new GUIStyle
                    {
                        normal =
                        {
                            background = MakeTex(IsDark
                                ? new Color(0.19f, 0.19f, 0.19f, 1f)
                                : new Color(0.7f, 0.7f, 0.7f, 1f))
                        },
                        padding = new RectOffset(10, 10, 10, 10)
                    };
                return _darkBackground;
            }
        }

        public static GUIStyle BackgroundStyle
        {
            get
            {
                if (Culled(_background))
                    _background = new GUIStyle
                    {
                        normal =
                        {
                            background = MakeTex(IsDark
                                ? new Color(0.23f, 0.23f, 0.23f, 1f)
                                : new Color(0.8f, 0.8f, 0.8f, 1f))
                        },
                        padding = new RectOffset(5, 0, 5, 5)
                    };
                return _background;
            }
        }

        /// <summary> True when a texture-backed style is missing or its background texture has been destroyed. </summary>
        private static bool Culled(GUIStyle style) => style == null || style.normal.background == null;

        /// <summary> 1×1 solid texture that survives editor memory cleanup (so the style it backs stays visible). </summary>
        private static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
