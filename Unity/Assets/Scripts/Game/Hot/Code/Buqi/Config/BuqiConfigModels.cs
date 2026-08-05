using System.Collections.Generic;
using BattleConditionKind = Game.Hot.Buqi.Battle.BuqiConditionKind;
using BattleEffect = Game.Hot.Buqi.Battle.BuqiEffect;
using BattleQuality = Game.Hot.Buqi.Battle.BuqiQuality;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;
using BattleTarget = Game.Hot.Buqi.Battle.BuqiTarget;
using BattleTrigger = Game.Hot.Buqi.Battle.BuqiTrigger;

namespace Game.Hot.Buqi.Config
{
    public sealed class BuqiConfigCatalog
    {
        public BuqiGlobalConfigRow Global = new BuqiGlobalConfigRow();
        public List<BuqiItemConfigRow> Items = new List<BuqiItemConfigRow>();
        public List<BuqiRefinementConfigRow> Refinements = new List<BuqiRefinementConfigRow>();
        public List<BuqiEchoConfigRow> Echoes = new List<BuqiEchoConfigRow>();
    }

    public sealed class BuqiGlobalConfigRow
    {
        public string ContentVersion = string.Empty;
        public int InitialExecution;
        public int BufferCap;
        public int NoiseThreshold;
        public int NoiseIncidentDamage;
        public int BoardSlotCount;
        public int NormalDurationTicks;
        public int HardCapTicks;
        public int OvertimeStartTicks;
        public int MaxTickEvents;
        public int MaxItemEventsPerTick;
    }

    public sealed class BuqiItemConfigRow
    {
        public string DefinitionId = string.Empty;
        public string DisplayName = string.Empty;
        public BattleSize Size = BattleSize.S;
        public int BasePrice;
        public int BaseCooldownTicks;
        public string ArchetypeId = string.Empty;
        public List<string> Tags = new List<string>();
        public List<BuqiEffectConfigRow> Effects = new List<BuqiEffectConfigRow>();
    }

    public sealed class BuqiEffectConfigRow
    {
        public BattleTrigger Trigger = BattleTrigger.OnUse;
        public BattleEffect Effect = BattleEffect.Damage;
        public BattleTarget Target = BattleTarget.EnemyExecution;
        public int Amount;
        public int DurationTicks = 30;
        public string ReasonCode = string.Empty;
        public BattleConditionKind ConditionKind = BattleConditionKind.None;
        public int ConditionThreshold;
        public int UseCountThreshold;
        public int ChargeReadLimit;
        public int AmountPerCharge;
        public bool ChargeConsume;
        public bool ResetCountOnReached = true;
    }

    public sealed class BuqiRefinementConfigRow
    {
        public string RefinementId = string.Empty;
        public string DisplayName = string.Empty;
        public string Summary = string.Empty;
    }

    public sealed class BuqiEchoConfigRow
    {
        public string EchoId = string.Empty;
        public string DisplayName = string.Empty;
        public string Tier = string.Empty;
        public string Build = string.Empty;
        public BuqiBuildSnapshotConfigRow Snapshot = new BuqiBuildSnapshotConfigRow();
    }

    public sealed class BuqiBuildSnapshotConfigRow
    {
        public string SnapshotId = string.Empty;
        public string ArchetypeId = string.Empty;
        public int InitialExecution = 100;
        public int InitialBuffer;
        public int InitialNoiseDebt;
        public List<BuqiItemInstanceConfigRow> Items = new List<BuqiItemInstanceConfigRow>();
    }

    public sealed class BuqiItemInstanceConfigRow
    {
        public string InstanceId = string.Empty;
        public string DefinitionId = string.Empty;
        public BattleQuality Quality = BattleQuality.Normal;
        public int AnchorSlot;
        public string RefinementId = string.Empty;
    }
}
