using AdvancedPS.Core.Utils;
using AdvancedPS.Editor.Styles;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    public class PopupSystemEditor : EditorWindow
    {
        private enum Tab
        {
            Layers,
            Order,
            Displays,
            Settings,
        }
        private string imagesPath;

        public static string Version;
        
        private static Tab currentTab = Tab.Layers;
        
        private Texture2D bannerTexture;
        
        [MenuItem("APS/Layers")]
        public static void ShowLayers()
        {
            var window = GetWindow<PopupSystemEditor>("Popup System Editor");
            window.titleContent = new GUIContent($"Popup System Editor v{Version}");
            currentTab = Tab.Layers;
        }
        [MenuItem("APS/Order")]
        public static void ShowOrder()
        {
            var window = GetWindow<PopupSystemEditor>("Popup System Editor");
            window.titleContent = new GUIContent($"Popup System Editor v{Version}");
            currentTab = Tab.Order;
        }

        /// <summary>
        /// Opens the Order tab filtered to one layer — popups compete only inside their layer's canvas, so this is the
        /// useful entry point from a layer row (Layers tab) or a popup inspector.
        /// </summary>
        public static void ShowOrder(string layerName)
        {
            ShowOrder();
            PopupOrderEditorPanel.FocusLayer(layerName);
        }
        [MenuItem("APS/Displays")]
        public static void ShowDisplays()
        {
            var window = GetWindow<PopupSystemEditor>("Popup System Editor");
            window.titleContent = new GUIContent($"Popup System Editor v{Version}");
            currentTab = Tab.Displays;
        }
        [MenuItem("APS/Settings")]
        public static void ShowSettings()
        {
            var window = GetWindow<PopupSystemEditor>("Popup System Editor");
            window.titleContent = new GUIContent($"Popup System Editor v{Version}");
            currentTab = Tab.Settings;
        }

        private void OnEnable()
        {
            minSize = new Vector2(300, 450);
            
            imagesPath = FileSearcher.ImagesFolderPath;
            if (!string.IsNullOrEmpty(imagesPath))
                bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine(imagesPath, "AP_bundleBlack.png"));
            
            Version = PackageVersionHelper.GetVersion();
            
            // Initialize and load necessary resources
            PopupLayerEditorPanel.Initialize();
            PopupOrderEditorPanel.Initialize();
            PopupDisplaysEditorPanel.Initialize();
            PopupSettingsEditor.Initialize();
        }

        private void OnGUI()
        {
            bool isDarkTheme = EditorGUIUtility.isProSkin;
            
            var prevBg = GUI.backgroundColor;
            var prevCt = GUI.contentColor;
            GUI.backgroundColor = isDarkTheme ? Color.black : Color.white;
            GUI.contentColor = isDarkTheme ? Color.white : Color.black;
            
            if (isDarkTheme)
                EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), Color.black);

            DrawTabs();
            
            if (bannerTexture != null)
            {
                const float bannerWidth = 256;
                const float bannerHeight = 128;
                Rect blackBackgroundRect = GUILayoutUtility.GetRect(position.width, bannerHeight);
                EditorGUI.DrawRect(blackBackgroundRect, Color.black);

                Rect bannerRect = new Rect((position.width - bannerWidth) / 2, blackBackgroundRect.y, bannerWidth, bannerHeight);
                GUI.DrawTexture(bannerRect, bannerTexture);
            }

            EditorGUILayoutExtensions.DrawHorizontalLine(padding: 20);

            switch (currentTab)
            {
                case Tab.Layers:
                    PopupLayerEditorPanel.OnGUIInternal();
                    break;
                case Tab.Order:
                    PopupOrderEditorPanel.OnGUIInternal();
                    break;
                case Tab.Displays:
                    PopupDisplaysEditorPanel.OnGUIInternal();
                    break;
                case Tab.Settings:
                    PopupSettingsEditor.OnGUIInternal();
                    break;
            }
            
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevCt;
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Layers", currentTab == Tab.Layers ? APSEditorStyles.SelectedTabStyle : APSEditorStyles.NormalTabStyle))
            {
                currentTab = Tab.Layers;
            }
            if (GUILayout.Button("Order", currentTab == Tab.Order ? APSEditorStyles.SelectedTabStyle : APSEditorStyles.NormalTabStyle))
            {
                currentTab = Tab.Order;
            }
            if (GUILayout.Button("Displays", currentTab == Tab.Displays ? APSEditorStyles.SelectedTabStyle : APSEditorStyles.NormalTabStyle))
            {
                currentTab = Tab.Displays;
            }
            if (GUILayout.Button("Settings", currentTab == Tab.Settings ? APSEditorStyles.SelectedTabStyle : APSEditorStyles.NormalTabStyle))
            {
                currentTab = Tab.Settings;
            }
            GUILayout.EndHorizontal();
        }
    }
}
