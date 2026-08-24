using System.Collections.Generic;
using BattleConditionKind = Game.Hot.Buqi.Battle.BuqiConditionKind;
using BattleEffect = Game.Hot.Buqi.Battle.BuqiEffect;
using BattleQuality = Game.Hot.Buqi.Battle.BuqiQuality;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;
using BattleTarget = Game.Hot.Buqi.Battle.BuqiTarget;
using BattleTrigger = Game.Hot.Buqi.Battle.BuqiTrigger;
using BuqiItemCategory = Game.Hot.BuqiItemCategory;
using BuqiMerchantSpecialty = Game.Hot.BuqiMerchantSpecialty;

namespace Game.Hot.Buqi.Config
{
    public sealed class BuqiConfigCatalog
    {
        public BuqiGlobalConfigRow Global = new BuqiGlobalConfigRow();
        public List<BuqiItemConfigRow> Items = new List<BuqiItemConfigRow>();
        public List<BuqiRefinementConfigRow> Refinements = new List<BuqiRefinementConfigRow>();
        public List<BuqiEchoConfigRow> Echoes = new List<BuqiEchoConfigRow>();
        public List<BuqiMerchantConfigRow> Merchants = new List<BuqiMerchantConfigRow>();
        public List<BuqiTrainerConfigRow> Trainers = new List<BuqiTrainerConfigRow>();
        public List<BuqiTrainingProjectConfigRow> TrainingProjects = new List<BuqiTrainingProjectConfigRow>();
        public List<BuqiEventConfigRow> Events = new List<BuqiEventConfigRow>();
        public List<BuqiEventOptionConfigRow> EventOptions = new List<BuqiEventOptionConfigRow>();
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
        public int MaxChainDepth;
        public int MaxRepeatedReasonPerTick;
        public int DailyEconomyProcCap;
    }

    public sealed class BuqiItemConfigRow
    {
        public string DefinitionId = string.Empty;
        public string DisplayName = string.Empty;
        public string DesignNote = string.Empty;
        public string EffectDescription = string.Empty;
        public string LocalizationKey = string.Empty;
        public BattleSize Size = BattleSize.S;
        public int BasePrice;
        public int ImprovedUpgradeCost;
        public int FixedUpgradeCost;
        public int RefinementCost;
        public int BaseCooldownTicks;
        public string ArchetypeId = string.Empty;
        public string Role = string.Empty;
        public int UnlockDay;
        public string PositionHint = string.Empty;
        public List<string> Tags = new List<string>();
        public List<BuqiEffectConfigRow> Effects = new List<BuqiEffectConfigRow>();
        public List<BuqiRunEffectConfigRow> RunEffects = new List<BuqiRunEffectConfigRow>();
        public string UpgradeSummary = string.Empty;
        public string UpgradeLocalizationKey = string.Empty;
        public List<string> LinkIds = new List<string>();
        public BuqiItemCategory Category = BuqiItemCategory.Unknown;
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

    public sealed class BuqiRunEffectConfigRow
    {
        public string Trigger = string.Empty;
        public string Effect = string.Empty;
        public int Amount;
        public int Threshold;
        public int MaxPerDay;
        public string ReasonCode = string.Empty;
    }

    public sealed class BuqiMerchantConfigRow
    {
        public string MerchantId = string.Empty;
        public string DisplayName = string.Empty;
        public string LocalizationKey = string.Empty;
        public int MinDay;
        public int MaxDay;
        public int Weight;
        public List<string> PoolItemIds = new List<string>();
        public List<BuqiMerchantSlotConfigRow> Slots = new List<BuqiMerchantSlotConfigRow>();
        public BuqiMerchantSpecialty Specialty = BuqiMerchantSpecialty.General;
    }

    public sealed class BuqiMerchantSlotConfigRow
    {
        public string SlotId = string.Empty;
        public string SlotKind = string.Empty;
        public string BuildFilter = string.Empty;
        public string SizeFilter = string.Empty;
        public string QualityFilter = string.Empty;
        public string RequiredTag = string.Empty;
        public int MinUnlockDay;
        public int MaxUnlockDay;
        public int Weight;
        public int Count;
    }

    public sealed class BuqiTrainerConfigRow
    {
        public string TrainerId = string.Empty;
        public string DisplayName = string.Empty;
        public string LocalizationKey = string.Empty;
        public int MinDay;
        public int MaxDay;
        public int Weight;
        public List<string> ProjectIds = new List<string>();
    }

    public sealed class BuqiTrainingProjectConfigRow
    {
        public string ProjectId = string.Empty;
        public string TrainerId = string.Empty;
        public string DisplayName = string.Empty;
        public string LocalizationKey = string.Empty;
        public int MinDay;
        public int MaxDay;
        public int Cost;
        public string RequiredTag = string.Empty;
        public string ExcludedTag = string.Empty;
        public string EffectKind = string.Empty;
        public int Amount;
        public int Duration;
        public int MaxPerRun;
        public string Summary = string.Empty;
        public string SummaryLocalizationKey = string.Empty;
    }

    public sealed class BuqiEventConfigRow
    {
        public string EventId = string.Empty;
        public string DisplayName = string.Empty;
        public string LocalizationKey = string.Empty;
        public int MinDay;
        public int MaxDay;
        public int Weight;
        public List<string> RequiredFlags = new List<string>();
        public List<string> ForbiddenFlags = new List<string>();
        public string RevisitEventId = string.Empty;
        public int RevisitDelayDays;
        public bool DayNineResolution;
        public List<string> OptionIds = new List<string>();
    }

    public sealed class BuqiEventOptionConfigRow
    {
        public string OptionId = string.Empty;
        public string EventId = string.Empty;
        public int Order;
        public string DisplayName = string.Empty;
        public string LocalizationKey = string.Empty;
        public string ConditionKind = string.Empty;
        public string ConditionValue = string.Empty;
        public List<string> RequiredFlags = new List<string>();
        public List<string> ForbiddenFlags = new List<string>();
        public List<BuqiEventCostConfigRow> Costs = new List<BuqiEventCostConfigRow>();
        public List<BuqiEventOutcomeConfigRow> Outcomes = new List<BuqiEventOutcomeConfigRow>();
        public List<string> SetFlags = new List<string>();
        public List<string> ClearFlags = new List<string>();
        public string FollowUpEventId = string.Empty;
        public int FollowUpDelayDays;
        public string Summary = string.Empty;
        public string SummaryLocalizationKey = string.Empty;
    }

    public sealed class BuqiEventCostConfigRow
    {
        public string Kind = string.Empty;
        public int Amount;
        public string Value = string.Empty;
    }

    public sealed class BuqiEventOutcomeConfigRow
    {
        public string Kind = string.Empty;
        public int Amount;
        public string Value = string.Empty;
        public int DurationDays;
        public string ReasonCode = string.Empty;
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
