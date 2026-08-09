using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Hot.Buqi.Run.Supply
{
    public sealed class BuqiFunctionalFormationRule
    {
        public string ArchetypeId = string.Empty;
        public int MinimumDistinctDefinitions = 1;
        public List<BuqiSupplyProductRole> RequiredRoles = new List<BuqiSupplyProductRole>();
        public List<string> RequiredTags = new List<string>();
    }

    public sealed class BuqiSupplyOwnedItem
    {
        public string DefinitionId = string.Empty;
        public BuqiSupplyQuality Quality;
        public string RefinementId = string.Empty;
        public int AnchorSlot = -1;

        public static BuqiSupplyOwnedItem FromDefinition(BuqiSupplyDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            return new BuqiSupplyOwnedItem
            {
                DefinitionId = definition.DefinitionId,
                Quality = definition.Quality,
                RefinementId = definition.RefinementId,
            };
        }
    }

    public sealed class BuqiExactEchoPart
    {
        public string DefinitionId = string.Empty;
        public BuqiSupplyQuality Quality;
        public string RefinementId = string.Empty;
        public int AnchorSlot = -1;
    }

    public sealed class BuqiExactEchoRecipe
    {
        public string EchoId = string.Empty;
        public List<BuqiExactEchoPart> Parts = new List<BuqiExactEchoPart>();
    }

    public static class BuqiSupplyFormationEvaluator
    {
        public static bool IsFunctional(
            BuqiFunctionalFormationRule rule,
            IEnumerable<BuqiSupplyDefinition> ownedDefinitions)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            if (ownedDefinitions == null)
                throw new ArgumentNullException(nameof(ownedDefinitions));
            if (rule.MinimumDistinctDefinitions < 1)
                return false;

            List<BuqiSupplyDefinition> matching = ownedDefinitions
                .Where(definition => definition != null &&
                    (string.IsNullOrEmpty(rule.ArchetypeId) ||
                     string.Equals(definition.ArchetypeId, rule.ArchetypeId, StringComparison.Ordinal)))
                .ToList();
            if (matching.Select(definition => definition.DefinitionId)
                    .Distinct(StringComparer.Ordinal).Count() < rule.MinimumDistinctDefinitions)
            {
                return false;
            }

            if (rule.RequiredRoles.Any(role => matching.All(definition => definition.Role != role)))
                return false;

            var tags = new HashSet<string>(
                matching.SelectMany(definition => definition.Tags)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag)),
                StringComparer.Ordinal);
            return rule.RequiredTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .All(tags.Contains);
        }

        public static bool IsExactEcho(
            BuqiExactEchoRecipe recipe,
            IEnumerable<BuqiSupplyOwnedItem> ownedItems)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (ownedItems == null)
                throw new ArgumentNullException(nameof(ownedItems));
            if (recipe.Parts.Count == 0)
                return false;

            List<BuqiSupplyOwnedItem> available = ownedItems
                .Where(item => item != null)
                .ToList();
            foreach (BuqiExactEchoPart part in recipe.Parts)
            {
                if (part == null)
                    return false;

                int matchIndex = available.FindIndex(item => IsExactMatch(part, item));
                if (matchIndex < 0)
                    return false;
                available.RemoveAt(matchIndex);
            }
            return true;
        }

        private static bool IsExactMatch(BuqiExactEchoPart part, BuqiSupplyOwnedItem item)
        {
            return string.Equals(part.DefinitionId, item.DefinitionId, StringComparison.Ordinal) &&
                   part.Quality == item.Quality &&
                   string.Equals(part.RefinementId, item.RefinementId, StringComparison.Ordinal) &&
                   part.AnchorSlot == item.AnchorSlot;
        }
    }
}
