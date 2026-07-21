using System;
using System.IO;
using AdvancedPS.Core.System;
using Newtonsoft.Json;
using UnityEngine;

namespace AdvancedPS.Core.Utils
{
    public static class FileSearcher
    {
        private const string PackageFolderName = "advanced-popup-system";
        
        private const string DisplaysPath = "Runtime/Generated/Displays/";
        private const string LayersEnumPath = "Runtime/Generated/";
        private const string ImagesPath = "Runtime/Images/";

        private const string LayersEnumFileName = "PopupLayerEnum.generated.cs";

        // Minimal, COMPILABLE default used only as a safety net when the generated PopupLayerEnum file is missing (a
        // corrupted or partial import). Writing an empty file here would leave the project without the PopupLayerEnum
        // type — a hard compile error. The Layers tooling regenerates the real enum (from the external store) right
        // after. (The Addressable popup index is a data asset now — see AddressablePopupIndexAsset — so a missing file
        // is just an empty catalog, not a compile error, and needs no default here.)
        private const string DefaultLayersEnumContent =
            "using System;\n" +
            "namespace AdvancedPS.Core\n" +
            "{\n" +
            "    [Flags]\n" +
            "    public enum PopupLayerEnum\n" +
            "    {\n" +
            "        None = 0,\n" +
            "    }\n" +
            "}\n";

        public static readonly string ImagesFolderPath;
        public static readonly string DisplaysFolderPath;
        public static readonly string LayersEnumFilePath;

        static FileSearcher()
        {
            ImagesFolderPath = GetImagesFolderPathInternal().Replace(@"\", "/");
            DisplaysFolderPath = GetDisplaysFolderPathInternal().Replace(@"\", "/");
            LayersEnumFilePath = GetLayersEnumFilePathInternal().Replace(@"\", "/");
        }
        
        public static string ToAssetPath(string pathFs)
        {
            var p = pathFs.Replace("\\", "/");
            if (p.StartsWith("Assets/") || p == "Assets") return p;

            var data = Application.dataPath.Replace("\\", "/");
            if (!p.StartsWith(data))
                throw new Exception($"Path not under Assets: {p}");

            return "Assets" + p.Substring(data.Length);
        }

        public static string ToFsPath(string assetPath)
        {
            var p = assetPath.Replace("\\", "/");
            if (!p.StartsWith("Assets"))
                throw new Exception($"Not an asset path: {p}");
            return Path.Combine(Application.dataPath, p.Substring("Assets".Length)).Replace("\\", "/");
        }

        
        private static string GetImagesFolderPathInternal()
        {
            string folderPath = FindProtectedFolderPath();
            if (string.IsNullOrEmpty(folderPath))
            {
                throw new DirectoryNotFoundException($"The folder '{PackageFolderName}' was not found.");
            }

            string imagesFolderPath = Path.Combine(folderPath, ImagesPath);
            if (!Directory.Exists(imagesFolderPath))
            {
                Directory.CreateDirectory(imagesFolderPath);
            }

            return imagesFolderPath;
        }
        
        private static string GetDisplaysFolderPathInternal()
        {
            string folderPath = FindProtectedFolderPath();
            if (string.IsNullOrEmpty(folderPath))
            {
                throw new DirectoryNotFoundException($"The folder '{PackageFolderName}' was not found.");
            }

            string imagesFolderPath = Path.Combine(folderPath, DisplaysPath);
            if (!Directory.Exists(imagesFolderPath))
            {
                Directory.CreateDirectory(imagesFolderPath);
            }

            return imagesFolderPath;
        }
        
        private static string GetLayersEnumFilePathInternal()
        {
            string folderPath = FindProtectedFolderPath();
            if (string.IsNullOrEmpty(folderPath))
            {
                throw new DirectoryNotFoundException($"The folder '{PackageFolderName}' was not found.");
            }

            string layersEnumFolderPath = Path.Combine(folderPath, LayersEnumPath);
            if (!Directory.Exists(layersEnumFolderPath))
            {
                Directory.CreateDirectory(layersEnumFolderPath);
            }
            
            string settingsFilePath = Path.Combine(layersEnumFolderPath, LayersEnumFileName);
            if (!File.Exists(settingsFilePath))
            {
#if UNITY_EDITOR
                // Seed a compilable default (never an empty file — that removes the PopupLayerEnum type).
                File.WriteAllText(settingsFilePath, DefaultLayersEnumContent);
#endif
            }

            return settingsFilePath;
        }

        private static string FindProtectedFolderPath()
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Folder " + PackageFolderName);
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == PackageFolderName)
                {
                    return path;
                }
            }
            return null;
#else
            string path = Path.Combine(UnityEngine.Application.dataPath, PackageFolderName);
            if (Directory.Exists(path))
            {
                return path;
            }
            return null;
#endif
        }
        
        private static void GenerateBaseSettingsFile(string path)
        {
            var baseSettings = new PopupSettings
            {
                InspectorView = InspectorEnum.APSInspector,
                LogType = "Error"
            };

            File.WriteAllText(path, JsonConvert.SerializeObject(baseSettings));
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
    }
}