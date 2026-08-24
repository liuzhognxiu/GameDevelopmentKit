namespace Game.Hot.Buqi.Battle
{
    /// <summary>法门占位尺寸；数值直接等于占用格数。</summary>
    public enum BuqiSize : int { S = 1, M = 2, L = 3 }

    /// <summary>法门品质：普通、改良、定型。</summary>
    public enum BuqiQuality : int { Normal = 1, Improved = 2, Fixed = 3 }

    /// <summary>首阶段批准的六种触发模板。</summary>
    public enum BuqiTrigger : int
    {
        /// <summary>冷却到期后的主动使用。</summary>
        OnUse = 0,

        /// <summary>tick 0 首次冷却推进前触发。</summary>
        OnBattleStart = 1,

        /// <summary>只响应紧邻法门的主动 OnUse。</summary>
        OnAdjacentUse = 2,

        /// <summary>条件首次由 false 变为 true 时触发。</summary>
        OnFirstConditionMet = 3,

        /// <summary>主动使用次数达到阈值时触发。</summary>
        OnUseCountReached = 4,

        /// <summary>首次受到有效敌方延迟时触发。</summary>
        OnFirstInterfered = 5,
    }

    /// <summary>首阶段唯一允许的六类基础效果。</summary>
    public enum BuqiEffect : int
    {
        Damage = 0,
        Buffer = 1,
        Haste = 2,
        Delay = 3,
        Charge = 4,
        Noise = 5,
        Heal = 6,
        Regen = 7,
        Poison = 8,
        Burn = 9,
        Freeze = 10,
        Ammo = 11,
        Flight = 12,
        Rage = 13,
    }

    /// <summary>确定性目标选择器；v0.6 仍不允许随机目标。</summary>
    public enum BuqiTarget : int
    {
        EnemyExecution = 0,
        Self = 1,
        LeftAdjacentItem = 2,
        RightAdjacentItem = 3,
        AllAdjacentItems = 4,
        ShortestCooldownEnemyItem = 5,
        LongestCooldownEnemyItem = 6,
        LeftmostEnemyItem = 7,
        RightmostEnemyItem = 8,
    }

    /// <summary>单 tick 严格阶段顺序；枚举序号参与事件稳定排序。</summary>
    public enum BuqiEventPhase : int
    {
        PreTick = 0,
        Declare = 1,
        Resolve = 2,
        Chain = 3,
        Aggregate = 4,
        PostTick = 5,
    }

    /// <summary>战报事件类型，用于区分声明、生效、空转、免疫与截断。</summary>
    public enum BuqiEventType : int
    {
        Declare = 0,
        Effect = 1,
        Truncate = 2,
        NoTarget = 3,
        Immune = 4,
        Invalid = 5,
    }

    /// <summary>战斗结果。</summary>
    public enum BattleOutcome : int
    {
        LeftWin = 0,
        RightWin = 1,
        Draw = 2,
        InvalidBuild = 3,
        Aborted = 4,
    }

    /// <summary>战斗结束原因。</summary>
    public enum TerminationReason : int
    {
        Normal = 0,
        Overtime = 1,
        HardCap = 2,
        InvalidBuild = 3,
        Aborted = 4,
        Storm = 5,
    }

    /// <summary>首次条件触发使用的条件种类。</summary>
    public enum BuqiConditionKind : int
    {
        None = 0,
        BufferLost = 1,
        ChargeAtLeast = 2,
    }
}
