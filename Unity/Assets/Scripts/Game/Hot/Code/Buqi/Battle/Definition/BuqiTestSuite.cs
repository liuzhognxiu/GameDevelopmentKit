#if UNITY_EDITOR || BUQI_HEADLESS
using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>一个具有稳定 ID 的版本化战斗契约向量。</summary>
    public sealed class BuqiTestVector
    {
        /// <summary>approved hash 文件中的稳定键。</summary>
        public string Id = string.Empty;

        /// <summary>该向量的完整双边战斗请求。</summary>
        public BattleRequest Request;
    }

    /// <summary>
    /// Unity EditMode 与 .NET 无头端共享的最小测试内容、契约向量和压力构筑生成器。
    /// 该夹具只在 UNITY_EDITOR 或 BUQI_HEADLESS 下编译，不进入正式 Player 内容。
    /// </summary>
    public static class BuqiTestSuite
    {
        /// <summary>测试定义与快照共同使用的内容版本。</summary>
        public const string FixtureContentVersion = "buqi-test-cv1";

        /// <summary>创建覆盖六效果、六触发、相邻关系和事件上限的代码内测试定义。</summary>
        public static IItemDefinitionProvider CreateFixtureProvider()
        {
            var definitions = new Dictionary<string, BuqiItemDefinition>(StringComparer.Ordinal)
            {
                ["damage"] = CreateDefinition("damage", 1, 30,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 10, "strike")),
                ["buffer"] = CreateDefinition("buffer", 1, 30,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Buffer, BuqiTarget.Self, 20, "shield")),
                ["large"] = CreateDefinition("large", 3, 60,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 25, "large-strike")),
                ["medium"] = CreateDefinition("medium", 2, 45,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 18, "medium-strike")),
                ["haste"] = CreateDefinition("haste", 1, 30,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Haste, BuqiTarget.Self, 2000, "haste", 30)),
                ["delay"] = CreateDefinition("delay", 1, 30,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Delay, BuqiTarget.EnemyExecution, 2000, "delay", 30)),
                ["charge"] = CreateDefinition("charge", 1, 10,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Charge, BuqiTarget.Self, 2, "charge")),
                ["opening-charge-source"] = CreateDefinition("opening-charge-source", 1, 300,
                    Effect(BuqiTrigger.OnBattleStart, BuqiEffect.Charge, BuqiTarget.RightAdjacentItem, 3, "opening-charge")),
                ["charge-consumer"] = CreateDefinition("charge-consumer", 1, 300,
                    ChargedEffect(BuqiTrigger.OnBattleStart, 1, 2, 3, true, "charge-consume-a"),
                    ChargedEffect(BuqiTrigger.OnBattleStart, 1, 2, 3, true, "charge-consume-b")),
                ["charge-reader"] = CreateDefinition("charge-reader", 1, 300,
                    ChargedEffect(BuqiTrigger.OnBattleStart, 1, 2, 3, false, "charge-read-a"),
                    ChargedEffect(BuqiTrigger.OnBattleStart, 1, 2, 3, false, "charge-read-b")),
                ["charge-rewrite"] = CreateDefinition("charge-rewrite", 1, 30,
                    ChargedEffect(BuqiTrigger.OnUse, 2, 2, 3, true, "charge-rewrite")),
                ["charge-no-target"] = CreateDefinition("charge-no-target", 1, 300,
                    ChargedEffect(
                        BuqiTrigger.OnBattleStart,
                        BuqiTarget.RightAdjacentItem,
                        1,
                        2,
                        3,
                        true,
                        "charge-no-target")),
                ["noise"] = CreateDefinition("noise", 1, 30,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Noise, BuqiTarget.EnemyExecution, 21, "noise")),
                ["adjacent-source"] = CreateDefinition("adjacent-source", 1, 30,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 8, "adjacent-source")),
                ["adjacent-response"] = CreateDefinition("adjacent-response", 1, 300,
                    Effect(BuqiTrigger.OnAdjacentUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 6, "adjacent-response")),
                ["use-count"] = CreateDefinition("use-count", 1, 10,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 2, "count-use"),
                    CountEffect(3, 9, "count-burst")),
                ["battle-start-buffer"] = CreateDefinition("battle-start-buffer", 1, 300,
                    Effect(BuqiTrigger.OnBattleStart, BuqiEffect.Buffer, BuqiTarget.Self, 20, "opening-buffer")),
                ["battle-start-damage"] = CreateDefinition("battle-start-damage", 1, 300,
                    Effect(BuqiTrigger.OnBattleStart, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 15, "opening-damage")),
                ["interfered-response"] = CreateDefinition("interfered-response", 1, 300,
                    Effect(BuqiTrigger.OnFirstInterfered, BuqiEffect.Buffer, BuqiTarget.Self, 7, "interfered-response")),
                ["buffer-counter"] = CreateDefinition("buffer-counter", 1, 20,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Buffer, BuqiTarget.Self, 10, "counter-buffer"),
                    ConditionEffect(BuqiConditionKind.BufferLost, 0, 15, "buffer-lost-counter")),
                ["loop-cap"] = CreateDefinition("loop-cap", 1, 300,
                    Effect(BuqiTrigger.OnBattleStart, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 1, "cap-1"),
                    Effect(BuqiTrigger.OnBattleStart, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 1, "cap-2"),
                    Effect(BuqiTrigger.OnBattleStart, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 1, "cap-3"),
                    Effect(BuqiTrigger.OnBattleStart, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 1, "cap-4"),
                    Effect(BuqiTrigger.OnBattleStart, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 1, "cap-5")),
                ["passive"] = CreateDefinition("passive", 1, 300),
            };
            return new DictionaryDefinitionProvider(FixtureContentVersion, definitions);
        }

        /// <summary>
        /// 创建 15 个版本化向量，覆盖确定性、S/M/L 占位、空格阻断、同 tick 聚合、淬炼、劫火和非法构筑。
        /// 向量 ID 是批准哈希协议的一部分，重命名等同于变更测试基线。
        /// </summary>
        public static List<BuqiTestVector> CreateVectors()
        {
            return new List<BuqiTestVector>
            {
                Vector("determinism-basic",
                    Snapshot("L", 100, 0, Item("l0", "damage", 0), Item("l1", "buffer", 1)),
                    Snapshot("R", 100, 0, Item("r0", "damage", 0), Item("r1", "buffer", 1))),
                Vector("mirror",
                    Snapshot("L", 100, 0, Item("l0", "damage", 0), Item("l1", "buffer", 1)),
                    Snapshot("R", 100, 0, Item("r0", "large", 0))),
                Vector("sml-layout",
                    Snapshot("L", 100, 0, Item("l0", "large", 0), Item("l1", "medium", 3), Item("l2", "damage", 5)),
                    Snapshot("R", 100, 0, Item("r0", "damage", 0))),
                Vector("gap-blocks-adjacent",
                    Snapshot("L", 100, 0, Item("source", "adjacent-source", 0), Item("response", "adjacent-response", 2)),
                    Snapshot("R", 100, 0, Item("target", "passive", 0))),
                Vector("adjacency-chain",
                    Snapshot("L", 100, 0, Item("source", "adjacent-source", 0), Item("response", "adjacent-response", 1)),
                    Snapshot("R", 100, 0, Item("target", "passive", 0))),
                Vector("same-tick-buffer",
                    Snapshot("L", 100, 0, Item("shield", "battle-start-buffer", 0)),
                    Snapshot("R", 100, 0, Item("strike", "battle-start-damage", 0))),
                Vector("noise-threshold",
                    Snapshot("L", 1000, 0, Item("noise", "noise", 0)),
                    Snapshot("R", 1000, 0, Item("target", "passive", 0))),
                Vector("charge-cap",
                    Snapshot("L", 1000, 0, Item("charge", "charge", 0)),
                    Snapshot("R", 1000, 0, Item("target", "passive", 0))),
                Vector("reliable",
                    Snapshot("L", 100, 0, Item("delay", "delay", 0)),
                    Snapshot("R", 100, 0, Item("reliable", "interfered-response", 0, "A-04"))),
                Vector("rewrite",
                    Snapshot("L", 100, 0, Item("rewrite", "adjacent-source", 0, "A-03"), Item("response", "adjacent-response", 1)),
                    Snapshot("R", 100, 0, Item("target", "passive", 0))),
                Vector("use-count",
                    Snapshot("L", 1000, 0, Item("counter", "use-count", 0)),
                    Snapshot("R", 1000, 0, Item("target", "passive", 0))),
                Vector("loop-cap",
                    Snapshot("L", 1000, 0, Item("cap", "loop-cap", 0)),
                    Snapshot("R", 1000, 0, Item("target", "passive", 0))),
                Vector("overtime",
                    Snapshot("L", 2, 0, Item("left", "passive", 0)),
                    Snapshot("R", 2, 0, Item("right", "passive", 0))),
                Vector("illegal-overlap",
                    Snapshot("L", 100, 0, Item("small", "damage", 0), Item("large", "large", 0)),
                    Snapshot("R", 100, 0, Item("target", "damage", 0))),
                Vector("illegal-out-of-bounds",
                    Snapshot("L", 100, 0, Item("large", "large", 6)),
                    Snapshot("R", 100, 0, Item("target", "damage", 0))),
            };
        }

        public static BuqiTestVector FindVector(List<BuqiTestVector> vectors, string id)
        {
            foreach (BuqiTestVector vector in vectors)
            {
                if (vector.Id == id)
                    return vector;
            }
            return null;
        }

        public static BuildSnapshot Snapshot(
            string id,
            int execution,
            int buffer,
            params ItemInstance[] items)
        {
            var snapshot = new BuildSnapshot
            {
                SnapshotId = id,
                ContentVersion = FixtureContentVersion,
                ArchetypeId = "test",
                InitialExecution = execution,
                InitialBuffer = buffer,
            };
            snapshot.Items.AddRange(items);
            return snapshot;
        }

        public static ItemInstance Item(string instanceId, string definitionId, int anchorSlot, string annotationId = "")
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

        public static BattleRequest Request(BuildSnapshot left, BuildSnapshot right)
        {
            return new BattleRequest
            {
                RuleVersion = BuqiBattleSimulator.RuleVersion,
                BattleSeed = 0,
                RoundIndex = 0,
                Left = left,
                Right = right,
            };
        }

        /// <summary>
        /// 以固定种子生成指定数量的不同合法 8 格构筑，用于循环、异常和性能压力验证。
        /// 该样本不代表平衡分布，也不得用于胜率结论；放置时按定义真实尺寸筛选可用候选。
        /// </summary>
        public static List<BuildSnapshot> GenerateStressBuilds(int count, int seed)
        {
            var random = new DeterministicRandom(seed);
            string[] definitionIds =
            {
                "damage", "buffer", "large", "medium", "haste", "delay", "charge",
                "noise", "adjacent-source", "adjacent-response", "use-count", "battle-start-buffer",
            };
            var definitionSizes = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["damage"] = 1, ["buffer"] = 1, ["large"] = 3, ["medium"] = 2,
                ["haste"] = 1, ["delay"] = 1, ["charge"] = 1, ["noise"] = 1,
                ["adjacent-source"] = 1, ["adjacent-response"] = 1,
                ["use-count"] = 1, ["battle-start-buffer"] = 1,
            };
            var result = new List<BuildSnapshot>(count);
            for (int buildIndex = 0; buildIndex < count; buildIndex++)
            {
                var snapshot = Snapshot(BuqiText.Format("stress-{0}", buildIndex), 80 + random.Range(60), random.Range(20));
                int slot = 0;
                int itemIndex = 0;
                while (slot < BuqiBoardValidator.BoardSlotCount)
                {
                    int remaining = BuqiBoardValidator.BoardSlotCount - slot;
                    var candidates = new List<string>();
                    foreach (string definitionId in definitionIds)
                    {
                        if (definitionSizes[definitionId] <= remaining)
                            candidates.Add(definitionId);
                    }
                    string selected = candidates[random.Range(candidates.Count)];
                    string annotation = string.Empty;
                    int annotationRoll = random.Range(12);
                    if (annotationRoll < 6)
                        annotation = BuqiText.Format("A-0{0}", annotationRoll + 1);
                    ItemInstance item = Item(BuqiText.Format("s{0}-{1}", buildIndex, itemIndex), selected, slot, annotation);
                    item.Quality = 1 + random.Range(3);
                    snapshot.Items.Add(item);
                    slot += definitionSizes[selected];
                    itemIndex++;
                }
                result.Add(snapshot);
            }
            return result;
        }

        private static BuqiTestVector Vector(string id, BuildSnapshot left, BuildSnapshot right)
        {
            return new BuqiTestVector { Id = id, Request = Request(left, right) };
        }

        private static BuqiEffectSpec Effect(
            BuqiTrigger trigger,
            BuqiEffect effect,
            BuqiTarget target,
            int amount,
            string reason,
            int durationTicks = 30)
        {
            return new BuqiEffectSpec
            {
                Trigger = trigger,
                Effect = effect,
                Target = target,
                Amount = amount,
                DurationTicks = durationTicks,
                ReasonCode = reason,
            };
        }

        private static BuqiEffectSpec ChargedEffect(
            BuqiTrigger trigger,
            int baseAmount,
            int amountPerCharge,
            int chargeReadLimit,
            bool consume,
            string reason)
        {
            return ChargedEffect(
                trigger,
                BuqiTarget.EnemyExecution,
                baseAmount,
                amountPerCharge,
                chargeReadLimit,
                consume,
                reason);
        }

        private static BuqiEffectSpec ChargedEffect(
            BuqiTrigger trigger,
            BuqiTarget target,
            int baseAmount,
            int amountPerCharge,
            int chargeReadLimit,
            bool consume,
            string reason)
        {
            BuqiEffectSpec spec = Effect(
                trigger,
                BuqiEffect.Damage,
                target,
                baseAmount,
                reason);
            spec.AmountPerCharge = amountPerCharge;
            spec.ChargeReadLimit = chargeReadLimit;
            spec.ChargeConsume = consume;
            return spec;
        }

        private static BuqiEffectSpec CountEffect(int threshold, int amount, string reason)
        {
            BuqiEffectSpec spec = Effect(
                BuqiTrigger.OnUseCountReached,
                BuqiEffect.Damage,
                BuqiTarget.EnemyExecution,
                amount,
                reason);
            spec.UseCountThreshold = threshold;
            return spec;
        }

        private static BuqiEffectSpec ConditionEffect(
            BuqiConditionKind condition,
            int threshold,
            int amount,
            string reason)
        {
            BuqiEffectSpec spec = Effect(
                BuqiTrigger.OnFirstConditionMet,
                BuqiEffect.Damage,
                BuqiTarget.EnemyExecution,
                amount,
                reason);
            spec.ConditionKind = condition;
            spec.ConditionThreshold = threshold;
            return spec;
        }

        private static BuqiItemDefinition CreateDefinition(
            string id,
            int size,
            int cooldownTicks,
            params BuqiEffectSpec[] effects)
        {
            var definition = new BuqiItemDefinition
            {
                DefinitionId = id,
                Size = size,
                BaseCooldownTicks = cooldownTicks,
            };
            definition.Effects.AddRange(effects);
            return definition;
        }

        /// <summary>
        /// 测试夹具专用确定性伪随机数；不依赖 System.Random 的实现版本，也不进入正式战斗随机流。
        /// </summary>
        private sealed class DeterministicRandom
        {
            private ulong m_State;

            public DeterministicRandom(int seed)
            {
                m_State = ((ulong)(uint)seed) ^ 0x9E3779B97F4A7C15UL;
            }

            public int Range(int maximum)
            {
                m_State = m_State * 6364136223846793005UL + 1442695040888963407UL;
                uint value = (uint)(m_State >> 32);
                value ^= value >> 15;
                value *= 0x2C1B3C6D;
                value ^= value >> 12;
                return (int)(value % (uint)maximum);
            }
        }
    }
}
#endif
