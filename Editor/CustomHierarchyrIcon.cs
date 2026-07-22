using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    [InitializeOnLoad]
    public static class CustomInspectorIconEditor
    {
        private static readonly PopupSettings Settings;
        
        private static string imagesPath;
        private static Texture2D popupIcon;

        static CustomInspectorIconEditor()
        {
            Settings = SettingsManager.Settings;
            imagesPath = FileSearcher.ImagesFolderPath;
            if (!string.IsNullOrEmpty(imagesPath))
                popupIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(imagesPath + "AP_LogoBlack32.png");

            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            if (Settings.InspectorView != InspectorEnum.APSInspector) return;
            
            Object obj = EditorUtility.InstanceIDToObject(instanceID);
            if (popupIcon != null && obj is GameObject go && go.GetComponent<IAdvancedPopup>() != null)
            {
                Rect rect = new Rect(selectionRect.x + selectionRect.width - 16, selectionRect.y, 16, 16);
                GUI.Label(rect, new GUIContent(popupIcon));
            }
        }
    }
}
