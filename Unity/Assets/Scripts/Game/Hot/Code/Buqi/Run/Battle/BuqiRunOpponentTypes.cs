using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.Run.Battle
{
    public enum BuqiPveDifficulty
    {
        Initial = 0,
        Intermediate = 1,
        Dangerous = 2,
    }

    public enum BuqiRunOpponentSource
    {
        PvePreset = 0,
        LocalPlayerPreset = 1,
    }

    public sealed class BuqiRunOpponent
    {
        public string OpponentId = string.Empty;
        public string DisplayName = string.Empty;
        public BuqiRunOpponentSource Source;
        public BuildSnapshot Build;
    }

    public sealed class BuqiLocalOpponentPool
    {
        public List<BuqiRunOpponent> Pve = new List<BuqiRunOpponent>();
        public List<BuqiRunOpponent> Pvp = new List<BuqiRunOpponent>();
    }

    public sealed class BuqiPveThreatDto
    {
        public int Rank;
        public int InitialExecution;
        public int InitialBuffer;
        public int InitialNoiseDebt;
        public int EquippedItemCount;

        public BuqiPveThreatDto Clone()
        {
            return new BuqiPveThreatDto
            {
                Rank = Rank,
                InitialExecution = InitialExecution,
                InitialBuffer = InitialBuffer,
                InitialNoiseDebt = InitialNoiseDebt,
                EquippedItemCount = EquippedItemCount,
            };
        }
    }

    public sealed class BuqiPveRewardDto
    {
        public int Rank;
        public int VictoryProgress;

        public BuqiPveRewardDto Clone()
        {
            return new BuqiPveRewardDto
            {
                Rank = Rank,
                VictoryProgress = VictoryProgress,
            };
        }
    }

    public sealed class BuqiPveChoiceCard
    {
        public string ChoiceId = string.Empty;
        public BuqiPveDifficulty Difficulty;
        public string OpponentId = string.Empty;
        public string OpponentName = string.Empty;
        public BuildSnapshot OpponentBuild;
        public BuqiPveThreatDto Threat = new BuqiPveThreatDto();
        public BuqiPveRewardDto Reward = new BuqiPveRewardDto();

        public BuqiPveChoiceCard Clone()
        {
            return new BuqiPveChoiceCard
            {
                ChoiceId = ChoiceId,
                Difficulty = Difficulty,
                OpponentId = OpponentId,
                OpponentName = OpponentName,
                OpponentBuild = BuqiRunBattleSnapshotUtility.CloneBuild(OpponentBuild),
                Threat = Threat?.Clone(),
                Reward = Reward?.Clone(),
            };
        }
    }

    public sealed class BuqiPveSelection
    {
        public string SelectionId = string.Empty;
        public int Day;
        public int SourceRngCursor;
        public int NextRngCursor;
        public BuildSnapshot CurrentBoard;
        public List<BuqiPveChoiceCard> Cards = new List<BuqiPveChoiceCard>();

        public BuqiPveSelection Clone()
        {
            var clone = new BuqiPveSelection
            {
                SelectionId = SelectionId,
                Day = Day,
                SourceRngCursor = SourceRngCursor,
                NextRngCursor = NextRngCursor,
                CurrentBoard = BuqiRunBattleSnapshotUtility.CloneBuild(CurrentBoard),
            };

            if (Cards != null)
            {
                foreach (BuqiPveChoiceCard card in Cards)
                    clone.Cards.Add(card?.Clone());
            }

            return clone;
        }
    }

    internal static class BuqiRunBattleSnapshotUtility
    {
        public static BuqiRunOpponent CloneOpponent(BuqiRunOpponent source)
        {
            if (source == null)
                return null;

            return new BuqiRunOpponent
            {
                OpponentId = source.OpponentId,
                DisplayName = source.DisplayName,
                Source = source.Source,
                Build = CloneBuild(source.Build),
            };
        }

        public static BuildSnapshot CloneBuild(BuildSnapshot source)
        {
            if (source == null)
                return null;

            var clone = new BuildSnapshot
            {
                SnapshotId = source.SnapshotId,
                ContentVersion = source.ContentVersion,
                ArchetypeId = source.ArchetypeId,
                InitialExecution = source.InitialExecution,
                InitialBuffer = source.InitialBuffer,
                InitialNoiseDebt = source.InitialNoiseDebt,
            };

            if (source.Items == null)
                return clone;

            foreach (ItemInstance item in source.Items)
                clone.Items.Add(CloneItem(item));
            return clone;
        }

        public static BuildSnapshot CreateBuildSnapshot(
            BuqiBuildSnapshotConfigRow source,
            string contentVersion,
            string snapshotPrefix,
            string itemPrefix)
        {
            if (source == null)
                return null;

            var clone = new BuildSnapshot
            {
                SnapshotId = string.IsNullOrEmpty(snapshotPrefix)
                    ? source.SnapshotId
                    : BuqiText.Format("{0}{1}", snapshotPrefix, source.SnapshotId),
                ContentVersion = contentVersion ?? string.Empty,
                ArchetypeId = source.ArchetypeId,
                InitialExecution = source.InitialExecution,
                InitialBuffer = source.InitialBuffer,
                InitialNoiseDebt = source.InitialNoiseDebt,
            };

            if (source.Items == null)
                return clone;

            foreach (BuqiItemInstanceConfigRow item in source.Items)
            {
                if (item == null)
                    continue;

                clone.Items.Add(new ItemInstance
                {
                    InstanceId = string.IsNullOrEmpty(itemPrefix)
                        ? item.InstanceId
                        : BuqiText.Format("{0}{1}", itemPrefix, item.InstanceId),
                    DefinitionId = item.DefinitionId,
                    Quality = (int)item.Quality,
                    AnchorSlot = item.AnchorSlot,
                    AnnotationId = item.RefinementId,
                });
            }

            return clone;
        }

        private static ItemInstance CloneItem(ItemInstance source)
        {
            if (source == null)
                return null;

            var clone = new ItemInstance
            {
                InstanceId = source.InstanceId,
                DefinitionId = source.DefinitionId,
                Quality = source.Quality,
                AnchorSlot = source.AnchorSlot,
                AnnotationId = source.AnnotationId,
            };

            if (source.TemporaryModifiers == null)
                return clone;

            foreach (TemporaryModifier modifier in source.TemporaryModifiers)
                clone.TemporaryModifiers.Add(CloneModifier(modifier));
            return clone;
        }

        private static TemporaryModifier CloneModifier(TemporaryModifier source)
        {
            if (source == null)
                return null;

            return new TemporaryModifier
            {
                Effect = source.Effect,
                SourceInstanceId = source.SourceInstanceId,
                RemainingTicks = source.RemainingTicks,
                Bps = source.Bps,
            };
        }
    }
}
