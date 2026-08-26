using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>
    /// 单条法门效果定义：由触发、基础效果、目标、数值和可选条件组成。
    /// 真实内容后续由 Luban 转换为不可变运行时定义；Step 1 使用代码夹具验证规则。
    /// </summary>
    public sealed class BuqiEffectSpec
    {
        /// <summary>触发时机。</summary>
        public BuqiTrigger Trigger = BuqiTrigger.OnUse;

        /// <summary>六类基础效果之一。</summary>
        public BuqiEffect Effect = BuqiEffect.Damage;

        /// <summary>目标选择器；战斗目标不使用随机。</summary>
        public BuqiTarget Target = BuqiTarget.EnemyExecution;

        /// <summary>基础数值；伤害、护体、冷却推进和资源变化均使用整数。</summary>
        public int Amount;

        /// <summary>加速或延迟持续 tick；其他效果忽略该字段。</summary>
        public int DurationTicks = 30;

        /// <summary>稳定原因码，用于战报统计和契约测试，不作为玩家显示文本。</summary>
        public string ReasonCode = string.Empty;

        /// <summary>OnFirstConditionMet 使用的条件种类。</summary>
        public BuqiConditionKind ConditionKind = BuqiConditionKind.None;

        /// <summary>OnFirstConditionMet 的条件阈值。</summary>
        public int ConditionThreshold;

        /// <summary>OnUseCountReached 的主动使用次数阈值。</summary>
        public int UseCountThreshold;

        /// <summary>达到次数阈值后是否扣除该阈值，允许继续循环累计。</summary>
        public bool ResetCountOnReached = true;

        /// <summary>确定性暴击率，单位为万分比；命中后效果量固定翻倍。</summary>
        public int CriticalChanceBps;

        /// <summary>一次触发的独立结算次数；每次结算均占用现有事件预算。</summary>
        public int RepeatCount = 1;

        /// <summary>触发狂怒所需的怒气阈值。</summary>
        public int RageThreshold = 100;

        public int RageDurationTicks = 50;

        public int RageCooldownReductionBps = 1000;

        /// <summary>该 Flight 效果生效期间提供的额外 Damage 万分比。</summary>
        public int FlightDamageBonusBps;

        /// <summary>该 Flight 状态结束时对自身造成的普通伤害。</summary>
        public int FlightEndDamage;

        /// <summary>
        /// 生成跨端稳定的效果语义标识，用于日志、排序和哈希。
        /// 所有会改变声明结果或触发条件的字段都必须纳入，避免不同配置落到同一标识。
        /// </summary>
        public string GetEffectId()
        {
            string identity = BuqiText.Format(
                "{0}:{1}:{2}:{3}:{4}:{5}:{6}",
                Trigger,
                Effect,
                Target,
                Amount,
                DurationTicks,
                ConditionKind,
                ConditionThreshold);
            string behavior = BuqiText.Format(
                "{0}:{1}",
                UseCountThreshold,
                ResetCountOnReached);
            string legacyId = BuqiText.Format("{0}:{1}", identity, behavior);
            if (CriticalChanceBps == 0 &&
                RepeatCount == 1 &&
                RageThreshold == 100 &&
                RageDurationTicks == 50 &&
                RageCooldownReductionBps == 1000 &&
                FlightDamageBonusBps == 0 &&
                FlightEndDamage == 0)
            {
                return legacyId;
            }
            string extendedId = BuqiText.Format(
                "{0}:v3:{1}:{2}:{3}:{4}:{5}:{6}",
                legacyId,
                CriticalChanceBps,
                RepeatCount,
                RageThreshold,
                RageDurationTicks,
                RageCooldownReductionBps,
                FlightDamageBonusBps);
            return BuqiText.Format("{0}:{1}", extendedId, FlightEndDamage);
        }
    }

    public sealed class BuqiBattleRuleConfig
    {
        public int StormStartTicks = 300;
        public int StormBaseDamage = 1;
        public int StormRampDamage = 1;

        public BuqiBattleRuleConfig Clone()
        {
            return new BuqiBattleRuleConfig
            {
                StormStartTicks = StormStartTicks,
                StormBaseDamage = StormBaseDamage,
                StormRampDamage = StormRampDamage,
            };
        }
    }

    public interface IBuqiBattleRuleProvider
    {
        BuqiBattleRuleConfig BattleRules { get; }
    }

    /// <summary>单张法门的内容定义。</summary>
    public sealed class BuqiItemDefinition
    {
        /// <summary>配置定义 ID。</summary>
        public string DefinitionId = string.Empty;

        /// <summary>占用格数：S/M/L 分别为 1/2/3。</summary>
        public int Size = (int)BuqiSize.S;

        /// <summary>基础冷却 tick；进入运行态后再应用 A-01/A-02。</summary>
        public int BaseCooldownTicks = 30;

        /// <summary>主动使用弹药上限；0 表示无限弹药。</summary>
        public int AmmoCapacity;

        /// <summary>由基础原语组合的效果列表，不允许在模拟器中写卡牌特例。</summary>
        public List<BuqiEffectSpec> Effects = new List<BuqiEffectSpec>();
    }

    /// <summary>
    /// 内容定义访问边界。模拟器只依赖该纯 C# 接口，不直接依赖 Luban、Unity 资源或热更生命周期。
    /// </summary>
    public interface IItemDefinitionProvider
    {
        /// <summary>当前定义集合对应的内容版本。</summary>
        string ContentVersion { get; }

        /// <summary>按定义 ID 查询法门配置。</summary>
        bool TryGet(string definitionId, out BuqiItemDefinition definition);
    }

    /// <summary>测试和工具使用的内存字典定义提供器。</summary>
    public sealed class DictionaryDefinitionProvider : IItemDefinitionProvider, IBuqiBattleRuleProvider
    {
        private readonly Dictionary<string, BuqiItemDefinition> m_Definitions;

        public DictionaryDefinitionProvider(
            string contentVersion,
            Dictionary<string, BuqiItemDefinition> definitions,
            BuqiBattleRuleConfig battleRules = null)
        {
            ContentVersion = contentVersion;
            m_Definitions = definitions;
            BattleRules = (battleRules ?? new BuqiBattleRuleConfig()).Clone();
        }

        public string ContentVersion { get; }

        public BuqiBattleRuleConfig BattleRules { get; }

        public bool TryGet(string definitionId, out BuqiItemDefinition definition)
        {
            return m_Definitions.TryGetValue(definitionId, out definition);
        }
    }
}
