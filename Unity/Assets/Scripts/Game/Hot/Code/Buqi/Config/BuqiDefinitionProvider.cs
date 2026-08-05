using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;

namespace Game.Hot.Buqi.Config
{
    public sealed class BuqiDefinitionProvider : IItemDefinitionProvider
    {
        private readonly Dictionary<string, BuqiItemDefinition> m_Definitions;

        public BuqiDefinitionProvider(BuqiConfigCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            ContentVersion = catalog.Global == null ? string.Empty : catalog.Global.ContentVersion;
            m_Definitions = new Dictionary<string, BuqiItemDefinition>(StringComparer.Ordinal);
            foreach (BuqiItemConfigRow row in catalog.Items)
            {
                if (row == null || string.IsNullOrEmpty(row.DefinitionId))
                    continue;
                m_Definitions[row.DefinitionId] = CopyItem(row);
            }
        }

        public string ContentVersion { get; }

        public bool TryGet(string definitionId, out BuqiItemDefinition definition)
        {
            if (m_Definitions.TryGetValue(definitionId, out BuqiItemDefinition stored))
            {
                definition = CopyItem(stored);
                return true;
            }

            definition = null;
            return false;
        }

        private static BuqiItemDefinition CopyItem(BuqiItemConfigRow row)
        {
            var definition = new BuqiItemDefinition
            {
                DefinitionId = row.DefinitionId,
                Size = (int)row.Size,
                BaseCooldownTicks = row.BaseCooldownTicks,
            };
            foreach (BuqiEffectConfigRow effect in row.Effects)
                definition.Effects.Add(CopyEffect(effect));
            return definition;
        }

        private static BuqiItemDefinition CopyItem(BuqiItemDefinition source)
        {
            var definition = new BuqiItemDefinition
            {
                DefinitionId = source.DefinitionId,
                Size = source.Size,
                BaseCooldownTicks = source.BaseCooldownTicks,
            };
            foreach (BuqiEffectSpec effect in source.Effects)
                definition.Effects.Add(CopyEffect(effect));
            return definition;
        }

        private static BuqiEffectSpec CopyEffect(BuqiEffectConfigRow source)
        {
            return new BuqiEffectSpec
            {
                Trigger = source.Trigger,
                Effect = source.Effect,
                Target = source.Target,
                Amount = source.Amount,
                DurationTicks = source.DurationTicks,
                ReasonCode = source.ReasonCode,
                ConditionKind = source.ConditionKind,
                ConditionThreshold = source.ConditionThreshold,
                UseCountThreshold = source.UseCountThreshold,
                ChargeReadLimit = source.ChargeReadLimit,
                AmountPerCharge = source.AmountPerCharge,
                ChargeConsume = source.ChargeConsume,
                ResetCountOnReached = source.ResetCountOnReached,
            };
        }

        private static BuqiEffectSpec CopyEffect(BuqiEffectSpec source)
        {
            return new BuqiEffectSpec
            {
                Trigger = source.Trigger,
                Effect = source.Effect,
                Target = source.Target,
                Amount = source.Amount,
                DurationTicks = source.DurationTicks,
                ReasonCode = source.ReasonCode,
                ConditionKind = source.ConditionKind,
                ConditionThreshold = source.ConditionThreshold,
                UseCountThreshold = source.UseCountThreshold,
                ChargeReadLimit = source.ChargeReadLimit,
                AmountPerCharge = source.AmountPerCharge,
                ChargeConsume = source.ChargeConsume,
                ResetCountOnReached = source.ResetCountOnReached,
            };
        }
    }
}
