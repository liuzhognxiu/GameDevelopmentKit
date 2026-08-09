using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public enum BuqiLinkDirection : int
    {
        Clockwise = 0,
        CounterClockwise = 1,
        AnyAdjacent = 2,
    }

    public enum BuqiLinkTriggerSource : int
    {
        SelfUse = 0,
        AdjacentUse = 1,
        BattleStart = 2,
        LinkActivation = 3,
    }

    public enum BuqiLinkStackMode : int
    {
        Add = 0,
        Max = 1,
        ReplaceHigherPriority = 2,
        UniqueSource = 3,
    }

    public enum BuqiFormationRequirementKind : int
    {
        MatchingItems = 0,
        MatchingLinks = 1,
    }

    public sealed class BuqiLinkItem
    {
        public string InstanceId = string.Empty;
        public string DefinitionId = string.Empty;
        public string AnnotationId = string.Empty;
        public int AnchorSlot;
        public int Size = 1;
        public int Quality = (int)BuqiQuality.Normal;
        public HashSet<string> Tags = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<BuqiEffect> Effects = new HashSet<BuqiEffect>();
        public HashSet<BuqiTrigger> Triggers = new HashSet<BuqiTrigger>();
        public HashSet<BuqiConditionKind> Conditions = new HashSet<BuqiConditionKind>();
    }

    public sealed class BuqiLinkBoard
    {
        public const int SlotCount = 8;
        private readonly List<BuqiLinkItem> m_Items;

        public BuqiLinkBoard(IEnumerable<BuqiLinkItem> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            m_Items = new List<BuqiLinkItem>(items);
            m_Items.Sort(CompareItems);
            Validate();
        }

        public IReadOnlyList<BuqiLinkItem> Items => m_Items;

        public static BuqiLinkBoard FromSide(SideState side, IItemDefinitionProvider provider)
        {
            if (side == null)
                throw new ArgumentNullException(nameof(side));
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            var items = new List<BuqiLinkItem>();
            foreach (ItemState state in side.Items)
            {
                var item = new BuqiLinkItem
                {
                    InstanceId = state.InstanceId,
                    DefinitionId = state.DefinitionId,
                    AnnotationId = state.AnnotationId,
                    AnchorSlot = state.AnchorSlot,
                    Size = state.Size,
                    Quality = state.Quality,
                };
                if (provider.TryGet(state.DefinitionId, out BuqiItemDefinition definition))
                {
                    foreach (BuqiEffectSpec effect in definition.Effects)
                    {
                        item.Effects.Add(effect.Effect);
                        item.Triggers.Add(effect.Trigger);
                        if (effect.ConditionKind != BuqiConditionKind.None)
                            item.Conditions.Add(effect.ConditionKind);
                    }
                }
                items.Add(item);
            }
            return new BuqiLinkBoard(items);
        }

        private void Validate()
        {
            var occupied = new bool[SlotCount];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiLinkItem item in m_Items)
            {
                if (item == null)
                    throw new ArgumentException("Board items cannot contain null.", nameof(m_Items));
                if (string.IsNullOrWhiteSpace(item.InstanceId) || !ids.Add(item.InstanceId))
                    throw new ArgumentException("Board item instance ids must be non-empty and unique.", nameof(m_Items));
                if (item.AnchorSlot < 0 || item.AnchorSlot >= SlotCount)
                    throw new ArgumentOutOfRangeException(nameof(item.AnchorSlot));
                if (item.Size < 1 || item.Size > 3 || item.AnchorSlot + item.Size > SlotCount)
                    throw new ArgumentOutOfRangeException(nameof(item.Size), "Items cannot wrap across slot seven and zero.");
                for (int slot = item.AnchorSlot; slot < item.AnchorSlot + item.Size; slot++)
                {
                    if (occupied[slot])
                        throw new ArgumentException("Board items cannot overlap.", nameof(m_Items));
                    occupied[slot] = true;
                }
            }
        }

        private static int CompareItems(BuqiLinkItem left, BuqiLinkItem right)
        {
            int comparison = left.AnchorSlot.CompareTo(right.AnchorSlot);
            return comparison != 0 ? comparison : string.CompareOrdinal(left.InstanceId, right.InstanceId);
        }
    }

    public sealed class BuqiLinkCondition
    {
        public string DefinitionId = string.Empty;
        public string AnnotationId = string.Empty;
        public string RequiredTag = string.Empty;
        public HashSet<string> AnyTags = new HashSet<string>(StringComparer.Ordinal);
        public BuqiEffect? RequiredEffect;
        public HashSet<BuqiEffect> AnyEffects = new HashSet<BuqiEffect>();
        public BuqiTrigger? RequiredTrigger;
        public BuqiConditionKind? RequiredCondition;
        public int MinimumQuality;
        public int MaximumQuality;
        public int MinimumSize;
        public int MaximumSize;
    }

    public sealed class BuqiLinkRule
    {
        public string RuleId = string.Empty;
        public BuqiLinkDirection Direction;
        public BuqiLinkTriggerSource TriggerSource;
        public BuqiLinkCondition SourceCondition = new BuqiLinkCondition();
        public BuqiLinkCondition TargetCondition = new BuqiLinkCondition();
        public int Priority;
        public int Amount;
        public string StackGroup = string.Empty;
        public BuqiLinkStackMode StackMode = BuqiLinkStackMode.Add;
        public int StackLimit = 1;
        public string ExclusiveGroup = string.Empty;
        public int MaxTriggersPerTick;
        public int MaxTriggersPerActiveUse;
    }

    public sealed class BuqiLinkFact
    {
        public string RuleId = string.Empty;
        public string SourceInstanceId = string.Empty;
        public string TargetInstanceId = string.Empty;
        public int SourceAnchorSlot = int.MaxValue;
        public int TargetAnchorSlot = int.MaxValue;
        public BuqiLinkDirection Direction;
        public bool IsConnected;
        public string ReasonCode = string.Empty;
        internal BuqiLinkItem Source;
        internal BuqiLinkItem Target;
    }

    public sealed class BuqiFormationRequirement
    {
        public string RequirementId = string.Empty;
        public BuqiFormationRequirementKind Kind;
        public int MinimumCount = 1;
        public BuqiLinkCondition ItemCondition = new BuqiLinkCondition();
        public BuqiLinkCondition SourceCondition = new BuqiLinkCondition();
        public BuqiLinkCondition TargetCondition = new BuqiLinkCondition();

        public static BuqiFormationRequirement Items(string id, int minimumCount, BuqiLinkCondition condition)
        {
            return new BuqiFormationRequirement
            {
                RequirementId = id,
                Kind = BuqiFormationRequirementKind.MatchingItems,
                MinimumCount = minimumCount,
                ItemCondition = condition ?? new BuqiLinkCondition(),
            };
        }

        public static BuqiFormationRequirement Links(
            string id,
            int minimumCount,
            BuqiLinkCondition source,
            BuqiLinkCondition target)
        {
            return new BuqiFormationRequirement
            {
                RequirementId = id,
                Kind = BuqiFormationRequirementKind.MatchingLinks,
                MinimumCount = minimumCount,
                SourceCondition = source ?? new BuqiLinkCondition(),
                TargetCondition = target ?? new BuqiLinkCondition(),
            };
        }
    }

    public sealed class BuqiFormationRule
    {
        public string FormationId = string.Empty;
        public int Priority;
        public List<BuqiFormationRequirement> Requirements = new List<BuqiFormationRequirement>();
    }

    public sealed class BuqiFormationFact
    {
        public string FormationId = string.Empty;
        public int Priority;
        public bool IsFormed;
        public List<string> MissingRequirements = new List<string>();
    }

    public sealed class BuqiEchoSlot
    {
        public string DefinitionId = string.Empty;
        public string AnnotationId = string.Empty;
        public int AnchorSlot;
        public int Quality = (int)BuqiQuality.Normal;
    }

    public sealed class BuqiEchoBlueprint
    {
        public string EchoId = string.Empty;
        public List<BuqiEchoSlot> Items = new List<BuqiEchoSlot>();
    }

    public sealed class BuqiEchoMatchFact
    {
        public string EchoId = string.Empty;
        public bool IsEvaluated;
        public bool IsExactMatch;
        public List<string> Mismatches = new List<string>();
    }

    public sealed class BuqiLinkEvaluation
    {
        public IReadOnlyList<BuqiLinkFact> Links = Array.Empty<BuqiLinkFact>();
        public IReadOnlyList<BuqiFormationFact> Formations = Array.Empty<BuqiFormationFact>();
        public BuqiEchoMatchFact Echo = new BuqiEchoMatchFact();
    }

    public sealed class BuqiLinkTriggerContext
    {
        public int Tick;
        public int ChainDepth;
        public string RootEventId = string.Empty;
        public string ActiveUseId = string.Empty;
        public BuqiLinkTriggerSource TriggerSource;
        public string TriggeredByInstanceId = string.Empty;
        public string StateHash = string.Empty;
    }

    public sealed class BuqiLinkTriggerFact
    {
        public string RuleId = string.Empty;
        public string SourceInstanceId = string.Empty;
        public string TargetInstanceId = string.Empty;
        public string TriggeredByInstanceId = string.Empty;
        public string RootEventId = string.Empty;
        public int Priority;
        public int Amount;
        public bool IsTriggered;
        public string ReasonCode = string.Empty;
        internal BuqiLinkRule Rule;
        internal BuqiLinkFact Link;
    }

    public sealed class BuqiLinkTriggerAttempt
    {
        public int Tick;
        public int ChainDepth;
        public string RootEventId = string.Empty;
        public string ActiveUseId = string.Empty;
        public string RuleId = string.Empty;
        public string SourceInstanceId = string.Empty;
        public string TargetInstanceId = string.Empty;
        public string StateHash = string.Empty;
        public int RuleMaxTriggersPerTick;
        public int RuleMaxTriggersPerActiveUse;
    }

    public sealed class BuqiLinkExecutionLimits
    {
        public static BuqiLinkExecutionLimits Default => new BuqiLinkExecutionLimits
        {
            MaxTriggersPerTick = 256,
            MaxTriggersPerActiveUse = 16,
            MaxChainDepth = 16,
            MaxAbilityFiresPerRoot = 8,
            MaxSignatureRepeats = 2,
        };

        public int MaxTriggersPerTick;
        public int MaxTriggersPerActiveUse;
        public int MaxChainDepth;
        public int MaxAbilityFiresPerRoot;
        public int MaxSignatureRepeats;
    }
}
