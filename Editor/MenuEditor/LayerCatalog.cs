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

        /// <summary>
        /// Outcome of a store read. The distinction between <see cref="Missing"/> and <see cref="Unreadable"/> is
        /// load-bearing: a read hiccup (locked / mid-write / corrupt) must NOT be mistaken for "no store", or the heal
        /// would overwrite a real layer set with defaults. See <see cref="Reconcile(string,bool)"/>.
        /// </summary>
        private enum StoreState { Missing, Ok, Unreadable }

        /// <summary>Reads the ordered layer names from the store, or null when it is missing or unreadable.</summary>
        internal static string[] LoadNames()
        {
            return TryLoadNames(out string[] names) == StoreState.Ok ? names : null;
        }

        /// <summary>
        /// Reads the layer names, transparently recovering from the <c>.bak</c> sidecar when the live store is
        /// missing/corrupt but a good backup exists (and re-materializing the live store from it).
        /// </summary>
        private static StoreState TryLoadNames(out string[] names)
        {
            string path = StorePath;
            bool mainExists = File.Exists(path);

            if (mainExists && TryParse(path, out names))
                return StoreState.Ok;

            // Live store is missing or unreadable — fall back to the last-good backup.
            string bak = path + ".bak";
            if (File.Exists(bak) && TryParse(bak, out names))
            {
                Debug.LogWarning($"[APS] {StoreFileName} was {(mainExists ? "unreadable" : "missing")}; " +
                                 "recovered the layer set from its .bak backup.");
                WriteAtomic(names); // heal the live store so later reads are clean
                return StoreState.Ok;
            }

            names = null;
            return mainExists ? StoreState.Unreadable : StoreState.Missing;
        }

        private static bool TryParse(string file, out string[] names)
        {
            names = null;
            try
            {
                string text = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(text)) return false;
                LayerData data = JsonUtility.FromJson<LayerData>(text);
                if (data?.names == null) return false;
                names = Normalize(data.names);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Writes the ordered, sanitized layer names to the store (source of truth, outside the package).</summary>
        internal static void SaveNames(IEnumerable<string> names)
        {
            WriteAtomic(Normalize(names));
        }

        /// <summary>
        /// Crash-safe store write: serialize to a temp file, then atomically promote it, keeping the previous good copy
        /// as <c>.bak</c>. A killed editor mid-write can no longer truncate the store (the old file stays intact until
        /// the atomic swap), and the <c>.bak</c> gives <see cref="TryLoadNames"/> something to recover from.
        /// </summary>
        private static void WriteAtomic(string[] names)
        {
            string path = StorePath;
            try
            {
                string json = JsonUtility.ToJson(new LayerData { names = names }, true);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(path))
                {
                    string bak = path + ".bak";
                    try
                    {
                        File.Replace(tmp, path, bak); // atomic where supported: path→bak, tmp→path
                    }
                    catch
                    {
                        // Some filesystems / AV shields reject File.Replace — fall back to a move sequence.
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Move(path, bak);
                        File.Move(tmp, path);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                }
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

            StoreState state = TryLoadNames(out string[] names);
            if (state == StoreState.Unreadable)
            {
                // The store exists but could not be read (locked / mid-write / corrupt) and no usable .bak. Seeding
                // defaults here would destroy the real layer set — the exact corruption we must never cause. Leave the
                // enum untouched and heal on a later reload once the file is readable again.
                Debug.LogError($"[APS] {StoreFileName} is unreadable — skipping the layer heal so it is not overwritten. " +
                               "Restore or delete the file (a .bak may sit next to it) and reopen the editor.");
                return;
            }
            if (state == StoreState.Missing)
            {
                // No store yet (fresh install / transition update): seed it from the file's current names, else defaults.
                string[] seed = ParseNamesFromSource(SafeRead(enumFsPath));
                if (seed.Length == 0) seed = DefaultLayerNames;
                SaveNames(seed);
                names = seed;
            }

            string desired = GenerateEnumSource(names);
            string current = SafeRead(enumFsPath);
            if (current != null && NormalizeEol(current) == NormalizeEol(desired))
                return; // already in sync

            // A Git/registry install lives in Library/PackageCache, which Unity treats as IMMUTABLE: writing there
            // succeeds at the filesystem level and then raises "asset(s) located in immutable packages were unexpectedly
            // altered", and Package Manager may drop the change at any time. So do not write — say what is wrong instead.
            if (!FileSearcher.IsPackageWritable)
            {
                HealUnwritable(names, enumFsPath);
                return;
            }

            try
            {
                File.WriteAllText(enumFsPath, desired);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[APS] Failed to regenerate PopupLayerEnum at '{enumFsPath}': {ex.Message}");
                return;
            }

            if (allowImport)
            {
                try { AssetDatabase.ImportAsset(FileSearcher.ToAssetPath(enumFsPath)); }
                catch { /* path not under Assets or DB busy — the on-disk write still stands */ }
            }
        }

        /// <summary>
        /// The store has layers the compiled enum lacks, and the package is read-only — a Git/registry install lives in
        /// <c>Library/PackageCache</c>, which Unity treats as immutable (writing there raises "assets in immutable
        /// packages were unexpectedly altered" and Package Manager drops the change on the next resolve, so a patch there
        /// is not a fix). The only thing that actually restores them is a writable copy, so <b>embed one and say so</b> —
        /// no prompt: the project does not compile in this state, and customized layers already imply an embedded
        /// install. Unity recompiles once, the next heal writes the enum, and the layers are back.
        /// <para>
        /// Guarded four ways: only when the enum is genuinely <b>missing</b> names (a reorder or a removal leaves the
        /// consumer's code compiling and is not worth changing their install for); not while an embed is already in
        /// flight; once per session, so a failing embed cannot loop; and never right after the user handed the copy back
        /// themselves (<see cref="PackageUpdater.DetachedThisSession"/> — that dialog already warned about this exact
        /// fallback). In batch mode it only logs: CI must not rewrite the project's <c>Packages/</c>.
        /// </para>
        /// </summary>
        private static void HealUnwritable(string[] names, string enumFsPath)
        {
            var compiled = new HashSet<string>(ParseNamesFromSource(SafeRead(enumFsPath)));
            string[] missing = names.Select(Sanitize)
                                    .Where(n => !string.IsNullOrEmpty(n) && !compiled.Contains(n))
                                    .ToArray();
            if (missing.Length == 0) return;

            const string triedKey = "APS_LayerHealEmbedTried";
            if (SessionState.GetBool(triedKey, false) || PackageUpdater.EmbedInProgress) return;
            SessionState.SetBool(triedKey, true);

            string list = string.Join(", ", missing);
            if (PackageUpdater.DetachedThisSession || Application.isBatchMode)
            {
                Debug.LogError($"[APS] Layers {list} are in {StoreFileName} but not in the compiled PopupLayerEnum, and " +
                               "APS is installed read-only so they cannot be regenerated. Code using them will not build — " +
                               "enable Customization in APS ▸ Layers to embed a writable copy.");
                return;
            }

            Debug.Log($"[APS] Restoring your layers ({list}) after the update: APS is installed read-only, so it is " +
                      "embedding a writable copy of itself. Unity recompiles once and the layers come back from " +
                      $"{StoreFileName} — nothing else changes.");
            PackageUpdater.BeginEmbed();
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
