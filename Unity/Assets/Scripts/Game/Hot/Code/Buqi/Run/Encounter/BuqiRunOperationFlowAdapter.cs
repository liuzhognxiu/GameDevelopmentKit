using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Training;

namespace Game.Hot.Buqi.Run.Encounter
{
    public sealed class BuqiRunOperationEventView
    {
        public string EventId = string.Empty;
        public int Day;
        public BuqiRunPeriod Period;
        public string TriggeredScheduleId = string.Empty;
        public bool IsScheduledReturn;
        public List<string> OptionIds = new List<string>();
        public List<BuqiRunOperationEventOptionView> Options =
            new List<BuqiRunOperationEventOptionView>();
        public List<BuqiRunEventFrozenValue> FrozenResults =
            new List<BuqiRunEventFrozenValue>();
    }

    public sealed class BuqiRunOperationEventOptionView
    {
        public string OptionId = string.Empty;
        public int CoinCost;
        public bool Eligible;
        public bool Affordable;
        public bool HasRequiredTargets;
        public List<BuqiRunOperationTargetRequirement> Targets =
            new List<BuqiRunOperationTargetRequirement>();
    }

    public sealed class BuqiRunOperationTargetRequirement
    {
        public string ActionId = string.Empty;
        public BuqiRunEventActionKind ActionKind;
        public string BuildTag = string.Empty;
        public List<string> CandidateInstanceIds = new List<string>();
    }

    public sealed class BuqiRunOperationTrainingOffer
    {
        public string TrainingId = string.Empty;
        public BuqiRunTrainingKind Kind;
        public int CoinCost;
        public string CounterCostId = string.Empty;
        public int CounterCost;
        public string RequiredBuildTag = string.Empty;
        public bool RequiresTarget;
        public bool Eligible;
        public bool Affordable;
        public bool HasEligibleTarget;
        public bool Available;
        public List<string> CandidateInstanceIds = new List<string>();
    }

    public sealed class BuqiRunOperationView
    {
        public string OperationId = string.Empty;
        public bool Consumed;
        public int Day;
        public BuqiRunPeriod Period;
        public int Experience;
        public BuqiRunOperationEventView Event;
        public List<BuqiRunOperationTrainingOffer> TrainingOffers =
            new List<BuqiRunOperationTrainingOffer>();
        public List<string> Flags = new List<string>();
        public List<BuqiRunScheduledReturn> ScheduledReturns = new List<BuqiRunScheduledReturn>();
    }

    public sealed class BuqiRunOperationFlowResult
    {
        public bool Success;
        public bool Created;
        public bool Replayed;
        public string FailureReason = string.Empty;
        public BuqiRunEventRuntimeState State = null!;
        public BuqiRunOperationView View = null!;
    }

    public sealed class BuqiRunOperationFlowAdapter
    {
        private readonly IBuqiRunEventDefinitionCatalog m_Events;
        private readonly IBuqiRunEventItemCatalog m_Items;
        private readonly IBuqiRunTrainingDefinitionCatalog m_Training;
        private readonly BuqiRunEventSelector m_Selector;
        private readonly BuqiRunEventExecutor m_Executor;
        private readonly BuqiRunTrainingService m_TrainingService;
        private readonly BuqiRunEventSaveCodec m_SaveCodec = new BuqiRunEventSaveCodec();

        public BuqiRunOperationFlowAdapter(
            IBuqiRunEventDefinitionCatalog events,
            IBuqiRunEventItemCatalog items,
            IBuqiRunTrainingDefinitionCatalog training)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            m_Events = events;
            m_Items = items ?? throw new ArgumentNullException(nameof(items));
            m_Training = training ?? throw new ArgumentNullException(nameof(training));
            m_Selector = new BuqiRunEventSelector(events, items);
            m_Executor = new BuqiRunEventExecutor(events, items);
            m_TrainingService = new BuqiRunTrainingService(training, items);
        }

        public BuqiRunEventRuntimeState CreateState(BuqiRunEconomySnapshot economy)
        {
            if (economy == null)
                throw new ArgumentNullException(nameof(economy));

            return new BuqiRunEventRuntimeState
            {
                Economy = economy.Clone(),
            };
        }

        public BuqiRunOperationView Compose(BuqiRunEventRuntimeState source)
        {
            ValidateState(source);
            var view = new BuqiRunOperationView
            {
                OperationId = CreateOperationId(source),
                Consumed = IsOperationConsumed(source),
                Day = source.Economy.Run.Day,
                Period = source.Economy.Run.Period,
                Experience = source.Experience,
            };

            AddSortedDistinct(source.Flags, view.Flags);
            if (source.ScheduledReturns != null)
            {
                for (int index = 0; index < source.ScheduledReturns.Count; index++)
                {
                    BuqiRunScheduledReturn scheduled = source.ScheduledReturns[index];
                    if (scheduled != null)
                        view.ScheduledReturns.Add(scheduled.Clone());
                }
                view.ScheduledReturns.Sort((left, right) =>
                    string.CompareOrdinal(left.ScheduleId, right.ScheduleId));
            }

            BuqiRunPendingEvent pending = source.PendingEvent;
            if (pending != null && pending.IsActive)
            {
                view.Event = new BuqiRunOperationEventView
                {
                    EventId = pending.EventId,
                    Day = pending.Day,
                    Period = pending.Period,
                    TriggeredScheduleId = pending.TriggeredScheduleId ?? string.Empty,
                    IsScheduledReturn = !string.IsNullOrWhiteSpace(pending.TriggeredScheduleId),
                    OptionIds = pending.OptionIds == null
                        ? new List<string>()
                        : new List<string>(pending.OptionIds),
                };
                if (pending.RandomResults != null)
                {
                    for (int index = 0; index < pending.RandomResults.Count; index++)
                        view.Event.FrozenResults.Add(pending.RandomResults[index].Clone());
                }
                AddEventOptions(source, pending, view.Event.Options);
            }

            if ((pending == null || !pending.IsActive) && !view.Consumed)
                AddTrainingOffers(source, view.TrainingOffers);
            return view;
        }

        public BuqiRunOperationFlowResult OpenEvent(BuqiRunEventRuntimeState source)
        {
            ValidateState(source);
            if (IsOperationConsumed(source))
                return Failure(source, "当前经营时段已完成。");

            BuqiRunEventSelectionResult selected = m_Selector.Select(source);
            return selected.Success
                ? Success(selected.State, selected.Created, false)
                : Failure(selected.State, selected.FailureReason);
        }

        public BuqiRunOperationFlowResult ExecuteEvent(
            BuqiRunEventRuntimeState source,
            BuqiRunEventChoiceRequest request)
        {
            ValidateState(source);
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            BuqiRunEventExecutionResult executed = m_Executor.Execute(
                source,
                NormalizeEventRequest(source, request));
            return executed.Success
                ? Success(executed.State, false, executed.Replayed)
                : Failure(executed.State, executed.FailureReason);
        }

        public BuqiRunOperationFlowResult ExecuteTraining(
            BuqiRunEventRuntimeState source,
            BuqiRunTrainingRequest request)
        {
            ValidateState(source);
            if (source.PendingEvent != null && source.PendingEvent.IsActive)
                return Failure(source, "请先完成当前事件，再进行训练。");
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            BuqiRunTrainingResult trained = m_TrainingService.Execute(
                source,
                new BuqiRunTrainingRequest
                {
                    ResolutionId = CreateOperationId(source),
                    TrainingId = request.TrainingId,
                    TargetInstanceId = request.TargetInstanceId,
                });
            return trained.Success
                ? Success(trained.State, false, trained.Replayed)
                : Failure(trained.State, trained.FailureReason);
        }

        public BuqiRunOperationFlowResult SynchronizeEconomy(
            BuqiRunEventRuntimeState source,
            BuqiRunEconomySnapshot expectedBase,
            BuqiRunEconomySnapshot economy)
        {
            ValidateState(source);
            if (expectedBase == null)
                throw new ArgumentNullException(nameof(expectedBase));
            if (economy == null)
                throw new ArgumentNullException(nameof(economy));
            if (!EconomiesEqual(source.Economy, expectedBase))
                return Failure(source, "当前资源状态已变化，请重新操作。");
            if (!MatchesRunIdentity(expectedBase, economy))
                return Failure(source.Clone(), "资源数据与当前游戏进度不匹配。");
            if (economy.Run.Revision < expectedBase.Run.Revision)
                return Failure(source.Clone(), "资源数据已过期，请重新操作。");

            BuqiRunPendingEvent pending = source.PendingEvent;
            if (pending != null && pending.IsActive &&
                (pending.Day != economy.Run.Day || pending.Period != economy.Run.Period))
            {
                return Failure(source.Clone(), "请先完成当前事件，再进入下一时段。");
            }

            BuqiRunEventRuntimeState working = source.Clone();
            working.Economy = economy.Clone();
            working = BuqiRunEventTransitions.RemoveExpiredReturns(working);
            return Success(working, false, false);
        }

        public BuqiRunEventSaveData CaptureSave(BuqiRunEventRuntimeState source)
        {
            ValidateState(source);
            return m_SaveCodec.Capture(source);
        }

        public bool TryRestore(
            BuqiRunEconomySnapshot economy,
            BuqiRunEventSaveData save,
            out BuqiRunEventRuntimeState state,
            out string error)
        {
            return m_SaveCodec.TryRestore(economy, save, out state, out error);
        }

        private void AddTrainingOffers(
            BuqiRunEventRuntimeState source,
            List<BuqiRunOperationTrainingOffer> target)
        {
            IReadOnlyList<BuqiRunTrainingDefinition> configured = m_Training.TrainingDefinitions;
            if (configured == null)
                return;

            var ordered = new List<BuqiRunTrainingDefinition>();
            for (int index = 0; index < configured.Count; index++)
            {
                BuqiRunTrainingDefinition definition = configured[index];
                if (IsValidTrainingDefinition(definition))
                    ordered.Add(definition);
            }
            ordered.Sort((left, right) => string.CompareOrdinal(left.TrainingId, right.TrainingId));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ordered.Count; index++)
            {
                BuqiRunTrainingDefinition definition = ordered[index];
                if (!seen.Add(definition.TrainingId))
                    continue;

                bool requiresTarget = definition.Kind == BuqiRunTrainingKind.Upgrade ||
                                      definition.Kind == BuqiRunTrainingKind.DirectedStrengthening;
                bool eligible = IsEligible(definition.Eligibility, source);
                bool affordable = CanPay(source, definition);
                List<string> targetIds = requiresTarget
                    ? GetEligibleTargetIds(source, definition)
                    : new List<string>();
                bool hasTarget = !requiresTarget || targetIds.Count > 0;
                bool available = eligible && affordable && hasTarget &&
                                 CanExecuteTraining(source, definition, requiresTarget);
                target.Add(new BuqiRunOperationTrainingOffer
                {
                    TrainingId = definition.TrainingId,
                    Kind = definition.Kind,
                    CoinCost = definition.CoinCost,
                    CounterCostId = definition.CounterCostId ?? string.Empty,
                    CounterCost = definition.CounterCost,
                    RequiredBuildTag = definition.RequiredBuildTag ?? string.Empty,
                    RequiresTarget = requiresTarget,
                    Eligible = eligible,
                    Affordable = affordable,
                    HasEligibleTarget = hasTarget,
                    Available = available,
                    CandidateInstanceIds = targetIds,
                });
            }
        }

        private void AddEventOptions(
            BuqiRunEventRuntimeState source,
            BuqiRunPendingEvent pending,
            List<BuqiRunOperationEventOptionView> target)
        {
            if (!m_Events.TryGet(pending.EventId, out BuqiRunEventDefinition definition) ||
                definition?.Options == null)
            {
                return;
            }

            for (int optionIdIndex = 0; optionIdIndex < pending.OptionIds.Count; optionIdIndex++)
            {
                string optionId = pending.OptionIds[optionIdIndex];
                BuqiRunEventOptionDefinition option = null;
                for (int optionIndex = 0; optionIndex < definition.Options.Count; optionIndex++)
                {
                    if (string.Equals(definition.Options[optionIndex].OptionId, optionId, StringComparison.Ordinal))
                    {
                        option = definition.Options[optionIndex];
                        break;
                    }
                }
                if (option == null)
                    continue;

                var projected = new BuqiRunOperationEventOptionView
                {
                    OptionId = option.OptionId,
                    CoinCost = option.CoinCost,
                    Eligible = IsEligible(option.Eligibility, source),
                    Affordable = source.Economy.Run.Coins >= option.CoinCost,
                    HasRequiredTargets = true,
                };
                if (option.Actions != null)
                {
                    for (int actionIndex = 0; actionIndex < option.Actions.Count; actionIndex++)
                    {
                        BuqiRunEventActionDefinition action = option.Actions[actionIndex];
                        if (!RequiresItemTarget(action.Kind))
                            continue;

                        BuqiRunOperationTargetRequirement requirement = BuildTargetRequirement(source, action);
                        projected.Targets.Add(requirement);
                        if (requirement.CandidateInstanceIds.Count == 0)
                            projected.HasRequiredTargets = false;
                    }
                }
                target.Add(projected);
            }
        }

        private BuqiRunOperationTargetRequirement BuildTargetRequirement(
            BuqiRunEventRuntimeState source,
            BuqiRunEventActionDefinition action)
        {
            var requirement = new BuqiRunOperationTargetRequirement
            {
                ActionId = action.ActionId,
                ActionKind = action.Kind,
                BuildTag = action.BuildTag ?? string.Empty,
            };
            foreach (BuqiRunItemInstance item in source.Economy.Items.Values)
            {
                if (!string.IsNullOrWhiteSpace(action.BuildTag) &&
                    !m_Items.HasBuildTag(item.DefinitionId, action.BuildTag))
                {
                    continue;
                }
                if (action.Kind == BuqiRunEventActionKind.UpgradeItem)
                {
                    int steps = action.QualitySteps == 0 ? 1 : action.QualitySteps;
                    if (steps < 1 || (long)item.Quality + steps > (int)BuqiRunItemQuality.Finalized)
                        continue;
                }
                if (action.Kind == BuqiRunEventActionKind.ApplyRefinement &&
                    !string.IsNullOrWhiteSpace(item.RefinementId))
                {
                    continue;
                }
                requirement.CandidateInstanceIds.Add(item.InstanceId);
            }
            requirement.CandidateInstanceIds.Sort(StringComparer.Ordinal);
            return requirement;
        }

        private bool CanExecuteTraining(
            BuqiRunEventRuntimeState source,
            BuqiRunTrainingDefinition definition,
            bool requiresTarget)
        {
            string preflightId = CreatePreflightResolutionId(source, definition.TrainingId);
            if (!requiresTarget)
            {
                return m_TrainingService.Execute(
                    source,
                    new BuqiRunTrainingRequest
                    {
                        ResolutionId = preflightId,
                        TrainingId = definition.TrainingId,
                    }).Success;
            }

            foreach (BuqiRunItemInstance item in source.Economy.Items.Values)
            {
                BuqiRunTrainingResult result = m_TrainingService.Execute(
                    source,
                    new BuqiRunTrainingRequest
                    {
                        ResolutionId = preflightId,
                        TrainingId = definition.TrainingId,
                        TargetInstanceId = item.InstanceId,
                    });
                if (result.Success)
                    return true;
            }
            return false;
        }

        private static bool RequiresItemTarget(BuqiRunEventActionKind kind)
        {
            return kind == BuqiRunEventActionKind.UpgradeItem ||
                   kind == BuqiRunEventActionKind.SacrificeItem ||
                   kind == BuqiRunEventActionKind.ApplyRefinement;
        }

        private static BuqiRunEventChoiceRequest NormalizeEventRequest(
            BuqiRunEventRuntimeState source,
            BuqiRunEventChoiceRequest request)
        {
            var normalized = new BuqiRunEventChoiceRequest
            {
                ResolutionId = CreateOperationId(source),
                EventId = request.EventId,
                OptionId = request.OptionId,
            };
            if (request.Targets != null)
            {
                for (int index = 0; index < request.Targets.Count; index++)
                {
                    BuqiRunEventTargetSelection target = request.Targets[index];
                    if (target == null)
                        continue;
                    normalized.Targets.Add(new BuqiRunEventTargetSelection
                    {
                        ActionId = target.ActionId,
                        InstanceId = target.InstanceId,
                    });
                }
            }
            return normalized;
        }

        private static string CreateOperationId(BuqiRunEventRuntimeState source)
        {
            return BuqiText.Format(
                "operation:{0}:{1}:{2}:{3}",
                source.Economy.Run.RunSeed,
                source.Economy.Run.Day,
                (int)source.Economy.Run.Period,
                source.Economy.Run.EncounterIndex);
        }

        private static string CreatePreflightResolutionId(
            BuqiRunEventRuntimeState source,
            string trainingId)
        {
            string prefix = BuqiText.Format(
                "__operation_preflight__:{0}:{1}",
                CreateOperationId(source),
                trainingId);
            string candidate = prefix;
            int suffix = 0;
            while (HasResolution(source, candidate))
                candidate = BuqiText.Format("{0}:{1}", prefix, ++suffix);
            return candidate;
        }

        private static bool IsOperationConsumed(BuqiRunEventRuntimeState source)
        {
            return HasResolution(source, CreateOperationId(source));
        }

        private static bool HasResolution(BuqiRunEventRuntimeState source, string resolutionId)
        {
            for (int index = 0; index < source.AppliedResolutions.Count; index++)
            {
                if (string.Equals(
                        source.AppliedResolutions[index].ResolutionId,
                        resolutionId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsEligible(
            BuqiRunEventEligibility eligibility,
            BuqiRunEventRuntimeState state)
        {
            if (!IsValidEligibility(eligibility))
                return false;

            int day = state.Economy.Run.Day;
            if (day < eligibility.MinDay || day > eligibility.MaxDay)
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
                if (!HasOwnedBuildTag(state, eligibility.RequiredBuildTags[tagIndex]))
                    return false;
            }

            return true;
        }

        private List<string> GetEligibleTargetIds(
            BuqiRunEventRuntimeState state,
            BuqiRunTrainingDefinition definition)
        {
            var result = new List<string>();
            foreach (BuqiRunItemInstance item in state.Economy.Items.Values)
            {
                if (!string.IsNullOrWhiteSpace(definition.RequiredBuildTag) &&
                    !m_Items.HasBuildTag(item.DefinitionId, definition.RequiredBuildTag))
                {
                    continue;
                }

                if (definition.Kind == BuqiRunTrainingKind.Upgrade)
                {
                    long quality = (long)item.Quality + definition.QualitySteps;
                    if (quality <= (int)BuqiRunItemQuality.Finalized)
                        result.Add(item.InstanceId);
                    continue;
                }

                if (definition.Kind == BuqiRunTrainingKind.DirectedStrengthening &&
                    (!string.IsNullOrWhiteSpace(definition.RefinementId) &&
                     !string.IsNullOrWhiteSpace(item.RefinementId)))
                {
                    continue;
                }

                result.Add(item.InstanceId);
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private bool HasOwnedBuildTag(BuqiRunEventRuntimeState state, string buildTag)
        {
            foreach (BuqiRunItemInstance item in state.Economy.Items.Values)
            {
                if (m_Items.HasBuildTag(item.DefinitionId, buildTag))
                    return true;
            }
            return false;
        }

        private static bool CanPay(
            BuqiRunEventRuntimeState state,
            BuqiRunTrainingDefinition definition)
        {
            if (state.Economy.Run.Coins < definition.CoinCost)
                return false;
            if (definition.CounterCost == 0)
                return true;

            for (int index = 0; index < state.Counters.Count; index++)
            {
                BuqiRunEventCounter counter = state.Counters[index];
                if (string.Equals(counter.CounterId, definition.CounterCostId, StringComparison.Ordinal))
                    return counter.Value >= definition.CounterCost;
            }
            return false;
        }

        private static bool IsValidTrainingDefinition(BuqiRunTrainingDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.TrainingId) ||
                !IsValidEligibility(definition.Eligibility) || definition.CoinCost < 0 ||
                definition.CounterCost < 0 || definition.CoinReward < 0 ||
                definition.ExperienceReward < 0 || definition.RewardCounterAmount < 0 ||
                definition.ModifierDurationBattles < 0 ||
                !Enum.IsDefined(typeof(BuqiRunTrainingKind), definition.Kind) ||
                !Enum.IsDefined(typeof(BuqiRunModifierKind), definition.ModifierKind) ||
                (definition.CounterCost > 0 && string.IsNullOrWhiteSpace(definition.CounterCostId)))
            {
                return false;
            }

            switch (definition.Kind)
            {
                case BuqiRunTrainingKind.Upgrade:
                    return definition.QualitySteps > 0;
                case BuqiRunTrainingKind.DirectedStrengthening:
                    return !string.IsNullOrWhiteSpace(definition.RequiredBuildTag) &&
                           (!string.IsNullOrWhiteSpace(definition.RefinementId) ||
                            definition.ModifierDurationBattles > 0);
                case BuqiRunTrainingKind.Economy:
                    return definition.CoinReward > 0 || definition.RewardCounterAmount > 0;
                case BuqiRunTrainingKind.Experience:
                    return definition.ExperienceReward > 0 || definition.RewardCounterAmount > 0;
                default:
                    return false;
            }
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

        private static bool MatchesRunIdentity(
            BuqiRunEconomySnapshot current,
            BuqiRunEconomySnapshot replacement)
        {
            return current.Run.RunSeed == replacement.Run.RunSeed &&
                   string.Equals(
                       current.Run.ContentVersion,
                       replacement.Run.ContentVersion,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       current.Run.RuleVersion,
                       replacement.Run.RuleVersion,
                       StringComparison.Ordinal);
        }

        private static bool EconomiesEqual(
            BuqiRunEconomySnapshot left,
            BuqiRunEconomySnapshot right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Run == null || right.Run == null ||
                left.NextItemOrdinal != right.NextItemOrdinal ||
                !RunStatesEqual(left.Run, right.Run) ||
                left.Items == null || right.Items == null ||
                left.Items.Count != right.Items.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, BuqiRunItemInstance> pair in left.Items)
            {
                if (!right.Items.TryGetValue(pair.Key, out BuqiRunItemInstance other) ||
                    pair.Value == null || other == null ||
                    !string.Equals(pair.Value.InstanceId, other.InstanceId, StringComparison.Ordinal) ||
                    !string.Equals(pair.Value.DefinitionId, other.DefinitionId, StringComparison.Ordinal) ||
                    pair.Value.Quality != other.Quality ||
                    !string.Equals(pair.Value.RefinementId, other.RefinementId, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool RunStatesEqual(BuqiRunState left, BuqiRunState right)
        {
            return string.Equals(left.ContentVersion, right.ContentVersion, StringComparison.Ordinal) &&
                   string.Equals(left.RuleVersion, right.RuleVersion, StringComparison.Ordinal) &&
                   left.RunSeed == right.RunSeed &&
                   left.RngCursor == right.RngCursor &&
                   left.Revision == right.Revision &&
                   left.Day == right.Day &&
                   left.EncounterIndex == right.EncounterIndex &&
                   left.Period == right.Period &&
                   left.Phase == right.Phase &&
                   left.Outcome == right.Outcome &&
                   left.Coins == right.Coins &&
                   left.Wins == right.Wins &&
                   left.DaoSeals == right.DaoSeals &&
                   left.CurrentOmen == right.CurrentOmen &&
                   left.Lives == right.Lives &&
                   left.TribulationRoute == right.TribulationRoute &&
                   left.TribulationDaoSealsSpent == right.TribulationDaoSealsSpent &&
                   left.TribulationStage == right.TribulationStage &&
                   left.TribulationSuccesses == right.TribulationSuccesses &&
                   SequenceEqual(left.BoardInstanceIds, right.BoardInstanceIds) &&
                   SequenceEqual(left.StorageInstanceIds, right.StorageInstanceIds) &&
                   SetEqual(left.AppliedCommandIds, right.AppliedCommandIds) &&
                   SetEqual(left.AppliedSettlementIds, right.AppliedSettlementIds);
        }

        private static bool SequenceEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static bool SetEqual(HashSet<string> left, HashSet<string> right)
        {
            if (ReferenceEquals(left, right))
                return true;
            return left != null && right != null && left.SetEquals(right);
        }

        private static void AddSortedDistinct(
            IReadOnlyList<string> source,
            List<string> target)
        {
            if (source == null)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                string value = source[index];
                if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                    target.Add(value);
            }
            target.Sort(StringComparer.Ordinal);
        }

        private static void ValidateState(BuqiRunEventRuntimeState source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.Economy == null || source.Economy.Run == null)
                throw new ArgumentException("经营状态缺少资源数据。", nameof(source));

            BuqiRunPendingEvent pending = source.PendingEvent;
            if (pending != null && pending.IsActive &&
                (pending.Day != source.Economy.Run.Day ||
                 pending.Period != source.Economy.Run.Period ||
                 pending.OptionIds == null || pending.OptionIds.Count != 3 ||
                 pending.RandomResults == null))
            {
                throw new ArgumentException(
                    "当前事件与经营时段不匹配。",
                    nameof(source));
            }
        }

        private BuqiRunOperationFlowResult Success(
            BuqiRunEventRuntimeState state,
            bool created,
            bool replayed)
        {
            return new BuqiRunOperationFlowResult
            {
                Success = true,
                Created = created,
                Replayed = replayed,
                State = state,
                View = Compose(state),
            };
        }

        private BuqiRunOperationFlowResult Failure(
            BuqiRunEventRuntimeState state,
            string reason)
        {
            BuqiRunEventRuntimeState unchanged = state.Clone();
            return new BuqiRunOperationFlowResult
            {
                Success = false,
                FailureReason = reason ?? string.Empty,
                State = unchanged,
                View = Compose(unchanged),
            };
        }
    }
}
