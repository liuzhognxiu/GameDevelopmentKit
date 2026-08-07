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
                errors.Add("配置目录不能为空");
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
                errors.Add("全局配置不能为空");
                return;
            }

            if (string.IsNullOrEmpty(global.ContentVersion))
                errors.Add("全局内容版本不能为空");
            if (global.InitialExecution != BuqiBattleSimulator.DefaultMaxExecution)
                errors.Add("全局初始道基必须与战斗模拟器一致");
            if (global.BufferCap != BuqiBattleSimulator.BufferCap)
                errors.Add("全局护体上限必须与战斗模拟器一致");
            if (global.NoiseThreshold != BuqiBattleSimulator.NoiseThreshold)
                errors.Add("全局失衡阈值必须与战斗模拟器一致");
            if (global.NoiseIncidentDamage != BuqiBattleSimulator.NoiseAccidentDamage)
                errors.Add("全局失衡事故伤害必须与战斗模拟器一致");
            if (global.BoardSlotCount != BuqiBoardValidator.BoardSlotCount)
                errors.Add("全局棋盘格数必须为 8");
            if (global.NormalDurationTicks != BuqiBattleSimulator.NormalTickCount)
                errors.Add("全局正常战斗时长必须与战斗模拟器一致");
            if (global.HardCapTicks != BuqiBattleSimulator.HardCapTick)
                errors.Add("全局战斗硬上限必须与战斗模拟器一致");
            if (global.OvertimeStartTicks != BuqiBattleSimulator.NormalTickCount)
                errors.Add("全局劫火开始时刻必须与战斗模拟器一致");
            if (global.MaxTickEvents != BuqiBattleSimulator.MaxEventsPerTick)
                errors.Add("全局每时刻事件上限必须与战斗模拟器一致");
            if (global.MaxItemEventsPerTick != BuqiBattleSimulator.MaxEventsPerItemPerTick)
                errors.Add("全局每件装备每时刻事件上限必须与战斗模拟器一致");
        }

        private static Dictionary<string, BuqiItemConfigRow> ValidateItems(
            List<BuqiItemConfigRow> rows,
            List<string> errors)
        {
            var items = new Dictionary<string, BuqiItemConfigRow>(StringComparer.Ordinal);
            if (rows == null)
            {
                errors.Add("装备表不能为空");
                return items;
            }

            if (rows.Count != s_EnabledItemIds.Length)
                errors.Add(BuqiText.Format(
                    "应有 {0} 件已启用装备，实际为 {1} 件",
                    s_EnabledItemIds.Length,
                    rows.Count));

            foreach (BuqiItemConfigRow row in rows)
            {
                if (row == null)
                {
                    errors.Add("装备表行不能为空");
                    continue;
                }

                string where = BuqiText.Format("装备 {0}", row.DefinitionId);
                if (string.IsNullOrEmpty(row.DefinitionId))
                {
                    errors.Add("装备 definitionId 不能为空");
                    continue;
                }
                if (!IsExpectedItemId(row.DefinitionId))
                    errors.Add(BuqiText.Format("已启用装备 {0} 超出当前扩展范围", row.DefinitionId));
                if (items.ContainsKey(row.DefinitionId))
                    errors.Add(BuqiText.Format("装备 ID {0} 重复", row.DefinitionId));
                else
                    items.Add(row.DefinitionId, row);

                if (!Enum.IsDefined(typeof(BattleSize), row.Size))
                    errors.Add(BuqiText.Format("{0}：尺寸 {1} 无效", where, row.Size));
                if (row.BasePrice <= 0)
                    errors.Add(BuqiText.Format("{0}：基础价格必须大于 0", where));
                if (row.BasePrice != ExpectedPrice(row.Size))
                    errors.Add(BuqiText.Format("{0}：价格必须与尺寸匹配", where));
                if (row.BaseCooldownTicks <= 0)
                    errors.Add(BuqiText.Format("{0}：冷却必须大于 0", where));
                if (string.IsNullOrEmpty(row.ArchetypeId))
                    errors.Add(BuqiText.Format("{0}：构筑方向 ID 为空", where));
                else if (!IsExpectedBuildId(row.ArchetypeId))
                    errors.Add(BuqiText.Format("{0}：未知构筑方向 {1}", where, row.ArchetypeId));
                if (row.Effects == null || row.Effects.Count == 0)
                {
                    errors.Add(BuqiText.Format("{0}：至少需要一个效果", where));
                    continue;
                }

                for (int index = 0; index < row.Effects.Count; index++)
                    ValidateEffect(row.Effects[index], BuqiText.Format("{0}.效果[{1}]", where, index), errors);
            }

            foreach (string expectedId in s_EnabledItemIds)
            {
                if (!items.ContainsKey(expectedId))
                    errors.Add(BuqiText.Format("缺少已启用装备 {0}", expectedId));
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
                errors.Add(BuqiText.Format("{0}：效果为空", where));
                return;
            }

            if (!Enum.IsDefined(typeof(BattleTrigger), effect.Trigger))
                errors.Add(BuqiText.Format("{0}：触发器 {1} 无效", where, effect.Trigger));
            if (!Enum.IsDefined(typeof(BattleEffect), effect.Effect))
                errors.Add(BuqiText.Format("{0}：效果 {1} 无效", where, effect.Effect));
            if (!Enum.IsDefined(typeof(BattleTarget), effect.Target))
                errors.Add(BuqiText.Format("{0}：目标 {1} 无效", where, effect.Target));
            if (string.IsNullOrEmpty(effect.ReasonCode))
                errors.Add(BuqiText.Format("{0}：原因码为空", where));

            if (effect.Trigger == BattleTrigger.OnUseCountReached && effect.UseCountThreshold <= 0)
                errors.Add(BuqiText.Format("{0}：OnUseCountReached 需要使用次数阈值", where));
            if (effect.Trigger == BattleTrigger.OnFirstConditionMet &&
                effect.ConditionKind == BattleConditionKind.None)
            {
                errors.Add(BuqiText.Format("{0}：OnFirstConditionMet 需要条件类型", where));
            }
            if (effect.ChargeConsume && effect.ChargeReadLimit <= 0)
                errors.Add(BuqiText.Format("{0}：消耗蓄力时需要读取上限", where));
            if (effect.ChargeReadLimit < 0 || effect.AmountPerCharge < 0)
                errors.Add(BuqiText.Format("{0}：蓄力字段必须大于等于 0", where));

            switch (effect.Effect)
            {
                case BattleEffect.Damage:
                    if (effect.Target != BattleTarget.EnemyExecution)
                        errors.Add(BuqiText.Format("{0}：Damage 需要 EnemyExecution 目标", where));
                    if (effect.Amount <= 0)
                        errors.Add(BuqiText.Format("{0}：Damage 数值必须大于 0", where));
                    break;
                case BattleEffect.Buffer:
                    if (effect.Target != BattleTarget.Self)
                        errors.Add(BuqiText.Format("{0}：Buffer 需要 Self 目标", where));
                    if (effect.Amount <= 0)
                        errors.Add(BuqiText.Format("{0}：Buffer 数值必须大于 0", where));
                    break;
                case BattleEffect.Heal:
                    if (effect.Target != BattleTarget.Self)
                        errors.Add(BuqiText.Format("{0}：Heal 需要 Self 目标", where));
                    if (effect.Amount <= 0)
                        errors.Add(BuqiText.Format("{0}：Heal 数值必须大于 0", where));
                    break;
                case BattleEffect.Regen:
                    if (effect.Target != BattleTarget.Self)
                        errors.Add(BuqiText.Format("{0}：Regen 需要 Self 目标", where));
                    ValidateStatusAmount(effect, where, errors);
                    break;
                case BattleEffect.Poison:
                    if (effect.Target != BattleTarget.EnemyExecution)
                        errors.Add(BuqiText.Format("{0}：Poison 需要 EnemyExecution 目标", where));
                    ValidateStatusAmount(effect, where, errors);
                    break;
                case BattleEffect.Burn:
                    if (effect.Target != BattleTarget.EnemyExecution)
                        errors.Add(BuqiText.Format("{0}：Burn 需要 EnemyExecution 目标", where));
                    ValidateStatusAmount(effect, where, errors);
                    break;
                case BattleEffect.Freeze:
                    if (!IsEnemyItemTarget(effect.Target))
                        errors.Add(BuqiText.Format("{0}：Freeze 需要敌方装备目标", where));
                    if (effect.Amount <= 0)
                        errors.Add(BuqiText.Format("{0}：Freeze 数值必须大于 0", where));
                    break;
                case BattleEffect.Charge:
                    if (!IsItemTarget(effect.Target))
                        errors.Add(BuqiText.Format("{0}：Charge 需要装备目标", where));
                    if (effect.Amount == 0)
                        errors.Add(BuqiText.Format("{0}：Charge 数值不能为 0", where));
                    break;
                case BattleEffect.Haste:
                    if (!IsItemTarget(effect.Target))
                        errors.Add(BuqiText.Format("{0}：Haste 需要装备目标", where));
                    ValidateModifierAmount(effect, where, errors);
                    break;
                case BattleEffect.Delay:
                    if (!IsEnemyItemTarget(effect.Target))
                        errors.Add(BuqiText.Format("{0}：Delay 需要敌方装备目标", where));
                    ValidateModifierAmount(effect, where, errors);
                    break;
                case BattleEffect.Noise:
                    if (effect.Target != BattleTarget.Self)
                        errors.Add(BuqiText.Format("{0}：Noise 需要 Self 目标", where));
                    if (effect.Amount == 0)
                        errors.Add(BuqiText.Format("{0}：Noise 数值不能为 0", where));
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
                errors.Add("淬炼表不能为空");
                return refinements;
            }
            if (rows.Count != s_EnabledRefinementIds.Length)
                errors.Add(BuqiText.Format(
                    "应有 {0} 个淬炼，实际为 {1} 个",
                    s_EnabledRefinementIds.Length,
                    rows.Count));

            foreach (BuqiRefinementConfigRow row in rows)
            {
                if (row == null)
                {
                    errors.Add("淬炼表行不能为空");
                    continue;
                }
                if (string.IsNullOrEmpty(row.RefinementId))
                {
                    errors.Add("淬炼 ID 不能为空");
                    continue;
                }
                if (!refinements.Add(row.RefinementId))
                    errors.Add(BuqiText.Format("淬炼 ID {0} 重复", row.RefinementId));
                if (!IsExpectedRefinementId(row.RefinementId))
                    errors.Add(BuqiText.Format("淬炼 {0} 超出当前扩展范围", row.RefinementId));
                if (string.IsNullOrEmpty(row.DisplayName))
                    errors.Add(BuqiText.Format("淬炼 {0}：显示名称为空", row.RefinementId));
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
                errors.Add("道影表不能为空");
                return;
            }
            if (catalog.Echoes.Count != 16)
                errors.Add(BuqiText.Format("应有 16 个道影，实际为 {0} 个", catalog.Echoes.Count));

            var echoIds = new HashSet<string>(StringComparer.Ordinal);
            IItemDefinitionProvider provider = new BuqiDefinitionProvider(catalog);
            foreach (BuqiEchoConfigRow echo in catalog.Echoes)
            {
                if (echo == null)
                {
                    errors.Add("道影表行不能为空");
                    continue;
                }
                string where = BuqiText.Format("道影 {0}", echo.EchoId);
                if (string.IsNullOrEmpty(echo.EchoId))
                    errors.Add("道影 ID 不能为空");
                else if (!echoIds.Add(echo.EchoId))
                    errors.Add(BuqiText.Format("道影 ID {0} 重复", echo.EchoId));
                if (string.IsNullOrEmpty(echo.Build))
                    errors.Add(BuqiText.Format("{0}：构筑方向为空", where));
                else if (!IsExpectedBuildId(echo.Build))
                    errors.Add(BuqiText.Format("{0}：未知构筑方向 {1}", where, echo.Build));
                if (echo.Snapshot == null)
                {
                    errors.Add(BuqiText.Format("{0}：快照为空", where));
                    continue;
                }
                if (!string.Equals(echo.Build, echo.Snapshot.ArchetypeId, StringComparison.Ordinal))
                {
                    errors.Add(BuqiText.Format(
                        "{0}：构筑方向 {1} 与快照 archetype {2} 不匹配",
                        where,
                        echo.Build,
                        echo.Snapshot.ArchetypeId));
                }

                CheckRawAnchorDuplicates(echo.Snapshot, where, errors);
                foreach (BuqiItemInstanceConfigRow instance in echo.Snapshot.Items)
                {
                    if (instance == null)
                    {
                        errors.Add(BuqiText.Format("{0}：快照装备为空", where));
                        continue;
                    }
                    if (!items.ContainsKey(instance.DefinitionId))
                        errors.Add(BuqiText.Format("{0}：未知的 definitionId {1}", where, instance.DefinitionId));
                    if (!string.IsNullOrEmpty(instance.RefinementId) && !refinements.Contains(instance.RefinementId))
                        errors.Add(BuqiText.Format("{0}：未知的 refinementId {1}", where, instance.RefinementId));
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
                    errors.Add(BuqiText.Format("{0}：在棋位 {1} 与装备[{2}] 重叠", where, item.AnchorSlot, previous));
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
                errors.Add(BuqiText.Format("{0}：修正数值必须大于 0", where));
            if (effect.DurationTicks <= 0)
                errors.Add(BuqiText.Format("{0}：修正持续时刻必须大于 0", where));
        }

        private static void ValidateStatusAmount(
            BuqiEffectConfigRow effect,
            string where,
            List<string> errors)
        {
            if (effect.Amount <= 0)
                errors.Add(BuqiText.Format("{0}：状态数值必须大于 0", where));
            if (effect.DurationTicks <= 0)
                errors.Add(BuqiText.Format("{0}：状态持续时刻必须大于 0", where));
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
