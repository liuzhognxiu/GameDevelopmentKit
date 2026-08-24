using System;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Encounter;
using Game.Hot.Buqi.Run.Training;

namespace Game.Hot.Buqi.Run.Integration
{
    public static class BuqiRunBattleModifierProjector
    {
        private const int BattleDurationTicks = 600;
        private const int BufferCap = 60;

        public static void Apply(
            BuildSnapshot snapshot,
            BuqiRunEventRuntimeState runtime,
            IBuqiRunBuildTagCatalog itemTags)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (runtime?.TemporaryModifiers == null)
                return;
            if (itemTags == null)
                throw new ArgumentNullException(nameof(itemTags));

            foreach (BuqiRunTemporaryModifier modifier in runtime.TemporaryModifiers)
            {
                if (modifier == null || modifier.RemainingBattles <= 0 ||
                    !HasTarget(snapshot, modifier.BuildTag, itemTags))
                {
                    continue;
                }

                if (modifier.Kind == BuqiRunModifierKind.StartingShield)
                {
                    snapshot.InitialBuffer = Math.Min(BufferCap,
                        snapshot.InitialBuffer + Math.Max(0, modifier.Value));
                }
                else if (modifier.Kind == BuqiRunModifierKind.RecoveryPercent)
                {
                    snapshot.InitialExecution += Math.Max(0, modifier.Value);
                }
                else if (modifier.Kind == BuqiRunModifierKind.CooldownPercent)
                {
                    ApplyCooldown(snapshot, modifier, itemTags);
                }
            }
        }

        private static void ApplyCooldown(
            BuildSnapshot snapshot,
            BuqiRunTemporaryModifier modifier,
            IBuqiRunBuildTagCatalog itemTags)
        {
            int bps = Math.Abs(modifier.Value);
            if (bps > 0 && bps <= 100)
                bps *= 100;
            string sourceId = !string.IsNullOrWhiteSpace(modifier.ModifierId)
                ? modifier.ModifierId
                : !string.IsNullOrWhiteSpace(modifier.SourceId)
                    ? modifier.SourceId
                    : "run-modifier";

            foreach (ItemInstance item in snapshot.Items)
            {
                if (item == null || !MatchesTag(item.DefinitionId, modifier.BuildTag, itemTags))
                    continue;
                item.TemporaryModifiers.Add(new TemporaryModifier
                {
                    Effect = modifier.Value < 0 ? BuqiEffect.Delay : BuqiEffect.Haste,
                    SourceInstanceId = sourceId,
                    RemainingTicks = modifier.DurationTicks > 0
                        ? modifier.DurationTicks
                        : BattleDurationTicks,
                    Bps = bps,
                });
            }
        }

        private static bool HasTarget(
            BuildSnapshot snapshot,
            string buildTag,
            IBuqiRunBuildTagCatalog itemTags)
        {
            if (string.IsNullOrWhiteSpace(buildTag))
                return true;
            return snapshot.Items.Exists(item =>
                item != null && itemTags.HasBuildTag(item.DefinitionId, buildTag));
        }

        private static bool MatchesTag(
            string definitionId,
            string buildTag,
            IBuqiRunBuildTagCatalog itemTags)
        {
            return string.IsNullOrWhiteSpace(buildTag) || itemTags.HasBuildTag(definitionId, buildTag);
        }
    }
}
