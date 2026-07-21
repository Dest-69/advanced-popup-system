using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AdvancedPS.Core;
using AdvancedPS.Core.Utils;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Source of truth for the popup layer set. The durable copy lives <b>outside</b> the package, in the consumer
    /// project's <c>ProjectSettings/APS_Layers.json</c>, so importing a new package version never clobbers it — the
    /// same principle that keeps <c>AP_Settings.json</c> in the consumer project (see <c>SettingsManager</c>). The
    /// compiled <c>PopupLayerEnum.generated.cs</c> shipped inside the package is only a regenerated <i>projection</i>
    /// of that list; <see cref="Reconcile()"/> restores it from the store after an update overwrites it.
    /// </summary>
    internal static class LayerCatalog
    {
        /// <summary>Starter layers a fresh consumer gets when no external store exists yet.</summary>
        internal static readonly string[] DefaultLayerNames = { "GUI", "GAME", "MENU", "OVERLAY" };

        private const string StoreFileName = "APS_Layers.json";

        /// <summary>
        /// When true, <see cref="Reconcile(string,bool)"/> is a no-op. The package exporter raises this while it
        /// temporarily resets the enum to a clean default for the build, so the heal path does not fight it.
        /// </summary>
        internal static bool SuppressReconcile;

        [Serializable]
        private class LayerData { public string[] names; }

        /// <summary>
        /// <c>ProjectSettings/APS_Layers.json</c> — editor-only, lives outside <c>Assets</c>, is never shipped in a
        /// player build and is never touched by a <c>.unitypackage</c> import.
        /// </summary>
        internal static string StorePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
                return Path.Combine(projectRoot, "ProjectSettings", StoreFileName).Replace('\\', '/');
            }
        }

        internal static bool StoreExists => File.Exists(StorePath);

        #region Store I/O

        /// <summary>Reads the ordered layer names from the store, or null when it does not exist / is unreadable.</summary>
        internal static string[] LoadNames()
        {
            try
            {
                if (File.Exists(StorePath))
                {
                    LayerData data = JsonUtility.FromJson<LayerData>(File.ReadAllText(StorePath));
                    if (data?.names != null)
                        return Normalize(data.names);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[APS] Failed to read {StoreFileName}: {ex.Message}");
            }
            return null;
        }

        /// <summary>Writes the ordered, sanitized layer names to the store (source of truth, outside the package).</summary>
        internal static void SaveNames(IEnumerable<string> names)
        {
            try
            {
                var data = new LayerData { names = Normalize(names) };
                File.WriteAllText(StorePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[APS] Failed to write {StoreFileName}: {ex.Message}");
            }
        }

        #endregion

        #region Codegen

        /// <summary>
        /// Builds the <c>PopupLayerEnum</c> source from an ordered name list: <c>None = 0</c>, then <c>1 &lt;&lt; bit</c>
        /// (max 31 flags for an <c>int</c> bitmask). This is the single codegen path used by the Layers panel, the
        /// heal reconcile, and the exporter's clean-default reset.
        /// </summary>
        internal static string GenerateEnumSource(IEnumerable<string> names)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("namespace AdvancedPS.Core");
            sb.AppendLine("{");
            sb.AppendLine("    [Flags]");
            sb.AppendLine("    public enum PopupLayerEnum");
            sb.AppendLine("    {");
            sb.AppendLine("        None = 0,");

            int bit = 0;
            foreach (string raw in names ?? Enumerable.Empty<string>())
            {
                string name = Sanitize(raw);
                if (string.IsNullOrEmpty(name) || name == "NONE") continue;
                if (bit >= 31)
                {
                    Debug.LogError("[APS] Too many layer flags for an int enum. Max 31 — extra layers were dropped.");
                    break;
                }
                sb.AppendLine($"        {name} = 1 << {bit},");
                bit++;
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Layer names parsed out of an enum source string (the <c>NAME = 1 &lt;&lt; bit</c> lines), excluding None.</summary>
        internal static string[] ParseNamesFromSource(string source)
        {
            if (string.IsNullOrEmpty(source)) return Array.Empty<string>();
            var names = new List<string>();
            foreach (Match m in Regex.Matches(source, @"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*1\s*<<\s*\d+", RegexOptions.Multiline))
            {
                string name = Sanitize(m.Groups[1].Value);
                if (!string.IsNullOrEmpty(name) && name != "NONE" && !names.Contains(name))
                    names.Add(name);
            }
            return names.ToArray();
        }

        /// <summary>Names currently compiled into <see cref="PopupLayerEnum"/> (the live projection), excluding None.</summary>
        internal static string[] CurrentEnumNames()
        {
            return Enum.GetNames(typeof(PopupLayerEnum)).Where(n => n != "None").ToArray();
        }

        #endregion

        #region Reconcile

        /// <summary>Resolves the enum file, then <see cref="Reconcile(string,bool)"/>. Safe to call from load hooks.</summary>
        internal static void Reconcile()
        {
            string path;
            try { path = FileSearcher.LayersEnumFilePath; }
            catch { return; }
            Reconcile(path, allowImport: true);
        }

        /// <summary>
        /// Reconciles the compiled enum file with the external store.
        /// <list type="bullet">
        /// <item>Store exists → the enum file is regenerated from it. This is what restores a consumer's layers after
        /// a package update overwrote the file with the shipped default (called from the pre-compile postprocessor).</item>
        /// <item>Store missing (fresh install / first run) → it is seeded from whatever names the enum file currently
        /// defines, or from <see cref="DefaultLayerNames"/> if the file is empty.</item>
        /// </list>
        /// Writes only when content actually differs, so it never loops or triggers a needless recompile.
        /// </summary>
        /// <param name="enumFsPath">Filesystem path of <c>PopupLayerEnum.generated.cs</c>.</param>
        /// <param name="allowImport">
        /// When false (inside an <c>AssetPostprocessor</c>), the file is written but not re-imported — the on-disk
        /// content is picked up by the compile that follows the import batch, avoiding postprocessor reentrancy.
        /// </param>
        internal static void Reconcile(string enumFsPath, bool allowImport)
        {
            if (SuppressReconcile || string.IsNullOrEmpty(enumFsPath)) return;

            string[] names = LoadNames();
            if (names == null)
            {
                // No store yet: seed it from the file's current names (fresh install / transition update).
                string[] seed = ParseNamesFromSource(SafeRead(enumFsPath));
                if (seed.Length == 0) seed = DefaultLayerNames;
                SaveNames(seed);
                names = seed;
            }

            string desired = GenerateEnumSource(names);
            string current = SafeRead(enumFsPath);
            if (current != null && NormalizeEol(current) == NormalizeEol(desired))
                return; // already in sync

            try
            {
                File.WriteAllText(enumFsPath, desired);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[APS] Failed to regenerate PopupLayerEnum: {ex.Message}");
                return;
            }

            if (allowImport)
            {
                try { AssetDatabase.ImportAsset(FileSearcher.ToAssetPath(enumFsPath)); }
                catch { /* path not under Assets or DB busy — the on-disk write still stands */ }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Uppercase, spaces/dashes → underscore, collapse repeats — mirrors the Layers panel validation. Allows digits
        /// but not as the first char (valid C# enum-member rule); must stay in sync with that panel's validator.
        /// </summary>
        internal static string Sanitize(string name)
        {
            if (name == null) return null;
            name = Regex.Replace(name, @"[\s-]+", "_");
            name = Regex.Replace(name, "_+", "_");
            name = name.ToUpperInvariant();
            return Regex.IsMatch(name, @"^[A-Z_][A-Z0-9_]*$") ? name : null;
        }

        private static string[] Normalize(IEnumerable<string> names)
        {
            var result = new List<string>();
            foreach (string n in names ?? Enumerable.Empty<string>())
            {
                string s = Sanitize(n);
                if (!string.IsNullOrEmpty(s) && s != "NONE" && !result.Contains(s))
                    result.Add(s);
            }
            return result.ToArray();
        }

        private static string SafeRead(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }

        private static string NormalizeEol(string s) => s?.Replace("\r\n", "\n");

        #endregion
    }
}
