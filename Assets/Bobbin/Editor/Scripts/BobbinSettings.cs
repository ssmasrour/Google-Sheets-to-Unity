using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Bobbin
{

    public enum FileType { txt, csv, json, xml, jpg, png, bytes }
    [System.Serializable]
    public class BobbinPath : TreeElement
    {
        public bool enabled = true;
        public FileType fileType;
        public string initUrl, url, filePath, lastFileHash, label, sheetId;
        public Object assetReference;

        public BobbinPath (string name, int depth, int id) : base (name, depth, id)
        {
    
        }

        public string GetSourceUrl()
        {
            if (!string.IsNullOrEmpty(initUrl))
            {
                return initUrl.Trim();
            }

            return string.IsNullOrEmpty(url) ? string.Empty : url.Trim();
        }

        public void SetSourceUrl(string value)
        {
            var normalizedValue = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
            initUrl = normalizedValue;
            url = normalizedValue;
        }
    }

    public class BobbinSettings : ScriptableObject
    {
        public List<BobbinPath> paths = new List<BobbinPath>();


        public bool autoRefresh = false;
        public double refreshInterval = 60.0;
        public int requestTimeoutSeconds = 30;

        const string SettingsFileName = "BobbinSettings.asset";
        const string BobbinFolderName = "Bobbin";
        const string EditorFolderName = "Editor";
        const string DefaultSettingsFilePath = "Assets/Bobbin/Editor/BobbinSettings.asset";

        #region Singleton Behaviour

        private static BobbinSettings instance;
        public static BobbinSettings Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                // attempt to get instance from disk.
                var possibleTempData = AssetDatabase.LoadAssetAtPath<BobbinSettings>(GetSettingsFilePath());
                if (possibleTempData != null)
                {
                    instance = possibleTempData;
                    instance.EnsureValidRoot();
                    return instance;
                }

                // no instance exists, create a new instance.
                instance = CreateInstance<BobbinSettings>();
                instance.EnsureValidRoot();
                EnsureSettingsFolderExists(GetSettingsFilePath());
                AssetDatabase.CreateAsset(instance, GetSettingsFilePath());
                AssetDatabase.SaveAssets();
                return instance;
            }
        }

        #endregion

        // pasted in from https://github.com/radiatoryang/merino/commit/6bbc24f4a50262b32d99c417f37e371eb8741ece ... thanks @charblar
        /// <summary>
        /// Returns the path of the Bobbin folder, based on the location of BobbinWindow.cs since that should always be in there.
        /// </summary>
        public static string LocateBobbinFolder()
        {
            string[] results = Directory.GetFiles(Application.dataPath, "BobbinCore.cs", SearchOption.AllDirectories);
            foreach (var result in results)
            {
                var parent = Directory.GetParent(result);
                while (parent != null)
                {
                    if (parent.Name == BobbinFolderName)
                    {
                        return parent.FullName;
                    }

                    parent = parent.Parent;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the path Bobbin settings data should live.
        /// </summary>
        public static string GetSettingsFilePath()
        {
            var bobbinFolder = LocateBobbinFolder();
            if (string.IsNullOrEmpty(bobbinFolder))
            {
                return DefaultSettingsFilePath;
            }

            var settingsPath = Path.Combine(bobbinFolder, EditorFolderName, SettingsFileName);
            var dataPath = NormalizePath(Application.dataPath);
            var normalizedSettingsPath = NormalizePath(settingsPath);

            if (!normalizedSettingsPath.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
            {
                return DefaultSettingsFilePath;
            }

            return "Assets" + normalizedSettingsPath.Substring(dataPath.Length);
        }

        public void EnsureValidRoot()
        {
            if (paths == null)
            {
                paths = new List<BobbinPath>();
            }

            if (paths.Count == 0 || paths[0] == null || paths[0].depth != -1)
            {
                paths.Insert(0, new BobbinPath("root", -1, 0));
            }

            paths[0].name = string.IsNullOrEmpty(paths[0].name) ? "root" : paths[0].name;
            paths[0].depth = -1;
        }

        static void EnsureSettingsFolderExists(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullAssetPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            var directoryPath = Path.GetDirectoryName(fullAssetPath);

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                AssetDatabase.Refresh();
            }
        }

        static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }

    }

}
