using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;

namespace Game.Hot.Buqi.Run.Integration
{
    public enum BuqiRunRewardKind
    {
        Coins = 0,
        Item = 1,
        Experience = 2,
        Upgrade = 3,
        Refinement = 4,
    }

    public sealed class BuqiRunRewardSettings
    {
        public int CandidateCount = 3;
        public int CoinAmount = 3;
        public int ExperienceAmount = 3;
        public int ExperiencePerLevel = 5;
        public List<string> ItemDefinitionIds = new List<string>();
        public List<string> RefinementIds = new List<string>();
    }

    [Serializable]
    public sealed class BuqiRunRewardCandidate
    {
        public string CandidateId = string.Empty;
        public BuqiRunRewardKind Kind;
        public int Amount;
        public string DefinitionId = string.Empty;
        public string RefinementId = string.Empty;
        public string Title = string.Empty;
        public string Description = string.Empty;

        public BuqiRunRewardCandidate Clone()
        {
            return (BuqiRunRewardCandidate)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class BuqiRunRewardState
    {
        public string StageId = string.Empty;
        public long RunSeed;
        public int Day;
        public BuqiRunPeriod Period;
        public int RuntimeRevision;
        public List<BuqiRunRewardCandidate> Candidates = new List<BuqiRunRewardCandidate>();
        public string SelectedCandidateId = string.Empty;
        public bool Claimed;
        public string ClaimedCandidateId = string.Empty;
        public string AppliedCommandId = string.Empty;
        public bool LevelUp;

        public BuqiRunRewardState Clone()
        {
            var clone = new BuqiRunRewardState
            {
                StageId = StageId,
                RunSeed = RunSeed,
                Day = Day,
                Period = Period,
                RuntimeRevision = RuntimeRevision,
                SelectedCandidateId = SelectedCandidateId,
                Claimed = Claimed,
                ClaimedCandidateId = ClaimedCandidateId,
                AppliedCommandId = AppliedCommandId,
                LevelUp = LevelUp,
            };
            for (int index = 0; index < Candidates.Count; index++)
                clone.Candidates.Add(Candidates[index].Clone());
            return clone;
        }
    }

    [Serializable]
    public sealed class BuqiRunRewardSaveData
    {
        public const string CurrentVersion = "buqi-reward-stage-v1";

        public string SchemaVersion = CurrentVersion;
        public BuqiRunRewardState Reward = new BuqiRunRewardState();
    }

    public sealed class BuqiRunRewardResult
    {
        public bool Success;
        public bool Replayed;
        public bool LevelUp;
        public string FailureReason = string.Empty;
        public BuqiRunEventRuntimeState Runtime = null!;
        public BuqiRunRewardState Reward = null!;
    }

    public sealed class BuqiRunRewardService
    {
        private const string RewardSourceKind = "reward";

        private readonly IBuqiRunItemCatalog m_Items;
        private readonly BuqiRunEconomyService m_Economy;
        private readonly BuqiRunRewardSettings m_Settings;

        public BuqiRunRewardService(IBuqiRunItemCatalog items, BuqiRunRewardSettings settings)
        {
            m_Items = items ?? throw new ArgumentNullException(nameof(items));
            m_Economy = new BuqiRunEconomyService(items);
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (settings.CandidateCount < 2 || settings.CandidateCount > 4)
                throw new ArgumentOutOfRangeException(nameof(settings.CandidateCount));
            if (settings.CoinAmount < 0 || settings.ExperienceAmount < 0 || settings.ExperiencePerLevel < 1)
                throw new ArgumentException("Reward amounts and level threshold are invalid.", nameof(settings));
        }

        public BuqiRunRewardState Open(BuqiRunEventRuntimeState runtime, string stageId)
        {
            ValidateRuntime(runtime);
            if (string.IsNullOrWhiteSpace(stageId))
                throw new ArgumentException("Reward stage id is required.", nameof(stageId));

            var state = new BuqiRunRewardState
            {
                StageId = stageId,
                RunSeed = runtime.Economy.Run.RunSeed,
                Day = runtime.Economy.Run.Day,
                Period = runtime.Economy.Run.Period,
                RuntimeRevision = runtime.Economy.Run.Revision,
            };
            AddCandidate(state, BuqiRunRewardKind.Coins, m_Settings.CoinAmount, string.Empty, string.Empty);
            AddCandidate(state, BuqiRunRewardKind.Experience, m_Settings.ExperienceAmount, string.Empty, string.Empty);

            int optionalIndex = StableIndex(stageId, 3);
            while (state.Candidates.Count < m_Settings.CandidateCount)
            {
                switch (optionalIndex % 3)
                {
                    case 0:
                        string itemId = FirstValidItemId();
                        if (!string.IsNullOrEmpty(itemId) && HasStorageSpace(runtime.Economy))
                            AddCandidate(state, BuqiRunRewardKind.Item, 1, itemId, string.Empty);
                        else
                            AddFallbackCandidate(state);
                        break;
                    case 1:
                        if (HasUpgradeableTarget(runtime))
                            AddCandidate(state, BuqiRunRewardKind.Upgrade, 1, string.Empty, string.Empty);
                        else
                            AddFallbackCandidate(state);
                        break;
                    default:
                        string refinementId = FirstNonEmpty(m_Settings.RefinementIds);
                        if (!string.IsNullOrEmpty(refinementId) && HasRefinementTarget(runtime))
                            AddCandidate(state, BuqiRunRewardKind.Refinement, 1, string.Empty, refinementId);
                        else
                            AddFallbackCandidate(state);
                        break;
                }
                optionalIndex++;
            }
            return state;
        }

        public int LevelForExperience(int experience)
        {
            if (experience < 0)
                throw new ArgumentOutOfRangeException(nameof(experience));
            return ResolveLevel(experience);
        }

        public BuqiRunRewardState Preview(BuqiRunRewardState source, string candidateId)
        {
            ValidateReward(source);
            BuqiRunRewardState working = source.Clone();
            if (source.Claimed)
                return working;
            if (FindCandidate(source, candidateId) == null)
                return working;
            working.SelectedCandidateId = candidateId;
            return working;
        }

        public BuqiRunRewardResult Claim(
            BuqiRunEventRuntimeState runtime,
            BuqiRunRewardState reward,
            string commandId,
            string targetInstanceId)
        {
            ValidateRuntime(runtime);
            ValidateReward(reward);
            if (!MatchesRuntime(reward, runtime))
                return Failure(runtime, reward, "Reward state does not match the current run.");
            if (string.IsNullOrWhiteSpace(commandId))
                return Failure(runtime, reward, "Reward command id is required.");

            BuqiRunResolutionRecord prior = FindResolution(runtime, commandId);
            if (prior != null)
            {
                bool matches = string.Equals(prior.SourceKind, RewardSourceKind, StringComparison.Ordinal) &&
                               string.Equals(prior.ContentId, reward.StageId, StringComparison.Ordinal) &&
                               string.Equals(prior.ChoiceId, reward.ClaimedCandidateId, StringComparison.Ordinal);
                return matches
                    ? Success(runtime.Clone(), reward.Clone(), true, reward.LevelUp)
                    : Failure(runtime, reward, "Reward command id was already used for another claim.");
            }
            if (reward.Claimed)
                return Failure(runtime, reward, "Reward has already been claimed.");

            BuqiRunRewardCandidate candidate = FindCandidate(reward, reward.SelectedCandidateId);
            if (candidate == null)
                return Failure(runtime, reward, "A reward must be previewed before it is claimed.");

            BuqiRunEventRuntimeState working = runtime.Clone();
            int levelBefore = ResolveLevel(working.Experience);
            if (!TryApply(working, candidate, targetInstanceId, out string error))
                return Failure(runtime, reward, error);

            working.AppliedResolutions.Add(new BuqiRunResolutionRecord
            {
                ResolutionId = commandId,
                SourceKind = RewardSourceKind,
                ContentId = reward.StageId,
                ChoiceId = candidate.CandidateId,
                RequestFingerprint = targetInstanceId ?? string.Empty,
            });
            working.Economy.Run.Revision = runtime.Economy.Run.Revision + 1;
            bool levelUp = ResolveLevel(working.Experience) > levelBefore;

            BuqiRunRewardState claimed = reward.Clone();
            claimed.Claimed = true;
            claimed.ClaimedCandidateId = candidate.CandidateId;
            claimed.AppliedCommandId = commandId;
            claimed.RuntimeRevision = working.Economy.Run.Revision;
            claimed.LevelUp = levelUp;
            return Success(working, claimed, false, levelUp);
        }

        public BuqiRunRewardSaveData Capture(BuqiRunRewardState source)
        {
            ValidateReward(source);
            return new BuqiRunRewardSaveData { Reward = source.Clone() };
        }

        public bool TryRestore(
            BuqiRunEventRuntimeState runtime,
            BuqiRunRewardSaveData save,
            out BuqiRunRewardState reward,
            out string error)
        {
            ValidateRuntime(runtime);
            reward = null;
            if (save == null || !string.Equals(save.SchemaVersion, BuqiRunRewardSaveData.CurrentVersion, StringComparison.Ordinal) || save.Reward == null)
            {
                error = "Reward save schema is invalid.";
                return false;
            }
            try
            {
                ValidateReward(save.Reward);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            if (!MatchesRuntime(save.Reward, runtime))
            {
                error = "Reward save does not match the current run.";
                return false;
            }
            reward = save.Reward.Clone();
            error = string.Empty;
            return true;
        }

        private bool TryApply(
            BuqiRunEventRuntimeState runtime,
            BuqiRunRewardCandidate candidate,
            string targetInstanceId,
            out string error)
        {
            switch (candidate.Kind)
            {
                case BuqiRunRewardKind.Coins:
                    long coins = (long)runtime.Economy.Run.Coins + candidate.Amount;
                    if (coins > int.MaxValue)
                    {
                        error = "Reward coin result is out of range.";
                        return false;
                    }
                    runtime.Economy.Run.Coins = (int)coins;
                    error = string.Empty;
                    return true;
                case BuqiRunRewardKind.Experience:
                    long experience = (long)runtime.Experience + candidate.Amount;
                    if (experience > int.MaxValue)
                    {
                        error = "Reward experience result is out of range.";
                        return false;
                    }
                    runtime.Experience = (int)experience;
                    error = string.Empty;
                    return true;
                case BuqiRunRewardKind.Item:
                    BuqiRunEconomyResult granted = m_Economy.GrantFreeItem(runtime.Economy, candidate.DefinitionId);
                    if (!granted.Success)
                    {
                        error = granted.FailureReason;
                        return false;
                    }
                    runtime.Economy = granted.Snapshot;
                    error = string.Empty;
                    return true;
                case BuqiRunRewardKind.Upgrade:
                    if (!TryGetTarget(runtime, targetInstanceId, out BuqiRunItemInstance upgradeTarget, out error))
                        return false;
                    if (upgradeTarget.Quality == BuqiRunItemQuality.Finalized)
                    {
                        error = "Reward target is already finalized.";
                        return false;
                    }
                    upgradeTarget.Quality++;
                    return true;
                case BuqiRunRewardKind.Refinement:
                    if (!TryGetTarget(runtime, targetInstanceId, out BuqiRunItemInstance refinementTarget, out error))
                        return false;
                    if (!string.IsNullOrWhiteSpace(refinementTarget.RefinementId))
                    {
                        error = "Reward target already has a refinement.";
                        return false;
                    }
                    refinementTarget.RefinementId = candidate.RefinementId;
                    return true;
                default:
                    error = "Reward kind is invalid.";
                    return false;
            }
        }

        private static bool TryGetTarget(
            BuqiRunEventRuntimeState runtime,
            string instanceId,
            out BuqiRunItemInstance item,
            out string error)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(instanceId) || !runtime.Economy.Items.TryGetValue(instanceId, out item))
            {
                error = "A valid owned reward target is required.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private void AddCandidate(
            BuqiRunRewardState state,
            BuqiRunRewardKind kind,
            int amount,
            string definitionId,
            string refinementId)
        {
            int index = state.Candidates.Count;
            state.Candidates.Add(new BuqiRunRewardCandidate
            {
                CandidateId = BuqiText.Format("{0}:reward:{1}", state.StageId, index),
                Kind = kind,
                Amount = amount,
                DefinitionId = definitionId ?? string.Empty,
                RefinementId = refinementId ?? string.Empty,
                Title = RewardTitle(kind),
                Description = Describe(kind, amount, definitionId, refinementId),
            });
        }

        private void AddFallbackCandidate(BuqiRunRewardState state)
        {
            if (state.Candidates.Count % 2 == 0)
                AddCandidate(state, BuqiRunRewardKind.Coins, m_Settings.CoinAmount + state.Candidates.Count, string.Empty, string.Empty);
            else
                AddCandidate(state, BuqiRunRewardKind.Experience, m_Settings.ExperienceAmount + state.Candidates.Count, string.Empty, string.Empty);
        }

        private static bool HasStorageSpace(BuqiRunEconomySnapshot economy)
        {
            return economy?.Run?.StorageInstanceIds != null &&
                   economy.Run.StorageInstanceIds.Exists(string.IsNullOrEmpty);
        }

        private static bool HasUpgradeableTarget(BuqiRunEventRuntimeState runtime)
        {
            foreach (BuqiRunItemInstance item in runtime.Economy.Items.Values)
            {
                if (item != null && item.Quality < BuqiRunItemQuality.Finalized)
                    return true;
            }
            return false;
        }

        private static bool HasRefinementTarget(BuqiRunEventRuntimeState runtime)
        {
            foreach (BuqiRunItemInstance item in runtime.Economy.Items.Values)
            {
                if (item != null && string.IsNullOrWhiteSpace(item.RefinementId))
                    return true;
            }
            return false;
        }

        private static string Describe(BuqiRunRewardKind kind, int amount, string definitionId, string refinementId)
        {
            switch (kind)
            {
                case BuqiRunRewardKind.Coins: return BuqiText.Format("获得 {0} 金币", amount);
                case BuqiRunRewardKind.Item: return BuqiText.Format("获得器物 {0}", definitionId);
                case BuqiRunRewardKind.Experience: return BuqiText.Format("获得 {0} 经验", amount);
                case BuqiRunRewardKind.Upgrade: return "选择一件已有器物提升品质";
                case BuqiRunRewardKind.Refinement: return BuqiText.Format("选择一件器物施加改造 {0}", refinementId);
                default: return string.Empty;
            }
        }

        private static string RewardTitle(BuqiRunRewardKind kind)
        {
            switch (kind)
            {
                case BuqiRunRewardKind.Coins: return "金币";
                case BuqiRunRewardKind.Item: return "器物";
                case BuqiRunRewardKind.Experience: return "经验";
                case BuqiRunRewardKind.Upgrade: return "品质提升";
                case BuqiRunRewardKind.Refinement: return "器物改造";
                default: return string.Empty;
            }
        }

        private string FirstValidItemId()
        {
            for (int index = 0; index < m_Settings.ItemDefinitionIds.Count; index++)
            {
                string id = m_Settings.ItemDefinitionIds[index];
                if (!string.IsNullOrWhiteSpace(id) && m_Items.TryGet(id, out BuqiRunItemDefinition definition) && definition != null)
                    return id;
            }
            return string.Empty;
        }

        private static string FirstNonEmpty(IReadOnlyList<string> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                    return values[index];
            }
            return string.Empty;
        }

        private int ResolveLevel(int experience)
        {
            return experience / m_Settings.ExperiencePerLevel + 1;
        }

        private static int StableIndex(string value, int modulus)
        {
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < value.Length; index++)
                    hash = hash * 31 + value[index];
                return (hash & int.MaxValue) % modulus;
            }
        }

        private static BuqiRunRewardCandidate FindCandidate(BuqiRunRewardState state, string candidateId)
        {
            for (int index = 0; index < state.Candidates.Count; index++)
            {
                if (string.Equals(state.Candidates[index].CandidateId, candidateId, StringComparison.Ordinal))
                    return state.Candidates[index];
            }
            return null;
        }

        private static BuqiRunResolutionRecord FindResolution(BuqiRunEventRuntimeState runtime, string resolutionId)
        {
            for (int index = 0; index < runtime.AppliedResolutions.Count; index++)
            {
                if (string.Equals(runtime.AppliedResolutions[index].ResolutionId, resolutionId, StringComparison.Ordinal))
                    return runtime.AppliedResolutions[index];
            }
            return null;
        }

        private static bool MatchesRuntime(BuqiRunRewardState reward, BuqiRunEventRuntimeState runtime)
        {
            return reward.RunSeed == runtime.Economy.Run.RunSeed &&
                   reward.Day == runtime.Economy.Run.Day &&
                   reward.Period == runtime.Economy.Run.Period &&
                   reward.RuntimeRevision == runtime.Economy.Run.Revision;
        }

        private static void ValidateRuntime(BuqiRunEventRuntimeState runtime)
        {
            if (runtime == null || runtime.Economy == null || runtime.Economy.Run == null)
                throw new ArgumentException("Reward runtime is unavailable.", nameof(runtime));
        }

        private static void ValidateReward(BuqiRunRewardState reward)
        {
            if (reward == null || string.IsNullOrWhiteSpace(reward.StageId) || reward.Candidates == null ||
                reward.Candidates.Count < 2 || reward.Candidates.Count > 4)
            {
                throw new ArgumentException("Reward state is invalid.", nameof(reward));
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < reward.Candidates.Count; index++)
            {
                BuqiRunRewardCandidate candidate = reward.Candidates[index];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.CandidateId) || !ids.Add(candidate.CandidateId))
                    throw new ArgumentException("Reward candidate ids must be non-empty and unique.", nameof(reward));
            }
            if (reward.Claimed && (string.IsNullOrWhiteSpace(reward.ClaimedCandidateId) ||
                                   string.IsNullOrWhiteSpace(reward.AppliedCommandId)))
            {
                throw new ArgumentException("Claimed reward state is incomplete.", nameof(reward));
            }
        }

        private static BuqiRunRewardResult Success(
            BuqiRunEventRuntimeState runtime,
            BuqiRunRewardState reward,
            bool replayed,
            bool levelUp)
        {
            return new BuqiRunRewardResult
            {
                Success = true,
                Replayed = replayed,
                LevelUp = levelUp,
                Runtime = runtime,
                Reward = reward,
            };
        }

        private static BuqiRunRewardResult Failure(
            BuqiRunEventRuntimeState runtime,
            BuqiRunRewardState reward,
            string error)
        {
            return new BuqiRunRewardResult
            {
                Success = false,
                FailureReason = error,
                Runtime = runtime.Clone(),
                Reward = reward.Clone(),
            };
        }
    }
}
