using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Run.Economy;

namespace Game.Hot.Buqi.Run.Encounter
{
    [Flags]
    public enum BuqiRunPeriodMask
    {
        None = 0,
        Morning = 1 << 0,
        Noon = 1 << 1,
        AllOperations = Morning | Noon,
    }

    public enum BuqiRunEventActionKind
    {
        GrantCoins = 0,
        GrantItem = 1,
        GrantRandomItem = 2,
        UpgradeItem = 3,
        SacrificeItem = 4,
        AddTemporaryModifier = 5,
        SetFlag = 6,
        ClearFlag = 7,
        AddCounter = 8,
        AddExperience = 9,
        ScheduleReturn = 10,
        ApplyRefinement = 11,
        RestoreLife = 12,
    }

    public enum BuqiRunModifierKind
    {
        DamagePercent = 0,
        ShieldPercent = 1,
        RecoveryPercent = 2,
        CooldownPercent = 3,
        StartingShield = 4,
        EconomyPercent = 5,
        ExperiencePercent = 6,
    }

    public sealed class BuqiRunEventEligibility
    {
        public int MinDay = 1;
        public int MaxDay = 9;
        public BuqiRunPeriodMask Periods = BuqiRunPeriodMask.AllOperations;
        public List<string> RequiredFlags = new List<string>();
        public List<string> ForbiddenFlags = new List<string>();
        public List<string> RequiredBuildTags = new List<string>();

        public BuqiRunEventEligibility Clone()
        {
            return new BuqiRunEventEligibility
            {
                MinDay = MinDay,
                MaxDay = MaxDay,
                Periods = Periods,
                RequiredFlags = new List<string>(RequiredFlags),
                ForbiddenFlags = new List<string>(ForbiddenFlags),
                RequiredBuildTags = new List<string>(RequiredBuildTags),
            };
        }
    }

    public sealed class BuqiRunEventActionDefinition
    {
        public string ActionId = string.Empty;
        public BuqiRunEventActionKind Kind;
        public string ItemDefinitionId = string.Empty;
        public string BuildTag = string.Empty;
        public string FlagId = string.Empty;
        public string CounterId = string.Empty;
        public string ModifierId = string.Empty;
        public string ReturnEventId = string.Empty;
        public string ScheduleId = string.Empty;
        public string RefinementId = string.Empty;
        public int Amount;
        public int QualitySteps;
        public int DurationBattles;
        public int MinDayOffset;
        public int MaxDayOffset;
        public int WeightBonus;
        public BuqiRunModifierKind ModifierKind;
    }

    public sealed class BuqiRunEventOptionDefinition
    {
        public string OptionId = string.Empty;
        public int CoinCost;
        public BuqiRunEventEligibility Eligibility = new BuqiRunEventEligibility();
        public List<BuqiRunEventActionDefinition> Actions = new List<BuqiRunEventActionDefinition>();
    }

    public sealed class BuqiRunEventDefinition
    {
        public string EventId = string.Empty;
        public int BaseWeight = 1;
        public bool UniquePerRun;
        public int CooldownDays;
        public BuqiRunEventEligibility Eligibility = new BuqiRunEventEligibility();
        public List<BuqiRunEventOptionDefinition> Options = new List<BuqiRunEventOptionDefinition>();
    }

    public interface IBuqiRunEventDefinitionCatalog
    {
        IReadOnlyList<BuqiRunEventDefinition> Definitions { get; }

        bool TryGet(string eventId, out BuqiRunEventDefinition definition);
    }

    public interface IBuqiRunBuildTagCatalog
    {
        bool HasBuildTag(string definitionId, string buildTag);
    }

    public interface IBuqiRunEventItemCatalog : IBuqiRunItemCatalog, IBuqiRunBuildTagCatalog
    {
        IReadOnlyList<string> DefinitionIds { get; }

    }

    public sealed class BuqiRunEventTargetSelection
    {
        public string ActionId = string.Empty;
        public string InstanceId = string.Empty;
    }

    public sealed class BuqiRunEventChoiceRequest
    {
        public string ResolutionId = string.Empty;
        public string EventId = string.Empty;
        public string OptionId = string.Empty;
        public List<BuqiRunEventTargetSelection> Targets = new List<BuqiRunEventTargetSelection>();
    }
}
