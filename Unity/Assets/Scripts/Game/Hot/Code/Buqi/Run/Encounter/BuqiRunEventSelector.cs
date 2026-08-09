using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Encounter
{
    public sealed class BuqiRunEventSelector
    {
        private readonly IBuqiRunEventDefinitionCatalog m_Events;
        private readonly IBuqiRunEventItemCatalog m_Items;

        public BuqiRunEventSelector(
            IBuqiRunEventDefinitionCatalog events,
            IBuqiRunEventItemCatalog items)
        {
            m_Events = events ?? throw new ArgumentNullException(nameof(events));
            m_Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public BuqiRunEventSelectionResult Select(BuqiRunEventRuntimeState source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BuqiRunEventRuntimeState unchanged = source.Clone();
            BuqiRunPendingEvent current = source.PendingEvent;
            if (current != null && current.IsActive)
            {
                if (current.Day != source.Economy.Run.Day || current.Period != source.Economy.Run.Period)
                    return Fail(unchanged, "Frozen event does not match the current day and period.");

                return Success(unchanged, unchanged.PendingEvent, false);
            }

            List<WeightedEvent> candidates = CollectCandidates(source);
            if (candidates.Count == 0)
                return Fail(unchanged, "No eligible event is available.");

            int totalWeight = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].Weight > int.MaxValue - totalWeight)
                    return Fail(unchanged, "Eligible event weight exceeds the supported range.");
                totalWeight += candidates[index].Weight;
            }

            int cursor = source.Economy.Run.RngCursor;
            int roll = BuqiRunRandom.Next(source.Economy.Run.RunSeed, ref cursor, totalWeight);
            WeightedEvent selected = candidates[candidates.Count - 1];
            for (int index = 0; index < candidates.Count; index++)
            {
                if (roll < candidates[index].Weight)
                {
                    selected = candidates[index];
                    break;
                }

                roll -= candidates[index].Weight;
            }

            var pending = new BuqiRunPendingEvent
            {
                EventId = selected.Definition.EventId,
                Day = source.Economy.Run.Day,
                Period = source.Economy.Run.Period,
                TriggeredScheduleId = selected.TriggeredScheduleId,
            };
            for (int optionIndex = 0; optionIndex < selected.Definition.Options.Count; optionIndex++)
            {
                BuqiRunEventOptionDefinition option = selected.Definition.Options[optionIndex];
                pending.OptionIds.Add(option.OptionId);
                for (int actionIndex = 0; actionIndex < option.Actions.Count; actionIndex++)
                {
                    BuqiRunEventActionDefinition action = option.Actions[actionIndex];
                    if (action.Kind != BuqiRunEventActionKind.GrantRandomItem)
                        continue;

                    List<string> pool = GetItemPool(action.BuildTag);
                    if (pool.Count == 0)
                        return Fail(unchanged, "A random item action has no eligible definitions.");

                    string value = pool[BuqiRunRandom.Next(
                        source.Economy.Run.RunSeed,
                        ref cursor,
                        pool.Count)];
                    pending.RandomResults.Add(new BuqiRunEventFrozenValue
                    {
                        ActionId = action.ActionId,
                        Value = value,
                    });
                }
            }

            pending.RandomResults.Sort((left, right) =>
                string.CompareOrdinal(left.ActionId, right.ActionId));
            BuqiRunEventRuntimeState working = source.Clone();
            working.Economy.Run.RngCursor = cursor;
            working.PendingEvent = pending;
            return Success(working, pending, true);
        }

        private List<WeightedEvent> CollectCandidates(BuqiRunEventRuntimeState state)
        {
            var result = new List<WeightedEvent>();
            bool hasDeadlineReturn = false;
            IReadOnlyList<BuqiRunEventDefinition> definitions = m_Events.Definitions;
            if (definitions == null)
                return result;

            var ordered = new List<BuqiRunEventDefinition>();
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null)
                    ordered.Add(definitions[index]);
            }
            ordered.Sort((left, right) => string.CompareOrdinal(left.EventId, right.EventId));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ordered.Count; index++)
            {
                BuqiRunEventDefinition definition = ordered[index];
                if (!seen.Add(definition.EventId) ||
                    !BuqiRunEventRuntimeRules.IsValidDefinition(definition) ||
                    !BuqiRunEventRuntimeRules.IsEligible(definition.Eligibility, state, m_Items) ||
                    !AreOptionsEligible(definition, state) ||
                    IsBlockedByHistory(definition, state) ||
                    !CanResolveConfiguredActions(definition))
                {
                    continue;
                }

                int scheduleWeight = 0;
                string triggeredScheduleId = string.Empty;
                bool isDeadlineReturn = false;
                for (int returnIndex = 0; returnIndex < state.ScheduledReturns.Count; returnIndex++)
                {
                    BuqiRunScheduledReturn scheduled = state.ScheduledReturns[returnIndex];
                    if (!string.Equals(scheduled.EventId, definition.EventId, StringComparison.Ordinal) ||
                        state.Economy.Run.Day < scheduled.EarliestDay ||
                        state.Economy.Run.Day > scheduled.LatestDay)
                    {
                        continue;
                    }

                    if (scheduled.WeightBonus > 0 && scheduleWeight <= int.MaxValue - scheduled.WeightBonus)
                        scheduleWeight += scheduled.WeightBonus;
                    if (state.Economy.Run.Day == scheduled.LatestDay)
                        isDeadlineReturn = true;
                    if (string.IsNullOrEmpty(triggeredScheduleId) ||
                        string.CompareOrdinal(scheduled.ScheduleId, triggeredScheduleId) < 0)
                    {
                        triggeredScheduleId = scheduled.ScheduleId;
                    }
                }

                if (definition.BaseWeight <= 0 || definition.BaseWeight > int.MaxValue - scheduleWeight)
                    continue;

                result.Add(new WeightedEvent(
                    definition,
                    definition.BaseWeight + scheduleWeight,
                    triggeredScheduleId,
                    isDeadlineReturn));
                hasDeadlineReturn |= isDeadlineReturn;
            }

            if (hasDeadlineReturn)
                result.RemoveAll(candidate => !candidate.IsDeadlineReturn);
            return result;
        }

        private bool AreOptionsEligible(
            BuqiRunEventDefinition definition,
            BuqiRunEventRuntimeState state)
        {
            for (int index = 0; index < definition.Options.Count; index++)
            {
                if (!BuqiRunEventRuntimeRules.IsEligible(definition.Options[index].Eligibility, state, m_Items))
                    return false;
            }
            return true;
        }

        private bool CanResolveConfiguredActions(BuqiRunEventDefinition definition)
        {
            for (int optionIndex = 0; optionIndex < definition.Options.Count; optionIndex++)
            {
                IReadOnlyList<BuqiRunEventActionDefinition> actions = definition.Options[optionIndex].Actions;
                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    BuqiRunEventActionDefinition action = actions[actionIndex];
                    if (action.Kind == BuqiRunEventActionKind.GrantRandomItem &&
                        GetItemPool(action.BuildTag).Count == 0)
                    {
                        return false;
                    }
                    if (action.Kind == BuqiRunEventActionKind.GrantItem &&
                        (!m_Items.TryGet(action.ItemDefinitionId, out Game.Hot.Buqi.Run.Economy.BuqiRunItemDefinition item) ||
                         item == null || item.Size < 1))
                    {
                        return false;
                    }
                    if (action.Kind == BuqiRunEventActionKind.ScheduleReturn &&
                        (!m_Events.TryGet(action.ReturnEventId, out BuqiRunEventDefinition returnEvent) ||
                         !BuqiRunEventRuntimeRules.IsValidDefinition(returnEvent)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private List<string> GetItemPool(string buildTag)
        {
            var result = new List<string>();
            IReadOnlyList<string> definitionIds = m_Items.DefinitionIds;
            if (definitionIds == null)
                return result;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < definitionIds.Count; index++)
            {
                string definitionId = definitionIds[index];
                if (string.IsNullOrWhiteSpace(definitionId) ||
                    !seen.Add(definitionId) ||
                    !m_Items.HasBuildTag(definitionId, buildTag))
                {
                    continue;
                }
                result.Add(definitionId);
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static bool IsBlockedByHistory(
            BuqiRunEventDefinition definition,
            BuqiRunEventRuntimeState state)
        {
            int latestDay = int.MinValue;
            for (int index = 0; index < state.History.Count; index++)
            {
                BuqiRunEventHistoryEntry entry = state.History[index];
                if (!string.Equals(entry.EventId, definition.EventId, StringComparison.Ordinal))
                    continue;
                if (definition.UniquePerRun)
                    return true;
                if (entry.Day > latestDay)
                    latestDay = entry.Day;
            }

            return latestDay != int.MinValue &&
                   state.Economy.Run.Day - latestDay <= definition.CooldownDays;
        }

        private static BuqiRunEventSelectionResult Success(
            BuqiRunEventRuntimeState state,
            BuqiRunPendingEvent pending,
            bool created)
        {
            return new BuqiRunEventSelectionResult
            {
                Success = true,
                Created = created,
                State = state,
                Pending = pending.Clone(),
            };
        }

        private static BuqiRunEventSelectionResult Fail(BuqiRunEventRuntimeState state, string reason)
        {
            return new BuqiRunEventSelectionResult
            {
                Success = false,
                FailureReason = reason,
                State = state,
                Pending = state.PendingEvent?.Clone() ?? new BuqiRunPendingEvent(),
            };
        }

        private sealed class WeightedEvent
        {
            public WeightedEvent(
                BuqiRunEventDefinition definition,
                int weight,
                string triggeredScheduleId,
                bool isDeadlineReturn)
            {
                Definition = definition;
                Weight = weight;
                TriggeredScheduleId = triggeredScheduleId;
                IsDeadlineReturn = isDeadlineReturn;
            }

            public BuqiRunEventDefinition Definition { get; }
            public int Weight { get; }
            public string TriggeredScheduleId { get; }
            public bool IsDeadlineReturn { get; }
        }
    }

    internal static class BuqiRunEventRuntimeRules
    {
        public static bool IsValidDefinition(BuqiRunEventDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.EventId) ||
                definition.Eligibility == null || definition.Options == null ||
                definition.Options.Count != 3 || definition.CooldownDays < 0 ||
                !IsValidEligibility(definition.Eligibility))
            {
                return false;
            }

            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int optionIndex = 0; optionIndex < definition.Options.Count; optionIndex++)
            {
                BuqiRunEventOptionDefinition option = definition.Options[optionIndex];
                if (option == null || string.IsNullOrWhiteSpace(option.OptionId) ||
                    !optionIds.Add(option.OptionId) || option.CoinCost < 0 ||
                    option.Eligibility == null || !IsValidEligibility(option.Eligibility) ||
                    option.Actions == null)
                {
                    return false;
                }

                for (int actionIndex = 0; actionIndex < option.Actions.Count; actionIndex++)
                {
                    BuqiRunEventActionDefinition action = option.Actions[actionIndex];
                    if (action == null || string.IsNullOrWhiteSpace(action.ActionId) ||
                        !actionIds.Add(action.ActionId) ||
                        !Enum.IsDefined(typeof(BuqiRunEventActionKind), action.Kind) ||
                        !Enum.IsDefined(typeof(BuqiRunModifierKind), action.ModifierKind) ||
                        !IsValidAction(action))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool IsEligible(
            BuqiRunEventEligibility eligibility,
            BuqiRunEventRuntimeState state,
            IBuqiRunBuildTagCatalog items)
        {
            int day = state.Economy.Run.Day;
            if (!IsValidEligibility(eligibility) || day < eligibility.MinDay || day > eligibility.MaxDay)
                return false;

            BuqiRunPeriodMask period = ToMask(state.Economy.Run.Period);
            if (period == BuqiRunPeriodMask.None || (eligibility.Periods & period) == 0)
                return false;

            for (int index = 0; index < eligibility.RequiredFlags.Count; index++)
            {
                if (!state.HasFlag(eligibility.RequiredFlags[index]))
                    return false;
            }
            for (int index = 0; index < eligibility.ForbiddenFlags.Count; index++)
            {
                if (state.HasFlag(eligibility.ForbiddenFlags[index]))
                    return false;
            }
            for (int tagIndex = 0; tagIndex < eligibility.RequiredBuildTags.Count; tagIndex++)
            {
                string tag = eligibility.RequiredBuildTags[tagIndex];
                bool found = false;
                foreach (Game.Hot.Buqi.Run.Economy.BuqiRunItemInstance item in state.Economy.Items.Values)
                {
                    if (items.HasBuildTag(item.DefinitionId, tag))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }

            return true;
        }

        private static bool IsValidEligibility(BuqiRunEventEligibility eligibility)
        {
            return eligibility != null && eligibility.MinDay >= 1 &&
                   eligibility.MaxDay >= eligibility.MinDay &&
                   eligibility.Periods != BuqiRunPeriodMask.None &&
                   (eligibility.Periods & ~BuqiRunPeriodMask.AllOperations) == 0 &&
                   HasValidIds(eligibility.RequiredFlags) &&
                   HasValidIds(eligibility.ForbiddenFlags) &&
                   HasValidIds(eligibility.RequiredBuildTags);
        }

        private static bool IsValidAction(BuqiRunEventActionDefinition action)
        {
            switch (action.Kind)
            {
                case BuqiRunEventActionKind.GrantItem:
                    return !string.IsNullOrWhiteSpace(action.ItemDefinitionId);
                case BuqiRunEventActionKind.GrantRandomItem:
                    return !string.IsNullOrWhiteSpace(action.BuildTag);
                case BuqiRunEventActionKind.UpgradeItem:
                    return action.QualitySteps >= 0;
                case BuqiRunEventActionKind.AddTemporaryModifier:
                    return action.DurationBattles > 0;
                case BuqiRunEventActionKind.SetFlag:
                case BuqiRunEventActionKind.ClearFlag:
                    return !string.IsNullOrWhiteSpace(action.FlagId);
                case BuqiRunEventActionKind.AddCounter:
                    return !string.IsNullOrWhiteSpace(action.CounterId);
                case BuqiRunEventActionKind.ScheduleReturn:
                    return !string.IsNullOrWhiteSpace(action.ReturnEventId) &&
                           action.MinDayOffset >= 1 &&
                           action.MaxDayOffset >= action.MinDayOffset &&
                           action.WeightBonus >= 0;
                case BuqiRunEventActionKind.ApplyRefinement:
                    return !string.IsNullOrWhiteSpace(action.RefinementId);
                default:
                    return true;
            }
        }

        private static bool HasValidIds(IReadOnlyList<string> values)
        {
            if (values == null)
                return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(values[index]) || !seen.Add(values[index]))
                    return false;
            }
            return true;
        }

        private static BuqiRunPeriodMask ToMask(BuqiRunPeriod period)
        {
            switch (period)
            {
                case BuqiRunPeriod.MorningOperation:
                    return BuqiRunPeriodMask.Morning;
                case BuqiRunPeriod.NoonOperation:
                    return BuqiRunPeriodMask.Noon;
                default:
                    return BuqiRunPeriodMask.None;
            }
        }
    }
}
