using Game.Hot.Buqi.Run.Encounter;

namespace Game.Hot.Buqi.Run.Training
{
    public enum BuqiRunTrainingKind
    {
        Upgrade = 0,
        DirectedStrengthening = 1,
        Economy = 2,
        Experience = 3,
    }

    public sealed class BuqiRunTrainingDefinition
    {
        public string TrainingId = string.Empty;
        public BuqiRunTrainingKind Kind;
        public BuqiRunEventEligibility Eligibility = new BuqiRunEventEligibility();
        public int CoinCost;
        public string CounterCostId = string.Empty;
        public int CounterCost;
        public string RequiredBuildTag = string.Empty;
        public int QualitySteps;
        public string RefinementId = string.Empty;
        public string ModifierId = string.Empty;
        public BuqiRunModifierKind ModifierKind;
        public int ModifierValue;
        public int ModifierDurationBattles;
        public int CoinReward;
        public int ExperienceReward;
        public string RewardCounterId = string.Empty;
        public int RewardCounterAmount;
    }

    public interface IBuqiRunTrainingCatalog
    {
        bool TryGet(string trainingId, out BuqiRunTrainingDefinition definition);
    }

    public sealed class BuqiRunTrainingRequest
    {
        public string ResolutionId = string.Empty;
        public string TrainingId = string.Empty;
        public string TargetInstanceId = string.Empty;
    }

    public sealed class BuqiRunTrainingResult
    {
        public bool Success;
        public bool Replayed;
        public string FailureReason = string.Empty;
        public BuqiRunEventRuntimeState State = null!;
    }
}
