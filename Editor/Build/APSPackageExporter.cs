using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Dev-only tool that builds a distributable <c>.unitypackage</c> of the Advanced Popup System.
    ///
    /// Consumers never get a working copy of this tool, per distribution channel: the <c>.unitypackage</c> build
    /// <b>excludes</b> the whole <c>Editor/Build/</c> folder (deny-list below), while the UPM/git channel — which ships
    /// the repo as-is, deny-list not applying — carries the folder but never compiles it: its own asmdef
    /// (<c>dest-69.advanced-popup-system.editor.build</c>) is constrained to <c>APS_DEV</c>, a scripting define only
    /// this dev project sets (Project Settings ▸ Player). The build:
    /// <list type="bullet">
    /// <item>ships only files under <c>Assets/advanced-popup-system</c>, with no third-party dependencies pulled in
    /// (no <see cref="ExportPackageOptions.IncludeDependencies"/>);</item>
    /// <item>keeps sample <b>sources</b> in the hidden <c>Samples~/</c> folder (Unity ignores '~' folders): UPM consumers
    /// get them as on-demand Package Manager samples (<c>package.json</c> "samples"), never auto-compiled; for this
    /// <c>.unitypackage</c> it rebuilds each into a nested <c>Samples/&lt;Showcase&gt;.unitypackage</c> (opt-in, imported by
    /// double-click) and ships those;</item>
    /// <item>excludes the internal tooling (the Obsidian vault, <c>CLAUDE.md</c>, and this exporter). To edit sample
    /// sources in the dev project, check them out via this window's "Sample sources" buttons
    /// (<see cref="APSSampleDevMode"/>) — export refuses to run while they are checked out;</item>
    /// <item>ships <c>PopupLayerEnum</c> <b>inside</b> the package (assembly <c>AdvancedPS.Generated.Layers</c>) — a
    /// compile-time type a fresh install needs before any code runs; the dev repo keeps it at the default layer set so a
    /// consumer's custom layers are never shipped over. Consumer-side state (custom displays, the Addressable index,
    /// <c>AP_Settings.json</c>, <c>APS_Layers.json</c>) lives outside the package and is excluded automatically;</item>
    /// <item>writes the result to <c>Assets/Development/AdvancedPS_v&lt;version&gt;.unitypackage</c>.</item>
    /// </list>
    /// </summary>
    public class APSPackageExporter : EditorWindow
    {
        private const string PackageRoot   = "Assets/advanced-popup-system";
        private const string OutputFolder  = "Assets/Development";
        private const string SamplesFolder = PackageRoot + "/Samples";
        // Sample sources live here, hidden from Unity ('~' folder). UPM ships them as on-demand samples; the exporter
        // stages them into the visible Samples/ folder to rebuild the nested *.unitypackage.
        private const string SamplesTildeFolder = PackageRoot + "/Samples~";
        private const string ExporterFolder = PackageRoot + "/Editor/Build";
        private const string VaultFolder   = PackageRoot + "/AdvancedPopupSystem_Obsidian_Vault";
        private const string ClaudeFile    = PackageRoot + "/CLAUDE.md";
        private const string PackageJson   = PackageRoot + "/package.json";

        private string _version = "";
        private bool _updatePackageJson;
        private bool _rebuildSamples = true;
        private Vector2 _scroll;

        // Live preview of what the package will contain (files only; refreshed on open and after each export).
        private string[] _included;
        private string _breakdown = "";
        private bool _previewFoldout;
        private Vector2 _previewScroll;

        [MenuItem("APS/Build/Export Package…", false, 200)]
        private static void Open()
        {
            var window = GetWindow<APSPackageExporter>(true, "APS — Export Package");
            window.minSize = new Vector2(460, 360);
            window._version = ReadPackageVersion() ?? "1.0.0";
            window.RefreshPreview();
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Build a distributable package", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _version = EditorGUILayout.TextField("Version", _version);
            string outName = $"AdvancedPS_v{_version.Trim()}.unitypackage";
            EditorGUILayout.LabelField("Output", $"{OutputFolder}/{outName}");

            EditorGUILayout.Space(6);
            _rebuildSamples = EditorGUILayout.ToggleLeft(
                "Rebuild sample sub-packages (Samples/*/ → Samples/*.unitypackage)", _rebuildSamples);
            _updatePackageJson = EditorGUILayout.ToggleLeft(
                "Update package.json version to the value above", _updatePackageJson);
            if (_updatePackageJson)
                EditorGUILayout.HelpBox(
                    "A package.json version bump ripples to every UPM consumer. Add a matching CHANGELOG entry.",
                    MessageType.Warning);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    _included == null ? "Ships in the package" : $"Ships in the package — {_included.Length} files",
                    EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(64)))
                    RefreshPreview();
            }
            if (!string.IsNullOrEmpty(_breakdown))
                EditorGUILayout.HelpBox(_breakdown + "\n(＋ folder structure. PopupLayerEnum ships inside the package as a " +
                                        "compile-time type; custom displays generate into the consumer project. Sample sources " +
                                        "live in Samples~/ and ship as nested Samples/*.unitypackage.)", MessageType.None);

            if (_included != null)
            {
                _previewFoldout = EditorGUILayout.Foldout(_previewFoldout, "File list", true);
                if (_previewFoldout)
                {
                    _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.Height(150));
                    foreach (string p in _included)
                        EditorGUILayout.LabelField(p.Substring(PackageRoot.Length + 1), EditorStyles.miniLabel);
                    EditorGUILayout.EndScrollView();
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Kept out of the package", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "• Obsidian vault + CLAUDE.md (internal tooling)\n" +
                "• This exporter (Editor/Build/)\n" +
                "• Raw sample sources in Samples~/ (invisible to Unity → ship to UPM as on-demand samples;\n" +
                "  this .unitypackage carries the built Samples/*.unitypackage instead)\n" +
                "• Anything outside advanced-popup-system — no third-party dependencies",
                MessageType.None);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Sample sources", EditorStyles.miniBoldLabel);
            bool samplesCheckedOut = APSSampleDevMode.AnyCheckedOut;
            if (samplesCheckedOut)
            {
                EditorGUILayout.HelpBox(
                    "Sample sources are checked out into Samples/ for editing — export is disabled so raw sources " +
                    "can't ship.", MessageType.Warning);
                if (GUILayout.Button("Finish Editing — hide sources back into Samples~"))
                {
                    APSSampleDevMode.FinishEditing();
                    RefreshPreview();
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(!APSSampleDevMode.HasHiddenSources))
                {
                    if (GUILayout.Button("Edit Sample Sources — show in Samples/ for editing"))
                    {
                        APSSampleDevMode.StartEditing();
                        RefreshPreview();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_version) || samplesCheckedOut))
            {
                if (GUILayout.Button("Export", GUILayout.Height(32)))
                    Export();
            }

            EditorGUILayout.EndScrollView();
        }

        private void Export()
        {
            // Raw sample sources must never ship — refuse while APSSampleDevMode has them checked out into Samples/.
            if (APSSampleDevMode.AnyCheckedOut)
            {
                EditorUtility.DisplayDialog("APS Export",
                    "Sample sources are checked out for editing.\n" +
                    "Finish editing (the 'Sample sources' button in this window) so raw sources don't ship.", "OK");
                return;
            }

            string version = _version.Trim();
            if (string.IsNullOrEmpty(version))
            {
                EditorUtility.DisplayDialog("APS Export", "Version must not be empty.", "OK");
                return;
            }

            string outputAsset = $"{OutputFolder}/AdvancedPS_v{version}.unitypackage";
            string outputFs = ToFs(outputAsset);
            if (File.Exists(outputFs) && !EditorUtility.DisplayDialog("APS Export",
                    $"{Path.GetFileName(outputFs)} already exists.\nOverwrite it?", "Overwrite", "Cancel"))
                return;

            bool bumpVersion = _updatePackageJson;
            if (bumpVersion && !EditorUtility.DisplayDialog("APS Export",
                    $"Set package.json version to \"{version}\"?\nThis ripples to every UPM consumer — remember the CHANGELOG.",
                    "Update package.json", "Keep current"))
                bumpVersion = false;

            bool ok = false;
            int fileCount = 0;
            List<string> rebuilt = new List<string>();

            try
            {
                EditorUtility.DisplayProgressBar("APS Export", "Rebuilding samples…", 0.15f);
                if (_rebuildSamples)
                    rebuilt = RebuildSampleSubPackages();

                if (bumpVersion)
                    WritePackageVersion(version);

                // PopupLayerEnum ships inside the package (assembly AdvancedPS.Generated.Layers) — a compile-time type a
                // fresh install needs before any code runs. The dev repo keeps it at LayerCatalog.DefaultLayerNames, so
                // there is nothing to reset here; if the dev's working layer set ever diverges, stage the default set
                // before export (see the vault's "Build & Packaging" note) so custom layers never ship.
                AssetDatabase.Refresh();
                string[] assets = CollectPackageAssets();
                fileCount = assets.Length;

                EnsureFolder(OutputFolder);
                EditorUtility.DisplayProgressBar("APS Export", "Writing .unitypackage…", 0.75f);
                AssetDatabase.ExportPackage(assets, outputFs, ExportPackageOptions.Default);
                ok = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[APS Export] Failed: {ex}");
                EditorUtility.DisplayDialog("APS Export", "Export failed:\n" + ex.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            if (ok)
            {
                AssetDatabase.ImportAsset(outputAsset);
                UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(outputAsset);
                if (obj != null) EditorGUIUtility.PingObject(obj);
                Debug.Log($"<color=green>[APS Export]</color> Built {Path.GetFileName(outputFs)} — {fileCount} assets" +
                          (rebuilt.Count > 0 ? $"; samples: {string.Join(", ", rebuilt)}" : "") + ".");
                RefreshPreview();
            }
        }

        #region Collect

        /// <summary>Every asset under the package folder, minus the deny-list (internal tooling + this exporter).</summary>
        private static string[] CollectPackageAssets()
        {
            var result = new List<string>();
            foreach (string raw in AssetDatabase.GetAllAssetPaths())
            {
                string p = raw.Replace('\\', '/');
                if (p != PackageRoot && !p.StartsWith(PackageRoot + "/", StringComparison.Ordinal)) continue;
                if (IsDenied(p)) continue;
                result.Add(p);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Internal tooling never shipped: the Obsidian vault, <c>CLAUDE.md</c>, and this exporter. Sample <b>sources</b>
        /// need no entry — they live in the hidden <c>Samples~/</c> folder, invisible to the AssetDatabase; the visible
        /// <c>Samples/</c> folder holds only <c>Utils/</c> and the built nested <c>*.unitypackage</c>, both of which ship.
        /// </summary>
        private static bool IsDenied(string p)
        {
            if (p == VaultFolder || p.StartsWith(VaultFolder + "/", StringComparison.Ordinal)) return true;
            if (p == ClaudeFile) return true;
            if (p == ExporterFolder || p.StartsWith(ExporterFolder + "/", StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Recomputes the "ships in the package" preview (files only) and the per-area breakdown.</summary>
        private void RefreshPreview()
        {
            _included = CollectPackageAssets()
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();

            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (string p in _included)
            {
                string area = AreaOf(p);
                counts.TryGetValue(area, out int c);
                counts[area] = c + 1;
            }
            _breakdown = string.Join("    ", counts.Select(kv => $"{kv.Key}: {kv.Value}"));
        }

        /// <summary>Top-level area of an asset path under the package (its first folder, or "root" for package files).</summary>
        private static string AreaOf(string assetPath)
        {
            string rel = assetPath.Substring(PackageRoot.Length + 1);
            int slash = rel.IndexOf('/');
            return slash < 0 ? "root" : rel.Substring(0, slash);
        }

        #endregion

        #region Samples

        /// <summary>
        /// Rebuilds each nested <c>Samples/&lt;Showcase&gt;.unitypackage</c> from its source in the hidden
        /// <c>Samples~/</c> folder. Because Unity ignores '~' folders, <c>Samples~</c> is invisible to the AssetDatabase,
        /// so each showcase is briefly <b>staged</b> (copied — <c>.meta</c> files included, for stable GUIDs) into the
        /// visible <c>Samples/&lt;Showcase&gt;/</c> location to get GUIDs, exported, then removed again. Assembly reload is
        /// <b>locked</b> across the whole run so the freshly-imported sample scripts can't trigger a domain reload
        /// mid-build (which would abort this method and strand a staged copy).
        /// </summary>
        private static List<string> RebuildSampleSubPackages()
        {
            var built = new List<string>();
            string tildeFs = ToFs(SamplesTildeFolder);
            if (!Directory.Exists(tildeFs))
            {
                Debug.LogWarning($"[APS Export] No '{SamplesTildeFolder}' folder found — no sample sources to rebuild.");
                return built;
            }

            EditorApplication.LockReloadAssemblies();
            try
            {
                foreach (string srcDir in Directory.GetDirectories(tildeFs))
                {
                    string name = Path.GetFileName(srcDir);
                    string stagedAsset = $"{SamplesFolder}/{name}";
                    string stagedFs = ToFs(stagedAsset);
                    try
                    {
                        CopyDirectory(srcDir.Replace('\\', '/'), stagedFs);
                        // Reuse the parked folder .meta (kept next to the source by APSSampleDevMode) so the staged
                        // showcase folder keeps a stable GUID across exports.
                        if (File.Exists(srcDir + ".meta")) File.Copy(srcDir + ".meta", stagedFs + ".meta", true);
                        AssetDatabase.Refresh();

                        string outAsset = $"{SamplesFolder}/{name}.unitypackage";
                        // Recurse the staged folder but pull NO dependencies — the consumer already has core APS + DoTween.
                        AssetDatabase.ExportPackage(new[] { stagedAsset }, ToFs(outAsset), ExportPackageOptions.Recurse);
                        AssetDatabase.ImportAsset(outAsset, ImportAssetOptions.ForceUpdate);
                        built.Add(name);
                    }
                    finally
                    {
                        // Drop the staged copy: sources stay only in Samples~/, the built *.unitypackage stays in Samples/.
                        if (!AssetDatabase.DeleteAsset(stagedAsset) && Directory.Exists(stagedFs))
                        {
                            Directory.Delete(stagedFs, true);
                            if (File.Exists(stagedFs + ".meta")) File.Delete(stagedFs + ".meta");
                        }
                    }
                }
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.Refresh();
            }
            return built;
        }

        /// <summary>Recursively copies a directory — files (including <c>.meta</c>) and sub-directories.</summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, destDir + "/" + Path.GetFileName(file), true);
            foreach (string sub in Directory.GetDirectories(sourceDir))
                CopyDirectory(sub.Replace('\\', '/'), destDir + "/" + Path.GetFileName(sub));
        }

        #endregion

        #region package.json

        private static string ReadPackageVersion()
        {
            try
            {
                Match m = Regex.Match(File.ReadAllText(ToFs(PackageJson)), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                return m.Success ? m.Groups[1].Value : null;
            }
            catch { return null; }
        }

        private static void WritePackageVersion(string version)
        {
            string fs = ToFs(PackageJson);
            string txt = File.ReadAllText(fs);
            string updated = Regex.Replace(txt, "(\"version\"\\s*:\\s*\")[^\"]+(\")", "${1}" + version + "$2");
            File.WriteAllText(fs, updated);
            AssetDatabase.ImportAsset(PackageJson, ImportAssetOptions.ForceUpdate);
        }

        #endregion

        #region Helpers

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string ToFs(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }

        #endregion

    }
}
