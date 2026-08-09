using System;

namespace Game.Hot.Buqi.Run.Battle
{
    public sealed class BuqiPveRewardProfile
    {
        public int Rank;
        public int ArtifactChoiceCount;
        public int GoldOptionAmount;
        public int ExperienceOptionAmount;
        public int DefeatExperienceAmount;

        public BuqiPveRewardProfile Clone()
        {
            return new BuqiPveRewardProfile
            {
                Rank = Rank,
                ArtifactChoiceCount = ArtifactChoiceCount,
                GoldOptionAmount = GoldOptionAmount,
                ExperienceOptionAmount = ExperienceOptionAmount,
                DefeatExperienceAmount = DefeatExperienceAmount,
            };
        }
    }

    public static class BuqiPveRewardPolicy
    {
        public static BuqiPveRewardProfile Get(BuqiPveDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BuqiPveDifficulty.Initial:
                    return Create(
                        rank: 1,
                        artifactChoiceCount: 1,
                        goldOptionAmount: 4,
                        experienceOptionAmount: 2,
                        defeatExperienceAmount: 0);
                case BuqiPveDifficulty.Intermediate:
                    return Create(
                        rank: 2,
                        artifactChoiceCount: 2,
                        goldOptionAmount: 7,
                        experienceOptionAmount: 4,
                        defeatExperienceAmount: 1);
                case BuqiPveDifficulty.Dangerous:
                    return Create(
                        rank: 3,
                        artifactChoiceCount: 3,
                        goldOptionAmount: 11,
                        experienceOptionAmount: 7,
                        defeatExperienceAmount: 2);
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "PVE difficulty is invalid.");
            }
        }

        private static BuqiPveRewardProfile Create(
            int rank,
            int artifactChoiceCount,
            int goldOptionAmount,
            int experienceOptionAmount,
            int defeatExperienceAmount)
        {
            return new BuqiPveRewardProfile
            {
                Rank = rank,
                ArtifactChoiceCount = artifactChoiceCount,
                GoldOptionAmount = goldOptionAmount,
                ExperienceOptionAmount = experienceOptionAmount,
                DefeatExperienceAmount = defeatExperienceAmount,
            };
        }
    }
}
