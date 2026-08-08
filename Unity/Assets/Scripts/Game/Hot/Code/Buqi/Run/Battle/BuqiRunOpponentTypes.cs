using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.Run.Battle
{
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
