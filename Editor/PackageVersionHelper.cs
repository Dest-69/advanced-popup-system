using System.IO;
using AdvancedPS.Editor;
using UnityEditor;
using UnityEngine;

internal static class PackageVersionHelper
{
    private const string PackageName = "com.dest-69.advanced-popup-system";
    private static string _cached;

    public static string Version => _cached ??= Resolve();

    private static string Resolve()
    {
        var upm = Path.Combine("Packages", PackageName, "package.json");
        if (File.Exists(upm)) return ReadVersion(upm) ?? "Unknown";

        var script = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<PopupSystemEditor>());
        var path = AssetDatabase.GetAssetPath(script);
        var dir = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(dir))
        {
            var pj = Path.Combine(dir, "package.json");
            if (File.Exists(pj))
            {
                var v = ReadVersion(pj, mustMatchName: PackageName);
                if (v != null) return v;
            }
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return "Unknown";
    }

    private static string ReadVersion(string path, string mustMatchName = null)
    {
        try
        {
            var json = File.ReadAllText(path);
            var obj  = JsonUtility.FromJson<ApsVersion>(json);
            if (obj == null) return null;
            if (!string.IsNullOrEmpty(mustMatchName) && obj.name != mustMatchName) return null;
            return obj.version;
        }
        catch { return null; }
    }

    [System.Serializable] private class ApsVersion { public string name; public string version; }
}