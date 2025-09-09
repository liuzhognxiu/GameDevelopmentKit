using System.IO;
using UnityEditor;
using UnityEngine;

namespace AutoUI
{
    /// <summary>
    /// 测试Design/UIJson目录功能的脚本
    /// </summary>
    public class 测试DesignUIJson功能 : EditorWindow
    {
        [MenuItem("Tools/AutoUI - 测试Design/UIJson功能")]
        public static void TestDesignUIJson()
        {
            LogUtil.Log("=== 开始测试Design/UIJson功能 ===");
            
            try
            {
                // 测试配置加载
                AutoUIConfig.GetAutoUIConfigData();
                LogUtil.Log($"配置加载成功，UIJson路径: {AutoUIConfig.config.Default.Data.UIJsonPath}");
                
                // 测试路径构建
                string uiJsonPath = Path.Combine(Application.dataPath, AutoUIConfig.config.Default.Data.UIJsonPath);
                LogUtil.Log($"构建的完整路径: {uiJsonPath}");
                
                // 测试目录是否存在
                if (Directory.Exists(uiJsonPath))
                {
                    LogUtil.Log("✅ Design/UIJson目录存在");
                    
                    // 测试JSON文件扫描
                    string[] jsonFiles = AutoUIFile.GetJsonFilesFromRootDirectory();
                    LogUtil.Log($"找到 {jsonFiles.Length} 个JSON文件:");
                    
                    for (int i = 0; i < jsonFiles.Length; i++)
                    {
                        LogUtil.Log($"  {i + 1}. {Path.GetFileName(jsonFiles[i])}");
                    }
                    
                    // 测试JSON文件选择
                    if (jsonFiles.Length > 0)
                    {
                        LogUtil.Log("测试JSON文件选择功能...");
                        string selectedFile = AutoUIFile.SelectJsonFileFromRoot();
                        
                        if (!string.IsNullOrEmpty(selectedFile))
                        {
                            LogUtil.Log($"✅ 成功选择JSON文件: {Path.GetFileName(selectedFile)}");
                        }
                        else
                        {
                            LogUtil.Log("❌ 未选择JSON文件");
                        }
                    }
                    else
                    {
                        LogUtil.Log("⚠️ 未找到JSON文件");
                    }
                }
                else
                {
                    LogUtil.LogError($"❌ Design/UIJson目录不存在: {uiJsonPath}");
                    LogUtil.LogError("请确保Design/UIJson目录存在并包含JSON文件");
                }
            }
            catch (System.Exception err)
            {
                LogUtil.LogError($"测试过程中发生错误: {err.Message}");
                LogUtil.LogError($"错误堆栈: {err.StackTrace}");
            }
            
            LogUtil.Log("=== Design/UIJson功能测试完成 ===");
        }
        
        [MenuItem("Tools/AutoUI - 创建测试JSON文件")]
        public static void CreateTestJsonFile()
        {
            try
            {
                // 确保Design/UIJson目录存在
                string uiJsonPath = Path.Combine(Application.dataPath, "../Design/UIJson");
                if (!Directory.Exists(uiJsonPath))
                {
                    Directory.CreateDirectory(uiJsonPath);
                    LogUtil.Log($"创建目录: {uiJsonPath}");
                }
                
                // 创建测试JSON文件
                string testJsonPath = Path.Combine(uiJsonPath, "test_ui.json");
                string testJsonContent = @"{
  ""name"": ""TestUI"",
  ""layerKind"": ""canvas"",
  ""visible"": true,
  ""opacity"": 1.0,
  ""rectTransform"": {
    ""anchor"": [{"": 0, ""y"": 0}, {""x"": 1, ""y"": 1}],
    ""pivot"": {""x"": 0.5, ""y"": 0.5},
    ""anchoredPosition"": {""x"": 0, ""y"": 0},
    ""sizeDelta"": {""x"": 0, ""y"": 0}
  },
  ""canvasLayerData"": {
    ""kind"": ""canvas"",
    ""width"": 1920,
    ""height"": 1080,
    ""renderMode"": ""overlay""
  },
  ""layers"": []
}";
                
                File.WriteAllText(testJsonPath, testJsonContent);
                LogUtil.Log($"✅ 创建测试JSON文件: {testJsonPath}");
                
                // 刷新资源
                AssetDatabase.Refresh();
            }
            catch (System.Exception err)
            {
                LogUtil.LogError($"创建测试文件时发生错误: {err.Message}");
            }
        }
    }
}
