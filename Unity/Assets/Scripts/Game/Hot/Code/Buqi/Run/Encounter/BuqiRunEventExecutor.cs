using System;
using System.Collections.Generic;
using System.Text;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;

namespace Game.Hot.Buqi.Run.Encounter
{
    public sealed class BuqiRunEventExecutor
    {
        private const string EventSourceKind = "event";

        private readonly IBuqiRunEventDefinitionCatalog m_Events;
        private readonly IBuqiRunEventItemCatalog m_Items;
        private readonly BuqiRunEconomyService m_Economy;

        public BuqiRunEventExecutor(
            IBuqiRunEventDefinitionCatalog events,
            IBuqiRunEventItemCatalog items)
        {
            m_Events = events ?? throw new ArgumentNullException(nameof(events));
            m_Items = items ?? throw new ArgumentNullException(nameof(items));
            m_Economy = new BuqiRunEconomyService(items);
        }

        public BuqiRunEventExecutionResult Execute(
            BuqiRunEventRuntimeState source,
            BuqiRunEventChoiceRequest request)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.ResolutionId) ||
                string.IsNullOrWhiteSpace(request.EventId))
            {
                return Fail(source, "Resolution id and event id are required.");
            }
            if (!TargetsAreUnique(request.Targets))
                return Fail(source, "Event targets must have unique action ids.");

            string requestFingerprint = CreateRequestFingerprint(request);

            BuqiRunResolutionRecord prior = FindResolution(source, request.ResolutionId);
            if (prior != null)
            {
                bool matches = string.Equals(prior.SourceKind, EventSourceKind, StringComparison.Ordinal) &&
                               string.Equals(prior.ContentId, request.EventId, StringComparison.Ordinal) &&
                               string.Equals(prior.ChoiceId, request.OptionId, StringComparison.Ordinal) &&
                               string.Equals(prior.RequestFingerprint, requestFingerprint, StringComparison.Ordinal);
                return matches
                    ? Success(source.Clone(), true)
                    : Fail(source, "Resolution id was already used for another decision.");
            }

            BuqiRunPendingEvent pending = source.PendingEvent;
            if (pending == null || !pending.IsActive)
                return Fail(source, "No frozen event is pending.");
            if (!string.Equals(pending.EventId, request.EventId, StringComparison.Ordinal))
                return Fail(source, "Request event id does not match the frozen event.");
            if (pending.Day != source.Economy.Run.Day || pending.Period != source.Economy.Run.Period)
                return Fail(source, "Frozen event does not match the current day and period.");
            if (string.IsNullOrWhiteSpace(request.OptionId) || !Contains(pending.OptionIds, request.OptionId))
                return Fail(source, "Option is not part of the frozen event.");
            if (!m_Events.TryGet(pending.EventId, out BuqiRunEventDefinition definition) ||
                definition == null || !BuqiRunEventRuntimeRules.IsValidDefinition(definition))
            {
                return Fail(source, "Frozen event definition is unavailable or invalid.");
            }

            BuqiRunEventOptionDefinition option = FindOption(definition.Options, request.OptionId);
            if (option == null || !BuqiRunEventRuntimeRules.IsEligible(option.Eligibility, source, m_Items))
                return Fail(source, "Option conditions are not satisfied.");
            if (source.Economy.Run.Coins < option.CoinCost)
                return Fail(source, "Not enough coins.");

            BuqiRunEventRuntimeState working = source.Clone();
            working.Economy.Run.Coins -= option.CoinCost;
            for (int index = 0; index < option.Actions.Count; index++)
            {
                if (!TryApplyAction(
                        working,
                        pending,
                        definition,
                        option.Actions[index],
                        request,
                        out string actionError))
                {
                    return Fail(source, actionError);
                }
            }

            working.History.Add(new BuqiRunEventHistoryEntry
            {
                EventId = definition.EventId,
                Day = working.Economy.Run.Day,
                OptionId = option.OptionId,
                ResolutionId = request.ResolutionId,
            });
            working.AppliedResolutions.Add(new BuqiRunResolutionRecord
            {
                ResolutionId = request.ResolutionId,
                SourceKind = EventSourceKind,
                ContentId = definition.EventId,
                ChoiceId = option.OptionId,
                RequestFingerprint = requestFingerprint,
            });
            RemoveTriggeredSchedule(working.ScheduledReturns, pending.TriggeredScheduleId);
            working.PendingEvent = new BuqiRunPendingEvent();
            working.Economy.Run.Revision++;
            return Success(working, false);
        }

        private bool TryApplyAction(
            BuqiRunEventRuntimeState state,
            BuqiRunPendingEvent pending,
            BuqiRunEventDefinition definition,
            BuqiRunEventActionDefinition action,
            BuqiRunEventChoiceRequest request,
            out string error)
        {
            switch (action.Kind)
            {
                case BuqiRunEventActionKind.GrantCoins:
                    return TryAddCoins(state, action.Amount, out error);

                case BuqiRunEventActionKind.RestoreLife:
                    return TryRestoreLife(state, action.Amount, out error);

                case BuqiRunEventActionKind.GrantItem:
                    return TryGrantItem(state, action.ItemDefinitionId, out error);

                case BuqiRunEventActionKind.GrantRandomItem:
                    if (!TryGetFrozenValue(pending.RandomResults, action.ActionId, out string itemDefinitionId))
                    {
                        error = "Frozen random item result is missing.";
                        return false;
                    }
                    return TryGrantItem(state, itemDefinitionId, out error);

                case BuqiRunEventActionKind.UpgradeItem:
                    return TryUpgradeItem(state, action, request.Targets, out error);

                case BuqiRunEventActionKind.SacrificeItem:
                    return TrySacrificeItem(state, action, request.Targets, out error);

                case BuqiRunEventActionKind.ApplyRefinement:
                    return TryApplyRefinement(state, action, request.Targets, out error);

                case BuqiRunEventActionKind.AddTemporaryModifier:
                    return TryAddModifier(state, definition.EventId, action, request.ResolutionId, out error);

                case BuqiRunEventActionKind.SetFlag:
                    if (string.IsNullOrWhiteSpace(action.FlagId))
                    {
                        error = "Flag id is required.";
                        return false;
                    }
                    if (!state.HasFlag(action.FlagId))
                        state.Flags.Add(action.FlagId);
                    error = string.Empty;
                    return true;

                case BuqiRunEventActionKind.ClearFlag:
                    if (string.IsNullOrWhiteSpace(action.FlagId))
                    {
                        error = "Flag id is required.";
                        return false;
                    }
                    state.Flags.RemoveAll(value => string.Equals(value, action.FlagId, StringComparison.Ordinal));
                    error = string.Empty;
                    return true;

                case BuqiRunEventActionKind.AddCounter:
                    return TryAddCounter(state, action.CounterId, action.Amount, out error);

                case BuqiRunEventActionKind.AddExperience:
                    return TryAddExperience(state, action.Amount, out error);

                case BuqiRunEventActionKind.ScheduleReturn:
                    return TryScheduleReturn(state, definition.EventId, action, request.ResolutionId, out error);

                default:
                    error = "Event action kind is invalid.";
                    return false;
            }
        }

        private bool TryGrantItem(
            BuqiRunEventRuntimeState state,
            string definitionId,
            out string error)
        {
            BuqiRunEconomyResult result = m_Economy.GrantFreeItem(state.Economy, definitionId);
            if (!result.Success)
            {
                error = result.FailureReason;
                return false;
            }

            state.Economy = result.Snapshot;
            error = string.Empty;
            return true;
        }

        private bool TryUpgradeItem(
            BuqiRunEventRuntimeState state,
            BuqiRunEventActionDefinition action,
            IReadOnlyList<BuqiRunEventTargetSelection> targets,
            out string error)
        {
            if (!TryGetTargetItem(state, action.ActionId, action.BuildTag, targets, out BuqiRunItemInstance item, out error))
                return false;

            int steps = action.QualitySteps == 0 ? 1 : action.QualitySteps;
            long targetQuality = (long)item.Quality + steps;
            if (steps < 1 || targetQuality > (int)BuqiRunItemQuality.Finalized)
            {
                error = "Requested item upgrade is out of range.";
                return false;
            }

            item.Quality = (BuqiRunItemQuality)targetQuality;
            error = string.Empty;
            return true;
        }

        private bool TrySacrificeItem(
            BuqiRunEventRuntimeState state,
            BuqiRunEventActionDefinition action,
            IReadOnlyList<BuqiRunEventTargetSelection> targets,
            out string error)
        {
            if (!TryGetTargetItem(state, action.ActionId, action.BuildTag, targets, out BuqiRunItemInstance item, out error))
                return false;

            state.Economy.Items.Remove(item.InstanceId);
            ClearSlots(state.Economy.Run.BoardInstanceIds, item.InstanceId);
            ClearSlots(state.Economy.Run.StorageInstanceIds, item.InstanceId);
            error = string.Empty;
            return true;
        }

        private bool TryApplyRefinement(
            BuqiRunEventRuntimeState state,
            BuqiRunEventActionDefinition action,
            IReadOnlyList<BuqiRunEventTargetSelection> targets,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(action.RefinementId))
            {
                error = "Refinement id is required.";
                return false;
            }
            if (!TryGetTargetItem(state, action.ActionId, action.BuildTag, targets, out BuqiRunItemInstance item, out error))
                return false;
            if (!string.IsNullOrWhiteSpace(item.RefinementId))
            {
                error = "Item target already has a refinement.";
                return false;
            }

            item.RefinementId = action.RefinementId;
            error = string.Empty;
            return true;
        }

        private static bool TryAddModifier(
            BuqiRunEventRuntimeState state,
            string eventId,
            BuqiRunEventActionDefinition action,
            string resolutionId,
            out string error)
        {
            if (action.DurationBattles < 1)
            {
                error = "Temporary modifier duration must be positive.";
                return false;
            }

            state.TemporaryModifiers.Add(new BuqiRunTemporaryModifier
            {
                ModifierId = GameFramework.Utility.Text.Format(
                    "{0}:{1}",
                    resolutionId,
                    action.ActionId),
                SourceId = string.IsNullOrWhiteSpace(action.ModifierId) ? eventId : action.ModifierId,
                BuildTag = action.BuildTag,
                Kind = action.ModifierKind,
                Value = action.Amount,
                RemainingBattles = action.DurationBattles,
            });
            error = string.Empty;
            return true;
        }

        private bool TryScheduleReturn(
            BuqiRunEventRuntimeState state,
            string sourceEventId,
            BuqiRunEventActionDefinition action,
            string resolutionId,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(action.ReturnEventId) ||
                action.MinDayOffset < 1 || action.MaxDayOffset < action.MinDayOffset ||
                action.WeightBonus < 0 ||
                !m_Events.TryGet(action.ReturnEventId, out BuqiRunEventDefinition returnDefinition) ||
                returnDefinition == null || !BuqiRunEventRuntimeRules.IsValidDefinition(returnDefinition))
            {
                error = "Scheduled return definition is invalid.";
                return false;
            }

            string scheduleId = string.IsNullOrWhiteSpace(action.ScheduleId)
                ? GameFramework.Utility.Text.Format("{0}:{1}", resolutionId, action.ActionId)
                : action.ScheduleId;
            for (int index = 0; index < state.ScheduledReturns.Count; index++)
            {
                if (string.Equals(state.ScheduledReturns[index].ScheduleId, scheduleId, StringComparison.Ordinal))
                {
                    error = "Scheduled return id already exists.";
                    return false;
                }
            }

            long earliestDay = (long)state.Economy.Run.Day + action.MinDayOffset;
            long latestDay = (long)state.Economy.Run.Day + action.MaxDayOffset;
            if (latestDay > int.MaxValue)
            {
                error = "Scheduled return day is out of range.";
                return false;
            }

            state.ScheduledReturns.Add(new BuqiRunScheduledReturn
            {
                ScheduleId = scheduleId,
                EventId = action.ReturnEventId,
                EarliestDay = (int)earliestDay,
                LatestDay = (int)latestDay,
                WeightBonus = action.WeightBonus,
            });
            error = string.Empty;
            return true;
        }

        private bool TryGetTargetItem(
            BuqiRunEventRuntimeState state,
            string actionId,
            string requiredBuildTag,
            IReadOnlyList<BuqiRunEventTargetSelection> targets,
            out BuqiRunItemInstance item,
            out string error)
        {
            item = null;
            string instanceId = string.Empty;
            if (targets != null)
            {
                for (int index = 0; index < targets.Count; index++)
                {
                    if (string.Equals(targets[index].ActionId, actionId, StringComparison.Ordinal))
                    {
                        instanceId = targets[index].InstanceId;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(instanceId) ||
                !state.Economy.Items.TryGetValue(instanceId, out item))
            {
                error = "A valid item target is required.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(requiredBuildTag) &&
                !m_Items.HasBuildTag(item.DefinitionId, requiredBuildTag))
            {
                error = "Item target does not match the required build tag.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryAddCoins(BuqiRunEventRuntimeState state, int amount, out string error)
        {
            long value = (long)state.Economy.Run.Coins + amount;
            if (value < 0 || value > int.MaxValue)
            {
                error = "Coin result is out of range.";
                return false;
            }
            state.Economy.Run.Coins = (int)value;
            error = string.Empty;
            return true;
        }

        private static bool TryRestoreLife(BuqiRunEventRuntimeState state, int amount, out string error)
        {
            long value = (long)state.Economy.Run.Lives + amount;
            if (amount < 0 || value > BuqiRunRules.StartingLifePool)
            {
                error = "Life result is out of range.";
                return false;
            }

            state.Economy.Run.Lives = (int)value;
            error = string.Empty;
            return true;
        }

        private static bool TryAddExperience(BuqiRunEventRuntimeState state, int amount, out string error)
        {
            long value = (long)state.Experience + amount;
            if (value < 0 || value > int.MaxValue)
            {
                error = "Experience result is out of range.";
                return false;
            }
            state.Experience = (int)value;
            error = string.Empty;
            return true;
        }

        private static bool TryAddCounter(
            BuqiRunEventRuntimeState state,
            string counterId,
            int amount,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(counterId))
            {
                error = "Counter id is required.";
                return false;
            }

            BuqiRunEventCounter counter = null;
            for (int index = 0; index < state.Counters.Count; index++)
            {
                if (string.Equals(state.Counters[index].CounterId, counterId, StringComparison.Ordinal))
                {
                    counter = state.Counters[index];
                    break;
                }
            }
            long value = (long)(counter?.Value ?? 0) + amount;
            if (value < int.MinValue || value > int.MaxValue)
            {
                error = "Counter result is out of range.";
                return false;
            }

            if (counter == null)
            {
                state.Counters.Add(new BuqiRunEventCounter { CounterId = counterId, Value = (int)value });
            }
            else
            {
                counter.Value = (int)value;
            }
            error = string.Empty;
            return true;
        }

        private static BuqiRunResolutionRecord FindResolution(
            BuqiRunEventRuntimeState state,
            string resolutionId)
        {
            for (int index = 0; index < state.AppliedResolutions.Count; index++)
            {
                if (string.Equals(state.AppliedResolutions[index].ResolutionId, resolutionId, StringComparison.Ordinal))
                    return state.AppliedResolutions[index];
            }
            return null;
        }

        private static BuqiRunEventOptionDefinition FindOption(
            IReadOnlyList<BuqiRunEventOptionDefinition> options,
            string optionId)
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (string.Equals(options[index].OptionId, optionId, StringComparison.Ordinal))
                    return options[index];
            }
            return null;
        }

        private static bool TryGetFrozenValue(
            IReadOnlyList<BuqiRunEventFrozenValue> values,
            string actionId,
            out string value)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index].ActionId, actionId, StringComparison.Ordinal))
                {
                    value = values[index].Value;
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
            value = string.Empty;
            return false;
        }

        private static bool TargetsAreUnique(IReadOnlyList<BuqiRunEventTargetSelection> targets)
        {
            if (targets == null)
                return true;
            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < targets.Count; index++)
            {
                if (targets[index] == null || string.IsNullOrWhiteSpace(targets[index].ActionId) ||
                    !actionIds.Add(targets[index].ActionId))
                {
                    return false;
                }
            }
            return true;
        }

        private static string CreateRequestFingerprint(BuqiRunEventChoiceRequest request)
        {
            var orderedTargets = request.Targets == null
                ? new List<BuqiRunEventTargetSelection>()
                : new List<BuqiRunEventTargetSelection>(request.Targets);
            orderedTargets.Sort((left, right) =>
            {
                int action = string.CompareOrdinal(left.ActionId, right.ActionId);
                return action != 0
                    ? action
                    : string.CompareOrdinal(left.InstanceId, right.InstanceId);
            });

            var builder = new StringBuilder();
            AppendFingerprintPart(builder, request.EventId);
            AppendFingerprintPart(builder, request.OptionId);
            for (int index = 0; index < orderedTargets.Count; index++)
            {
                AppendFingerprintPart(builder, orderedTargets[index].ActionId);
                AppendFingerprintPart(builder, orderedTargets[index].InstanceId);
            }
            return builder.ToString();
        }

        private static void AppendFingerprintPart(StringBuilder builder, string value)
        {
            string normalized = value ?? string.Empty;
            builder.Append(normalized.Length);
            builder.Append(':');
            builder.Append(normalized);
            builder.Append('|');
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            if (values == null)
                return false;
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ClearSlots(IList<string> slots, string instanceId)
        {
            for (int index = 0; index < slots.Count; index++)
            {
                if (string.Equals(slots[index], instanceId, StringComparison.Ordinal))
                    slots[index] = string.Empty;
            }
        }

        private static void RemoveTriggeredSchedule(
            IList<BuqiRunScheduledReturn> scheduledReturns,
            string scheduleId)
        {
            if (string.IsNullOrEmpty(scheduleId))
                return;
            for (int index = scheduledReturns.Count - 1; index >= 0; index--)
            {
                if (string.Equals(scheduledReturns[index].ScheduleId, scheduleId, StringComparison.Ordinal))
                    scheduledReturns.RemoveAt(index);
            }
        }

        private static BuqiRunEventExecutionResult Success(BuqiRunEventRuntimeState state, bool replayed)
        {
            return new BuqiRunEventExecutionResult
            {
                Success = true,
                Replayed = replayed,
                State = state,
            };
        }

        private static BuqiRunEventExecutionResult Fail(BuqiRunEventRuntimeState source, string reason)
        {
            return new BuqiRunEventExecutionResult
            {
                Success = false,
                FailureReason = reason,
                State = source.Clone(),
            };
        }
    }
}
