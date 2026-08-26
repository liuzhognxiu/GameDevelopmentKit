#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Game.Hot.Buqi.Battle;
using UnityEditor;
using UnityEngine;

namespace Game.Hot.Editor.Buqi
{
    /// <summary>
    /// 九法门战斗沙盒窗口。它只负责 P-1 记录、调试输入和展示，所有战斗结果仍由纯 C# 模拟器生成。
    /// </summary>
    internal sealed class BuqiBattleSandboxWindow : EditorWindow
    {
        private const int MaxDisplayedEvents = 500;
        private const string P1SessionStateKey = "Game.Hot.Buqi.P1.ActiveBatch.v2";
        private const string P1ExposureTombstoneKey = "Game.Hot.Buqi.P1.Exposure.v1";

        private readonly BuqiSandboxLogFilter m_Filter = new BuqiSandboxLogFilter();
        private List<BuqiSandboxScenario> m_Scenarios = new List<BuqiSandboxScenario>();
        private BuqiSandboxRunResult m_RunResult;
        private BuqiSandboxRepeatResult m_RepeatResult;
        private BuqiSandboxWalkthroughBatch m_WalkthroughBatch;
        private BuqiSandboxWalkthroughRecord m_WalkthroughRecord;
        private BuqiSandboxExposureTombstone m_ExposureTombstone;
        private bool m_CurrentRecordExported;
        private string m_InvalidatedReason = string.Empty;
        private string m_ReplacementParticipantId = string.Empty;
        private Vector2 m_MainScroll;
        private Vector2 m_LogScroll;
        private bool m_UseP1Scenarios = true;
        private int m_SelectedScenarioIndex;
        private string m_TickFilter = string.Empty;
        private string m_ChainFilter = string.Empty;
        private string m_SourceFilter = string.Empty;
        private string m_ReasonFilter = string.Empty;
        private string m_ParticipantId = string.Empty;
        private BuqiSandboxParticipantProfile m_ParticipantProfile;
        private bool m_PredictionSkipped;
        private string m_Prediction = string.Empty;
        private string m_PrimaryCause = string.Empty;
        private string m_ChangeIntent = string.Empty;
        private string m_EvidenceEventIds = string.Empty;
        private string m_ModeratorNotes = string.Empty;
        private BuqiSandboxChangeKind m_ChangeKind;

        [MenuItem("游戏/不器/战斗沙盒", false, 200)]
        private static void Open()
        {
            BuqiBattleSandboxWindow window = GetWindow<BuqiBattleSandboxWindow>();
            window.titleContent = new GUIContent("不器战斗沙盒");
            window.minSize = new Vector2(920f, 680f);
            window.Show();
        }

        [MenuItem("游戏/不器/运行 P-1 快速构筑摘要", false, 201)]
        private static void RunFastSummary()
        {
            LogSummary(BuqiBattleSandbox.FindScenario("fast-space-choice"));
        }

        [MenuItem("游戏/不器/运行 P-1 快速护体摘要", false, 202)]
        private static void RunFastBufferSummary()
        {
            LogSummary(BuqiBattleSandbox.CreateFastBufferWalkthroughVariant());
        }

        [MenuItem("游戏/不器/运行 P-1 快速护体 A-02 摘要", false, 203)]
        private static void RunFastBufferDelayedDamageSummary()
        {
            LogSummary(BuqiBattleSandbox.CreateFastBufferDelayedDamageWalkthroughVariant());
        }

        private static void LogSummary(BuqiSandboxScenario scenario)
        {
            BuqiSandboxRunResult runResult = BuqiBattleSandbox.Run(scenario);
            BuqiSandboxBattleSummary summary = BuqiBattleSandbox.CreateBattleSummary(runResult);
            Debug.Log(BuqiText.Format(
                "[Buqi P-1] {0}: {1}",
                scenario.Id,
                BuqiBattleSandbox.FormatBattleSummary(summary)));
        }

        private void OnEnable()
        {
            ReloadScenarios(false);
            RestoreP1Session();
        }

        private void OnDisable()
        {
            SaveP1Session();
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
            if (!string.IsNullOrWhiteSpace(m_InvalidatedReason))
            {
                DrawInvalidatedSession();
                EditorGUILayout.EndScrollView();
                return;
            }
            DrawScenarioSelector();
            DrawWalkthrough();
            DrawActions();
            DrawResult();
            DrawDefinitions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("《不器》九法门战斗沙盒", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "仅用于规则、日志和 P-1 认知走查，不进入正式玩家流程；战斗结果始终由纯 C# 模拟器生成。",
                MessageType.Info);
            EditorGUI.BeginDisabledGroup(
                m_WalkthroughBatch != null ||
                m_ExposureTombstone != null ||
                !string.IsNullOrWhiteSpace(m_InvalidatedReason));
            bool useP1Scenarios = EditorGUILayout.ToggleLeft("P-1 固定三轮模式", m_UseP1Scenarios);
            EditorGUI.EndDisabledGroup();
            if (useP1Scenarios != m_UseP1Scenarios)
            {
                m_UseP1Scenarios = useP1Scenarios;
                ReloadScenarios(true);
            }
            EditorGUILayout.HelpBox(
                m_UseP1Scenarios
                    ? "预测锁定前不能运行；三轮依次只增加一个主要改动。"
                    : "Step 2 调试模式的记录不会计入 P-1 Gate。",
                m_UseP1Scenarios ? MessageType.Info : MessageType.Warning);
        }

        private void DrawScenarioSelector()
        {
            string[] names = new string[m_Scenarios.Count];
            for (int index = 0; index < m_Scenarios.Count; index++)
                names[index] = m_Scenarios[index].DisplayName;

            EditorGUI.BeginDisabledGroup(m_UseP1Scenarios || m_WalkthroughRecord != null);
            int selected = EditorGUILayout.Popup(
                m_UseP1Scenarios ? "P-1 题目" : "验证场景",
                m_SelectedScenarioIndex,
                names);
            EditorGUI.EndDisabledGroup();
            if (selected != m_SelectedScenarioIndex)
            {
                m_SelectedScenarioIndex = selected;
                ClearRoundState();
            }

            BuqiSandboxScenario scenario = m_Scenarios[m_SelectedScenarioIndex];
            EditorGUILayout.LabelField("目标", scenario.VerificationGoal, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("固定 seed", scenario.Request.BattleSeed.ToString());
        }

        private void DrawInvalidatedSession()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("P-1 作废批次", EditorStyles.boldLabel);
            string blockedBatchId = m_ExposureTombstone?.BatchId ?? m_WalkthroughBatch?.BatchId;
            string blockedParticipantId =
                m_ExposureTombstone?.ParticipantId ?? m_WalkthroughBatch?.ParticipantId;
            EditorGUILayout.LabelField("批次 ID", blockedBatchId ?? "未知");
            EditorGUILayout.LabelField("已曝光参与者", blockedParticipantId ?? "未知");
            EditorGUILayout.HelpBox(
                m_InvalidatedReason +
                "\n该参与者已经看过本题结果，不能在当前批次或新批次中重新预测。请更换参与者后再开始。",
                MessageType.Error);
            m_ReplacementParticipantId = EditorGUILayout.TextField(
                "新参与者 ID", m_ReplacementParticipantId);
            bool canReplace = m_ExposureTombstone != null &&
                              !string.IsNullOrWhiteSpace(m_ReplacementParticipantId) &&
                              !string.Equals(
                                  m_ReplacementParticipantId.Trim(),
                                  m_ExposureTombstone.ParticipantId,
                                  StringComparison.Ordinal);
            EditorGUI.BeginDisabledGroup(!canReplace);
            if (GUILayout.Button("更换参与者并结束作废批次", GUILayout.Height(28f)))
            {
                try
                {
                    string replacementParticipantId = m_ReplacementParticipantId.Trim();
                    BuqiSandboxWalkthroughBatch replacementBatch =
                        BuqiBattleSandbox.CreateReplacementWalkthroughBatch(
                            m_ExposureTombstone,
                            Guid.NewGuid().ToString("N"),
                            replacementParticipantId);
                    BuqiSandboxWalkthroughSession replacementSession =
                        BuqiBattleSandbox.CreateWalkthroughSession(
                            replacementBatch, null, false, string.Empty);

                    SessionState.SetString(
                        P1SessionStateKey,
                        BuqiBattleSandbox.SerializeWalkthroughSession(replacementSession));
                    SessionState.EraseString(P1ExposureTombstoneKey);

                    m_WalkthroughBatch = replacementBatch;
                    m_WalkthroughRecord = null;
                    m_ExposureTombstone = null;
                    m_InvalidatedReason = string.Empty;
                    m_ReplacementParticipantId = string.Empty;
                    m_ParticipantId = replacementBatch.ParticipantId;
                    m_ParticipantProfile = replacementBatch.ParticipantProfile;
                    m_SelectedScenarioIndex = 0;
                    ClearRecordState();
                }
                catch (Exception exception)
                {
                    ShowNotification(new GUIContent(exception.Message));
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawWalkthrough()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("P-1 认知走查记录", EditorStyles.boldLabel);
            if (m_WalkthroughBatch != null)
            {
                EditorGUILayout.LabelField("批次 ID", m_WalkthroughBatch.BatchId);
                EditorGUILayout.LabelField(
                    "批次进度",
                    BuqiText.Format("{0}/3", m_WalkthroughBatch.NextQuestionIndex));
            }

            bool participantLocked = m_WalkthroughBatch != null || m_WalkthroughRecord != null;
            EditorGUI.BeginDisabledGroup(participantLocked);
            m_ParticipantId = EditorGUILayout.TextField("参与者 ID", m_ParticipantId);
            m_ParticipantProfile = (BuqiSandboxParticipantProfile)EditorGUILayout.EnumPopup(
                "参与者画像", m_ParticipantProfile);
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(m_WalkthroughRecord != null);
            m_PredictionSkipped = EditorGUILayout.ToggleLeft(
                "跳过预测（本轮不进入 Gate 评审候选）", m_PredictionSkipped);
            EditorGUILayout.LabelField("战前预测", EditorStyles.miniBoldLabel);
            EditorGUI.BeginDisabledGroup(m_PredictionSkipped);
            m_Prediction = EditorGUILayout.TextArea(m_Prediction, GUILayout.MinHeight(42f));
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button(
                    m_PredictionSkipped ? "锁定跳过状态" : "锁定战前预测",
                    GUILayout.Height(24f)))
            {
                try
                {
                    if (m_UseP1Scenarios)
                    {
                        BuqiSandboxWalkthroughBatch batch = m_WalkthroughBatch ??
                            BuqiBattleSandbox.CreateWalkthroughBatch(
                                Guid.NewGuid().ToString("N"),
                                m_ParticipantId,
                                m_ParticipantProfile);
                        BuqiSandboxWalkthroughRecord record = BuqiBattleSandbox.BeginWalkthrough(
                            batch,
                            m_Scenarios[m_SelectedScenarioIndex],
                            m_Prediction,
                            m_PredictionSkipped,
                            DateTime.UtcNow.ToString("O"),
                            Guid.NewGuid().ToString("N"));
                        m_WalkthroughBatch = batch;
                        m_WalkthroughRecord = record;
                        m_ParticipantId = batch.ParticipantId;
                        m_ParticipantProfile = batch.ParticipantProfile;
                        m_CurrentRecordExported = false;
                        SaveP1Session();
                    }
                    else
                    {
                        m_WalkthroughRecord = BuqiBattleSandbox.BeginWalkthrough(
                            m_Scenarios[m_SelectedScenarioIndex],
                            m_ParticipantId,
                            m_ParticipantProfile,
                            m_Prediction,
                            m_PredictionSkipped,
                            DateTime.UtcNow.ToString("O"));
                    }
                }
                catch (Exception exception)
                {
                    ShowNotification(new GUIContent(exception.Message));
                }
            }
            EditorGUI.EndDisabledGroup();

            if (m_WalkthroughRecord == null)
                return;

            EditorGUILayout.LabelField(
                "记录状态",
                m_WalkthroughRecord.HasBattleResult
                     ? (m_WalkthroughRecord.IsComplete ? "完整" : "已绑定结果，待归因")
                     : "已记录预测，待运行战斗");
            EditorGUILayout.LabelField("锁定时间（UTC）", m_WalkthroughRecord.PredictionLockedAtUtc);
            EditorGUI.BeginDisabledGroup(
                !m_WalkthroughRecord.HasBattleResult || m_WalkthroughRecord.IsComplete);
            EditorGUILayout.LabelField("战后主因", EditorStyles.miniBoldLabel);
            m_PrimaryCause = EditorGUILayout.TextArea(m_PrimaryCause, GUILayout.MinHeight(42f));
            m_EvidenceEventIds = EditorGUILayout.TextField("证据事件 ID", m_EvidenceEventIds);
            m_ChangeKind = (BuqiSandboxChangeKind)EditorGUILayout.EnumPopup("改动类型", m_ChangeKind);
            EditorGUILayout.LabelField("下一轮改动及预期影响", EditorStyles.miniBoldLabel);
            m_ChangeIntent = EditorGUILayout.TextArea(m_ChangeIntent, GUILayout.MinHeight(42f));
            EditorGUILayout.LabelField("主持人备注", EditorStyles.miniBoldLabel);
            m_ModeratorNotes = EditorGUILayout.TextArea(m_ModeratorNotes, GUILayout.MinHeight(36f));
            if (GUILayout.Button("完成归因与下一轮改动", GUILayout.Height(24f)))
            {
                try
                {
                    if (m_UseP1Scenarios)
                    {
                        BuqiBattleSandbox.CompleteWalkthrough(
                            m_WalkthroughBatch,
                            m_WalkthroughRecord,
                            m_RunResult,
                            m_PrimaryCause,
                            m_ChangeKind,
                            m_ChangeIntent,
                            ParseEvidenceEventIds(m_EvidenceEventIds),
                            m_ModeratorNotes);
                        SaveP1Session();
                    }
                    else
                    {
                        BuqiBattleSandbox.CompleteWalkthrough(
                            m_WalkthroughRecord,
                            m_RunResult,
                            m_PrimaryCause,
                            m_ChangeKind,
                            m_ChangeIntent,
                            ParseEvidenceEventIds(m_EvidenceEventIds),
                            m_ModeratorNotes);
                    }
                }
                catch (Exception exception)
                {
                    ShowNotification(new GUIContent(exception.Message));
                }
            }
            EditorGUI.EndDisabledGroup();

            if (!m_WalkthroughRecord.IsComplete)
                return;

            EditorGUILayout.HelpBox(
                m_WalkthroughRecord.EligibleForGateReview
                    ? "本轮结构完整，可交给独立评审；这不等于已经计入或通过 P-1 Gate。"
                    : "本轮已保存，但因跳过预测、非固定题序或缺少证据而不进入 Gate 评审候选。",
                m_WalkthroughRecord.EligibleForGateReview ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导出本轮 JSON", GUILayout.Height(24f)))
                ExportWalkthrough();
            if (m_UseP1Scenarios)
            {
                EditorGUI.BeginDisabledGroup(!m_CurrentRecordExported);
                if (GUILayout.Button(
                        m_WalkthroughBatch.IsComplete ? "结束本批次" : "开始下一轮",
                        GUILayout.Height(24f)))
                {
                    if (m_WalkthroughBatch.IsComplete)
                        FinishBatch();
                    else
                        StartNextRound();
                }
                EditorGUI.EndDisabledGroup();
            }
            else if (GUILayout.Button("开始下一轮", GUILayout.Height(24f)))
            {
                ClearRoundState();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(
                m_UseP1Scenarios &&
                (m_WalkthroughRecord == null || m_WalkthroughRecord.HasBattleResult));
            if (GUILayout.Button("运行一场", GUILayout.Height(28f)))
            {
                m_RunResult = BuqiBattleSandbox.Run(m_Scenarios[m_SelectedScenarioIndex]);
                m_RepeatResult = null;
                BindWalkthroughResult();
            }
            if (!m_UseP1Scenarios && GUILayout.Button("重复 100 次", GUILayout.Height(28f)))
            {
                BuqiSandboxScenario scenario = m_Scenarios[m_SelectedScenarioIndex];
                m_RunResult = BuqiBattleSandbox.Run(scenario);
                m_RepeatResult = BuqiBattleSandbox.Repeat(scenario, 100);
                BindWalkthroughResult();
            }
            EditorGUI.EndDisabledGroup();

            if (m_UseP1Scenarios &&
                m_WalkthroughRecord != null &&
                !m_WalkthroughRecord.HasBattleResult)
            {
                if (GUILayout.Button("取消未曝光预测", GUILayout.Height(28f)))
                {
                    try
                    {
                        BuqiBattleSandbox.CancelWalkthroughAttempt(
                            m_WalkthroughBatch, m_WalkthroughRecord);
                        ClearRecordState();
                        SaveP1Session();
                    }
                    catch (Exception exception)
                    {
                        ShowNotification(new GUIContent(exception.Message));
                    }
                }
            }
            else if (!m_UseP1Scenarios && GUILayout.Button("清空结果", GUILayout.Height(28f)))
            {
                ClearRoundState();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void BindWalkthroughResult()
        {
            if (m_WalkthroughRecord == null || m_RunResult == null)
                return;
            try
            {
                if (m_UseP1Scenarios)
                {
                    BuqiBattleSandbox.BindWalkthroughResult(
                        m_WalkthroughBatch, m_WalkthroughRecord, m_RunResult);
                    m_ExposureTombstone = BuqiBattleSandbox.CreateExposureTombstone(
                        m_WalkthroughBatch, m_WalkthroughRecord);
                    SessionState.SetString(
                        P1ExposureTombstoneKey,
                        BuqiBattleSandbox.SerializeExposureTombstone(m_ExposureTombstone));
                    SaveP1Session();
                }
                else
                {
                    BuqiBattleSandbox.BindWalkthroughResult(m_WalkthroughRecord, m_RunResult);
                }
            }
            catch (Exception exception)
            {
                ShowNotification(new GUIContent(exception.Message));
            }
        }

        private void ReloadScenarios(bool resetIndex)
        {
            m_Scenarios = m_UseP1Scenarios
                ? BuqiBattleSandbox.CreateP1WalkthroughScenarios()
                : BuqiBattleSandbox.CreateScenarios();
            m_SelectedScenarioIndex = resetIndex
                ? 0
                : Mathf.Clamp(m_SelectedScenarioIndex, 0, m_Scenarios.Count - 1);
            if (resetIndex)
                ClearRoundState();
        }

        private void StartNextRound()
        {
            if (m_WalkthroughBatch == null ||
                m_WalkthroughBatch.IsComplete ||
                m_WalkthroughRecord == null ||
                !m_WalkthroughRecord.IsComplete ||
                !m_CurrentRecordExported)
            {
                ShowNotification(new GUIContent("请先完成并导出当前轮次。"));
                return;
            }

            m_SelectedScenarioIndex = m_WalkthroughBatch.NextQuestionIndex;
            ClearRecordState();
            SaveP1Session();
        }

        private void FinishBatch()
        {
            if (m_WalkthroughBatch == null ||
                !m_WalkthroughBatch.IsComplete ||
                m_WalkthroughRecord == null ||
                !m_WalkthroughRecord.IsComplete ||
                !m_CurrentRecordExported)
            {
                ShowNotification(new GUIContent("请先完成并导出第三轮记录。"));
                return;
            }

            SessionState.EraseString(P1SessionStateKey);
            SessionState.EraseString(P1ExposureTombstoneKey);
            m_WalkthroughBatch = null;
            m_ExposureTombstone = null;
            m_ParticipantId = string.Empty;
            m_ParticipantProfile = BuqiSandboxParticipantProfile.AutoBuilder;
            m_SelectedScenarioIndex = 0;
            ClearRecordState();
            ShowNotification(new GUIContent("P-1 三轮批次已结束，请将三份记录交给独立评审。"));
        }

        private void ClearRoundState()
        {
            ClearRecordState();
            if (!m_UseP1Scenarios)
                m_WalkthroughBatch = null;
        }

        private void ClearRecordState()
        {
            m_RunResult = null;
            m_RepeatResult = null;
            m_WalkthroughRecord = null;
            m_CurrentRecordExported = false;
            m_PredictionSkipped = false;
            m_Prediction = string.Empty;
            m_PrimaryCause = string.Empty;
            m_ChangeKind = BuqiSandboxChangeKind.Purchase;
            m_ChangeIntent = string.Empty;
            m_EvidenceEventIds = string.Empty;
            m_ModeratorNotes = string.Empty;
        }

        private void ExportWalkthrough()
        {
            try
            {
                BuqiSandboxWalkthroughExport export =
                    BuqiBattleSandbox.CreateWalkthroughExport(m_WalkthroughRecord);
                string fileName = BuqiText.Format(
                    "buqi-p1-{0}-{1}.json",
                    SanitizeFileName(export.ParticipantId),
                    SanitizeFileName(export.QuestionId));
                string path = EditorUtility.SaveFilePanel(
                    "导出 P-1 单轮记录", string.Empty, fileName, "json");
                if (string.IsNullOrEmpty(path))
                    return;

                BuqiBattleSandbox.WriteWalkthroughJson(path, m_WalkthroughRecord);
                if (m_UseP1Scenarios)
                {
                    BuqiBattleSandbox.MarkWalkthroughExported(
                        m_WalkthroughBatch, m_WalkthroughRecord);
                }
                m_CurrentRecordExported = true;
                SaveP1Session();
                ShowNotification(new GUIContent("P-1 记录已导出。"));
            }
            catch (Exception exception)
            {
                ShowNotification(new GUIContent(exception.Message));
            }
        }

        private void SaveP1Session()
        {
            if (!m_UseP1Scenarios || m_WalkthroughBatch == null)
                return;

            BuqiSandboxWalkthroughSession session =
                BuqiBattleSandbox.CreateWalkthroughSession(
                    m_WalkthroughBatch,
                    m_WalkthroughRecord,
                    m_CurrentRecordExported,
                    m_InvalidatedReason);
            SessionState.SetString(
                P1SessionStateKey,
                BuqiBattleSandbox.SerializeWalkthroughSession(session));
        }

        private void RestoreP1Session()
        {
            string exposureJson = SessionState.GetString(
                P1ExposureTombstoneKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(exposureJson))
            {
                try
                {
                    m_ExposureTombstone =
                        BuqiBattleSandbox.DeserializeExposureTombstone(exposureJson);
                }
                catch (Exception exception)
                {
                    m_UseP1Scenarios = true;
                    m_InvalidatedReason =
                        "曝光墓碑损坏，已按失败关闭处理，不能恢复为空白批次：" + exception.Message;
                    return;
                }
            }

            string json = SessionState.GetString(P1SessionStateKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                if (m_ExposureTombstone != null)
                    ActivateExposureRecovery("主会话缺失，但检测到已曝光结果墓碑。");
                return;
            }

            try
            {
                BuqiSandboxWalkthroughSession state =
                    BuqiBattleSandbox.DeserializeWalkthroughSession(json);
                if (m_ExposureTombstone != null &&
                    !BuqiBattleSandbox.IsExposureTombstoneConsistent(
                        state,
                        m_ExposureTombstone,
                        out string consistencyReason))
                {
                    ActivateExposureRecovery(consistencyReason);
                    return;
                }

                m_UseP1Scenarios = true;
                m_Scenarios = BuqiBattleSandbox.CreateP1WalkthroughScenarios();
                m_WalkthroughBatch = state.Batch;
                m_WalkthroughRecord = state.Record;
                m_CurrentRecordExported = state.CurrentRecordExported;
                m_InvalidatedReason = state.InvalidatedReason;
                m_ParticipantId = state.Batch.ParticipantId;
                m_ParticipantProfile = state.Batch.ParticipantProfile;
                m_SelectedScenarioIndex = state.Record != null
                    ? state.Record.RoundIndex
                    : Mathf.Clamp(state.Batch.NextQuestionIndex, 0, m_Scenarios.Count - 1);

                if (state.Record == null || state.IsInvalidated)
                    return;

                m_PredictionSkipped = state.Record.PredictionSkipped;
                m_Prediction = state.Record.Prediction;
                m_PrimaryCause = state.Record.PrimaryCause;
                m_ChangeKind = state.Record.ChangeKind;
                m_ChangeIntent = state.Record.ChangeIntent;
                m_EvidenceEventIds = string.Join(",", state.Record.EvidenceEventIds);
                m_ModeratorNotes = state.Record.ModeratorNotes;

                if (!state.Record.HasBattleResult)
                    return;

                m_RunResult = BuqiBattleSandbox.Run(m_Scenarios[m_SelectedScenarioIndex]);
                if (!string.Equals(
                        m_RunResult.Result.BattleLogHash,
                        state.Record.BattleLogHash,
                        StringComparison.Ordinal))
                {
                    string reason =
                        "恢复记录与当前模拟器 hash 不一致，当前已曝光样本作废。";
                    BuqiBattleSandbox.InvalidateWalkthroughSession(state, reason);
                    m_InvalidatedReason = state.InvalidatedReason;
                    m_RunResult = null;
                    SessionState.SetString(
                        P1SessionStateKey,
                        BuqiBattleSandbox.SerializeWalkthroughSession(state));
                }
            }
            catch (Exception exception)
            {
                SessionState.EraseString(P1SessionStateKey);
                if (m_ExposureTombstone != null)
                {
                    ActivateExposureRecovery(
                        "主会话损坏，但已保留结果曝光墓碑：" + exception.Message);
                }
                else
                {
                    m_WalkthroughBatch = null;
                    ClearRecordState();
                    ShowNotification(new GUIContent("P-1 批次恢复失败：" + exception.Message));
                }
            }
        }

        private void ActivateExposureRecovery(string reason)
        {
            m_UseP1Scenarios = true;
            m_Scenarios = BuqiBattleSandbox.CreateP1WalkthroughScenarios();
            m_WalkthroughBatch = null;
            m_WalkthroughRecord = null;
            m_CurrentRecordExported = false;
            m_InvalidatedReason = reason;
            m_ParticipantId = m_ExposureTombstone.ParticipantId;
            m_ParticipantProfile = m_ExposureTombstone.ParticipantProfile;
            m_SelectedScenarioIndex = 0;
        }

        private static List<int> ParseEvidenceEventIds(string value)
        {
            var result = new List<int>();
            string[] tokens = (value ?? string.Empty).Split(
                new[] { ',', ';', ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                if (!int.TryParse(token, out int eventId))
                    throw new FormatException("证据事件 ID 必须是用逗号或空格分隔的整数。");
                result.Add(eventId);
            }
            return result;
        }

        private static string SanitizeFileName(string value)
        {
            string sanitized = value ?? string.Empty;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                sanitized = sanitized.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(sanitized) ? "record" : sanitized.Trim();
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
            BuqiSandboxBattleSummary summary = BuqiBattleSandbox.CreateBattleSummary(m_RunResult);
            EditorGUILayout.HelpBox(
                BuqiBattleSandbox.FormatBattleSummary(summary),
                MessageType.None);

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

            EditorGUILayout.LabelField("左侧 10 格", m_RunResult.LeftBoardText, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("右侧 10 格", m_RunResult.RightBoardText, EditorStyles.wordWrappedLabel);
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
