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
            Displays,
            Settings,
        }
        private string imagesPath;

        private const string Version = "1.9.2";
        
        private static Tab currentTab = Tab.Layers;
        
        private Texture2D bannerTexture;
        private Texture2D iconTexture;
        private Texture2D blackTexture;
        
        [MenuItem("APS/Layers")]
        public static void ShowLayers()
        {
            var window = GetWindow<PopupSystemEditor>("Popup System Editor");
            window.titleContent = new GUIContent($"Popup System Editor v{Version}");
            currentTab = Tab.Layers;
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
            bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagesPath + "AP_bundleBlack.png");
            iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagesPath + "AP_LogoBlack.png");
            if (iconTexture != null)
            {
                blackTexture = new Texture2D(1, 1);
                blackTexture.SetPixel(0, 0, Color.black);
                blackTexture.Apply();
            }
            
            // Initialize and load necessary resources
            PopupLayerEditorPanel.Initialize();
            PopupDisplaysEditorPanel.Initialize();
            PopupSettingsEditor.Initialize();
        }

        private void OnGUI()
        {
            bool isDarkTheme = EditorGUIUtility.isProSkin;
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
                GUI.DrawTexture(blackBackgroundRect, blackTexture);

                Rect bannerRect = new Rect((position.width - bannerWidth) / 2, blackBackgroundRect.y, bannerWidth, bannerHeight);
                GUI.DrawTexture(bannerRect, bannerTexture);

                GUILayout.Space(blackBackgroundRect.height);
            }

            EditorGUILayoutExtensions.DrawHorizontalLine(padding: 20);

            switch (currentTab)
            {
                case Tab.Layers:
                    PopupLayerEditorPanel.OnGUIInternall();
                    break;
                case Tab.Displays:
                    PopupDisplaysEditorPanel.OnGUIInternall();
                    break;
                case Tab.Settings:
                    PopupSettingsEditor.OnGUIInternall();
                    break;
            }
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Layers", currentTab == Tab.Layers ? APSEditorStyles.SelectedTabStyle : APSEditorStyles.NormalTabStyle))
            {
                currentTab = Tab.Layers;
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
