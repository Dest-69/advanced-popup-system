using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
// Alias: UnityEditor also has an unrelated UnityEditor.PackageInfo, so the bare name is ambiguous under `using UnityEditor`.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
#endif

namespace AdvancedPS.Core.Utils
{
    /// <summary>
    /// Resolves the paths APS codegen and editor tooling need, in a way that survives being installed as a
    /// <b>read-only UPM package</b> (Git URL / registry → <c>Library/PackageCache</c>) as well as an embedded/loose
    /// folder under <c>Assets/</c>.
    ///
    /// Two path families:
    /// <list type="bullet">
    /// <item><b>Package assets (read-only)</b> — images, built-in displays. Resolved against the package's own location
    /// via <see cref="PackageInfo.FindForAssembly"/> (with a folder-name fallback), so they work whether APS lives in
    /// <c>Packages/</c> or <c>Assets/</c>. Never written.</item>
    /// <item><b>Generated code (writable)</b> — <c>PopupLayerEnum</c> and custom displays. Always written to the
    /// <b>consumer project</b> under <c>Assets/AdvancedPopupSystem/Generated/</c>, never into the package — so a
    /// read-only install can still regenerate them and a package update never clobbers them.</item>
    /// </list>
    ///
    /// All accessors are <b>lazy and non-throwing</b>: a failure logs once and returns <c>null</c> (callers guard), so a
    /// bad moment never poisons the type with a <see cref="TypeInitializationException"/> — the old eager static
    /// constructor did exactly that under UPM, cascading into every icon site, the Displays panel, and the layer heal.
    /// </summary>
    public static class FileSearcher
    {
        private const string PackageFolderName = "advanced-popup-system";

        // Package-relative (read-only) sub-paths.
        private const string ImagesSubPath = "Runtime/Images/";
        private const string BuiltinDisplaysSubPath = "Runtime/Generated/Displays";
        // The layer enum ships INSIDE the package (its own assembly, AdvancedPS.Generated.Layers) so a fresh install
        // compiles with no code needing to run first — the previous "generate it into the consumer on first import"
        // approach deadlocked (core can't compile → the tooling that would generate it can't run). The heal regenerates
        // it in place when the package is writable (embedded / imported into Assets); on a read-only UPM install it
        // stays at the shipped default.
        private const string LayersSubPath = "Runtime/Generated/Layers";
        private const string LayersEnumFileName = "PopupLayerEnum.generated.cs";

        // Consumer-project (writable) CUSTOM-display code lives OUTSIDE the package. Displays carry no compile-time
        // dependency from core (they are found by reflection), so there is no fresh-install chicken-and-egg here — a
        // read-only install can still author them.
        private const string GeneratedRootUnderAssets = "AdvancedPopupSystem/Generated";
        private const string DisplaysSubfolder = "Displays";

        /// <summary>Name of the consumer-side assembly that holds user-authored custom displays (references core).</summary>
        internal const string DisplaysAsmdefName = "AdvancedPS.Generated.Displays";

        internal const string DisplaysAsmdefContent =
            "{\n" +
            "    \"name\": \"AdvancedPS.Generated.Displays\",\n" +
            "    \"rootNamespace\": \"AdvancedPS.Core\",\n" +
            "    \"references\": [\n" +
            "        \"dest-69.advanced-popup-system\",\n" +
            "        \"AdvancedPS.Generated.Layers\"\n" +
            "    ],\n" +
            "    \"includePlatforms\": [],\n" +
            "    \"excludePlatforms\": [],\n" +
            "    \"allowUnsafeCode\": false,\n" +
            "    \"overrideReferences\": false,\n" +
            "    \"precompiledReferences\": [],\n" +
            "    \"autoReferenced\": true,\n" +
            "    \"defineConstraints\": [],\n" +
            "    \"versionDefines\": [],\n" +
            "    \"noEngineReferences\": false\n" +
            "}\n";

        /// <summary>
        /// A single type that keeps the custom-displays assembly compilable while the folder holds no display yet. Unity
        /// reports a hard error for an <c>.asmdef</c> with no scripts, and this folder exists from the moment the Displays
        /// panel resolves its path — long before anyone generates a display into it.
        /// </summary>
        private const string DisplaysMarkerFileName = "AssemblyMarker.cs";

        private const string DisplaysMarkerContent =
            "// Keeps the AdvancedPS.Generated.Displays assembly compilable while it holds no custom display yet:\n" +
            "// Unity reports an error for an assembly definition with no scripts. APS creates this file next to the\n" +
            "// .asmdef and never touches it again — safe to delete once you have generated a display of your own.\n" +
            "namespace AdvancedPS.Core\n" +
            "{\n" +
            "    internal static class GeneratedDisplaysAssemblyMarker { }\n" +
            "}\n";

        private static bool _loggedFail;

        private static void LogFailOnce(string message)
        {
            if (_loggedFail) return;
            _loggedFail = true;
            Debug.LogError("[APS] " + message);
        }

        #region Package assets (read-only)

        /// <summary>
        /// Asset-database path (<c>Packages/…</c> or <c>Assets/…</c>) of the package's images folder, trailing '/'.
        /// Use with <see cref="AssetDatabase.LoadAssetAtPath"/> (which resolves <c>Packages/</c> paths). Null if the
        /// package could not be located.
        /// </summary>
        public static string ImagesFolderPath
        {
            get
            {
#if UNITY_EDITOR
                string root = PackageRootAssetPath();
                if (string.IsNullOrEmpty(root))
                {
                    LogFailOnce($"Could not locate the '{PackageFolderName}' package to resolve image paths.");
                    return null;
                }
                return root.TrimEnd('/') + "/" + ImagesSubPath;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Real filesystem path of the package root, no trailing '/'. Resolves for every shape APS can take — UPM
        /// (<c>Packages/</c> or the read-only cache) and a loose folder under <c>Assets/</c>, where there is no
        /// <see cref="PackageInfo"/> to ask. Null if it could not be located.
        /// </summary>
        public static string PackageRootPath
        {
            get
            {
#if UNITY_EDITOR
                return PackageRootFsPath();
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Real filesystem path of the package's <b>built-in</b> displays folder (Fade/Scale/Slide/DoTween), for
        /// listing them read-only. Null if the package could not be located.
        /// </summary>
        public static string BuiltinDisplaysFolderPath
        {
            get
            {
#if UNITY_EDITOR
                string root = PackageRootFsPath();
                return string.IsNullOrEmpty(root) ? null : root.TrimEnd('/') + "/" + BuiltinDisplaysSubPath;
#else
                return null;
#endif
            }
        }

        #endregion

        #region Generated code (writable, consumer project)

        /// <summary>Absolute FS path of the consumer generated root (<c>&lt;project&gt;/Assets/AdvancedPopupSystem/Generated</c>).</summary>
        private static string GeneratedRootFs =>
            (Application.dataPath + "/" + GeneratedRootUnderAssets).Replace('\\', '/');

        /// <summary>
        /// Real FS path of the shipped <c>PopupLayerEnum.generated.cs</c> (inside the package). The heal regenerates it
        /// in place when the package is writable; under a read-only UPM install a write fails and the shipped default
        /// stands. Null if the package could not be located.
        /// </summary>
        public static string LayersEnumFilePath
        {
            get
            {
#if UNITY_EDITOR
                string root = PackageRootFsPath();
                if (string.IsNullOrEmpty(root))
                {
                    LogFailOnce($"Could not locate the '{PackageFolderName}' package to resolve the layer enum path.");
                    return null;
                }
                return root.TrimEnd('/') + "/" + LayersSubPath + "/" + LayersEnumFileName;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// Absolute FS path of the consumer-side custom-displays folder. Ensures the folder, the
        /// <see cref="DisplaysAsmdefName"/> asmdef and its marker type exist. Null on IO failure.
        /// </summary>
        public static string CustomDisplaysFolderPath
        {
            get
            {
#if UNITY_EDITOR
                try
                {
                    string dir = GeneratedRootFs + "/" + DisplaysSubfolder;
                    Directory.CreateDirectory(dir);
                    EnsureFile(dir + "/" + DisplaysAsmdefName + ".asmdef", DisplaysAsmdefContent);
                    // The asmdef must never stand alone: an assembly definition with no scripts is a compile error, and
                    // this folder stays empty until the user generates their first display (see DisplaysMarkerContent).
                    EnsureFile(dir + "/" + DisplaysMarkerFileName, DisplaysMarkerContent);
                    return dir;
                }
                catch (Exception ex)
                {
                    LogFailOnce($"Failed to prepare the generated Displays folder: {ex.Message}");
                    return null;
                }
#else
                return null;
#endif
            }
        }

        #endregion

        #region Path conversion

        /// <summary>Filesystem path → asset-database path. Handles the consumer <c>Assets/</c> tree and the package (<c>Packages/…</c> under UPM).</summary>
        public static string ToAssetPath(string pathFs)
        {
            var p = pathFs.Replace('\\', '/');
            if (p == "Assets" || p.StartsWith("Assets/") || p.StartsWith("Packages/")) return p;

            var data = Application.dataPath.Replace('\\', '/');
            if (p == data) return "Assets";
            if (p.StartsWith(data + "/")) return "Assets" + p.Substring(data.Length);

#if UNITY_EDITOR
            // Real UPM install: files live under Library/PackageCache — map the resolved FS path back to Packages/<name>.
            PackageInfo pkg = Pkg;
            if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath) && !string.IsNullOrEmpty(pkg.assetPath))
            {
                var resolved = pkg.resolvedPath.Replace('\\', '/').TrimEnd('/');
                if (p == resolved) return pkg.assetPath.TrimEnd('/');
                if (p.StartsWith(resolved + "/")) return pkg.assetPath.TrimEnd('/') + p.Substring(resolved.Length);
            }
#endif
            throw new Exception($"Path is not under Assets or the APS package: {p}");
        }

        /// <summary>Asset-database path → filesystem path. Handles the consumer <c>Assets/</c> tree and the package (<c>Packages/…</c> under UPM).</summary>
        public static string ToFsPath(string assetPath)
        {
            var p = assetPath.Replace('\\', '/');
            if (p == "Assets") return Application.dataPath.Replace('\\', '/');
            if (p.StartsWith("Assets/")) return (Application.dataPath + p.Substring("Assets".Length)).Replace('\\', '/');

#if UNITY_EDITOR
            if (p == "Packages" || p.StartsWith("Packages/"))
            {
                PackageInfo pkg = Pkg;
                if (pkg != null && !string.IsNullOrEmpty(pkg.assetPath) && !string.IsNullOrEmpty(pkg.resolvedPath))
                {
                    var a = pkg.assetPath.TrimEnd('/');
                    var r = pkg.resolvedPath.Replace('\\', '/').TrimEnd('/');
                    if (p == a) return r;
                    if (p.StartsWith(a + "/")) return (r + p.Substring(a.Length)).Replace('\\', '/');
                }
            }
#endif
            throw new Exception($"Not an asset path under Assets or the APS package: {p}");
        }

        #endregion

        #region Package location

#if UNITY_EDITOR
        private static PackageInfo _pkg;
        private static bool _pkgResolved;

        /// <summary>The UPM package this assembly belongs to, or null when APS is a loose/embedded folder under Assets.</summary>
        private static PackageInfo Pkg
        {
            get
            {
                if (!_pkgResolved)
                {
                    try { _pkg = PackageInfo.FindForAssembly(typeof(FileSearcher).Assembly); }
                    catch { _pkg = null; }
                    _pkgResolved = true;
                }
                return _pkg;
            }
        }

        /// <summary>
        /// True when the package can be written to (so layers can be regenerated in place): a loose folder under
        /// <c>Assets/</c>, or an Embedded/Local UPM package. A Git/registry install lives read-only in
        /// <c>Library/PackageCache</c> — <see cref="EmbedPackage"/> makes it writable.
        /// </summary>
        public static bool IsPackageWritable
        {
            get
            {
                PackageInfo pkg = Pkg;
                if (pkg == null) return true; // loose in Assets
                return pkg.source == UnityEditor.PackageManager.PackageSource.Embedded
                    || pkg.source == UnityEditor.PackageManager.PackageSource.Local;
            }
        }

        /// <summary>The package's UPM name, or null when APS is a loose folder under Assets.</summary>
        public static string PackageName => Pkg?.name;

        /// <summary>
        /// Embeds the package (copies it from the read-only cache into <c>Packages/</c>, writable) so layers can be
        /// edited. No-op when APS is loose in Assets. Unity recompiles afterwards; editing is available once it settles.
        /// </summary>
        public static void EmbedPackage()
        {
            PackageInfo pkg = Pkg;
            if (pkg != null && !string.IsNullOrEmpty(pkg.name))
                UnityEditor.PackageManager.Client.Embed(pkg.name);
        }

        /// <summary>Asset-database path of the package root (<c>Packages/…</c> or <c>Assets/…</c>), no trailing '/'. Null if unresolved.</summary>
        private static string PackageRootAssetPath()
        {
            PackageInfo pkg = Pkg;
            if (pkg != null && !string.IsNullOrEmpty(pkg.assetPath)) return pkg.assetPath.TrimEnd('/');

            // Fallback: a loose folder under Assets/ (imported .unitypackage), possibly present before UPM metadata.
            foreach (string guid in AssetDatabase.FindAssets("t:Folder " + PackageFolderName))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == PackageFolderName) return path;
            }
            return null;
        }

        /// <summary>Real filesystem path of the package root, no trailing '/'. Null if unresolved.</summary>
        private static string PackageRootFsPath()
        {
            PackageInfo pkg = Pkg;
            if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
                return pkg.resolvedPath.Replace('\\', '/').TrimEnd('/');

            string asset = PackageRootAssetPath();
            if (string.IsNullOrEmpty(asset)) return null;
            if (asset == "Assets") return Application.dataPath.Replace('\\', '/');
            if (asset.StartsWith("Assets/"))
                return (Application.dataPath + asset.Substring("Assets".Length)).Replace('\\', '/');
            return null;
        }

        private static void EnsureFile(string fsPath, string content)
        {
            if (!File.Exists(fsPath)) File.WriteAllText(fsPath, content);
        }
#endif

        #endregion
    }
}
