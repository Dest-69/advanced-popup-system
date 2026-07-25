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

        // Unity derives both the docking tab and a floating window's OS title bar from titleContent, so this is the one
        // name the window can have. The version used to live here too — it is drawn in the window now (DrawVersionBar),
        // which is also the only place it can carry the "(latest)"/"(new …)" badge.
        private const string WindowTitle = "Advanced Popup System";

        private static Tab currentTab = Tab.Layers;

        private Texture2D bannerTexture;

        private static void Open(Tab tab)
        {
            var window = GetWindow<PopupSystemEditor>(WindowTitle);
            window.titleContent = new GUIContent(WindowTitle);
            currentTab = tab;
        }

        [MenuItem("APS/Layers")]
        public static void ShowLayers() => Open(Tab.Layers);

        [MenuItem("APS/Order")]
        public static void ShowOrder() => Open(Tab.Order);

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
        public static void ShowDisplays() => Open(Tab.Displays);

        [MenuItem("APS/Settings")]
        public static void ShowSettings() => Open(Tab.Settings);

        private void OnEnable()
        {
            minSize = new Vector2(300, 450);
            
            imagesPath = FileSearcher.ImagesFolderPath;
            if (!string.IsNullOrEmpty(imagesPath))
                bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(System.IO.Path.Combine(imagesPath, "AP_bundleBlack.png"));
            
            Version = PackageVersionHelper.GetVersion();
            // Once per editor session, and only while the window is actually open — repaint when the answer lands.
            // The check outlives the window, so the callback has to survive it being closed first.
            PackageUpdater.EnsureLatestChecked(() => { if (this != null) Repaint(); });

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

            DrawVersionBar();
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

        /// <summary>
        /// Installed version, how it compares to the Git remote, and the update itself — Package Manager offers no
        /// update for either shape APS can be installed in (see <see cref="PackageUpdater"/>), so this row is the one
        /// place that does. Drawn above the tabs: it is about the window, not about whichever tab is open. It degrades
        /// quietly — no remote answer (offline, non-GitHub host) means no badge, and an install nobody but its owner
        /// should touch means no button.
        /// </summary>
        private void DrawVersionBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"v{Version}", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            string latest = PackageUpdater.LatestVersion;
            bool updateAvailable = PackageUpdater.UpdateAvailable;
            if (!string.IsNullOrEmpty(latest))
            {
                // The window multiplies text by GUI.contentColor (black on the light skin), which would swallow the
                // style's colour — neutralise it for the badge only.
                var prevContent = GUI.contentColor;
                GUI.contentColor = Color.white;
                GUILayout.Label(updateAvailable ? $"(new {latest})" : "(latest)",
                    updateAvailable ? APSEditorStyles.VersionNewStyle : APSEditorStyles.VersionOkStyle,
                    GUILayout.ExpandWidth(false));
                GUI.contentColor = prevContent;
            }

            if (updateAvailable && PackageUpdater.CanUpdate)
            {
                GUILayout.Space(6);
                using (new EditorGUI.DisabledScope(PackageUpdater.IsBusy))
                {
                    if (GUILayout.Button(PackageUpdater.IsBusy ? "Updating…" : "Update",
                            GUILayout.Width(70), GUILayout.Height(16)))
                        PackageUpdater.BeginUpdate();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
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
