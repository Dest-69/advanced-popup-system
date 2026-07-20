using System.Collections.Generic;
using System.IO;
using System.Text;
using AdvancedPS.Core;
using AdvancedPS.Core.System;
using AdvancedPS.Core.Utils;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AdvancedPS.Editor
{
    /// <summary>
    /// Editor tooling for the Addressable popup index. Scans popup prefabs flagged <c>Addressable</c>, keeps them in a
    /// dedicated Addressables group, and regenerates <c>Runtime/Generated/AddressablePopupIndex.generated.cs</c> — the
    /// catalog the runtime reads to load popups on demand. Compiled only under <c>APS_ADDRESSABLES</c>. Triggered from
    /// the menu and automatically when a popup prefab changes (see <see cref="AddressablePopupPostprocessor"/>).
    /// </summary>
    public static class AddressablePopupIndexGenerator
    {
        private const string GroupName = "Advanced Popup System";

        /// <summary> Guards against re-entrancy while the generator rewrites assets. </summary>
        internal static bool IsGenerating;

        [MenuItem("Tools/Advanced Popup System/Regenerate Addressable Index")]
        public static void Regenerate()
        {
            if (IsGenerating) return;
            IsGenerating = true;
            try
            {
                AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                if (settings == null)
                {
                    APLogger.LogError("<color=red>[APS Addressables]</color> Could not obtain AddressableAssetSettings.");
                    return;
                }

                AddressableAssetGroup group = GetOrCreateGroup(settings);

                var entries = new List<PopupEntryData>();
                var keptGuids = new HashSet<string>();
                var seenTypeNames = new HashSet<string>();

                foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;

                    var popup = go.GetComponent<IAdvancedPopup>();
                    if (popup == null || !popup.Addressable) continue;

                    string typeName = popup.GetType().FullName;
                    // The index is keyed by type full name — two Addressable prefabs of the same type would collide.
                    // Give each Addressable popup a distinct AdvancedPopup subclass; skip (and warn about) duplicates.
                    if (!seenTypeNames.Add(typeName))
                    {
                        APLogger.LogWarning($"<color=orange>[APS Addressables]</color> Duplicate Addressable popup type '{typeName}' at '{path}' — each Addressable popup needs a distinct AdvancedPopup subclass. Skipping this one.");
                        continue;
                    }
                    // Deterministic address = type full name (survives prefab moves; unique per popup type).
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    entry.SetAddress(typeName, false);
                    keptGuids.Add(guid);

                    entries.Add(new PopupEntryData
                    {
                        TypeName = typeName,
                        Address = typeName,
                        Layer = popup.PopupLayer,
                        LoadMode = popup.AddressableLoadMode,
                        HideBehavior = popup.AddressableHideBehavior
                    });
                }

                // Drop popups that were un-flagged (still in our group but no longer Addressable). Collect first —
                // RemoveAssetEntry mutates group.entries.
                var stale = new List<AddressableAssetEntry>();
                foreach (AddressableAssetEntry e in group.entries)
                    if (!keptGuids.Contains(e.guid))
                        stale.Add(e);
                foreach (AddressableAssetEntry e in stale)
                    settings.RemoveAssetEntry(e.guid, false);

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);

                WriteIndexFile(entries);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings)
        {
            AddressableAssetGroup group = settings.FindGroup(GroupName);
            if (group == null)
            {
                group = settings.CreateGroup(GroupName, false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }
            return group;
        }

        private static void WriteIndexFile(List<PopupEntryData> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ------------------------------------------------------------------------------------------------");
            sb.AppendLine("// GENERATED FILE — do not hand-edit. Regenerated by AddressablePopupIndexGenerator (APS editor).");
            sb.AppendLine("// Maps each Addressable-flagged popup to its Addressables key + load settings. Keyed by type full");
            sb.AppendLine("// name (string): user popups live in the consumer assembly, which the core cannot reference.");
            sb.AppendLine("// ------------------------------------------------------------------------------------------------");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace AdvancedPS.Core");
            sb.AppendLine("{");
            sb.AppendLine("    internal static partial class AddressablePopupIndex");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// One Addressable popup: how to load it and where it belongs. <see cref=\"TypeName\"/> is the popup");
            sb.AppendLine("        /// component's <c>Type.FullName</c> — matched against live popups for the scene-wins dedup.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        internal readonly struct Entry");
            sb.AppendLine("        {");
            sb.AppendLine("            public readonly string TypeName;");
            sb.AppendLine("            public readonly string Address;");
            sb.AppendLine("            public readonly PopupLayerEnum Layer;");
            sb.AppendLine("            public readonly LoadMode LoadMode;");
            sb.AppendLine("            public readonly HideBehavior HideBehavior;");
            sb.AppendLine();
            sb.AppendLine("            public Entry(string typeName, string address, PopupLayerEnum layer, LoadMode loadMode, HideBehavior hideBehavior)");
            sb.AppendLine("            {");
            sb.AppendLine("                TypeName = typeName;");
            sb.AppendLine("                Address = address;");
            sb.AppendLine("                Layer = layer;");
            sb.AppendLine("                LoadMode = loadMode;");
            sb.AppendLine("                HideBehavior = hideBehavior;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            if (entries.Count == 0)
            {
                sb.AppendLine("        internal static readonly Entry[] Entries = Array.Empty<Entry>();");
            }
            else
            {
                sb.AppendLine("        internal static readonly Entry[] Entries =");
                sb.AppendLine("        {");
                foreach (PopupEntryData e in entries)
                {
                    sb.AppendLine($"            new Entry(\"{e.TypeName}\", \"{e.Address}\", " +
                                  $"(PopupLayerEnum){(int)e.Layer}, LoadMode.{e.LoadMode}, HideBehavior.{e.HideBehavior}),");
                }
                sb.AppendLine("        };");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string content = sb.ToString().Replace("\r\n", "\n");
            string path = FileSearcher.AddressableIndexFilePath;

            // Idempotent: only rewrite (and trigger a recompile) when the content actually changed.
            if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == content)
                return;

            File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(FileSearcher.ToAssetPath(path));
            APLogger.Log($"<color=green>[APS Addressables]</color> Regenerated popup index ({entries.Count} popup(s)).");
        }

        private struct PopupEntryData
        {
            public string TypeName;
            public string Address;
            public PopupLayerEnum Layer;
            public LoadMode LoadMode;
            public HideBehavior HideBehavior;
        }
    }
}
