using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEngine;
    
namespace AdvancedPS.Editor
{
    public static class EditorGUILayoutExtensions
    {
        /// <summary>
        /// Centered, bold section title followed by a divider — the shared header for the inspector's boxes
        /// (General Settings, Modules, Addressable, Pool, …). Replaces the repeated FlexibleSpace/Label/FlexibleSpace
        /// blocks so every section reads the same.
        /// </summary>
        public static void DrawSectionHeader(string title)
        {
            GUILayout.Label(title, APSEditorStyles.HeaderLabelStyle);
            DrawHorizontalLine();
        }

        public static void DrawHorizontalLine(Color color = default, float thickness = 1f, float padding = 5f, float margin = 0f)
        {
            if (color == default)
            {
                color = EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.2f) :
                    new Color(0.1f, 0.1f, 0.1f, 0.5f);
            }
            
            thickness -= 0.1f;
            Rect rect = EditorGUILayout.GetControlRect(false, thickness + padding);
            rect.height = thickness;
            rect.y += padding / 2;
            rect.x += margin;
            rect.width -= margin * 2;
            EditorGUI.DrawRect(rect, color);
        }
        
        public static void DrawVerticalLine(Color color = default, float thickness = 1f, float padding = 0f, float margin = 0f)
        {
            if (color == default)
            {
                color = EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.2f) :
                    new Color(0.1f, 0.1f, 0.1f, 0.5f);
            }
            
            Rect rect = EditorGUILayout.GetControlRect(false, 0, APSEditorStyles.LineStyle, GUILayout.ExpandHeight(true));
            rect.x += rect.width / 2f - thickness / 2f + padding / 2f;
            rect.y += margin;
            rect.width = thickness;
            rect.height -= margin * 2f;
            EditorGUI.DrawRect(rect, color);
        }
    }
}