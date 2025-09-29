using System.IO;
using UnityEditor;
using UnityEngine;

internal static class PackageVersionHelper
{
    private const string PackageName = "com.dest-69.advanced-popup-system";
    private static string _cached;
    private static bool _resolving;

    public static string GetVersion()
    {
        if (!string.IsNullOrEmpty(_cached)) return _cached;

        var upm = Path.Combine("Packages", PackageName, "package.json");
        if (File.Exists(upm))
            return _cached = ReadVersion(upm) ?? "Unknown";

        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !_resolving)
        {
            _resolving = true;
            EditorApplication.delayCall += () =>
            {
                _cached = ResolveViaAssetDatabase() ?? "Unknown";
                _resolving = false;
            };
        }

        return _cached ?? "Unknown";
    }

    private static string ResolveViaAssetDatabase()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:MonoScript PopupSystemEditor"))
        {
            var p  = AssetDatabase.GUIDToAssetPath(guid);
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(p);
            if (ms != null && ms.GetClass()?.FullName == "AdvancedPS.Editor.PopupSystemEditor")
            {
                var dir = Path.GetDirectoryName(p);
                while (!string.IsNullOrEmpty(dir))
                {
                    var pj = Path.Combine(dir, "package.json");
                    if (File.Exists(pj))
                    {
                        var v = ReadVersion(pj, PackageName);
                        if (v != null) return v;
                    }
                    var parent = Path.GetDirectoryName(dir);
                    if (parent == dir) break;
                    dir = parent;
                }
            }
        }
        return null;
    }

    private static string ReadVersion(string path, string mustName = null)
    {
        try
        {
            var json = File.ReadAllText(path);
            var obj  = JsonUtility.FromJson<ApsVersion>(json);
            if (obj == null) return null;
            if (!string.IsNullOrEmpty(mustName) && obj.name != mustName) return null;
            return obj.version;
        }
        catch { return null; }
    }

    [System.Serializable]
    private class ApsVersion { public string name; public string version; }
}