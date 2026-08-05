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

        /// <summary>基础数值；伤害/护体/蓄力/失衡为整数，加速/延迟为 basis points。</summary>
        public int Amount;

        /// <summary>加速或延迟持续 tick；其他效果忽略该字段。</summary>
        public int DurationTicks = 30;

        /// <summary>稳定原因码，用于战报统计和契约测试，不作为玩家显示文本。</summary>
        public string ReasonCode = string.Empty;

        /// <summary>OnFirstConditionMet 使用的条件种类。</summary>
        public BuqiConditionKind ConditionKind = BuqiConditionKind.None;

        /// <summary>条件阈值，例如蓄力至少达到多少。</summary>
        public int ConditionThreshold;

        /// <summary>OnUseCountReached 的主动使用次数阈值。</summary>
        public int UseCountThreshold;

        /// <summary>声明效果时最多读取的自身蓄力；0 表示该效果不读取蓄力。</summary>
        public int ChargeReadLimit;

        /// <summary>每读取 1 点蓄力追加的基础效果量，先与 Amount 相加，再应用品质、淬炼和复写倍率。</summary>
        public int AmountPerCharge;

        /// <summary>是否在声明时消费本次读取的蓄力；只读型效果保持 false。</summary>
        public bool ChargeConsume;

        /// <summary>达到次数阈值后是否扣除该阈值，允许继续循环累计。</summary>
        public bool ResetCountOnReached = true;

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
                "{0}:{1}:{2}:{3}:{4}",
                UseCountThreshold,
                ChargeReadLimit,
                AmountPerCharge,
                ChargeConsume,
                ResetCountOnReached);
            return BuqiText.Format("{0}:{1}", identity, behavior);
        }
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
    public sealed class DictionaryDefinitionProvider : IItemDefinitionProvider
    {
        private readonly Dictionary<string, BuqiItemDefinition> m_Definitions;

        public DictionaryDefinitionProvider(string contentVersion, Dictionary<string, BuqiItemDefinition> definitions)
        {
            ContentVersion = contentVersion;
            m_Definitions = definitions;
        }

        public string ContentVersion { get; }

        public bool TryGet(string definitionId, out BuqiItemDefinition definition)
        {
            return m_Definitions.TryGetValue(definitionId, out definition);
        }
    }
}
