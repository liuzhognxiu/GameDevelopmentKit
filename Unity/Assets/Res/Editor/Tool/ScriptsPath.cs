using UnityEngine;  
using UnityEditor;  
using System.Collections.Generic;  
using System.IO;  
using System.Linq;  

/// <summary>  
/// 预制体脚本分析器 - 获取预制体上的所有脚本及其路径  
/// </summary>  
namespace Game  
{  
    public class PrefabScriptAnalyzer : EditorWindow  
    {  
        private GameObject prefabObject;  
        private Vector2 scrollPosition;  
        private List<ScriptInfo> scriptInfos = new List<ScriptInfo>();  
        private string searchFilter = "";  
        private bool showFullPaths = true;  
        private bool groupByScript = false;  
        private GUIStyle headerStyle;  
        private GUIStyle pathStyle;  

        [MenuItem("Tools/Prefab Script Analyzer")]  
        public static void ShowWindow()  
        {  
            GetWindow<PrefabScriptAnalyzer>("预制体脚本分析器");  
        }  

        private void OnEnable()  
        {  
            // 尝试从Selection获取预制体  
            if (Selection.activeGameObject != null)  
            {  
                if (PrefabUtility.IsPartOfPrefabAsset(Selection.activeGameObject) ||   
                    PrefabUtility.IsPartOfPrefabInstance(Selection.activeGameObject))  
                {  
                    prefabObject = Selection.activeGameObject;  
                    AnalyzePrefab();  
                }  
            }  
        }  

        private void OnGUI()  
        {  
            if (headerStyle == null)  
            {  
                InitStyles();  
            }  

            EditorGUILayout.Space(10);  
            EditorGUILayout.LabelField("预制体脚本分析器", headerStyle);  
            EditorGUILayout.Space(5);  
            EditorGUILayout.HelpBox("选择一个预制体，分析其中包含的所有脚本组件及路径", MessageType.Info);  
            EditorGUILayout.Space(10);  

            EditorGUI.BeginChangeCheck();  
            prefabObject = (GameObject)EditorGUILayout.ObjectField("选择预制体", prefabObject, typeof(GameObject), false);  
            if (EditorGUI.EndChangeCheck() && prefabObject != null)  
            {  
                AnalyzePrefab();  
            }  

            if (GUILayout.Button("分析预制体", GUILayout.Height(30)))  
            {  
                if (prefabObject != null)  
                {  
                    AnalyzePrefab();  
                }  
                else  
                {  
                    EditorUtility.DisplayDialog("提示", "请先选择一个预制体！", "确定");  
                }  
            }  

            EditorGUILayout.Space(5);  

            // 过滤选项  
            EditorGUILayout.BeginHorizontal();  
            searchFilter = EditorGUILayout.TextField("搜索", searchFilter);  
            if (GUILayout.Button("清除", GUILayout.Width(60)))  
            {  
                searchFilter = "";  
                GUI.FocusControl(null);  
            }  
            EditorGUILayout.EndHorizontal();  

            // 显示选项  
            EditorGUILayout.BeginHorizontal();  
            showFullPaths = EditorGUILayout.Toggle("显示完整路径", showFullPaths);  
            groupByScript = EditorGUILayout.Toggle("按脚本类型分组", groupByScript);  
            EditorGUILayout.EndHorizontal();  

            EditorGUILayout.Space(10);  

            if (scriptInfos.Count > 0)  
            {  
                // 脚本列表  
                EditorGUILayout.LabelField($"脚本列表 (共 {scriptInfos.Count} 个)", headerStyle);  
                EditorGUILayout.BeginHorizontal();  
                if (GUILayout.Button("复制所有路径", GUILayout.Width(120)))  
                {  
                    CopyAllPaths();  
                }  
                if (GUILayout.Button("导出为CSV", GUILayout.Width(120)))  
                {  
                    ExportToCsv();  
                }  
                EditorGUILayout.EndHorizontal();  
                
                EditorGUILayout.Space(5);  
                
                // 结果显示区域  
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);  

                if (groupByScript)  
                {  
                    DisplayGroupedScripts();  
                }  
                else  
                {  
                    DisplayScriptsList();  
                }  

                EditorGUILayout.EndScrollView();  
            }  
            else if (prefabObject != null)  
            {  
                EditorGUILayout.LabelField("该预制体上没有找到脚本组件。", EditorStyles.boldLabel);  
            }  
        }  

        private void DisplayScriptsList()  
        {  
            List<ScriptInfo> filteredScripts = FilterScripts();  
            
            for (int i = 0; i < filteredScripts.Count; i++)  
            {  
                ScriptInfo info = filteredScripts[i];  
                EditorGUILayout.BeginVertical(GUI.skin.box);  
                
                // 脚本基本信息  
                EditorGUILayout.BeginHorizontal();  
                EditorGUILayout.LabelField($"{i + 1}. {info.ScriptName}", EditorStyles.boldLabel);  
                
                if (GUILayout.Button("选择", GUILayout.Width(60)))  
                {  
                    Selection.activeObject = info.MonoScript;  
                    EditorGUIUtility.PingObject(info.MonoScript);  
                }  
                
                if (GUILayout.Button("复制路径", GUILayout.Width(80)))  
                {  
                    EditorGUIUtility.systemCopyBuffer = info.ScriptPath;  
                }  
                EditorGUILayout.EndHorizontal();  
                
                // 显示路径  
                EditorGUILayout.LabelField("路径:", pathStyle);  
                EditorGUILayout.SelectableLabel(info.ScriptPath, pathStyle, GUILayout.Height(20));  
                
                // 显示位置信息  
                EditorGUILayout.LabelField("附加到对象:", pathStyle);  
                EditorGUILayout.SelectableLabel(info.GameObjectPath, pathStyle, GUILayout.Height(20));  
                
                EditorGUILayout.EndVertical();  
                EditorGUILayout.Space(5);  
            }  
        }  
        
        private void DisplayGroupedScripts()  
        {  
            List<ScriptInfo> filteredScripts = FilterScripts();  
            
            // 按脚本名称分组  
            var groupedScripts = filteredScripts  
                .GroupBy(s => s.ScriptName)  
                .OrderBy(g => g.Key)  
                .ToList();  
                
            foreach (var group in groupedScripts)  
            {  
                EditorGUILayout.BeginVertical(GUI.skin.box);  
                
                // 脚本基本信息  
                EditorGUILayout.BeginHorizontal();  
                EditorGUILayout.LabelField($"{group.Key} ({group.Count()}个)", EditorStyles.boldLabel);  
                
                if (GUILayout.Button("选择", GUILayout.Width(60)))  
                {  
                    Selection.activeObject = group.First().MonoScript;  
                    EditorGUIUtility.PingObject(group.First().MonoScript);  
                }  
                
                if (GUILayout.Button("复制路径", GUILayout.Width(80)))  
                {  
                    EditorGUIUtility.systemCopyBuffer = group.First().ScriptPath;  
                }  
                EditorGUILayout.EndHorizontal();  
                
                // 显示路径  
                EditorGUILayout.LabelField("路径:", pathStyle);  
                EditorGUILayout.SelectableLabel(group.First().ScriptPath, pathStyle, GUILayout.Height(20));  
                
                // 显示位置信息  
                EditorGUILayout.LabelField("附加到的对象:", pathStyle);  
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);  
                foreach (var script in group)  
                {  
                    EditorGUILayout.SelectableLabel(script.GameObjectPath, GUILayout.Height(18));  
                }  
                EditorGUILayout.EndVertical();  
                
                EditorGUILayout.EndVertical();  
                EditorGUILayout.Space(5);  
            }  
        }  

        private List<ScriptInfo> FilterScripts()  
        {  
            if (string.IsNullOrEmpty(searchFilter))  
                return scriptInfos;  
                
            return scriptInfos.Where(s =>   
                s.ScriptName.ToLower().Contains(searchFilter.ToLower()) ||   
                s.ScriptPath.ToLower().Contains(searchFilter.ToLower()) ||  
                s.GameObjectPath.ToLower().Contains(searchFilter.ToLower())  
            ).ToList();  
        }  

        private void InitStyles()  
        {  
            headerStyle = new GUIStyle(EditorStyles.boldLabel);  
            headerStyle.fontSize = 14;  
            
            pathStyle = new GUIStyle(EditorStyles.label);  
            pathStyle.wordWrap = true;  
            pathStyle.richText = true;  
        }  

        private void AnalyzePrefab()  
        {  
            if (prefabObject == null) return;  
            
            scriptInfos.Clear();  
            
            // 获取预制体实例  
            GameObject prefabInstance = prefabObject;  
            
            // 如果是预制体实例，获取其根对象  
            if (PrefabUtility.IsPartOfPrefabInstance(prefabObject))  
            {  
                prefabInstance = PrefabUtility.GetOutermostPrefabInstanceRoot(prefabObject);  
            }  
            
            // 获取所有MonoBehaviour脚本  
            MonoBehaviour[] scripts = prefabInstance.GetComponentsInChildren<MonoBehaviour>(true);  
            
            foreach (MonoBehaviour script in scripts)  
            {  
                if (script == null) continue;  
                
                MonoScript monoScript = MonoScript.FromMonoBehaviour(script);  
                if (monoScript == null) continue;  
                
                string scriptPath = AssetDatabase.GetAssetPath(monoScript);  
                string scriptName = script.GetType().Name;  
                string gameObjectPath = GetGameObjectPath(script.gameObject, prefabInstance);  
                
                scriptInfos.Add(new ScriptInfo  
                {  
                    ScriptName = scriptName,  
                    ScriptPath = scriptPath,  
                    GameObjectPath = gameObjectPath,  
                    MonoScript = monoScript  
                });  
            }  
            
            scriptInfos = scriptInfos.OrderBy(s => s.ScriptName).ToList();  
        }  
        
        private string GetGameObjectPath(GameObject obj, GameObject root)  
        {  
            string path = obj.name;  
            Transform parent = obj.transform.parent;  
            
            while (parent != null && parent.gameObject != root.transform.parent?.gameObject)  
            {  
                path = parent.name + "/" + path;  
                parent = parent.parent;  
            }  
            
            return path;  
        }  
        
        private void CopyAllPaths()  
        {  
            if (scriptInfos.Count == 0) return;  
            
            List<ScriptInfo> filtered = FilterScripts();  
            string allPaths = string.Join("\n", filtered.Select(s => s.ScriptPath).Distinct());  
            EditorGUIUtility.systemCopyBuffer = allPaths;  
            
            EditorUtility.DisplayDialog("复制成功", $"已复制 {filtered.Select(s => s.ScriptPath).Distinct().Count()} 个脚本路径到剪贴板", "确定");  
        }  
        
        private void ExportToCsv()  
        {  
            if (scriptInfos.Count == 0) return;  
            
            string path = EditorUtility.SaveFilePanel("导出脚本列表", "", $"{prefabObject.name}_Scripts.csv", "csv");  
            if (string.IsNullOrEmpty(path)) return;  
            
            List<ScriptInfo> filtered = FilterScripts();  
            
            using (StreamWriter writer = new StreamWriter(path))  
            {  
                writer.WriteLine("Script Name,Script Path,GameObject Path");  
                
                foreach (var info in filtered)  
                {  
                    writer.WriteLine($"\"{info.ScriptName}\",\"{info.ScriptPath}\",\"{info.GameObjectPath}\"");  
                }  
            }  
            
            EditorUtility.RevealInFinder(path);  
        }  

        private class ScriptInfo  
        {  
            public string ScriptName { get; set; }  
            public string ScriptPath { get; set; }  
            public string GameObjectPath { get; set; }  
            public MonoScript MonoScript { get; set; }  
        }  
    }
}
