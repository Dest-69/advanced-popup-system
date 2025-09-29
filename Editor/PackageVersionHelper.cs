using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

internal static class PackageVersionHelper
{
    private const string PackageName = "com.dest-69.advanced-popup-system";
    private static string _cached;

    public static string GetVersion()
    {
        if (!string.IsNullOrEmpty(_cached)) return _cached;

        // 1) Try resolve by script location
        var guids = AssetDatabase.FindAssets($"t:MonoScript {nameof(AdvancedPS.Editor.PopupSystemEditor)}");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var pi = PackageInfo.FindForAssetPath(path);
            if (pi != null)
            {
                // If running from Packages, name will match; if embedded in Assets as dev, still returns version if package exists
                if (pi.name == PackageName && !string.IsNullOrEmpty(pi.version))
                    return _cached = pi.version;
            }
        }

        // 2) Direct package path (installed as UPM)
        var piPkg = PackageInfo.FindForAssetPath(Path.Combine("Packages", PackageName));
        if (piPkg != null && !string.IsNullOrEmpty(piPkg.version))
            return _cached = piPkg.version;

        // 3) Fallback: try local package.json above the editor script
        var fallback = TryWalkForPackageJson(guids);
        if (!string.IsNullOrEmpty(fallback))
            return _cached = fallback;

        // 4) Last resort
        return _cached = "Dev";
    }

    private static string TryWalkForPackageJson(string[] scriptGuids)
    {
        foreach (var guid in scriptGuids)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var dir = Path.GetDirectoryName(p);
            while (!string.IsNullOrEmpty(dir))
            {
                var pj = Path.Combine(dir, "package.json");
                if (File.Exists(pj))
                {
                    var v = ReadVersionFromJson(pj, PackageName);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
        }
        return null;
    }

    private static string ReadVersionFromJson(string path, string mustName)
    {
        try
        {
            var json = File.ReadAllText(path);
            var obj = JsonUtility.FromJson<ApsVersion>(json);
            if (obj == null) return null;
            if (!string.IsNullOrEmpty(mustName) && obj.name != mustName) return null;
            return string.IsNullOrEmpty(obj.version) ? null : obj.version;
        }
        catch { return null; }
    }

    [System.Serializable]
    private class ApsVersion { public string name; public string version; }
}