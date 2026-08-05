#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using UnityEditor;
using UnityEngine;

namespace Game.Hot.Editor.Buqi
{
    /// <summary>
    /// Step 2 九法门战斗沙盒窗口。它只负责输入和展示，所有战斗结果仍由纯 C# 模拟器生成。
    /// </summary>
    internal sealed class BuqiBattleSandboxWindow : EditorWindow
    {
        private const int MaxDisplayedEvents = 500;

        private readonly BuqiSandboxLogFilter m_Filter = new BuqiSandboxLogFilter();
        private List<BuqiSandboxScenario> m_Scenarios = new List<BuqiSandboxScenario>();
        private BuqiSandboxRunResult m_RunResult;
        private BuqiSandboxRepeatResult m_RepeatResult;
        private Vector2 m_MainScroll;
        private Vector2 m_LogScroll;
        private int m_SelectedScenarioIndex;
        private string m_TickFilter = string.Empty;
        private string m_ChainFilter = string.Empty;
        private string m_SourceFilter = string.Empty;
        private string m_ReasonFilter = string.Empty;

        [MenuItem("Game/Buqi/Battle Sandbox", false, 200)]
        private static void Open()
        {
            BuqiBattleSandboxWindow window = GetWindow<BuqiBattleSandboxWindow>();
            window.titleContent = new GUIContent("不器战斗沙盒");
            window.minSize = new Vector2(920f, 680f);
            window.Show();
        }

        private void OnEnable()
        {
            m_Scenarios = BuqiBattleSandbox.CreateScenarios();
            m_SelectedScenarioIndex = Mathf.Clamp(m_SelectedScenarioIndex, 0, m_Scenarios.Count - 1);
        }

        private void OnGUI()
        {
            if (m_Scenarios.Count == 0)
            {
                EditorGUILayout.HelpBox("没有可用的 Step 2 沙盒场景。", MessageType.Error);
                return;
            }

            m_MainScroll = EditorGUILayout.BeginScrollView(m_MainScroll);
            DrawHeader();
            DrawScenarioSelector();
            DrawActions();
            DrawResult();
            DrawDefinitions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("《不器》九法门战斗沙盒", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "仅用于 Step 2 规则和日志验证，不进入正式玩家流程。内容定义将在 Step 3 由 Luban 替换。",
                MessageType.Info);
        }

        private void DrawScenarioSelector()
        {
            string[] names = new string[m_Scenarios.Count];
            for (int index = 0; index < m_Scenarios.Count; index++)
                names[index] = m_Scenarios[index].DisplayName;

            int selected = EditorGUILayout.Popup("验证场景", m_SelectedScenarioIndex, names);
            if (selected != m_SelectedScenarioIndex)
            {
                m_SelectedScenarioIndex = selected;
                m_RunResult = null;
                m_RepeatResult = null;
            }

            BuqiSandboxScenario scenario = m_Scenarios[m_SelectedScenarioIndex];
            EditorGUILayout.LabelField("目标", scenario.VerificationGoal, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("固定 seed", scenario.Request.BattleSeed.ToString());
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("运行一场", GUILayout.Height(28f)))
            {
                m_RunResult = BuqiBattleSandbox.Run(m_Scenarios[m_SelectedScenarioIndex]);
                m_RepeatResult = null;
            }
            if (GUILayout.Button("重复 100 次", GUILayout.Height(28f)))
            {
                BuqiSandboxScenario scenario = m_Scenarios[m_SelectedScenarioIndex];
                m_RunResult = BuqiBattleSandbox.Run(scenario);
                m_RepeatResult = BuqiBattleSandbox.Repeat(scenario, 100);
            }
            if (GUILayout.Button("清空结果", GUILayout.Height(28f)))
            {
                m_RunResult = null;
                m_RepeatResult = null;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawResult()
        {
            if (m_RunResult == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("战斗结果", EditorStyles.boldLabel);
            BattleResult result = m_RunResult.Result;
            EditorGUILayout.LabelField(
                "概览",
                BuqiText.Format(
                    "{0} / {1} tick / L:{2} R:{3}",
                    result.Outcome, result.DurationTicks, result.LeftExecution, result.RightExecution));
            EditorGUILayout.SelectableLabel(
                BuqiText.Format("hash: {0}", result.BattleLogHash),
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (m_RepeatResult != null)
            {
                MessageType type = m_RepeatResult.IsDeterministic ? MessageType.Info : MessageType.Error;
                string message = m_RepeatResult.IsDeterministic
                    ? BuqiText.Format(
                        "确定性通过：{0}/{1} 次完成，hash 全部一致。",
                        m_RepeatResult.CompletedRuns, m_RepeatResult.RequestedRuns)
                    : BuqiText.Format(
                        "确定性失败：第 {0} 次前出现 hash 漂移。",
                        m_RepeatResult.CompletedRuns);
                EditorGUILayout.HelpBox(message, type);
            }

            EditorGUILayout.LabelField("左侧 8 格", m_RunResult.LeftBoardText, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("右侧 8 格", m_RunResult.RightBoardText, EditorStyles.wordWrappedLabel);
            DrawLog();
        }

        private void DrawLog()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("日志过滤", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            m_TickFilter = EditorGUILayout.TextField("tick", m_TickFilter);
            m_ChainFilter = EditorGUILayout.TextField("chainId", m_ChainFilter);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            m_SourceFilter = EditorGUILayout.TextField("来源", m_SourceFilter);
            m_ReasonFilter = EditorGUILayout.TextField("reason", m_ReasonFilter);
            EditorGUILayout.EndHorizontal();

            m_Filter.Tick = ParseTick(m_TickFilter);
            m_Filter.ChainId = m_ChainFilter.Trim();
            m_Filter.SourceInstanceId = m_SourceFilter.Trim();
            m_Filter.ReasonCode = m_ReasonFilter.Trim();
            List<BattleEvent> filtered = BuqiBattleSandbox.FilterLog(m_RunResult.Log, m_Filter);
            EditorGUILayout.LabelField(
                "事件数",
                BuqiText.Format("{0} / {1}", filtered.Count, m_RunResult.Log.Count));

            m_LogScroll = EditorGUILayout.BeginScrollView(m_LogScroll, GUILayout.MinHeight(250f));
            int displayCount = Mathf.Min(filtered.Count, MaxDisplayedEvents);
            for (int index = 0; index < displayCount; index++)
            {
                BattleEvent battleEvent = filtered[index];
                EditorGUILayout.SelectableLabel(
                    FormatEvent(battleEvent),
                    EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            if (filtered.Count > displayCount)
            {
                EditorGUILayout.HelpBox(
                    BuqiText.Format("仅显示前 {0} 条，请继续缩小过滤范围。", MaxDisplayedEvents),
                    MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawDefinitions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("九法门临时定义", EditorStyles.boldLabel);
            foreach (KeyValuePair<string, BuqiSandboxItemInfo> pair in BuqiBattleSandbox.ItemInfos)
            {
                BuqiSandboxItemInfo info = pair.Value;
                string placeholder = info.UsesPlaceholderSemantics ? " [占位语义]" : string.Empty;
                EditorGUILayout.LabelField(
                    BuqiText.Format("{0} {1}{2}", info.DefinitionId, info.DisplayName, placeholder),
                    info.RuleSummary,
                    EditorStyles.wordWrappedLabel);
            }
        }

        private static int ParseTick(string value)
        {
            return int.TryParse(value, out int tick) && tick >= 0 ? tick : -1;
        }

        private static string FormatEvent(BattleEvent battleEvent)
        {
            string firstHalf = BuqiText.Format(
                "#{0} t{1} {2} d{3}",
                battleEvent.Sequence, battleEvent.Tick, battleEvent.Phase, battleEvent.ChainDepth);
            string secondHalf = BuqiText.Format(
                "src={0} target={1} amount={2}",
                battleEvent.SourceInstanceId, battleEvent.TargetInstanceId, battleEvent.Amount);
            string thirdHalf = BuqiText.Format(
                "chain={0} reason={1}",
                battleEvent.ChainId, battleEvent.ReasonCode);
            return BuqiText.Format("{0} | {1} | {2}", firstHalf, secondHalf, thirdHalf);
        }
    }
}
#endif
