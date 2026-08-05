using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>
    /// 战前临时修正快照。该数据属于输入 DTO，只描述进入战斗时仍有效的加速或延迟。
    /// </summary>
    public sealed class TemporaryModifier
    {
        /// <summary>修正类型，只允许 Haste 或 Delay。</summary>
        public BuqiEffect Effect;

        /// <summary>产生该修正的法门实例 ID，用于同来源刷新和日志追踪。</summary>
        public string SourceInstanceId = string.Empty;

        /// <summary>剩余有效 tick；1 tick = 100 ms。</summary>
        public int RemainingTicks;

        /// <summary>修正幅度，使用万分比 basis points，例如 2000 表示 20%。</summary>
        public int Bps;
    }

    /// <summary>
    /// 单张法门的战前实例快照。尺寸和基础冷却不写入实例，由 DefinitionId 对应的配置定义提供。
    /// </summary>
    public sealed class ItemInstance
    {
        /// <summary>本场全局唯一的实例 ID，左右双方也不得重复。</summary>
        public string InstanceId = string.Empty;

        /// <summary>法门配置定义 ID。</summary>
        public string DefinitionId = string.Empty;

        /// <summary>品质：1 普通、2 改良、3 定型。</summary>
        public int Quality = (int)BuqiQuality.Normal;

        /// <summary>法门最左占位格，合法范围为 0..7。</summary>
        public int AnchorSlot;

        /// <summary>淬炼批注 ID；空字符串表示无批注，首阶段合法值为 A-01..A-06。</summary>
        public string AnnotationId = string.Empty;

        /// <summary>从战前事件或存档带入的限时修正。</summary>
        public List<TemporaryModifier> TemporaryModifiers = new List<TemporaryModifier>();
    }

    /// <summary>
    /// 单侧确定性构筑快照。快照会按固定字段顺序规范化并计算 SHA-256。
    /// </summary>
    public sealed class BuildSnapshot
    {
        /// <summary>快照业务 ID，用于定位道影、调试样本或战报来源。</summary>
        public string SnapshotId = string.Empty;

        /// <summary>内容配置版本，必须与定义提供器的版本完全一致。</summary>
        public string ContentVersion = string.Empty;

        /// <summary>构筑方向标签；只用于记录和筛选，不参与战斗计算。</summary>
        public string ArchetypeId = string.Empty;

        /// <summary>初始执行值。</summary>
        public int InitialExecution = 100;

        /// <summary>初始护体值。</summary>
        public int InitialBuffer;

        /// <summary>初始失衡债务；进入战斗后按 0..9 余数规则结算事故。</summary>
        public int InitialNoiseDebt;

        /// <summary>棋盘中的法门实例；规范化时按 AnchorSlot、InstanceId 排序。</summary>
        public List<ItemInstance> Items = new List<ItemInstance>();
    }

    /// <summary>
    /// 一场战斗的完整确定性输入。相同输入、规则版本和内容版本必须得到相同结果与日志哈希。
    /// </summary>
    public sealed class BattleRequest
    {
        /// <summary>战斗规则版本；当前必须为 0.4.0。</summary>
        public string RuleVersion = string.Empty;

        /// <summary>预留战斗种子；v0.4 不允许消费随机数，但仍写入日志元数据。</summary>
        public ulong BattleSeed;

        /// <summary>单局轮次索引，仅作为回放和追踪元数据。</summary>
        public int RoundIndex;

        /// <summary>左侧构筑快照。</summary>
        public BuildSnapshot Left;

        /// <summary>右侧构筑快照。</summary>
        public BuildSnapshot Right;
    }
}
