using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Game.Hot.Buqi.Battle
{
    /// <summary>
    /// 8 格构筑快照的纯 C# 合法性校验器。
    /// Unity Editor、正式 GameHot 业务层与无头验证器共用同一实现，因此不得依赖 Unity、UGF 或 ET 生命周期。
    /// </summary>
    public static class BuqiBoardValidator
    {
        /// <summary>首阶段棋盘固定格数。</summary>
        public const int BoardSlotCount = 8;

        // 首阶段只批准 A-01 至 A-06；使用 CultureInvariant 避免不同运行环境产生差异。
        private static readonly Regex s_AnnotationRegex =
            new Regex(@"^A-0[1-6]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// 校验内容版本、初始资源、实例唯一性、定义合法性、S/M/L 占位以及临时修正。
        /// 返回 false 时会尽量收集全部可定位错误，供编辑器与无头工具输出一致的诊断结果。
        /// </summary>
        public static bool Validate(
            BuildSnapshot snapshot,
            IItemDefinitionProvider provider,
            out List<string> errors)
        {
            errors = new List<string>();
            if (snapshot == null)
            {
                errors.Add("snapshot is null");
                return false;
            }

            if (provider == null)
            {
                errors.Add("definition provider is null");
                return false;
            }

            if (string.IsNullOrEmpty(snapshot.ContentVersion))
                errors.Add("contentVersion empty");
            else if (!string.Equals(snapshot.ContentVersion, provider.ContentVersion, System.StringComparison.Ordinal))
                errors.Add("contentVersion mismatch");

            if (snapshot.InitialExecution <= 0)
                errors.Add("initialExecution must be > 0");
            if (snapshot.InitialBuffer < 0)
                errors.Add("initialBuffer must be >= 0");
            else if (snapshot.InitialBuffer > BuqiBattleSimulator.BufferCap)
                errors.Add("initialBuffer exceeds buffer cap");
            if (snapshot.InitialNoiseDebt < 0)
                errors.Add("initialNoiseDebt must be >= 0");
            else if (snapshot.InitialNoiseDebt >= BuqiBattleSimulator.NoiseThreshold)
                errors.Add("initialNoiseDebt must be below noise threshold");
            if (snapshot.Items == null || snapshot.Items.Count == 0)
            {
                errors.Add("at least one item required");
                return false;
            }

            var occupied = new int[BoardSlotCount];
            var seenIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int index = 0; index < snapshot.Items.Count; index++)
            {
                ItemInstance item = snapshot.Items[index];
                string where = BuqiText.Format("item[{0}]", index);
                if (item == null)
                {
                    errors.Add(BuqiText.Format("{0}: null item", where));
                    continue;
                }

                if (string.IsNullOrEmpty(item.InstanceId))
                {
                    errors.Add(BuqiText.Format("{0}: empty instanceId", where));
                }
                else if (!seenIds.Add(item.InstanceId))
                {
                    errors.Add(BuqiText.Format("{0}: duplicate instanceId {1}", where, item.InstanceId));
                }

                if (item.AnchorSlot < 0 || item.AnchorSlot >= BoardSlotCount)
                    errors.Add(BuqiText.Format("{0}: anchorSlot out of range {1}", where, item.AnchorSlot));
                if (item.Quality < (int)BuqiQuality.Normal || item.Quality > (int)BuqiQuality.Fixed)
                    errors.Add(BuqiText.Format("{0}: quality out of range {1}", where, item.Quality));
                if (!string.IsNullOrEmpty(item.AnnotationId) && !s_AnnotationRegex.IsMatch(item.AnnotationId))
                    errors.Add(BuqiText.Format("{0}: invalid annotationId {1}", where, item.AnnotationId));

                ValidateTemporaryModifiers(item, where, errors);
                if (string.IsNullOrEmpty(item.DefinitionId) ||
                    !provider.TryGet(item.DefinitionId, out BuqiItemDefinition definition))
                {
                    errors.Add(BuqiText.Format("{0}: unknown definitionId {1}", where, item.DefinitionId));
                    continue;
                }

                if (!ValidateDefinition(definition, where, errors))
                    continue;
                if (item.AnchorSlot < 0 || item.AnchorSlot >= BoardSlotCount)
                    continue;

                int endSlot = item.AnchorSlot + definition.Size - 1;
                if (endSlot >= BoardSlotCount)
                {
                    errors.Add(BuqiText.Format(
                        "{0}: size {1} at slot {2} overflows board",
                        where, definition.Size, item.AnchorSlot));
                    continue;
                }

                for (int slot = item.AnchorSlot; slot <= endSlot; slot++)
                {
                    if (occupied[slot] != 0)
                    {
                        errors.Add(BuqiText.Format(
                            "{0}: overlap at slot {1} with item[{2}]",
                            where, slot, occupied[slot] - 1));
                    }
                    else
                    {
                        occupied[slot] = index + 1;
                    }
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// 临时修正必须显式声明 Haste 或 Delay，不能再通过 Bps 正负号推测效果类型。
        /// </summary>
        private static void ValidateTemporaryModifiers(
            ItemInstance item,
            string where,
            List<string> errors)
        {
            if (item.TemporaryModifiers == null)
                return;

            foreach (TemporaryModifier modifier in item.TemporaryModifiers)
            {
                if (modifier == null)
                {
                    errors.Add(BuqiText.Format("{0}: null temporaryModifier", where));
                    continue;
                }
                if (modifier.Effect != BuqiEffect.Haste && modifier.Effect != BuqiEffect.Delay)
                    errors.Add(BuqiText.Format("{0}: modifier effect must be Haste or Delay", where));
                if (string.IsNullOrEmpty(modifier.SourceInstanceId))
                    errors.Add(BuqiText.Format("{0}: modifier sourceInstanceId empty", where));
                if (modifier.RemainingTicks <= 0)
                    errors.Add(BuqiText.Format("{0}: modifier remainingTicks must be > 0", where));
                if (modifier.Bps < 0)
                    errors.Add(BuqiText.Format("{0}: modifier bps must be >= 0", where));
            }
        }

        /// <summary>
        /// 防御性校验定义提供器返回的数据；正式内容接入 Luban 后仍不信任未校验的外部配置。
        /// </summary>
        private static bool ValidateDefinition(
            BuqiItemDefinition definition,
            string where,
            List<string> errors)
        {
            if (definition.Size < (int)BuqiSize.S || definition.Size > (int)BuqiSize.L)
            {
                errors.Add(BuqiText.Format("{0}: definition size invalid {1}", where, definition.Size));
                return false;
            }
            if (definition.BaseCooldownTicks <= 0)
                errors.Add(BuqiText.Format("{0}: definition cooldown must be > 0", where));
            if (definition.Effects == null)
            {
                errors.Add(BuqiText.Format("{0}: definition effects is null", where));
                return false;
            }

            foreach (BuqiEffectSpec spec in definition.Effects)
            {
                if (spec == null)
                {
                    errors.Add(BuqiText.Format("{0}: null effect spec", where));
                    continue;
                }
                if (!System.Enum.IsDefined(typeof(BuqiTrigger), spec.Trigger))
                    errors.Add(BuqiText.Format("{0}: invalid trigger {1}", where, spec.Trigger));
                if (!System.Enum.IsDefined(typeof(BuqiEffect), spec.Effect))
                    errors.Add(BuqiText.Format("{0}: invalid effect {1}", where, spec.Effect));
                if (!System.Enum.IsDefined(typeof(BuqiTarget), spec.Target))
                    errors.Add(BuqiText.Format("{0}: invalid target {1}", where, spec.Target));
            }
            return true;
        }
    }
}
