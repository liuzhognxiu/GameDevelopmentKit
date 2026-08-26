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
        public const string FixtureContentVersion = "buqi-test-cv2";

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
                ["charge-advance"] = CreateDefinition("charge-advance", 1, 10,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Charge, BuqiTarget.Self, 2, "charge-advance")),
                ["noise"] = CreateDefinition("noise", 1, 30,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Noise, BuqiTarget.EnemyExecution, 21, "noise")),
                ["heal"] = CreateDefinition("heal", 1, 40,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Heal, BuqiTarget.Self, 12, "heal")),
                ["regen"] = CreateDefinition("regen", 1, 40,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Regen, BuqiTarget.Self, 3, "regen", 30)),
                ["poison"] = CreateDefinition("poison", 1, 40,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Poison, BuqiTarget.EnemyExecution, 4, "poison", 30)),
                ["burn"] = CreateDefinition("burn", 1, 40,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Burn, BuqiTarget.EnemyExecution, 5, "burn", 30)),
                ["freeze"] = CreateDefinition("freeze", 1, 40,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Freeze, BuqiTarget.ShortestCooldownEnemyItem, 10, "freeze", 10)),
                ["critical"] = CreateDefinition("critical", 1, 30,
                    CriticalEffect(10, 20000, "critical-strike")),
                ["critical-overflow"] = CreateDefinition("critical-overflow", 1, 30,
                    CriticalEffect(100, 100000, "critical-overflow", BuqiTrigger.OnUse)),
                ["saturated-flight"] = CreateDefinition("saturated-flight", 1, 10,
                    FlightEffect(BuqiTrigger.OnBattleStart, 1, 100, 5000, 0, "saturated-flight-enter"),
                    CriticalEffect(int.MaxValue, 20000, "saturated-flight-strike", BuqiTrigger.OnUse)),
                ["saturated-multi"] = CreateDefinition("saturated-multi", 1, 300,
                    RepeatedEffect(
                        BuqiTrigger.OnBattleStart, BuqiEffect.Damage, BuqiTarget.EnemyExecution,
                        int.MaxValue, 2, "saturated-multi")),
                ["multi-ammo"] = CreateAmmoDefinition("multi-ammo", 1, 10, 2,
                    RepeatedEffect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 2, 3, "multi-strike")),
                ["multi-cap"] = CreateDefinition("multi-cap", 1, 300,
                    RepeatedEffect(BuqiTrigger.OnBattleStart, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 1, 5, "multi-cap")),
                ["ammo-limited"] = CreateAmmoDefinition("ammo-limited", 1, 10, 1,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 4, "ammo-shot")),
                ["ammo-capped"] = CreateAmmoDefinition("ammo-capped", 1, 20, 1,
                    ConditionEffect(BuqiConditionKind.BufferLost, 0, 1, "ammo-cap-1"),
                    ConditionEffect(BuqiConditionKind.BufferLost, 0, 1, "ammo-cap-2"),
                    ConditionEffect(BuqiConditionKind.BufferLost, 0, 1, "ammo-cap-3"),
                    ConditionEffect(BuqiConditionKind.BufferLost, 0, 1, "ammo-cap-4"),
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 1, "ammo-cap-shot")),
                ["buffer-breaker"] = CreateDefinition("buffer-breaker", 1, 19,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 1, "buffer-break")),
                ["ammo-refill"] = CreateDefinition("ammo-refill", 1, 30,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Ammo, BuqiTarget.LeftAdjacentItem, 1, "ammo-refill")),
                ["flight"] = CreateDefinition("flight", 1, 10,
                    FlightEffect(BuqiTrigger.OnBattleStart, 1, 11, 5000, 7, "flight-enter"),
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Damage, BuqiTarget.EnemyExecution, 10, "flight-strike")),
                ["flight-leave"] = CreateDefinition("flight-leave", 1, 10,
                    FlightEffect(BuqiTrigger.OnBattleStart, 1, 100, 0, 7, "flight-enter"),
                    FlightEffect(BuqiTrigger.OnUse, -1, 0, 0, 0, "flight-leave")),
                ["flight-leave-repeat"] = CreateDefinition("flight-leave-repeat", 1, 10,
                    FlightEffect(BuqiTrigger.OnBattleStart, 1, 100, 0, 7, "flight-enter"),
                    RepeatedFlightLeave(2, "flight-leave-repeat")),
                ["flight-long"] = CreateDefinition("flight-long", 1, 300,
                    FlightEffect(BuqiTrigger.OnBattleStart, 1, 100, 0, 0, "flight-enter")),
                ["flight-source-strong"] = CreateDefinition("flight-source-strong", 1, 300,
                    FlightEffect(BuqiTrigger.OnBattleStart, 1, 20, 1000, 9, "flight-strong")),
                ["flight-source-weak"] = CreateAmmoDefinition("flight-source-weak", 1, 10, 1,
                    FlightEffect(BuqiTrigger.OnUse, 1, 5, 0, 1, "flight-weak")),
                ["delay-odd"] = CreateDefinition("delay-odd", 1, 30,
                    Effect(BuqiTrigger.OnUse, BuqiEffect.Delay, BuqiTarget.EnemyExecution, 2000, "delay-odd", 9)),
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
        /// 创建 15 个版本化向量，覆盖确定性、S/M/L 占位、空格阻断、同 tick 聚合、淬炼、沙暴和非法构筑。
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
                Vector("charge-advance",
                    Snapshot("L", 1000, 0, Item("charge", "charge-advance", 0)),
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
                Vector("storm",
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
        /// 以固定种子生成指定数量的不同合法构筑，用于循环、异常和性能压力验证。
        /// 该样本不代表平衡分布，也不得用于胜率结论；放置时按定义真实尺寸筛选可用候选。
        /// </summary>
        public static List<BuildSnapshot> GenerateStressBuilds(int count, int seed)
        {
            var random = new DeterministicRandom(seed);
            string[] definitionIds =
            {
                "damage", "buffer", "large", "medium", "haste", "delay", "charge-advance",
                "noise", "heal", "regen", "poison", "burn", "freeze",
                "adjacent-source", "adjacent-response", "use-count", "battle-start-buffer",
            };
            var definitionSizes = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["damage"] = 1, ["buffer"] = 1, ["large"] = 3, ["medium"] = 2,
                ["haste"] = 1, ["delay"] = 1, ["charge-advance"] = 1, ["noise"] = 1,
                ["heal"] = 1, ["regen"] = 1, ["poison"] = 1, ["burn"] = 1, ["freeze"] = 1,
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

        private static BuqiEffectSpec CriticalEffect(
            int amount,
            int multiplierBps,
            string reason,
            BuqiTrigger trigger = BuqiTrigger.OnBattleStart)
        {
            BuqiEffectSpec spec = Effect(
                trigger,
                BuqiEffect.Damage,
                BuqiTarget.EnemyExecution,
                amount,
                reason);
            spec.CriticalChanceBps = multiplierBps > 0 ? 10000 : 0;
            return spec;
        }

        private static BuqiEffectSpec RepeatedEffect(
            BuqiTrigger trigger,
            BuqiEffect effect,
            BuqiTarget target,
            int amount,
            int repeatCount,
            string reason)
        {
            BuqiEffectSpec spec = Effect(trigger, effect, target, amount, reason);
            spec.RepeatCount = repeatCount;
            return spec;
        }

        private static BuqiEffectSpec FlightEffect(
            BuqiTrigger trigger,
            int action,
            int durationTicks,
            int damageBonusBps,
            int endDamage,
            string reason)
        {
            BuqiEffectSpec spec = Effect(
                trigger,
                BuqiEffect.Flight,
                BuqiTarget.Self,
                action,
                reason,
                durationTicks);
            spec.FlightDamageBonusBps = damageBonusBps;
            spec.FlightEndDamage = endDamage;
            return spec;
        }

        private static BuqiEffectSpec RepeatedFlightLeave(int repeatCount, string reason)
        {
            BuqiEffectSpec spec = FlightEffect(
                BuqiTrigger.OnUse, -1, 0, 0, 0, reason);
            spec.RepeatCount = repeatCount;
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

        private static BuqiItemDefinition CreateAmmoDefinition(
            string id,
            int size,
            int cooldownTicks,
            int ammoCapacity,
            params BuqiEffectSpec[] effects)
        {
            BuqiItemDefinition definition = CreateDefinition(id, size, cooldownTicks, effects);
            definition.AmmoCapacity = ammoCapacity;
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
