using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using BattleConditionKind = Game.Hot.Buqi.Battle.BuqiConditionKind;
using BattleEffect = Game.Hot.Buqi.Battle.BuqiEffect;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;
using BattleTarget = Game.Hot.Buqi.Battle.BuqiTarget;
using BattleTrigger = Game.Hot.Buqi.Battle.BuqiTrigger;

namespace Game.Hot.Buqi.Config
{
    public static class BuqiConfigValidator
    {
        private static readonly string[] s_EnabledItemIds =
        {
            "W8-003", "W8-005", "W8-006",
            "W8-007", "W8-008", "W8-012",
            "W8-013", "W8-014", "W8-015",
            "W8-016", "W8-017", "W8-018",
            "W8-019", "W8-020", "W8-021",
            "W8-022", "W8-023", "W8-024",
            "W8-025", "W8-026", "W8-027",
            "W8-028", "W8-029", "W8-030",
        };

        private static readonly string[] s_EnabledBuildIds =
        {
            "fast", "buffer", "chain", "heal",
            "poison", "burn", "freeze", "overload",
        };

        private static readonly string[] s_EnabledRefinementIds =
        {
            "A-01", "A-02", "A-03", "A-04", "A-05", "A-06",
        };

        public static List<string> Validate(BuqiConfigCatalog catalog)
        {
            var errors = new List<string>();
            if (catalog == null)
            {
                errors.Add("catalog is null");
                return errors;
            }

            ValidateGlobal(catalog.Global, errors);
            Dictionary<string, BuqiItemConfigRow> items = ValidateItems(catalog.Items, errors);
            HashSet<string> refinements = ValidateRefinements(catalog.Refinements, errors);
            ValidateEchoes(catalog, items, refinements, errors);
            return errors;
        }

        private static void ValidateGlobal(BuqiGlobalConfigRow global, List<string> errors)
        {
            if (global == null)
            {
                errors.Add("global config is null");
                return;
            }

            if (string.IsNullOrEmpty(global.ContentVersion))
                errors.Add("global content version is empty");
            if (global.InitialExecution <= 0)
                errors.Add("global initial execution must be > 0");
            if (global.BufferCap <= 0)
                errors.Add("global buffer cap must be > 0");
            if (global.NoiseThreshold <= 0)
                errors.Add("global noise threshold must be > 0");
            if (global.NoiseIncidentDamage <= 0)
                errors.Add("global noise incident damage must be > 0");
            if (global.BoardSlotCount != BuqiBoardValidator.BoardSlotCount)
                errors.Add("global board slot count must be 8");
            if (global.NormalDurationTicks <= 0 || global.HardCapTicks <= global.NormalDurationTicks)
                errors.Add("global hard cap ticks must be greater than normal duration");
            if (global.OvertimeStartTicks != global.NormalDurationTicks)
                errors.Add("global overtime start must match normal duration");
            if (global.MaxTickEvents != 64)
                errors.Add("global max tick events must be 64");
            if (global.MaxItemEventsPerTick != 4)
                errors.Add("global max item events per tick must be 4");
        }

        private static Dictionary<string, BuqiItemConfigRow> ValidateItems(
            List<BuqiItemConfigRow> rows,
            List<string> errors)
        {
            var items = new Dictionary<string, BuqiItemConfigRow>(StringComparer.Ordinal);
            if (rows == null)
            {
                errors.Add("item table is null");
                return items;
            }

            if (rows.Count != s_EnabledItemIds.Length)
                errors.Add(BuqiText.Format(
                    "expected {0} enabled items, got {1}",
                    s_EnabledItemIds.Length,
                    rows.Count));

            foreach (BuqiItemConfigRow row in rows)
            {
                if (row == null)
                {
                    errors.Add("item row is null");
                    continue;
                }

                string where = BuqiText.Format("item {0}", row.DefinitionId);
                if (string.IsNullOrEmpty(row.DefinitionId))
                {
                    errors.Add("item definition id is empty");
                    continue;
                }
                if (!IsExpectedItemId(row.DefinitionId))
                    errors.Add(BuqiText.Format("enabled item {0} is outside expanded scope", row.DefinitionId));
                if (items.ContainsKey(row.DefinitionId))
                    errors.Add(BuqiText.Format("duplicate item id {0}", row.DefinitionId));
                else
                    items.Add(row.DefinitionId, row);

                if (!Enum.IsDefined(typeof(BattleSize), row.Size))
                    errors.Add(BuqiText.Format("{0}: invalid size {1}", where, row.Size));
                if (row.BasePrice <= 0)
                    errors.Add(BuqiText.Format("{0}: base price must be > 0", where));
                if (row.BasePrice != ExpectedPrice(row.Size))
                    errors.Add(BuqiText.Format("{0}: price must match size", where));
                if (row.BaseCooldownTicks <= 0)
                    errors.Add(BuqiText.Format("{0}: cooldown must be > 0", where));
                if (string.IsNullOrEmpty(row.ArchetypeId))
                    errors.Add(BuqiText.Format("{0}: archetype id is empty", where));
                else if (!IsExpectedBuildId(row.ArchetypeId))
                    errors.Add(BuqiText.Format("{0}: unknown build {1}", where, row.ArchetypeId));
                if (row.Effects == null || row.Effects.Count == 0)
                {
                    errors.Add(BuqiText.Format("{0}: at least one effect required", where));
                    continue;
                }

                for (int index = 0; index < row.Effects.Count; index++)
                    ValidateEffect(row.Effects[index], BuqiText.Format("{0}.effect[{1}]", where, index), errors);
            }

            foreach (string expectedId in s_EnabledItemIds)
            {
                if (!items.ContainsKey(expectedId))
                    errors.Add(BuqiText.Format("missing enabled item {0}", expectedId));
            }

            return items;
        }

        private static void ValidateEffect(
            BuqiEffectConfigRow effect,
            string where,
            List<string> errors)
        {
            if (effect == null)
            {
                errors.Add(BuqiText.Format("{0}: effect is null", where));
                return;
            }

            if (!Enum.IsDefined(typeof(BattleTrigger), effect.Trigger))
                errors.Add(BuqiText.Format("{0}: invalid trigger {1}", where, effect.Trigger));
            if (!Enum.IsDefined(typeof(BattleEffect), effect.Effect))
                errors.Add(BuqiText.Format("{0}: invalid effect {1}", where, effect.Effect));
            if (!Enum.IsDefined(typeof(BattleTarget), effect.Target))
                errors.Add(BuqiText.Format("{0}: invalid target {1}", where, effect.Target));
            if (string.IsNullOrEmpty(effect.ReasonCode))
                errors.Add(BuqiText.Format("{0}: reason code is empty", where));

            if (effect.Trigger == BattleTrigger.OnUseCountReached && effect.UseCountThreshold <= 0)
                errors.Add(BuqiText.Format("{0}: OnUseCountReached requires use count threshold", where));
            if (effect.Trigger == BattleTrigger.OnFirstConditionMet &&
                effect.ConditionKind == BattleConditionKind.None)
            {
                errors.Add(BuqiText.Format("{0}: OnFirstConditionMet requires condition kind", where));
            }
            if (effect.ChargeConsume && effect.ChargeReadLimit <= 0)
                errors.Add(BuqiText.Format("{0}: charge consume requires read limit", where));
            if (effect.ChargeReadLimit < 0 || effect.AmountPerCharge < 0)
                errors.Add(BuqiText.Format("{0}: charge fields must be >= 0", where));

            switch (effect.Effect)
            {
                case BattleEffect.Damage:
                    if (effect.Target != BattleTarget.EnemyExecution)
                        errors.Add(BuqiText.Format("{0}: Damage requires EnemyExecution target", where));
                    if (effect.Amount <= 0)
                        errors.Add(BuqiText.Format("{0}: Damage amount must be > 0", where));
                    break;
                case BattleEffect.Buffer:
                    if (effect.Target != BattleTarget.Self)
                        errors.Add(BuqiText.Format("{0}: Buffer requires Self target", where));
                    if (effect.Amount <= 0)
                        errors.Add(BuqiText.Format("{0}: Buffer amount must be > 0", where));
                    break;
                case BattleEffect.Heal:
                    if (effect.Target != BattleTarget.Self)
                        errors.Add(BuqiText.Format("{0}: Heal requires Self target", where));
                    if (effect.Amount <= 0)
                        errors.Add(BuqiText.Format("{0}: Heal amount must be > 0", where));
                    break;
                case BattleEffect.Regen:
                    if (effect.Target != BattleTarget.Self)
                        errors.Add(BuqiText.Format("{0}: Regen requires Self target", where));
                    ValidateStatusAmount(effect, where, errors);
                    break;
                case BattleEffect.Poison:
                    if (effect.Target != BattleTarget.EnemyExecution)
                        errors.Add(BuqiText.Format("{0}: Poison requires EnemyExecution target", where));
                    ValidateStatusAmount(effect, where, errors);
                    break;
                case BattleEffect.Burn:
                    if (effect.Target != BattleTarget.EnemyExecution)
                        errors.Add(BuqiText.Format("{0}: Burn requires EnemyExecution target", where));
                    ValidateStatusAmount(effect, where, errors);
                    break;
                case BattleEffect.Freeze:
                    if (!IsEnemyItemTarget(effect.Target))
                        errors.Add(BuqiText.Format("{0}: Freeze requires an enemy item target", where));
                    if (effect.Amount <= 0)
                        errors.Add(BuqiText.Format("{0}: Freeze amount must be > 0", where));
                    break;
                case BattleEffect.Charge:
                    if (!IsItemTarget(effect.Target))
                        errors.Add(BuqiText.Format("{0}: Charge requires an item target", where));
                    if (effect.Amount == 0)
                        errors.Add(BuqiText.Format("{0}: Charge amount must be non-zero", where));
                    break;
                case BattleEffect.Haste:
                    if (!IsItemTarget(effect.Target))
                        errors.Add(BuqiText.Format("{0}: Haste requires an item target", where));
                    ValidateModifierAmount(effect, where, errors);
                    break;
                case BattleEffect.Delay:
                    if (!IsEnemyItemTarget(effect.Target))
                        errors.Add(BuqiText.Format("{0}: Delay requires an enemy item target", where));
                    ValidateModifierAmount(effect, where, errors);
                    break;
                case BattleEffect.Noise:
                    if (effect.Target != BattleTarget.Self)
                        errors.Add(BuqiText.Format("{0}: Noise requires Self target", where));
                    if (effect.Amount == 0)
                        errors.Add(BuqiText.Format("{0}: Noise amount must be non-zero", where));
                    break;
            }
        }

        private static HashSet<string> ValidateRefinements(
            List<BuqiRefinementConfigRow> rows,
            List<string> errors)
        {
            var refinements = new HashSet<string>(StringComparer.Ordinal);
            if (rows == null)
            {
                errors.Add("refinement table is null");
                return refinements;
            }
            if (rows.Count != s_EnabledRefinementIds.Length)
                errors.Add(BuqiText.Format(
                    "expected {0} refinements, got {1}",
                    s_EnabledRefinementIds.Length,
                    rows.Count));

            foreach (BuqiRefinementConfigRow row in rows)
            {
                if (row == null)
                {
                    errors.Add("refinement row is null");
                    continue;
                }
                if (string.IsNullOrEmpty(row.RefinementId))
                {
                    errors.Add("refinement id is empty");
                    continue;
                }
                if (!refinements.Add(row.RefinementId))
                    errors.Add(BuqiText.Format("duplicate refinement id {0}", row.RefinementId));
                if (!IsExpectedRefinementId(row.RefinementId))
                    errors.Add(BuqiText.Format("refinement {0} is outside expanded scope", row.RefinementId));
                if (string.IsNullOrEmpty(row.DisplayName))
                    errors.Add(BuqiText.Format("refinement {0}: display name is empty", row.RefinementId));
            }

            return refinements;
        }

        private static void ValidateEchoes(
            BuqiConfigCatalog catalog,
            Dictionary<string, BuqiItemConfigRow> items,
            HashSet<string> refinements,
            List<string> errors)
        {
            if (catalog.Echoes == null)
            {
                errors.Add("echo table is null");
                return;
            }
            if (catalog.Echoes.Count != 16)
                errors.Add(BuqiText.Format("expected 16 echoes, got {0}", catalog.Echoes.Count));

            var echoIds = new HashSet<string>(StringComparer.Ordinal);
            IItemDefinitionProvider provider = new BuqiDefinitionProvider(catalog);
            foreach (BuqiEchoConfigRow echo in catalog.Echoes)
            {
                if (echo == null)
                {
                    errors.Add("echo row is null");
                    continue;
                }
                string where = BuqiText.Format("echo {0}", echo.EchoId);
                if (string.IsNullOrEmpty(echo.EchoId))
                    errors.Add("echo id is empty");
                else if (!echoIds.Add(echo.EchoId))
                    errors.Add(BuqiText.Format("duplicate echo id {0}", echo.EchoId));
                if (!string.IsNullOrEmpty(echo.Build) && !IsExpectedBuildId(echo.Build))
                    errors.Add(BuqiText.Format("{0}: unknown build {1}", where, echo.Build));
                if (echo.Snapshot == null)
                {
                    errors.Add(BuqiText.Format("{0}: snapshot is null", where));
                    continue;
                }

                CheckRawAnchorDuplicates(echo.Snapshot, where, errors);
                foreach (BuqiItemInstanceConfigRow instance in echo.Snapshot.Items)
                {
                    if (instance == null)
                    {
                        errors.Add(BuqiText.Format("{0}: null snapshot item", where));
                        continue;
                    }
                    if (!items.ContainsKey(instance.DefinitionId))
                        errors.Add(BuqiText.Format("{0}: unknown definitionId {1}", where, instance.DefinitionId));
                    if (!string.IsNullOrEmpty(instance.RefinementId) && !refinements.Contains(instance.RefinementId))
                        errors.Add(BuqiText.Format("{0}: unknown refinementId {1}", where, instance.RefinementId));
                }

                BuildSnapshot snapshot = ToBattleSnapshot(catalog.Global, echo.Snapshot);
                if (!BuqiBoardValidator.Validate(snapshot, provider, out List<string> boardErrors))
                {
                    foreach (string boardError in boardErrors)
                        errors.Add(BuqiText.Format("{0}: {1}", where, boardError));
                }
            }
        }

        private static BuildSnapshot ToBattleSnapshot(
            BuqiGlobalConfigRow global,
            BuqiBuildSnapshotConfigRow source)
        {
            var snapshot = new BuildSnapshot
            {
                SnapshotId = source.SnapshotId,
                ContentVersion = global == null ? string.Empty : global.ContentVersion,
                ArchetypeId = source.ArchetypeId,
                InitialExecution = source.InitialExecution,
                InitialBuffer = source.InitialBuffer,
                InitialNoiseDebt = source.InitialNoiseDebt,
            };
            foreach (BuqiItemInstanceConfigRow row in source.Items)
            {
                if (row == null)
                    continue;
                snapshot.Items.Add(new ItemInstance
                {
                    InstanceId = row.InstanceId,
                    DefinitionId = row.DefinitionId,
                    Quality = (int)row.Quality,
                    AnchorSlot = row.AnchorSlot,
                    AnnotationId = row.RefinementId,
                });
            }
            return snapshot;
        }

        private static void CheckRawAnchorDuplicates(
            BuqiBuildSnapshotConfigRow snapshot,
            string where,
            List<string> errors)
        {
            var anchors = new Dictionary<int, int>();
            for (int index = 0; index < snapshot.Items.Count; index++)
            {
                BuqiItemInstanceConfigRow item = snapshot.Items[index];
                if (item == null)
                    continue;
                if (anchors.TryGetValue(item.AnchorSlot, out int previous))
                    errors.Add(BuqiText.Format("{0}: overlap at slot {1} with item[{2}]", where, item.AnchorSlot, previous));
                else
                    anchors[item.AnchorSlot] = index;
            }
        }

        private static void ValidateModifierAmount(
            BuqiEffectConfigRow effect,
            string where,
            List<string> errors)
        {
            if (effect.Amount <= 0)
                errors.Add(BuqiText.Format("{0}: modifier amount must be > 0", where));
            if (effect.DurationTicks <= 0)
                errors.Add(BuqiText.Format("{0}: modifier duration must be > 0", where));
        }

        private static void ValidateStatusAmount(
            BuqiEffectConfigRow effect,
            string where,
            List<string> errors)
        {
            if (effect.Amount <= 0)
                errors.Add(BuqiText.Format("{0}: status amount must be > 0", where));
            if (effect.DurationTicks <= 0)
                errors.Add(BuqiText.Format("{0}: status duration must be > 0", where));
        }

        private static bool IsExpectedItemId(string itemId)
        {
            foreach (string expectedId in s_EnabledItemIds)
            {
                if (itemId == expectedId)
                    return true;
            }
            return false;
        }

        private static bool IsExpectedBuildId(string buildId)
        {
            foreach (string expectedId in s_EnabledBuildIds)
            {
                if (buildId == expectedId)
                    return true;
            }
            return false;
        }

        private static bool IsExpectedRefinementId(string refinementId)
        {
            foreach (string expectedId in s_EnabledRefinementIds)
            {
                if (refinementId == expectedId)
                    return true;
            }
            return false;
        }

        private static int ExpectedPrice(BattleSize size)
        {
            if (size == BattleSize.M)
                return 4;
            if (size == BattleSize.L)
                return 6;
            return 2;
        }

        private static bool IsItemTarget(BattleTarget target)
        {
            return target == BattleTarget.Self ||
                   target == BattleTarget.LeftAdjacentItem ||
                   target == BattleTarget.RightAdjacentItem ||
                   target == BattleTarget.AllAdjacentItems;
        }

        private static bool IsEnemyItemTarget(BattleTarget target)
        {
            return target == BattleTarget.ShortestCooldownEnemyItem ||
                   target == BattleTarget.LongestCooldownEnemyItem ||
                   target == BattleTarget.LeftmostEnemyItem ||
                   target == BattleTarget.RightmostEnemyItem;
        }
    }
}
