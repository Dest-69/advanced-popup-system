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
    /// This tool <b>excludes itself</b> from the exported package (its whole <c>Editor/Build/</c> folder is on the
    /// deny-list) so consumers never receive it. The build:
    /// <list type="bullet">
    /// <item>ships only files under <c>Assets/advanced-popup-system</c>, with no third-party dependencies pulled in
    /// (no <see cref="ExportPackageOptions.IncludeDependencies"/>);</item>
    /// <item>rebuilds each <c>Samples/&lt;Showcase&gt;/</c> into a nested <c>Samples/&lt;Showcase&gt;.unitypackage</c>
    /// and ships those instead of the raw sample sources;</item>
    /// <item>excludes the internal tooling (the Obsidian vault, <c>CLAUDE.md</c>, and this exporter);</item>
    /// <item>ships <b>no</b> generated <c>PopupLayerEnum</c> — it now lives in the consumer project
    /// (<c>Assets/AdvancedPopupSystem/Generated/</c>) and is seeded on import by the bundled
    /// <c>AdvancedPS.Bootstrap</c> assembly, so a consumer's own layer set is never shipped over (the Addressable index
    /// is likewise a consumer-side data asset, excluded automatically, like <c>AP_Settings.json</c>);</item>
    /// <item>writes the result to <c>Assets/Development/AdvancedPS_v&lt;version&gt;.unitypackage</c>.</item>
    /// </list>
    /// </summary>
    public class APSPackageExporter : EditorWindow
    {
        private const string PackageRoot   = "Assets/advanced-popup-system";
        private const string OutputFolder  = "Assets/Development";
        private const string SamplesFolder = PackageRoot + "/Samples";
        private const string ExporterFolder = PackageRoot + "/Editor/Build";
        private const string VaultFolder   = PackageRoot + "/AdvancedPopupSystem_Obsidian_Vault";
        private const string ClaudeFile    = PackageRoot + "/CLAUDE.md";
        private const string PackageJson   = PackageRoot + "/package.json";
        private const string SamplesUtils  = "Utils";

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
                EditorGUILayout.HelpBox(_breakdown + "\n(＋ folder structure. PopupLayerEnum and custom displays are " +
                                        "generated into the consumer project — not shipped; the bundled AdvancedPS.Bootstrap " +
                                        "assembly seeds them on import.)", MessageType.None);

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
                "• Raw sample sources (only the built Samples/*.unitypackage ship)\n" +
                "• Anything outside advanced-popup-system — no third-party dependencies",
                MessageType.None);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_version)))
            {
                if (GUILayout.Button("Export", GUILayout.Height(32)))
                    Export();
            }

            EditorGUILayout.EndScrollView();
        }

        private void Export()
        {
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

                // PopupLayerEnum is no longer part of the package — it is generated into the consumer project and seeded
                // by the bundled AdvancedPS.Bootstrap assembly on import — so there is nothing to stage or restore here.
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

        /// <summary>Every asset under the package folder, minus the deny-list (internal tooling, exporter, raw samples).</summary>
        private static string[] CollectPackageAssets()
        {
            string[] showcaseFolders = AssetDatabase.GetSubFolders(SamplesFolder)
                .Select(f => f.Replace('\\', '/'))
                .Where(f => Path.GetFileName(f) != SamplesUtils)
                .ToArray();

            var result = new List<string>();
            foreach (string raw in AssetDatabase.GetAllAssetPaths())
            {
                string p = raw.Replace('\\', '/');
                if (p != PackageRoot && !p.StartsWith(PackageRoot + "/", StringComparison.Ordinal)) continue;
                if (IsDenied(p, showcaseFolders)) continue;
                result.Add(p);
            }
            return result.ToArray();
        }

        private static bool IsDenied(string p, string[] showcaseFolders)
        {
            if (p == VaultFolder || p.StartsWith(VaultFolder + "/", StringComparison.Ordinal)) return true;
            if (p == ClaudeFile) return true;
            if (p == ExporterFolder || p.StartsWith(ExporterFolder + "/", StringComparison.Ordinal)) return true;
            foreach (string f in showcaseFolders)
                if (p == f || p.StartsWith(f + "/", StringComparison.Ordinal)) return true;
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

        private static List<string> RebuildSampleSubPackages()
        {
            var built = new List<string>();
            foreach (string folderRaw in AssetDatabase.GetSubFolders(SamplesFolder))
            {
                string folder = folderRaw.Replace('\\', '/');
                string name = Path.GetFileName(folder);
                if (name == SamplesUtils) continue;

                string outAsset = $"{SamplesFolder}/{name}.unitypackage";
                // Recurse the sample folder but pull NO dependencies — the consumer already has core APS + DoTween.
                AssetDatabase.ExportPackage(new[] { folder }, ToFs(outAsset), ExportPackageOptions.Recurse);
                AssetDatabase.ImportAsset(outAsset, ImportAssetOptions.ForceUpdate);
                built.Add(name);
            }
            return built;
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
