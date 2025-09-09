using System.IO;
using UnityEditor;
using UnityEngine;
using System;

namespace AutoUI
{

    public class AutoUIFile : EditorWindow
    {
        // 保存一个gameobject为预制体，并返回其路径
        public static string SavePrefabAndCleanup(GameObject target)
        {
            string prefabPath = AutoUIConfig.config.Default.Prefab.Path + "/" + AutoUIConfig.config.Default.Prefab.Name;
            PrefabUtility.SaveAsPrefabAsset(target,prefabPath);
            AssetDatabase.Refresh();
            return prefabPath;
        }
        public static string SavePrefabAndCleanup(GameObject target, string name)
        {
            string prefabPath = AutoUIConfig.config.Default.Prefab.Path + "/" + name+".prefab";
            PrefabUtility.SaveAsPrefabAsset(target, prefabPath);
            AssetDatabase.Refresh();
            return prefabPath;
        }
        public static string SavePrefabAndConnect(GameObject target, string name)
        {
            string prefabPath = AutoUIConfig.config.Default.Prefab.Path + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(target, prefabPath, InteractionMode.AutomatedAction);
            AssetDatabase.Refresh();
            return prefabPath;
        }
        public static GameObject LoadPrefab(string prefabName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AutoUIConfig.config.Default.Prefab.Path + "/" + prefabName + ".prefab");
            if (prefab == null)
            {
                LogUtil.LogError("未找到名为" + prefabName + "的预制体");
                return null;
            }
            return prefab;
        }
        public static string SelectFolderPath()
        {
            // 打开文件夹选择对话框
            var selectedFolderPath = EditorUtility.OpenFolderPanel("选择包含JSON文件的文件夹", "", "");
            LogUtil.Log("选择了文件夹" + selectedFolderPath);
            if (string.IsNullOrEmpty(selectedFolderPath))
            {
                LogUtil.Log("用户取消选择文件夹");
                return "";
            }
            return selectedFolderPath;
        }

        /// <summary>
        /// 方法1：选择对应的JSON文件
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <returns>选择的JSON文件路径</returns>
        public static string SelectJsonFileFromFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                LogUtil.LogError("文件夹路径为空");
                return "";
            }

            // 获取文件夹中所有JSON文件
            string[] jsonFiles = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
            
            if (jsonFiles.Length == 0)
            {
                LogUtil.LogError($"在文件夹 {folderPath} 中未找到JSON文件");
                return "";
            }

            if (jsonFiles.Length == 1)
            {
                LogUtil.Log($"自动选择唯一的JSON文件: {Path.GetFileName(jsonFiles[0])}");
                return jsonFiles[0];
            }

            // 如果有多个JSON文件，显示选择对话框
            string[] fileNames = new string[jsonFiles.Length];
            for (int i = 0; i < jsonFiles.Length; i++)
            {
                fileNames[i] = Path.GetFileName(jsonFiles[i]);
            }

            int selectedIndex = EditorUtility.DisplayDialogComplex(
                "选择JSON文件",
                $"在文件夹中找到 {jsonFiles.Length} 个JSON文件，请选择要使用的文件：",
                "取消",
                "使用第一个",
                "手动选择"
            );

            switch (selectedIndex)
            {
                case 0: // 取消
                    return "";
                case 1: // 使用第一个
                    LogUtil.Log($"使用第一个JSON文件: {fileNames[0]}");
                    return jsonFiles[0];
                case 2: // 手动选择
                    return SelectJsonFileManually(jsonFiles, fileNames);
                default:
                    return "";
            }
        }

        /// <summary>
        /// 手动选择JSON文件
        /// </summary>
        private static string SelectJsonFileManually(string[] jsonFiles, string[] fileNames)
        {
            // 创建选择窗口
            var window = EditorWindow.GetWindow<JsonFileSelectorWindow>("选择JSON文件");
            window.Initialize(jsonFiles, fileNames);
            window.ShowModal();
            
            return window.SelectedFilePath;
        }
        public static bool IsJsonFileExist(string folderPath)
        {
            string jsonPath = folderPath + "/data.json";
            if (!File.Exists(jsonPath))
            {
                LogUtil.LogError("未找到data.json文件！" + jsonPath);
                return false;
            }
            else
            {
                return true;
            }
        }
        // 选择本地文件夹中的图片，如果没有选择，则返回空字符串
        public static string GUIChooseImagePath()
        {
            string path = EditorUtility.OpenFilePanel("选择图片", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }
            else{
                return "";
            }
        }

        /// <summary>
        /// 方法2：直接读取Design/UIJson目录下的JSON文件
        /// </summary>
        /// <returns>找到的JSON文件路径</returns>
        public static string[] GetJsonFilesFromRootDirectory()
        {
            // 使用配置文件中的路径
            string uiJsonPath = Path.Combine(Application.dataPath, AutoUIConfig.config.Default.Data.UIJsonPath);
            
            if (!Directory.Exists(uiJsonPath))
            {
                LogUtil.LogError($"UIJson文件夹不存在: {uiJsonPath}");
                return new string[0];
            }
            
            string[] jsonFiles = Directory.GetFiles(uiJsonPath, "*.json", SearchOption.TopDirectoryOnly);
            
            LogUtil.Log($"在Design/UIJson目录找到 {jsonFiles.Length} 个JSON文件");
            return jsonFiles;
        }

        /// <summary>
        /// 方法2：自动选择Design/UIJson目录下的JSON文件
        /// </summary>
        /// <returns>选择的JSON文件路径</returns>
        public static string SelectJsonFileFromRoot()
        {
            string[] jsonFiles = GetJsonFilesFromRootDirectory();
            
            if (jsonFiles.Length == 0)
            {
                LogUtil.LogError("在Design/UIJson目录未找到JSON文件");
                return "";
            }

            if (jsonFiles.Length == 1)
            {
                LogUtil.Log($"自动选择Design/UIJson目录唯一的JSON文件: {Path.GetFileName(jsonFiles[0])}");
                return jsonFiles[0];
            }

            // 如果有多个JSON文件，显示选择对话框
            string[] fileNames = new string[jsonFiles.Length];
            for (int i = 0; i < jsonFiles.Length; i++)
            {
                fileNames[i] = Path.GetFileName(jsonFiles[i]);
            }

            int selectedIndex = EditorUtility.DisplayDialogComplex(
                "选择Design/UIJson目录JSON文件",
                $"在Design/UIJson目录找到 {jsonFiles.Length} 个JSON文件，请选择要使用的文件：",
                "取消",
                "使用第一个",
                "手动选择"
            );

            switch (selectedIndex)
            {
                case 0: // 取消
                    return "";
                case 1: // 使用第一个
                    LogUtil.Log($"使用第一个JSON文件: {fileNames[0]}");
                    return jsonFiles[0];
                case 2: // 手动选择
                    return SelectJsonFileManually(jsonFiles, fileNames);
                default:
                    return "";
            }
        }
    }

    /// <summary>
    /// JSON文件选择器窗口
    /// </summary>
    public class JsonFileSelectorWindow : EditorWindow
    {
        private string[] jsonFiles;
        private string[] fileNames;
        private int selectedIndex = 0;
        private Vector2 scrollPosition;
        public string SelectedFilePath { get; private set; } = "";

        public void Initialize(string[] files, string[] names)
        {
            jsonFiles = files;
            fileNames = names;
            selectedIndex = 0;
        }

        private void OnGUI()
        {
            GUILayout.Label("选择JSON文件", EditorStyles.boldLabel);
            GUILayout.Space(10);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            
            for (int i = 0; i < fileNames.Length; i++)
            {
                bool isSelected = GUILayout.Toggle(i == selectedIndex, fileNames[i], "Button");
                if (isSelected && i != selectedIndex)
                {
                    selectedIndex = i;
                }
            }
            
            GUILayout.EndScrollView();
            
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("确定"))
            {
                if (selectedIndex >= 0 && selectedIndex < jsonFiles.Length)
                {
                    SelectedFilePath = jsonFiles[selectedIndex];
                    LogUtil.Log($"选择了JSON文件: {fileNames[selectedIndex]}");
                }
                Close();
            }
            
            if (GUILayout.Button("取消"))
            {
                SelectedFilePath = "";
                Close();
            }
            GUILayout.EndHorizontal();
        }
    }


}






