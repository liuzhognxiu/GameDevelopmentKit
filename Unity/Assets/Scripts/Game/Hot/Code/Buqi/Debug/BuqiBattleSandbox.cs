#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>Step 2 沙盒中的三种验证方向。</summary>
    public enum BuqiSandboxArchetype
    {
        Fast = 0,
        BufferCounter = 1,
        Chain = 2,
    }

    /// <summary>九法门沙盒的显示元数据；Step 3 配置链路已建立。</summary>
    public sealed class BuqiSandboxItemInfo
    {
        public string DefinitionId = string.Empty;
        public string DisplayName = string.Empty;
        public BuqiSandboxArchetype Archetype;
        public string RuleSummary = string.Empty;
        public bool UsesPlaceholderSemantics;
    }

    /// <summary>一组可以直接运行的 Step 2 验证场景。</summary>
    public sealed class BuqiSandboxScenario
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public string VerificationGoal = string.Empty;
        public BattleRequest Request;
    }

    /// <summary>日志过滤条件；空字符串和负数表示不过滤该维度。</summary>
    public sealed class BuqiSandboxLogFilter
    {
        public int Tick = -1;
        public string ChainId = string.Empty;
        public string SourceInstanceId = string.Empty;
        public string ReasonCode = string.Empty;
    }

    /// <summary>P-1 走查中允许记录的针对性改动类型。</summary>
    public enum BuqiSandboxChangeKind
    {
        Purchase = 0,
        Refinement = 1,
        Position = 2,
    }

    /// <summary>
    /// P-1 单轮认知记录。预测先于战斗固化，主因和改动只能在绑定战斗结果后填写。
    /// 该记录仅用于体验走查，不参与战斗输入或结果计算。
    /// </summary>
    public sealed class BuqiSandboxWalkthroughRecord
    {
        public string ParticipantId = string.Empty;
        public string ScenarioId = string.Empty;
        public string Prediction = string.Empty;
        public string BattleLogHash = string.Empty;
        public BattleOutcome Outcome;
        public string PrimaryCause = string.Empty;
        public BuqiSandboxChangeKind ChangeKind;
        public string ChangeIntent = string.Empty;

        /// <summary>是否已经绑定由模拟器产生的战斗结果。</summary>
        public bool HasBattleResult => !string.IsNullOrEmpty(BattleLogHash);

        /// <summary>是否形成完整的“预测—结果—主因—针对性重构”记录。</summary>
        public bool IsComplete =>
            HasBattleResult &&
            !string.IsNullOrWhiteSpace(PrimaryCause) &&
            !string.IsNullOrWhiteSpace(ChangeIntent);
    }

    /// <summary>单次沙盒运行结果，包含终态、完整日志和可读棋盘布局。</summary>
    public sealed class BuqiSandboxRunResult
    {
        public BuqiSandboxScenario Scenario;
        public BattleResult Result;
        public SideState LeftFinal;
        public SideState RightFinal;
        public List<BattleEvent> Log = new List<BattleEvent>();
        public string LeftBoardText = string.Empty;
        public string RightBoardText = string.Empty;
    }

    /// <summary>P-1 通俗战斗摘要；所有字段均由模拟器终态和战斗日志投影得到。</summary>
    public sealed class BuqiSandboxBattleSummary
    {
        public BattleOutcome Outcome;
        public int DurationTicks;
        public int LeftExecution;
        public int RightExecution;
        public int LeftBuffer;
        public int RightBuffer;
        public int LeftBufferAbsorbed;
        public int RightBufferAbsorbed;
        public int LeftCounterDeclarationCount;
        public int RightCounterDeclarationCount;
        public int LeftCounterDeclaredDamage;
        public int RightCounterDeclaredDamage;
        public int LeftNoiseAccidentCount;
        public int RightNoiseAccidentCount;
        public int LeftNoiseAccidentDamage;
        public int RightNoiseAccidentDamage;
        public string BattleLogHash = string.Empty;
    }

    /// <summary>重复运行的确定性检查结果。</summary>
    public sealed class BuqiSandboxRepeatResult
    {
        public int RequestedRuns;
        public int CompletedRuns;
        public bool IsDeterministic;
        public string FirstHash = string.Empty;
        public string MismatchHash = string.Empty;
    }

    /// <summary>
    /// 《不器》Editor 战斗沙盒的纯逻辑入口。
    /// 本文件只在 Unity Editor 编译，不进入正式 Player；正式配置由 Step 3 Luban 链路提供。
    /// </summary>
    public static class BuqiBattleSandbox
    {
        public const string ContentVersion = "buqi-sandbox-cv1";
        public const ulong FixedBattleSeed = 2026080402UL;

        private static readonly Dictionary<string, BuqiSandboxItemInfo> s_ItemInfos = CreateItemInfos();

        /// <summary>返回九个验证法门的只读显示元数据。</summary>
        public static IReadOnlyDictionary<string, BuqiSandboxItemInfo> ItemInfos => s_ItemInfos;

        /// <summary>
        /// 创建九法门临时定义。蓄力动态增伤通过通用配置原语表达，
        /// 消费发生在声明时，不在模拟器中硬编码法门 ID。
        /// </summary>
        public static IItemDefinitionProvider CreateDefinitionProvider()
        {
            var definitions = new Dictionary<string, BuqiItemDefinition>(StringComparer.Ordinal)
            {
                ["W8-003"] = Definition("W8-003", (int)BuqiSize.S, 60,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 4, "W8-003-damage"),
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Haste, BuqiTarget.LeftAdjacentItem, 2000, "W8-003-haste", 30)),
                ["W8-005"] = Definition("W8-005", (int)BuqiSize.M, 70,
                    Effect(BuqiTrigger.OnAdjacentUse, BuqiEffect.Charge, BuqiTarget.Self, 1, "W8-005-adjacent-charge"),
                    ChargedEffect(BuqiTrigger.OnUse, 6, 2, 3, true, "W8-005-damage")),
                ["W8-006"] = Definition("W8-006", (int)BuqiSize.L, 100,
                    Effect(BuqiTrigger.OnBattleStart, BuqiEffect.Haste, BuqiTarget.AllAdjacentItems, 1500, "W8-006-opening-haste", 50),
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 16, "W8-006-damage"),
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Noise, BuqiTarget.Self, 2, "W8-006-noise")),
                ["W8-007"] = Definition("W8-007", (int)BuqiSize.S, 42,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Buffer, BuqiTarget.Self, 7, "W8-007-buffer")),
                ["W8-008"] = Definition("W8-008", (int)BuqiSize.S, 55,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Charge, BuqiTarget.Self, 1, "W8-008-charge"),
                    ConditionEffect(BuqiConditionKind.BufferLost, 0, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 8, "W8-008-buffer-counter")),
                ["W8-012"] = Definition("W8-012", (int)BuqiSize.L, 90,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Buffer, BuqiTarget.Self, 12, "W8-012-buffer"),
                    ConditionEffect(BuqiConditionKind.BufferLost, 0, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 14, "W8-012-buffer-counter")),
                ["W8-013"] = Definition("W8-013", (int)BuqiSize.S, 50,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 4, "W8-013-damage"),
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Charge, BuqiTarget.RightAdjacentItem, 1, "W8-013-pass-charge"),
                    Effect(BuqiTrigger.OnAdjacentUse, BuqiEffect.Charge, BuqiTarget.RightAdjacentItem, 1, "W8-013-adjacent-pass")),
                ["W8-014"] = Definition("W8-014", (int)BuqiSize.S, 60,
                    Effect(BuqiTrigger.OnAdjacentUse, BuqiEffect.Charge, BuqiTarget.Self, 1, "W8-014-adjacent-charge"),
                    ChargedEffect(BuqiTrigger.OnUse, 3, 3, 2, true, "W8-014-damage")),
                ["W8-015"] = Definition("W8-015", (int)BuqiSize.M, 65,
                    Effect(BuqiTrigger.OnAdjacentUse, BuqiEffect.Haste, BuqiTarget.Self, 2000, "W8-015-adjacent-haste", 30),
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 7, "W8-015-damage")),
            };
            return new DictionaryDefinitionProvider(ContentVersion, definitions);
        }

        /// <summary>创建快速、护体反制和周天连锁三组固定验证对局。</summary>
        public static List<BuqiSandboxScenario> CreateScenarios()
        {
            return new List<BuqiSandboxScenario>
            {
                Scenario(
                    "fast-space-choice",
                    "快速方向与大型法门挤压",
                    "验证 S/M/L 占位、开战加速、相邻蓄力与 A-01 加急代价。大型核心占 3 格后只能保留有限辅助位。",
                    Snapshot("fast-left", "fast", 120,
                        Item("fast-deadline", "W8-006", 0),
                        Item("fast-board", "W8-005", 3),
                        Item("fast-urgent", "W8-003", 5, "A-01")),
                    Snapshot("fast-right", "buffer-counter", 120,
                        Item("guard-buffer", "W8-007", 0),
                        Item("guard-risk", "W8-008", 1, "A-04"),
                        Item("guard-center", "W8-012", 2))),
                Scenario(
                    "buffer-loss-counter",
                    "护体受损转化反击",
                    "验证获得护体、普通伤害吸收、护体清空后首次条件反击，以及 A-04 可靠不接受错误加速/延迟。",
                    Snapshot("buffer-left", "buffer-counter", 160,
                        Item("buffer-temp", "W8-007", 0),
                        Item("buffer-risk", "W8-008", 1, "A-04"),
                        Item("buffer-center", "W8-012", 2)),
                    Snapshot("buffer-right", "fast", 160,
                        Item("attack-deadline", "W8-006", 0),
                        Item("attack-board", "W8-005", 3),
                        Item("attack-urgent", "W8-003", 5, "A-01"))),
                Scenario(
                    "adjacency-chain",
                    "周天相邻连锁",
                    "验证交接单、联签流程和流程节点连续相邻时的蓄力传递、相邻响应与 A-03 首次复写。",
                    Snapshot("chain-left", "chain", 140,
                        Item("chain-handover", "W8-013", 0),
                        Item("chain-sign", "W8-014", 1, "A-03"),
                        Item("chain-node", "W8-015", 2)),
                    Snapshot("chain-right", "buffer-counter", 140,
                        Item("chain-target-buffer", "W8-007", 0),
                        Item("chain-target-risk", "W8-008", 1),
                        Item("chain-target-center", "W8-012", 2))),
            };
        }

        /// <summary>按 ID 查找固定场景；找不到时返回 null。</summary>
        public static BuqiSandboxScenario FindScenario(string scenarioId)
        {
            foreach (BuqiSandboxScenario scenario in CreateScenarios())
            {
                if (string.Equals(scenario.Id, scenarioId, StringComparison.Ordinal))
                    return scenario;
            }
            return null;
        }

        /// <summary>
        /// 创建 P-1 第二轮“强化拖延”变体：右侧在空余格增加一张临时缓冲，其他输入保持不变。
        /// </summary>
        public static BuqiSandboxScenario CreateFastBufferWalkthroughVariant()
        {
            return Scenario(
                "fast-space-choice-buffer-plus",
                "快速方向与双临时缓冲",
                "验证右侧增加一张临时缓冲后，更多护体是否足以拖到左侧承受额外失衡事故。",
                Snapshot("fast-left", "fast", 120,
                    Item("fast-deadline", "W8-006", 0),
                    Item("fast-board", "W8-005", 3),
                    Item("fast-urgent", "W8-003", 5, "A-01")),
                Snapshot("fast-right-buffer-plus", "buffer-counter", 120,
                    Item("guard-buffer", "W8-007", 0),
                    Item("guard-risk", "W8-008", 1, "A-04"),
                    Item("guard-center", "W8-012", 2),
                    Item("guard-buffer-extra", "W8-007", 5)));
        }

        /// <summary>
        /// 创建 P-1 第三轮“强化输出”变体：保留右侧双缓冲，只给左侧蓄力法门增加 A-02 延期。
        /// </summary>
        public static BuqiSandboxScenario CreateFastBufferDelayedDamageWalkthroughVariant()
        {
            return Scenario(
                "fast-space-choice-buffer-plus-a02",
                "延期蓄力对双临时缓冲",
                "验证 W8-005 增加 A-02 后，效果量提升能否突破右侧双缓冲的持续护体供给。",
                Snapshot("fast-left-a02", "fast", 120,
                    Item("fast-deadline", "W8-006", 0),
                    Item("fast-board", "W8-005", 3, "A-02"),
                    Item("fast-urgent", "W8-003", 5, "A-01")),
                Snapshot("fast-right-buffer-plus", "buffer-counter", 120,
                    Item("guard-buffer", "W8-007", 0),
                    Item("guard-risk", "W8-008", 1, "A-04"),
                    Item("guard-center", "W8-012", 2),
                    Item("guard-buffer-extra", "W8-007", 5)));
        }

        /// <summary>创建一条战前认知记录；参与者、场景和预测均必须明确。</summary>
        public static BuqiSandboxWalkthroughRecord BeginWalkthrough(
            BuqiSandboxScenario scenario,
            string participantId,
            string prediction)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (string.IsNullOrWhiteSpace(participantId))
                throw new ArgumentException("P-1 参与者标识不能为空。", nameof(participantId));
            if (string.IsNullOrWhiteSpace(prediction))
                throw new ArgumentException("P-1 战前预测不能为空。", nameof(prediction));

            return new BuqiSandboxWalkthroughRecord
            {
                ParticipantId = participantId.Trim(),
                ScenarioId = scenario.Id,
                Prediction = prediction.Trim(),
            };
        }

        /// <summary>将模拟器结果绑定到战前记录；场景必须与记录一致，且只允许绑定一次。</summary>
        public static void BindWalkthroughResult(
            BuqiSandboxWalkthroughRecord record,
            BuqiSandboxRunResult runResult)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (runResult == null)
                throw new ArgumentNullException(nameof(runResult));
            if (record.HasBattleResult)
                throw new InvalidOperationException("P-1 记录已经绑定战斗结果。请开始新一轮走查。");
            if (!string.Equals(record.ScenarioId, runResult.Scenario.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("P-1 记录与战斗场景不一致。请重新记录战前预测。");

            record.BattleLogHash = runResult.Result.BattleLogHash;
            record.Outcome = runResult.Result.Outcome;
        }

        /// <summary>完成战后归因与下一轮改动；必须先绑定战斗结果。</summary>
        public static void CompleteWalkthrough(
            BuqiSandboxWalkthroughRecord record,
            string primaryCause,
            BuqiSandboxChangeKind changeKind,
            string changeIntent)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (!record.HasBattleResult)
                throw new InvalidOperationException("P-1 记录尚未绑定战斗结果。");
            if (string.IsNullOrWhiteSpace(primaryCause))
                throw new ArgumentException("P-1 战后主因不能为空。", nameof(primaryCause));
            if (string.IsNullOrWhiteSpace(changeIntent))
                throw new ArgumentException("P-1 下一轮改动及预期影响不能为空。", nameof(changeIntent));

            record.PrimaryCause = primaryCause.Trim();
            record.ChangeKind = changeKind;
            record.ChangeIntent = changeIntent.Trim();
        }

        /// <summary>运行指定场景并生成终态、完整日志与双方 8 格文本布局。</summary>
        public static BuqiSandboxRunResult Run(BuqiSandboxScenario scenario)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));

            IItemDefinitionProvider provider = CreateDefinitionProvider();
            BattleResult result = BuqiBattleSimulator.Simulate(
                scenario.Request, provider, out List<BattleEvent> log,
                out SideState leftFinal, out SideState rightFinal);
            return new BuqiSandboxRunResult
            {
                Scenario = scenario,
                Result = result,
                LeftFinal = leftFinal,
                RightFinal = rightFinal,
                Log = log,
                LeftBoardText = FormatBoard(scenario.Request.Left, provider),
                RightBoardText = FormatBoard(scenario.Request.Right, provider),
            };
        }

        /// <summary>从真实终态和日志生成 P-1 通俗摘要，不重新计算战斗结果。</summary>
        public static BuqiSandboxBattleSummary CreateBattleSummary(BuqiSandboxRunResult runResult)
        {
            if (runResult == null)
                throw new ArgumentNullException(nameof(runResult));

            var leftInstances = CreateInstanceSet(runResult.Scenario.Request.Left);
            var rightInstances = CreateInstanceSet(runResult.Scenario.Request.Right);
            var summary = new BuqiSandboxBattleSummary
            {
                Outcome = runResult.Result.Outcome,
                DurationTicks = runResult.Result.DurationTicks,
                LeftExecution = runResult.Result.LeftExecution,
                RightExecution = runResult.Result.RightExecution,
                LeftBuffer = runResult.Result.LeftBuffer,
                RightBuffer = runResult.Result.RightBuffer,
                BattleLogHash = runResult.Result.BattleLogHash,
            };

            foreach (BattleEvent battleEvent in runResult.Log)
            {
                if ((battleEvent.ReasonCode == "W8-008-buffer-counter" ||
                     battleEvent.ReasonCode == "W8-012-buffer-counter") &&
                    battleEvent.Type == BuqiEventType.Declare)
                {
                    if (leftInstances.Contains(battleEvent.SourceInstanceId))
                    {
                        summary.LeftCounterDeclarationCount++;
                        summary.LeftCounterDeclaredDamage += battleEvent.Amount;
                    }
                    else if (rightInstances.Contains(battleEvent.SourceInstanceId))
                    {
                        summary.RightCounterDeclarationCount++;
                        summary.RightCounterDeclaredDamage += battleEvent.Amount;
                    }
                    continue;
                }

                if (battleEvent.Type != BuqiEventType.Effect)
                    continue;

                if (battleEvent.ReasonCode == "BufferAbsorb")
                {
                    if (leftInstances.Contains(battleEvent.SourceInstanceId))
                        summary.RightBufferAbsorbed += battleEvent.Amount;
                    else if (rightInstances.Contains(battleEvent.SourceInstanceId))
                        summary.LeftBufferAbsorbed += battleEvent.Amount;
                }
                else if (battleEvent.ReasonCode == "NoiseAccident")
                {
                    if (leftInstances.Contains(battleEvent.SourceInstanceId))
                    {
                        summary.LeftNoiseAccidentCount++;
                        summary.LeftNoiseAccidentDamage += battleEvent.Amount;
                    }
                    else if (rightInstances.Contains(battleEvent.SourceInstanceId))
                    {
                        summary.RightNoiseAccidentCount++;
                        summary.RightNoiseAccidentDamage += battleEvent.Amount;
                    }
                }
            }
            return summary;
        }

        /// <summary>将 P-1 摘要格式化为玩家可直接理解的文本。</summary>
        public static string FormatBattleSummary(BuqiSandboxBattleSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            string finalState = BuqiText.Format(
                "胜负={0}，时长={1} tick；左侧生命={2}、护体={3}；右侧生命={4}、护体={5}。",
                summary.Outcome,
                summary.DurationTicks,
                summary.LeftExecution,
                summary.LeftBuffer,
                summary.RightExecution,
                summary.RightBuffer);
            string bufferAndCounter = BuqiText.Format(
                "护体吸收：左侧={0}、右侧={1}；反击声明：左侧={2} 次/{3} 伤害，右侧={4} 次/{5} 伤害。",
                summary.LeftBufferAbsorbed,
                summary.RightBufferAbsorbed,
                summary.LeftCounterDeclarationCount,
                summary.LeftCounterDeclaredDamage,
                summary.RightCounterDeclarationCount,
                summary.RightCounterDeclaredDamage);
            string noise = BuqiText.Format(
                "失衡事故：左侧={0} 次/{1} 伤害，右侧={2} 次/{3} 伤害；hash={4}",
                summary.LeftNoiseAccidentCount,
                summary.LeftNoiseAccidentDamage,
                summary.RightNoiseAccidentCount,
                summary.RightNoiseAccidentDamage,
                summary.BattleLogHash);
            return BuqiText.Format("{0}\n{1}\n{2}", finalState, bufferAndCounter, noise);
        }

        /// <summary>重复运行同一场景，验证结果和日志哈希没有状态残留或平台漂移。</summary>
        public static BuqiSandboxRepeatResult Repeat(BuqiSandboxScenario scenario, int runCount)
        {
            if (scenario == null)
                throw new ArgumentNullException(nameof(scenario));
            if (runCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(runCount));

            var repeatResult = new BuqiSandboxRepeatResult
            {
                RequestedRuns = runCount,
                IsDeterministic = true,
            };
            for (int index = 0; index < runCount; index++)
            {
                BuqiSandboxRunResult run = Run(scenario);
                repeatResult.CompletedRuns++;
                if (index == 0)
                {
                    repeatResult.FirstHash = run.Result.BattleLogHash;
                    continue;
                }
                if (!string.Equals(repeatResult.FirstHash, run.Result.BattleLogHash, StringComparison.Ordinal))
                {
                    repeatResult.IsDeterministic = false;
                    repeatResult.MismatchHash = run.Result.BattleLogHash;
                    break;
                }
            }
            return repeatResult;
        }

        /// <summary>按 tick、chainId、来源和 reasonCode 过滤日志。</summary>
        public static List<BattleEvent> FilterLog(
            List<BattleEvent> log,
            BuqiSandboxLogFilter filter)
        {
            var result = new List<BattleEvent>();
            if (log == null)
                return result;
            filter = filter ?? new BuqiSandboxLogFilter();
            foreach (BattleEvent battleEvent in log)
            {
                if (filter.Tick >= 0 && battleEvent.Tick != filter.Tick)
                    continue;
                if (!ContainsOrdinal(battleEvent.ChainId, filter.ChainId))
                    continue;
                if (!ContainsOrdinal(battleEvent.SourceInstanceId, filter.SourceInstanceId))
                    continue;
                if (!ContainsOrdinal(battleEvent.ReasonCode, filter.ReasonCode))
                    continue;
                result.Add(battleEvent);
            }
            return result;
        }

        /// <summary>将一侧构筑展开为固定 8 格文本；多格法门在后续占位格显示续接符。</summary>
        public static string FormatBoard(BuildSnapshot snapshot, IItemDefinitionProvider provider)
        {
            var slots = new string[BuqiBoardValidator.BoardSlotCount];
            for (int index = 0; index < slots.Length; index++)
                slots[index] = "空";

            foreach (ItemInstance item in snapshot.Items)
            {
                if (!provider.TryGet(item.DefinitionId, out BuqiItemDefinition definition))
                    continue;
                string name = s_ItemInfos.TryGetValue(item.DefinitionId, out BuqiSandboxItemInfo info)
                    ? info.DisplayName
                    : item.DefinitionId;
                slots[item.AnchorSlot] = BuqiText.Format("{0}({1})", name, SizeLabel(definition.Size));
                for (int offset = 1; offset < definition.Size; offset++)
                    slots[item.AnchorSlot + offset] = "续";
            }

            string firstHalf = BuqiText.Format(
                "[0]{0} | [1]{1} | [2]{2} | [3]{3}",
                slots[0], slots[1], slots[2], slots[3]);
            string secondHalf = BuqiText.Format(
                "[4]{0} | [5]{1} | [6]{2} | [7]{3}",
                slots[4], slots[5], slots[6], slots[7]);
            return BuqiText.Format("{0} | {1}", firstHalf, secondHalf);
        }

        private static Dictionary<string, BuqiSandboxItemInfo> CreateItemInfos()
        {
            return new Dictionary<string, BuqiSandboxItemInfo>(StringComparer.Ordinal)
            {
                ["W8-003"] = Info("W8-003", "加急通知", BuqiSandboxArchetype.Fast, "使用造成伤害并加速左邻。"),
                ["W8-005"] = Info("W8-005", "冲刺看板", BuqiSandboxArchetype.Fast, "相邻使用获得蓄力；使用时最多消费 3 点，每点使伤害 +2，基础 6。"),
                ["W8-006"] = Info("W8-006", "截止日", BuqiSandboxArchetype.Fast, "开战加速相邻；使用造成伤害并增加己方失衡。"),
                ["W8-007"] = Info("W8-007", "临时缓冲", BuqiSandboxArchetype.BufferCounter, "使用获得护体。"),
                ["W8-008"] = Info("W8-008", "风险清单", BuqiSandboxArchetype.BufferCounter, "护体首次清空后造成反击伤害。", true),
                ["W8-012"] = Info("W8-012", "灾备中心", BuqiSandboxArchetype.BufferCounter, "使用获得护体；护体首次清空后反击。", true),
                ["W8-013"] = Info("W8-013", "交接单", BuqiSandboxArchetype.Chain, "使用造成伤害并给右邻传递蓄力。"),
                ["W8-014"] = Info("W8-014", "联签流程", BuqiSandboxArchetype.Chain, "相邻使用获得蓄力；使用时最多消费 2 点，每点使伤害 +3，基础 3。"),
                ["W8-015"] = Info("W8-015", "流程节点", BuqiSandboxArchetype.Chain, "相邻使用时获得加速，主动使用造成伤害。", true),
            };
        }

        private static BuqiSandboxItemInfo Info(
            string definitionId,
            string displayName,
            BuqiSandboxArchetype archetype,
            string summary,
            bool placeholder = false)
        {
            return new BuqiSandboxItemInfo
            {
                DefinitionId = definitionId,
                DisplayName = displayName,
                Archetype = archetype,
                RuleSummary = summary,
                UsesPlaceholderSemantics = placeholder,
            };
        }

        private static BuqiSandboxScenario Scenario(
            string id,
            string displayName,
            string goal,
            BuildSnapshot left,
            BuildSnapshot right)
        {
            return new BuqiSandboxScenario
            {
                Id = id,
                DisplayName = displayName,
                VerificationGoal = goal,
                Request = new BattleRequest
                {
                    RuleVersion = BuqiBattleSimulator.RuleVersion,
                    BattleSeed = FixedBattleSeed,
                    RoundIndex = 0,
                    Left = left,
                    Right = right,
                },
            };
        }

        private static BuildSnapshot Snapshot(
            string snapshotId,
            string archetypeId,
            int execution,
            params ItemInstance[] items)
        {
            var snapshot = new BuildSnapshot
            {
                SnapshotId = snapshotId,
                ContentVersion = ContentVersion,
                ArchetypeId = archetypeId,
                InitialExecution = execution,
            };
            snapshot.Items.AddRange(items);
            return snapshot;
        }

        private static ItemInstance Item(
            string instanceId,
            string definitionId,
            int anchorSlot,
            string annotationId = "")
        {
            return new ItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                AnchorSlot = anchorSlot,
                AnnotationId = annotationId,
                Quality = (int)BuqiQuality.Normal,
            };
        }

        private static BuqiItemDefinition Definition(
            string definitionId,
            int size,
            int cooldownTicks,
            params BuqiEffectSpec[] effects)
        {
            var definition = new BuqiItemDefinition
            {
                DefinitionId = definitionId,
                Size = size,
                BaseCooldownTicks = cooldownTicks,
            };
            definition.Effects.AddRange(effects);
            return definition;
        }

        private static BuqiEffectSpec Effect(
            BuqiTrigger trigger,
            BuqiEffect effect,
            BuqiTarget target,
            int amount,
            string reasonCode,
            int durationTicks = 30)
        {
            return new BuqiEffectSpec
            {
                Trigger = trigger,
                Effect = effect,
                Target = target,
                Amount = amount,
                ReasonCode = reasonCode,
                DurationTicks = durationTicks,
            };
        }

        private static BuqiEffectSpec ChargedEffect(
            BuqiTrigger trigger,
            int baseAmount,
            int amountPerCharge,
            int chargeReadLimit,
            bool consume,
            string reasonCode)
        {
            BuqiEffectSpec spec = Effect(
                trigger,
                BuqiEffect.Damage,
                BuqiTarget.EnemyExecution,
                baseAmount,
                reasonCode);
            spec.AmountPerCharge = amountPerCharge;
            spec.ChargeReadLimit = chargeReadLimit;
            spec.ChargeConsume = consume;
            return spec;
        }

        private static BuqiEffectSpec ConditionEffect(
            BuqiConditionKind condition,
            int threshold,
            BuqiEffect effect,
            BuqiTarget target,
            int amount,
            string reasonCode)
        {
            BuqiEffectSpec spec = Effect(
                BuqiTrigger.OnFirstConditionMet,
                effect,
                target,
                amount,
                reasonCode);
            spec.ConditionKind = condition;
            spec.ConditionThreshold = threshold;
            return spec;
        }

        private static HashSet<string> CreateInstanceSet(BuildSnapshot snapshot)
        {
            var instances = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemInstance item in snapshot.Items)
                instances.Add(item.InstanceId);
            return instances;
        }

        private static string SizeLabel(int size)
        {
            if (size == (int)BuqiSize.L)
                return "L";
            if (size == (int)BuqiSize.M)
                return "M";
            return "S";
        }

        private static bool ContainsOrdinal(string source, string filter)
        {
            return string.IsNullOrEmpty(filter) ||
                   (!string.IsNullOrEmpty(source) && source.IndexOf(filter, StringComparison.Ordinal) >= 0);
        }
    }
}
#endif
