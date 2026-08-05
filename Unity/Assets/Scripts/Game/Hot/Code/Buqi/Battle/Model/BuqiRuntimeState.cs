using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>战斗中的限时加速/延迟状态。</summary>
    public sealed class TimedModifier
    {
        /// <summary>只允许 Haste 或 Delay。</summary>
        public BuqiEffect Effect;

        /// <summary>推进幅度，单位为 basis points。</summary>
        public int Bps;

        /// <summary>剩余有效 tick。</summary>
        public int RemainingTicks;

        /// <summary>修正来源实例 ID，用于同来源刷新、不叠加。</summary>
        public string SourceInstanceId = string.Empty;

        /// <summary>是否由敌方施加；A-04 可靠据此免疫敌方 Delay。</summary>
        public bool FromEnemy;
    }

    /// <summary>
    /// 单张法门的运行时状态。战斗结束后丢弃，不回写战前快照。
    /// </summary>
    public sealed class ItemState
    {
        /// <summary>实例唯一 ID。</summary>
        public string InstanceId = string.Empty;

        /// <summary>对应内容定义 ID。</summary>
        public string DefinitionId = string.Empty;

        /// <summary>用于 A-01..A-06 运行时语义。</summary>
        public string AnnotationId = string.Empty;

        /// <summary>品质等级，决定效果倍率。</summary>
        public int Quality;

        /// <summary>最左占位格。</summary>
        public int AnchorSlot;

        /// <summary>从内容定义缓存的占位尺寸。</summary>
        public int Size;

        /// <summary>应用 A-01/A-02 后的最终基础冷却 tick。</summary>
        public int EffectiveBaseCooldownTicks;

        /// <summary>剩余冷却进度，单位为 1/10000 tick。</summary>
        public int CooldownProgress;

        /// <summary>本 tick 是否在 PreTick 后到期，只允许声明一次主动使用。</summary>
        public bool ReadyThisTick;

        /// <summary>实例蓄力，严格限制在 0..9。</summary>
        public int Charge;

        /// <summary>本场主动 OnUse 次数累计。</summary>
        public int OwnUseCount;

        /// <summary>被相邻主动使用带起的响应次数统计。</summary>
        public int AdjacentUseCount;

        /// <summary>首次条件已经触发，后续不再重复触发。</summary>
        public bool FirstConditionUsed;

        /// <summary>首次有效敌方干扰已经触发，后续不再重复触发。</summary>
        public bool FirstInterferedUsed;

        /// <summary>A-03 复写已经消耗，本场不可再次复制。</summary>
        public bool RewriteUsed;

        /// <summary>挂在实例上的限时加速/延迟。</summary>
        public List<TimedModifier> Modifiers = new List<TimedModifier>();
    }

    /// <summary>
    /// 一方阵营的运行时状态。执行值、护体和失衡属于阵营；蓄力和触发计数属于实例。
    /// </summary>
    public sealed class SideState
    {
        /// <summary>执行值，降至 0 或以下即在 PostTick 判定失败。</summary>
        public int Execution;

        /// <summary>护体池，上限为 60；同 tick 新护体先于普通伤害加入。</summary>
        public int Buffer;

        /// <summary>失衡余数，正常保持在 0..9。</summary>
        public int Noise;

        /// <summary>本 tick 聚合后护体从正数降为 0，下一 tick 检查首条件。</summary>
        public bool BufferLostPending;

        /// <summary>本方所有法门实例。</summary>
        public List<ItemState> Items = new List<ItemState>();

        /// <summary>作用于本方冷却推进的阵营级限时修正。</summary>
        public List<TimedModifier> SideModifiers = new List<TimedModifier>();
    }
}
