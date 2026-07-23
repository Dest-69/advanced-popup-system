using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Dev-only toggle that makes the hidden sample sources editable in this dev project, driven by the
    /// "Sample sources" buttons in the <see cref="APSPackageExporter"/> window (APS ▸ Build ▸ Export Package…).
    ///
    /// Sources live in the hidden <c>Samples~/</c> folder so they never auto-compile in a consumer (Unity ignores '~'
    /// folders — which also makes them invisible in this dev project). <see cref="StartEditing"/> moves every showcase
    /// into the visible <c>Samples/</c> folder where Unity imports it as usual (<c>.meta</c>s travel with the files, so
    /// GUIDs stay stable); <see cref="FinishEditing"/> moves it back, parking the showcase's own folder <c>.meta</c>
    /// next to the source in <c>Samples~/</c> so the folder GUID survives round-trips and exporter stagings.
    /// While anything is checked out, <see cref="APSPackageExporter"/> refuses to build — raw sources must never ship.
    /// Lives in <c>Editor/Build/</c>, which is on the exporter's deny-list, so this tool never ships either.
    /// </summary>
    public static class APSSampleDevMode
    {
        private const string PackageRoot        = "Assets/advanced-popup-system";
        private const string SamplesFolder      = PackageRoot + "/Samples";
        private const string SamplesTildeFolder = PackageRoot + "/Samples~";

        /// <summary>Folders under <c>Samples/</c> that are raw-shipped content (never checked-out sources).</summary>
        private static readonly HashSet<string> RawShippedFolders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Utils" };

        /// <summary>True while any sample source is checked out into the visible <c>Samples/</c> folder.</summary>
        public static bool AnyCheckedOut => CheckedOutDirs().Any();

        /// <summary>True while there are hidden sources in <c>Samples~/</c> available for checkout.</summary>
        public static bool HasHiddenSources => SourceDirs().Any();

        #region Actions

        /// <summary>Moves every hidden showcase <c>Samples~/ → Samples/</c> so Unity imports it for editing.</summary>
        public static void StartEditing()
        {
            var moved = new List<string>();
            foreach (string srcFs in SourceDirs().ToArray())
            {
                string name = Path.GetFileName(srcFs);
                string destFs = ToFs($"{SamplesFolder}/{name}");
                if (Directory.Exists(destFs))
                {
                    Debug.LogError($"[APS Samples] '{SamplesFolder}/{name}' already exists — skipped. Resolve the duplicate manually.");
                    continue;
                }
                Directory.Move(srcFs, destFs);
                MoveIfExists(srcFs + ".meta", destFs + ".meta");
                moved.Add(name);
            }

            if (moved.Count == 0) return;
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[APS Samples]</color> Checked out for editing: {string.Join(", ", moved)}. " +
                      "Finish editing (APS ▸ Build ▸ Export Package…) before committing or exporting.");
        }

        /// <summary>Moves every checked-out showcase back <c>Samples/ → Samples~/</c>, parking its folder .meta.</summary>
        public static void FinishEditing()
        {
            string[] staged = CheckedOutDirs().ToArray();
            if (staged.Length == 0) return;

            // A loaded scene from a checked-out showcase keeps living in memory, but its file moves back into the
            // hidden folder — saving it afterwards would recreate it under Samples/. Safer to close it first.
            string openScene = LoadedSceneUnder(staged);
            if (openScene != null && !EditorUtility.DisplayDialog("APS Samples",
                    $"Scene '{openScene}' belongs to a checked-out showcase.\nClose it first, or its file will move " +
                    "into the hidden Samples~ folder while the scene stays open.", "Hide Anyway", "Cancel"))
                return;

            AssetDatabase.SaveAssets();
            var moved = new List<string>();
            foreach (string stagedFs in staged)
            {
                string name = Path.GetFileName(stagedFs);
                string destFs = ToFs($"{SamplesTildeFolder}/{name}");
                if (Directory.Exists(destFs))
                {
                    Debug.LogError($"[APS Samples] '{SamplesTildeFolder}/{name}' already exists — skipped. Resolve the duplicate manually.");
                    continue;
                }
                Directory.Move(stagedFs, destFs);
                MoveIfExists(stagedFs + ".meta", destFs + ".meta");
                moved.Add(name);
            }

            if (moved.Count == 0) return;
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[APS Samples]</color> Hidden back into Samples~: {string.Join(", ", moved)}.");
        }

        #endregion

        #region State

        /// <summary>One reminder per domain reload while sources are checked out, so a long session can't forget.</summary>
        [InitializeOnLoadMethod]
        private static void WarnIfCheckedOut()
        {
            if (AnyCheckedOut)
                Debug.Log("<color=yellow>[APS Samples]</color> Sample sources are checked out for editing — finish " +
                          "editing (APS ▸ Build ▸ Export Package… → Sample sources) before committing or exporting.");
        }

        /// <summary>Filesystem paths of the hidden source showcases (<c>Samples~/*</c>).</summary>
        private static IEnumerable<string> SourceDirs()
        {
            string root = ToFs(SamplesTildeFolder);
            return Directory.Exists(root) ? Directory.GetDirectories(root) : Enumerable.Empty<string>();
        }

        /// <summary>Filesystem paths of showcases currently checked out into <c>Samples/</c>.</summary>
        private static IEnumerable<string> CheckedOutDirs()
        {
            string root = ToFs(SamplesFolder);
            if (!Directory.Exists(root)) return Enumerable.Empty<string>();
            return Directory.GetDirectories(root).Where(d => !RawShippedFolders.Contains(Path.GetFileName(d)));
        }

        /// <summary>Asset path of a loaded scene inside any of the given staged showcases, or null.</summary>
        private static string LoadedSceneUnder(IEnumerable<string> stagedFsDirs)
        {
            string[] prefixes = stagedFsDirs.Select(d => $"{SamplesFolder}/{Path.GetFileName(d)}/").ToArray();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                string path = SceneManager.GetSceneAt(i).path;
                if (!string.IsNullOrEmpty(path) && prefixes.Any(p => path.StartsWith(p, StringComparison.Ordinal)))
                    return path;
            }
            return null;
        }

        #endregion

        #region Helpers

        private static void MoveIfExists(string fromFs, string toFs)
        {
            if (!File.Exists(fromFs)) return;
            if (File.Exists(toFs)) File.Delete(toFs);
            File.Move(fromFs, toFs);
        }

        private static string ToFs(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }

        #endregion
    }
}
