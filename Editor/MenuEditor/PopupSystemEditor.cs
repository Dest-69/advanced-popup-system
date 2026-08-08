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

            // Opening the window is a deliberate action, so it is worth one small GET at the remote — unlike OnEnable,
            // which also runs on every domain reload. The badge shows "sync…" until the answer lands (DrawVersionBar).
            PackageUpdater.EnsureLatestChecked(() => { if (window != null) window.Repaint(); }, force: true);
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
            // Not forced: OnEnable also fires on every domain reload, and one GET per recompile is noise. Opening the
            // window does force it (see Open). The check outlives the window, so the callback survives it being closed.
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
        /// update for either shape APS is normally installed in (see <see cref="PackageUpdater"/>), so this row is the
        /// one place that does. Drawn above the tabs: it is about the window, not about whichever tab is open. It
        /// degrades quietly — no remote answer (offline, non-GitHub host) means no badge.
        ///
        /// <b>Badge and button share one gate.</b> Whenever the check says a newer version exists the button is there —
        /// saying "you are out of date" and leaving nothing to press is the one outcome this row must not produce. What
        /// pressing it does is <see cref="PackageUpdater.Route"/>'s business.
        /// </summary>
        private void DrawVersionBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"v{Version}", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

            string latest = PackageUpdater.LatestVersion;
            bool updateAvailable = PackageUpdater.UpdateAvailable;
            bool checking = PackageUpdater.IsCheckingLatest;

            if (checking || !string.IsNullOrEmpty(latest))
            {
                // The window multiplies text by GUI.contentColor (black on the light skin), which would swallow the
                // style's colour — neutralise it for the badge only.
                var prevContent = GUI.contentColor;
                GUI.contentColor = Color.white;
                GUILayout.Label(checking ? SyncLabel() : updateAvailable ? $"(new {latest})" : "(latest)",
                    checking || !updateAvailable ? APSEditorStyles.VersionOkStyle : APSEditorStyles.VersionNewStyle,
                    GUILayout.ExpandWidth(false));
                GUI.contentColor = prevContent;
            }

            // Only while a check is in flight — a few seconds of animation, then the window goes idle again.
            if (checking)
                Repaint();

            if (updateAvailable)
            {
                GUILayout.Space(6);
                using (new EditorGUI.DisabledScope(PackageUpdater.IsBusy))
                {
                    var label = new GUIContent(PackageUpdater.IsBusy ? "Updating…" : "Update",
                        PackageUpdater.IsBusy ? "APS is reinstalling itself — Unity recompiles a few times."
                                              : PackageUpdater.UpdateTooltip);

                    if (GUILayout.Button(label, GUILayout.Width(70), GUILayout.Height(16)))
                        PackageUpdater.BeginUpdate();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// "sync" with an ellipsis that cycles while the remote check runs. The dots are padded to a constant width so
        /// the badge — and the row's centered layout — doesn't jitter as they come and go.
        /// </summary>
        private static string SyncLabel()
        {
            int dots = (int)(EditorApplication.timeSinceStartup * 2.5d) % 4;
            return "(sync" + new string('.', dots).PadRight(3) + ")";
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
