
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AutoUI
{
    class AutoUI : EditorWindow
    {

        // 选择的文件夹位置
        public static string selectedFolderPath = "../data.json";
        public static string selectedJsonPath;
        public static GameObject prefabGameObject;
        public static Layer layers;
        public static Dictionary<string, string> imageNameToSpritePath;

        [MenuItem("Tools/AutoUI")]
        public static void AutoUIMain()
        {
            AutoUIMainWithMode("folder");
        }

        [MenuItem("Tools/AutoUI - 从Design/UIJson选择JSON")]
        public static void AutoUIMainFromRoot()
        {
            AutoUIMainWithMode("root");
        }

        [MenuItem("Tools/AutoUI - 选择JSON文件")]
        public static void AutoUIMainSelectJson()
        {
            AutoUIMainWithMode("select");
        }

        /// <summary>
        /// 根据模式执行AutoUI主流程
        /// </summary>
        /// <param name="mode">选择模式：folder=文件夹选择，root=根目录选择，select=手动选择</param>
        public static void AutoUIMainWithMode(string mode)
        {
            LogUtil.Log("=== AutoUI start ===");
            try
            {
                LogUtil.ClearLogFile();
                AutoUIConfig.GetAutoUIConfigData();
                
                // 根据模式选择JSON文件
                selectedJsonPath = SelectJsonFileByMode(mode);
                if (string.IsNullOrEmpty(selectedJsonPath))
                {
                    LogUtil.LogError("解析终止，因为未选择有效的JSON文件。");
                    return;
                }
                
                imageNameToSpritePath = new Dictionary<string, string>();
            }
            catch (Exception err)
            {
                LogUtil.HandleAutoUIError(err);
                return;
            }
            LogUtil.Log("=== 开始解析json ===");
            try
            {
                string json = File.ReadAllText(selectedJsonPath);
                layers = LayerJsonParser.ParseFromJson(json);
                layers.VerifyLayers();
            }
            catch (Exception err)
            {
                LogUtil.HandleAutoUIError(err);
                return;
            }
            LogUtil.Log("=== 加载Sprite ===");
            try
            {
                AutoUIAssets.InitAssets(layers);
                LogUtil.Log("加载Sprite");
            }
            catch (Exception err)
            {
                LogUtil.HandleAutoUIError(err);
                return;
            }
            LogUtil.Log("=== 新建一个预制体 ===");
            try
            {
                prefabGameObject = AutoUIFrameworkProcessor.CreateCanvasWithData(layers);
                if (prefabGameObject == null)
                {
                    LogUtil.LogError("创建canvas失败");
                    return;
                }
                AutoUIFrameworkProcessor.ProcessAllLayers(in layers.layers, ref prefabGameObject);
                AutoUIFile.SavePrefabAndCleanup(prefabGameObject);
                LogUtil.Log("创建预制体成功");
            }
            catch (Exception err)
            {
                LogUtil.HandleAutoUIError(err);
                return;
            }
            
            LogUtil.Hint();
        }

        /// <summary>
        /// 根据模式选择JSON文件
        /// </summary>
        /// <param name="mode">选择模式</param>
        /// <returns>选择的JSON文件路径</returns>
        private static string SelectJsonFileByMode(string mode)
        {
            switch (mode.ToLower())
            {
                case "folder":
                    return SelectJsonFromFolder();
                case "root":
                    return SelectJsonFromRoot();
                case "select":
                    return SelectJsonManually();
                default:
                    LogUtil.LogError($"未知的选择模式: {mode}");
                    return "";
            }
        }

        /// <summary>
        /// 方法1：从文件夹选择JSON文件
        /// </summary>
        private static string SelectJsonFromFolder()
        {
            selectedFolderPath = AutoUIFile.SelectFolderPath();
            if (string.IsNullOrEmpty(selectedFolderPath))
            {
                LogUtil.LogError("未选择有效的文件夹路径");
                return "";
            }

            // 使用新的选择方法
            string jsonPath = AutoUIFile.SelectJsonFileFromFolder(selectedFolderPath);
            if (string.IsNullOrEmpty(jsonPath))
            {
                LogUtil.LogError("未选择有效的JSON文件");
                return "";
            }

            return jsonPath;
        }

        /// <summary>
        /// 方法2：从Design/UIJson目录选择JSON文件
        /// </summary>
        private static string SelectJsonFromRoot()
        {
            return AutoUIFile.SelectJsonFileFromRoot();
        }

        /// <summary>
        /// 方法3：手动选择JSON文件
        /// </summary>
        private static string SelectJsonManually()
        {
            string jsonPath = EditorUtility.OpenFilePanel("选择JSON文件", "", "json");
            if (string.IsNullOrEmpty(jsonPath))
            {
                LogUtil.LogError("未选择JSON文件");
                return "";
            }
            
            LogUtil.Log($"手动选择了JSON文件: {Path.GetFileName(jsonPath)}");
            return jsonPath;
        }

    }


}