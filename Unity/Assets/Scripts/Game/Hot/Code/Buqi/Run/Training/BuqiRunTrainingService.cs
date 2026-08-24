using System;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;

namespace Game.Hot.Buqi.Run.Training
{
    public sealed class BuqiRunTrainingService
    {
        private const string TrainingSourceKind = "training";

        private readonly IBuqiRunTrainingCatalog m_Training;
        private readonly IBuqiRunBuildTagCatalog m_Items;

        public BuqiRunTrainingService(
            IBuqiRunTrainingCatalog training,
            IBuqiRunBuildTagCatalog items)
        {
            m_Training = training ?? throw new ArgumentNullException(nameof(training));
            m_Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public BuqiRunTrainingResult Execute(
            BuqiRunEventRuntimeState source,
            BuqiRunTrainingRequest request)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.ResolutionId) ||
                string.IsNullOrWhiteSpace(request.TrainingId))
            {
                return Fail(source, "Resolution id and training id are required.");
            }

            string choiceId = string.IsNullOrWhiteSpace(request.TargetInstanceId)
                ? "apply"
                : request.TargetInstanceId;
            string requestFingerprint = CreateRequestFingerprint(request);
            BuqiRunResolutionRecord prior = FindResolution(source, request.ResolutionId);
            if (prior != null)
            {
                bool matches = string.Equals(prior.SourceKind, TrainingSourceKind, StringComparison.Ordinal) &&
                               string.Equals(prior.ContentId, request.TrainingId, StringComparison.Ordinal) &&
                               string.Equals(prior.ChoiceId, choiceId, StringComparison.Ordinal) &&
                               string.Equals(prior.RequestFingerprint, requestFingerprint, StringComparison.Ordinal);
                return matches
                    ? Success(source.Clone(), true)
                    : Fail(source, "Resolution id was already used for another decision.");
            }

            if (!m_Training.TryGet(request.TrainingId, out BuqiRunTrainingDefinition definition) ||
                !IsValidDefinition(definition))
            {
                return Fail(source, "Training definition is unavailable or invalid.");
            }
            if (!BuqiRunEventRuntimeRules.IsEligible(definition.Eligibility, source, m_Items))
                return Fail(source, "Training conditions are not satisfied.");
            if (definition.MaxPerRun > 0 && CountAppliedTraining(source, definition.TrainingId) >= definition.MaxPerRun)
                return Fail(source, "Training project has reached its run limit.");

            BuqiRunEventRuntimeState working = source.Clone();
            if (!TryPayCosts(working, definition, out string paymentError))
                return Fail(source, paymentError);
            if (!TryApplyTraining(working, definition, request, out string trainingError))
                return Fail(source, trainingError);

            working.AppliedResolutions.Add(new BuqiRunResolutionRecord
            {
                ResolutionId = request.ResolutionId,
                SourceKind = TrainingSourceKind,
                ContentId = definition.TrainingId,
                ChoiceId = choiceId,
                RequestFingerprint = requestFingerprint,
            });
            working.Economy.Run.Revision++;
            return Success(working, false);
        }

        private bool TryApplyTraining(
            BuqiRunEventRuntimeState state,
            BuqiRunTrainingDefinition definition,
            BuqiRunTrainingRequest request,
            out string error)
        {
            switch (definition.Kind)
            {
                case BuqiRunTrainingKind.Upgrade:
                    if (!TryGetTarget(state, request.TargetInstanceId, definition.RequiredBuildTag,
                            out BuqiRunItemInstance upgradeTarget, out error))
                    {
                        return false;
                    }
                    long targetQuality = (long)upgradeTarget.Quality + definition.QualitySteps;
                    if (targetQuality > (int)BuqiRunItemQuality.Finalized)
                    {
                        error = "Requested training upgrade is out of range.";
                        return false;
                    }
                    upgradeTarget.Quality = (BuqiRunItemQuality)targetQuality;
                    error = string.Empty;
                    return true;

                case BuqiRunTrainingKind.DirectedStrengthening:
                    if (!TryGetTarget(state, request.TargetInstanceId, definition.RequiredBuildTag,
                            out BuqiRunItemInstance directedTarget, out error))
                    {
                        return false;
                    }
                    if (!string.IsNullOrWhiteSpace(definition.RefinementId))
                    {
                        if (!string.IsNullOrWhiteSpace(directedTarget.RefinementId))
                        {
                            error = "Training target already has a refinement.";
                            return false;
                        }
                        directedTarget.RefinementId = definition.RefinementId;
                    }
                    if (definition.ModifierDurationBattles > 0)
                    {
                        state.TemporaryModifiers.Add(new BuqiRunTemporaryModifier
                        {
                            ModifierId = GameFramework.Utility.Text.Format(
                                "{0}:{1}",
                                request.ResolutionId,
                                definition.TrainingId),
                            SourceId = string.IsNullOrWhiteSpace(definition.ModifierId)
                                ? definition.TrainingId
                                : definition.ModifierId,
                            BuildTag = definition.RequiredBuildTag,
                            Kind = definition.ModifierKind,
                            Value = definition.ModifierValue,
                            RemainingBattles = definition.ModifierDurationBattles,
                            DurationTicks = definition.ModifierDurationTicks,
                        });
                    }
                    error = string.Empty;
                    return true;

                case BuqiRunTrainingKind.Economy:
                    if (!TryAddCoins(state, definition.CoinReward, out error))
                        return false;
                    return TryAddCounter(
                        state,
                        definition.RewardCounterId,
                        definition.RewardCounterAmount,
                        allowEmpty: true,
                        out error);

                case BuqiRunTrainingKind.Experience:
                    long experience = (long)state.Experience + definition.ExperienceReward;
                    if (experience > int.MaxValue)
                    {
                        error = "Training experience result is out of range.";
                        return false;
                    }
                    state.Experience = (int)experience;
                    return TryAddCounter(
                        state,
                        definition.RewardCounterId,
                        definition.RewardCounterAmount,
                        allowEmpty: true,
                        out error);

                default:
                    error = "Training kind is invalid.";
                    return false;
            }
        }

        private static bool TryPayCosts(
            BuqiRunEventRuntimeState state,
            BuqiRunTrainingDefinition definition,
            out string error)
        {
            if (state.Economy.Run.Coins < definition.CoinCost)
            {
                error = "Not enough coins for training.";
                return false;
            }
            state.Economy.Run.Coins -= definition.CoinCost;

            if (definition.CounterCost == 0)
            {
                error = string.Empty;
                return true;
            }

            BuqiRunEventCounter counter = FindCounter(state, definition.CounterCostId);
            if (counter == null || counter.Value < definition.CounterCost)
            {
                error = "Not enough configured counter resource for training.";
                return false;
            }
            counter.Value -= definition.CounterCost;
            error = string.Empty;
            return true;
        }

        private bool TryGetTarget(
            BuqiRunEventRuntimeState state,
            string instanceId,
            string buildTag,
            out BuqiRunItemInstance item,
            out string error)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(instanceId) ||
                !state.Economy.Items.TryGetValue(instanceId, out item))
            {
                error = "A valid training item target is required.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(buildTag) &&
                !m_Items.HasBuildTag(item.DefinitionId, buildTag))
            {
                error = "Training target does not match the required build tag.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryAddCoins(BuqiRunEventRuntimeState state, int amount, out string error)
        {
            long coins = (long)state.Economy.Run.Coins + amount;
            if (coins < 0 || coins > int.MaxValue)
            {
                error = "Training coin result is out of range.";
                return false;
            }
            state.Economy.Run.Coins = (int)coins;
            error = string.Empty;
            return true;
        }

        private static bool TryAddCounter(
            BuqiRunEventRuntimeState state,
            string counterId,
            int amount,
            bool allowEmpty,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(counterId))
            {
                if (allowEmpty && amount == 0)
                {
                    error = string.Empty;
                    return true;
                }
                error = "Training reward counter id is required.";
                return false;
            }

            BuqiRunEventCounter counter = FindCounter(state, counterId);
            long value = (long)(counter?.Value ?? 0) + amount;
            if (value < int.MinValue || value > int.MaxValue)
            {
                error = "Training counter result is out of range.";
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

        private static BuqiRunEventCounter FindCounter(BuqiRunEventRuntimeState state, string counterId)
        {
            for (int index = 0; index < state.Counters.Count; index++)
            {
                if (string.Equals(state.Counters[index].CounterId, counterId, StringComparison.Ordinal))
                    return state.Counters[index];
            }
            return null;
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

        private static string CreateRequestFingerprint(BuqiRunTrainingRequest request)
        {
            string target = request.TargetInstanceId ?? string.Empty;
            return GameFramework.Utility.Text.Format(
                "{0}:{1}|{2}:{3}|",
                request.TrainingId.Length,
                request.TrainingId,
                target.Length,
                target);
        }

        private static int CountAppliedTraining(BuqiRunEventRuntimeState state, string trainingId)
        {
            int count = 0;
            for (int index = 0; index < state.AppliedResolutions.Count; index++)
            {
                BuqiRunResolutionRecord resolution = state.AppliedResolutions[index];
                if (string.Equals(resolution.SourceKind, TrainingSourceKind, StringComparison.Ordinal) &&
                    string.Equals(resolution.ContentId, trainingId, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool IsValidDefinition(BuqiRunTrainingDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.TrainingId) ||
                definition.Eligibility == null || definition.CoinCost < 0 ||
                definition.CounterCost < 0 || definition.CoinReward < 0 ||
                definition.ExperienceReward < 0 || definition.RewardCounterAmount < 0 ||
                definition.ModifierDurationBattles < 0 || definition.MaxPerRun < 0 ||
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

        private static BuqiRunTrainingResult Success(BuqiRunEventRuntimeState state, bool replayed)
        {
            return new BuqiRunTrainingResult
            {
                Success = true,
                Replayed = replayed,
                State = state,
            };
        }

        private static BuqiRunTrainingResult Fail(BuqiRunEventRuntimeState source, string reason)
        {
            return new BuqiRunTrainingResult
            {
                Success = false,
                FailureReason = reason,
                State = source.Clone(),
            };
        }
    }
}
