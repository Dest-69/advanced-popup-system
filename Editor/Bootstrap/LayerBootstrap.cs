#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AdvancedPS.Bootstrap
{
    /// <summary>
    /// First-install safety net for the consumer-side generated layers assembly.
    ///
    /// APS generates <c>PopupLayerEnum</c> into the CONSUMER project (<c>Assets/AdvancedPopupSystem/Generated/Layers</c>)
    /// and the read-only core package <b>references that assembly by name</b>. On a brand-new install the assembly does
    /// not exist yet, so core cannot compile — and the APS editor tooling (which depends on core) cannot run to create
    /// it. This assembly breaks that deadlock: it has <b>no dependency on APS core</b>, so Unity compiles and runs it
    /// even while core is red. It seeds the generated Layers assembly (asmdef + a compilable <c>PopupLayerEnum</c>) from
    /// the external layer store (<c>ProjectSettings/APS_Layers.json</c>) when present — so custom layers survive a fresh
    /// install / migration — else from a safe default. Idempotent: it does nothing once the enum file exists; from then
    /// on the normal Layers tooling (<c>LayerCatalog</c> / the heal postprocessor) owns the file.
    ///
    /// The asmdef body here is kept byte-for-byte in sync with <c>FileSearcher.LayersAsmdefContent</c> (this assembly
    /// cannot reference core to share it). Both only ever write when the file is absent, so they never fight.
    /// </summary>
    [InitializeOnLoad]
    internal static class LayerBootstrap
    {
        private const string GeneratedLayersDir = "Assets/AdvancedPopupSystem/Generated/Layers";
        private const string AsmdefName = "AdvancedPS.Generated.Layers";
        private const string EnumFileName = "PopupLayerEnum.generated.cs";
        private const string StoreRelPath = "ProjectSettings/APS_Layers.json";

        private static readonly string[] DefaultLayerNames = { "GUI", "GAME", "MENU", "OVERLAY" };

        private const string AsmdefContent =
            "{\n" +
            "    \"name\": \"AdvancedPS.Generated.Layers\",\n" +
            "    \"rootNamespace\": \"AdvancedPS.Core\",\n" +
            "    \"references\": [],\n" +
            "    \"includePlatforms\": [],\n" +
            "    \"excludePlatforms\": [],\n" +
            "    \"allowUnsafeCode\": false,\n" +
            "    \"overrideReferences\": false,\n" +
            "    \"precompiledReferences\": [],\n" +
            "    \"autoReferenced\": true,\n" +
            "    \"defineConstraints\": [],\n" +
            "    \"versionDefines\": [],\n" +
            "    \"noEngineReferences\": true\n" +
            "}\n";

        static LayerBootstrap()
        {
            // Defer until the AssetDatabase has settled after the domain reload.
            EditorApplication.delayCall += EnsureGeneratedLayers;
        }

        private static void EnsureGeneratedLayers()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)!.FullName.Replace('\\', '/');
                string dir = projectRoot + "/" + GeneratedLayersDir;
                string enumFile = dir + "/" + EnumFileName;
                if (File.Exists(enumFile)) return; // already seeded — the Layers tooling owns it from here.

                Directory.CreateDirectory(dir);

                string asmdef = dir + "/" + AsmdefName + ".asmdef";
                if (!File.Exists(asmdef)) File.WriteAllText(asmdef, AsmdefContent);

                string[] names = ReadStoreNames(projectRoot + "/" + StoreRelPath) ?? DefaultLayerNames;
                File.WriteAllText(enumFile, GenerateEnumSource(names));

                AssetDatabase.Refresh();
                Debug.Log("<color=green>[APS]</color> Seeded the generated layers assembly at " + GeneratedLayersDir +
                          " (first-install bootstrap).");
            }
            catch (Exception ex)
            {
                Debug.LogError("[APS] Layer bootstrap failed: " + ex.Message);
            }
        }

        [Serializable]
        private class LayerData { public string[] names; }

        private static string[] ReadStoreNames(string storeFs)
        {
            try
            {
                if (!File.Exists(storeFs)) return null;
                LayerData data = JsonUtility.FromJson<LayerData>(File.ReadAllText(storeFs));
                return data?.names != null && data.names.Length > 0 ? data.names : null;
            }
            catch { return null; }
        }

        /// <summary>Mirror of <c>LayerCatalog.GenerateEnumSource</c> (EOL-insensitive: the heal normalizes before comparing).</summary>
        private static string GenerateEnumSource(string[] names)
        {
            var sb = new StringBuilder();
            sb.Append("using System;\n");
            sb.Append("namespace AdvancedPS.Core\n{\n    [Flags]\n    public enum PopupLayerEnum\n    {\n");
            sb.Append("        None = 0,\n");
            int bit = 0;
            foreach (string raw in names)
            {
                string name = Sanitize(raw);
                if (string.IsNullOrEmpty(name) || name == "NONE") continue;
                if (bit >= 31) break;
                sb.Append("        ").Append(name).Append(" = 1 << ").Append(bit).Append(",\n");
                bit++;
            }
            sb.Append("    }\n}\n");
            return sb.ToString();
        }

        /// <summary>Minimal mirror of <c>LayerCatalog.Sanitize</c>: upper-case, spaces/dashes → single '_', no leading digit.</summary>
        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var sb = new StringBuilder(name.Length);
            foreach (char c in name.Trim())
            {
                if (c == ' ' || c == '-' || c == '_')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
                }
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(char.ToUpperInvariant(c));
                }
            }
            string s = sb.ToString().Trim('_');
            if (s.Length == 0) return null;
            return s[0] >= '0' && s[0] <= '9' ? null : s; // C# enum member can't start with a digit
        }
    }
}
#endif
