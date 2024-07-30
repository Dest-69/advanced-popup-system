using System.IO;
using AdvancedPS.Core.System;
using Newtonsoft.Json;

namespace AdvancedPS.Core.Utils
{
    public static class FileSearcher
    {
        private const string PackageFolderName = "advanced-popup-system";
        
        private const string DisplaysPath = "Runtime/Generated/Displays/";
        private const string LayersEnumPath = "Runtime/Generated/";
        private const string SettingsPath = "Runtime/Settings/";
        private const string ImagesPath = "Runtime/Images/";
       
        private const string LayersEnumFileName = "PopupLayerEnum.generated.cs";
        private const string SettingsFileName = "AP_Settings.json";
       
        public static readonly string SettingsFilePath;
        public static readonly string ImagesFolderPath;
        public static readonly string DisplaysFolderPath;
        public static readonly string LayersEnumFilePath;

        static FileSearcher()
        {
            SettingsFilePath = GetSettingsFilePathInternal().Replace(@"\", "/");
            ImagesFolderPath = GetImagesFolderPathInternal().Replace(@"\", "/");
            DisplaysFolderPath = GetDisplaysFolderPathInternal().Replace(@"\", "/");
            LayersEnumFilePath = GetLayersEnumFilePathInternal().Replace(@"\", "/");
        }
        
        private static string GetSettingsFilePathInternal()
        {
            string folderPath = FindProtectedFolderPath();
            if (string.IsNullOrEmpty(folderPath))
            {
                throw new DirectoryNotFoundException($"The folder '{PackageFolderName}' was not found.");
            }

            string settingsFolderPath = Path.Combine(folderPath, SettingsPath);
            if (!Directory.Exists(settingsFolderPath))
            {
                Directory.CreateDirectory(settingsFolderPath);
            }

            string settingsFilePath = Path.Combine(settingsFolderPath, SettingsFileName);
            if (!File.Exists(settingsFilePath))
            {
                GenerateBaseSettingsFile(settingsFilePath);
            }

            return settingsFilePath;
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
                File.WriteAllText(settingsFilePath, "");
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
                CustomIconsEnabled = true,
                LogType = "Error"
            };

            File.WriteAllText(path, JsonConvert.SerializeObject(baseSettings));
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
    }
}