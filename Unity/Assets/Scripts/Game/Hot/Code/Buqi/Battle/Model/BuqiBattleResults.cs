namespace Game.Hot.Buqi.Battle
{
    /// <summary>
    /// 可回放战斗事件。字段顺序和枚举序号属于日志契约的一部分，变更需升级规则版本。
    /// </summary>
    public sealed class BattleEvent
    {
        /// <summary>最终稳定排序后的序号。</summary>
        public int Sequence;

        /// <summary>事件发生的固定步 tick。</summary>
        public int Tick;

        /// <summary>PreTick 到 PostTick 的阶段序号。</summary>
        public BuqiEventPhase Phase;

        /// <summary>连锁深度，越深表示由更深层响应产生。</summary>
        public int ChainDepth;

        /// <summary>定位同一条主动使用及其响应链。</summary>
        public string ChainId = string.Empty;

        /// <summary>声明该事件的法门实例。</summary>
        public string ActorInstanceId = string.Empty;

        /// <summary>真正产生数值贡献的来源实例。</summary>
        public string SourceInstanceId = string.Empty;

        /// <summary>具体目标实例；阵营级目标可为空。</summary>
        public string TargetInstanceId = string.Empty;

        /// <summary>声明、生效、无目标、免疫或截断。</summary>
        public BuqiEventType Type;

        /// <summary>本条事件的实际数值，吸收量、溢出量和事故伤害均单独记录。</summary>
        public int Amount;

        /// <summary>由完整配置语义字段组成的稳定效果 ID。</summary>
        public string EffectId = string.Empty;

        /// <summary>机器可统计的原因码，例如 BufferAbsorb、NoiseAccident。</summary>
        public string ReasonCode = string.Empty;
    }

    /// <summary>战斗终态和可重建战报所需的元数据。</summary>
    public sealed class BattleResult
    {
        /// <summary>规则版本。</summary>
        public string RuleVersion = string.Empty;

        /// <summary>模拟器实现版本；由模拟器创建结果时显式写入。</summary>
        public string SimulationVersion = string.Empty;

        /// <summary>双方构筑使用的内容版本。</summary>
        public string ContentVersion = string.Empty;

        /// <summary>战斗种子；确定性暴击判定会消费其派生随机序列。</summary>
        public ulong BattleSeed;

        /// <summary>单局轮次索引。</summary>
        public int RoundIndex;

        /// <summary>最终胜负。</summary>
        public BattleOutcome Outcome = BattleOutcome.Draw;

        /// <summary>实际运行的基础 tick 数；战斗无固定硬上限。</summary>
        public int DurationTicks;

        public int LeftExecution;
        public int RightExecution;
        public int LeftBuffer;
        public int RightBuffer;
        public int LeftNoise;
        public int RightNoise;

        /// <summary>Normal、Storm 或 InvalidBuild。</summary>
        public string TerminationReason = string.Empty;

        /// <summary>规范化全量结果与事件后的 SHA-256。</summary>
        public string BattleLogHash = string.Empty;

        /// <summary>左侧初始快照 SHA-256。</summary>
        public string LeftSnapshotHash = string.Empty;

        /// <summary>右侧初始快照 SHA-256。</summary>
        public string RightSnapshotHash = string.Empty;
    }
}
