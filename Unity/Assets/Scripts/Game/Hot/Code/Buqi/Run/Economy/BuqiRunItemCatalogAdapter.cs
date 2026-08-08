using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.Run.Economy
{
    public sealed class BuqiRunItemCatalogAdapter : IBuqiRunItemCatalog
    {
        private readonly Dictionary<string, BuqiRunItemDefinition> m_Definitions;

        public BuqiRunItemCatalogAdapter(BuqiConfigCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            m_Definitions = new Dictionary<string, BuqiRunItemDefinition>(StringComparer.Ordinal);
            foreach (BuqiItemConfigRow row in catalog.Items)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.DefinitionId))
                    throw new ArgumentException("Item definition id is required.", nameof(catalog));
                if (m_Definitions.ContainsKey(row.DefinitionId))
                    throw new ArgumentException(
                        BuqiText.Format("Duplicate item definition id: {0}", row.DefinitionId),
                        nameof(catalog));
                if ((int)row.Size < 1 || (int)row.Size > 3)
                    throw new ArgumentException(
                        BuqiText.Format("Item size must be positive: {0}", row.DefinitionId),
                        nameof(catalog));
                if (row.BasePrice < 0)
                    throw new ArgumentException(
                        BuqiText.Format("Item base price must be non-negative: {0}", row.DefinitionId),
                        nameof(catalog));

                m_Definitions.Add(row.DefinitionId, CreateDefinition(row));
            }
        }

        public bool TryGet(string definitionId, out BuqiRunItemDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definitionId)
                && m_Definitions.TryGetValue(definitionId, out BuqiRunItemDefinition existing)
                && existing != null)
            {
                definition = CloneDefinition(existing);
                return true;
            }

            definition = null!;
            return false;
        }

        private static BuqiRunItemDefinition CreateDefinition(BuqiItemConfigRow row)
        {
            int buyPrice = row.BasePrice;
            return new BuqiRunItemDefinition
            {
                DefinitionId = row.DefinitionId,
                Size = (int)row.Size,
                BuyPrice = buyPrice,
                SellPrice = Math.Max(1, buyPrice / 2),
                UpgradePrice = Math.Max(1, buyPrice),
                RefinementPrice = Math.Max(1, buyPrice),
            };
        }

        private static BuqiRunItemDefinition CloneDefinition(BuqiRunItemDefinition definition)
        {
            return new BuqiRunItemDefinition
            {
                DefinitionId = definition.DefinitionId,
                Size = definition.Size,
                BuyPrice = definition.BuyPrice,
                SellPrice = definition.SellPrice,
                UpgradePrice = definition.UpgradePrice,
                RefinementPrice = definition.RefinementPrice,
            };
        }
    }
}
