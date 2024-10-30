using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor.Styles
{
    public static class APSEditorStyles
    {
        public static readonly GUIStyle ToggleStyle;
        public static readonly GUIStyle ButtonBoltStyle;
        public static readonly GUIStyle SelectedTabStyle;
        public static readonly GUIStyle NormalTabStyle;
        public static readonly GUIStyle ScrollViewStyle;
        public static readonly GUIStyle BoldButtonStyle;
        public static readonly GUIStyle ExperimentalButtonStyle;
        public static readonly GUIStyle DarkBackgroundStyle;
        public static readonly GUIStyle BackgroundStyle;
        public static readonly GUIStyle LineStyle;
        
        static APSEditorStyles()
        {
            bool isDarkTheme = EditorGUIUtility.isProSkin;

            ButtonBoltStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };

            Texture2D selectedTexture = new Texture2D(1, 1);
            selectedTexture.SetPixel(0, 0, Color.white);
            selectedTexture.Apply();
            
            Texture2D normalTexture = new Texture2D(1, 1);
            normalTexture.SetPixel(0, 0, Color.gray);
            normalTexture.Apply();
            
            Texture2D normalTextureInspector = new Texture2D(1, 1);
            normalTextureInspector.SetPixel(0, 0, isDarkTheme ? 
                new Color(0.23f, 0.23f, 0.23f, 1f) : 
                new Color(0.8f, 0.8f, 0.8f, 1f));
            normalTextureInspector.Apply();
            
            Texture2D backgroundTexture = new Texture2D(1, 1);
            backgroundTexture.SetPixel(0, 0, isDarkTheme ? 
                new Color(0.19f, 0.19f, 0.19f, 1f) : 
                new Color(0.7f, 0.7f, 0.7f, 1f));
            backgroundTexture.Apply();

            ToggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 13,
                contentOffset = new Vector2(-10, 0)
            };
            
            LineStyle = new GUIStyle
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(-25,-25, 0, 0)
            };
            
            BackgroundStyle = new GUIStyle
            {
                normal =
                {
                    background = normalTextureInspector
                },
                padding = new RectOffset(5, 0, 5, 5)
            };
            
            DarkBackgroundStyle = new GUIStyle
            {
                normal =
                {
                    background = backgroundTexture
                },
                padding = new RectOffset(10, 10, 10, 10)
            };

            SelectedTabStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(4, 4, 4, 4),
                normal = { background = selectedTexture, textColor = Color.white }
            };
            
            BoldButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };

            ExperimentalButtonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(10, 10, 0, 0),
                fixedHeight = 18
            };

            NormalTabStyle = new GUIStyle(GUI.skin.button)
            {
                border = new RectOffset(2, 2, 2, 2),
                normal = { background = normalTexture, textColor = Color.gray }
            };
            
            ScrollViewStyle = new GUIStyle(GUI.skin.scrollView)
            {
                stretchHeight = true,
                stretchWidth = true,
            };
        }
    }
}