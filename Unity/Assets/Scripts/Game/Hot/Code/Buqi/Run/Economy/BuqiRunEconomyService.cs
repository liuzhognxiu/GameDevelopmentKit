using System;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Economy
{
    public sealed class BuqiRunEconomyService
    {
        private readonly IBuqiRunItemCatalog m_Catalog;

        public BuqiRunEconomyService(IBuqiRunItemCatalog catalog)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public BuqiRunEconomyResult Purchase(BuqiRunEconomySnapshot source, string definitionId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BuqiRunEconomySnapshot working = source.Clone();
            if (!HasDefinedQualities(working))
                return Fail(source, "Item quality is invalid.");
            if (!TryResolveDefinition(definitionId, out BuqiRunItemDefinition definition))
                return Fail(source, "Item definition was not found.");
            if (!IsPositiveSize(definition.Size))
                return Fail(source, "Item definition size must be positive.");
            if (!IsNonNegativePrice(definition.BuyPrice))
                return Fail(source, "Buy price must be non-negative.");
            if (working.Run.Coins < definition.BuyPrice)
                return Fail(source, "Not enough coins.");

            string mergeInstanceId = FindMergeTarget(working, definitionId, BuqiRunItemQuality.Common);
            if (!string.IsNullOrEmpty(mergeInstanceId))
            {
                BuqiRunItemInstance mergedItem = working.Items[mergeInstanceId];
                if (mergedItem.Quality == BuqiRunItemQuality.Finalized)
                    return Fail(source, "Item is already finalized.");

                mergedItem.Quality = AdvanceQuality(mergedItem.Quality);
                working.Run.Coins -= definition.BuyPrice;
                return Success(working, mergeInstanceId);
            }

            int storageSlot = FindFirstEmptyStorageSlot(working.Run.StorageInstanceIds);
            if (storageSlot < 0)
                return Fail(source, "No storage slot available.");

            string instanceId = working.CreateInstanceId();
            working.Run.StorageInstanceIds[storageSlot] = instanceId;
            working.Items[instanceId] = new BuqiRunItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                Quality = BuqiRunItemQuality.Common,
            };
            working.Run.Coins -= definition.BuyPrice;
            return Success(working, instanceId);
        }

        public BuqiRunEconomyResult Sell(BuqiRunEconomySnapshot source, string instanceId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BuqiRunEconomySnapshot working = source.Clone();
            if (!HasDefinedQualities(working))
                return Fail(source, "Item quality is invalid.");
            if (!TryResolveItem(working, instanceId, out BuqiRunItemInstance item, out BuqiRunItemDefinition definition))
                return Fail(source, "Item instance was not found.");
            if (!IsPositiveSize(definition.Size))
                return Fail(source, "Item definition size must be positive.");
            if (!IsNonNegativePrice(definition.SellPrice))
                return Fail(source, "Sell price must be non-negative.");

            RemoveInstanceFromSlots(working, instanceId);
            working.Items.Remove(instanceId);
            working.Run.Coins += definition.SellPrice;
            return Success(working, instanceId);
        }

        public BuqiRunEconomyResult Upgrade(BuqiRunEconomySnapshot source, string instanceId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BuqiRunEconomySnapshot working = source.Clone();
            if (!HasDefinedQualities(working))
                return Fail(source, "Item quality is invalid.");
            if (!TryResolveItem(working, instanceId, out BuqiRunItemInstance item, out BuqiRunItemDefinition definition))
                return Fail(source, "Item instance was not found.");
            if (!IsPositiveSize(definition.Size))
                return Fail(source, "Item definition size must be positive.");
            if (!IsNonNegativePrice(definition.UpgradePrice))
                return Fail(source, "Upgrade price must be non-negative.");
            if (item.Quality == BuqiRunItemQuality.Finalized)
                return Fail(source, "Item is already finalized.");
            if (working.Run.Coins < definition.UpgradePrice)
                return Fail(source, "Not enough coins.");

            item.Quality = AdvanceQuality(item.Quality);
            working.Run.Coins -= definition.UpgradePrice;
            return Success(working, instanceId);
        }

        public BuqiRunEconomyResult Refine(
            BuqiRunEconomySnapshot source,
            string instanceId,
            string refinementId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BuqiRunEconomySnapshot working = source.Clone();
            if (!HasDefinedQualities(working))
                return Fail(source, "Item quality is invalid.");
            if (!TryResolveItem(working, instanceId, out BuqiRunItemInstance item, out BuqiRunItemDefinition definition))
                return Fail(source, "Item instance was not found.");
            if (!IsPositiveSize(definition.Size))
                return Fail(source, "Item definition size must be positive.");
            if (!IsNonNegativePrice(definition.RefinementPrice))
                return Fail(source, "Refinement price must be non-negative.");
            if (string.IsNullOrWhiteSpace(refinementId))
                return Fail(source, "Refinement id is required.");
            if (!string.IsNullOrEmpty(item.RefinementId))
                return Fail(source, "Item already has a refinement.");
            if (working.Run.Coins < definition.RefinementPrice)
                return Fail(source, "Not enough coins.");

            item.RefinementId = refinementId;
            working.Run.Coins -= definition.RefinementPrice;
            return Success(working, instanceId);
        }

        private bool TryResolveDefinition(string definitionId, out BuqiRunItemDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definitionId)
                && m_Catalog.TryGet(definitionId, out BuqiRunItemDefinition? resolved)
                && resolved != null)
            {
                definition = resolved;
                return true;
            }

            definition = null!;
            return false;
        }

        private bool TryResolveItem(
            BuqiRunEconomySnapshot snapshot,
            string instanceId,
            out BuqiRunItemInstance item,
            out BuqiRunItemDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(instanceId)
                && snapshot.Items.TryGetValue(instanceId, out BuqiRunItemInstance? resolvedItem)
                && resolvedItem != null)
            {
                item = resolvedItem;
                return TryResolveDefinition(item.DefinitionId, out definition);
            }

            item = null!;
            definition = null!;
            return false;
        }

        private static string FindMergeTarget(
            BuqiRunEconomySnapshot snapshot,
            string definitionId,
            BuqiRunItemQuality quality)
        {
            for (int index = 0; index < snapshot.Run.BoardInstanceIds.Count; index++)
            {
                string instanceId = snapshot.Run.BoardInstanceIds[index];
                if (string.IsNullOrEmpty(instanceId))
                    continue;
                if (snapshot.Items.TryGetValue(instanceId, out BuqiRunItemInstance? item)
                    && item != null
                    && item.DefinitionId == definitionId
                    && item.Quality == quality
                    && item.Quality != BuqiRunItemQuality.Finalized)
                {
                    return instanceId;
                }
            }

            for (int index = 0; index < snapshot.Run.StorageInstanceIds.Count; index++)
            {
                string instanceId = snapshot.Run.StorageInstanceIds[index];
                if (string.IsNullOrEmpty(instanceId))
                    continue;
                if (snapshot.Items.TryGetValue(instanceId, out BuqiRunItemInstance? item)
                    && item != null
                    && item.DefinitionId == definitionId
                    && item.Quality == quality
                    && item.Quality != BuqiRunItemQuality.Finalized)
                {
                    return instanceId;
                }
            }

            return string.Empty;
        }

        private static int FindFirstEmptyStorageSlot(System.Collections.Generic.List<string> storageInstanceIds)
        {
            for (int index = 0; index < storageInstanceIds.Count; index++)
            {
                if (string.IsNullOrEmpty(storageInstanceIds[index]))
                    return index;
            }

            return -1;
        }

        private static void RemoveInstanceFromSlots(BuqiRunEconomySnapshot snapshot, string instanceId)
        {
            for (int index = 0; index < snapshot.Run.BoardInstanceIds.Count; index++)
            {
                if (snapshot.Run.BoardInstanceIds[index] == instanceId)
                    snapshot.Run.BoardInstanceIds[index] = string.Empty;
            }

            for (int index = 0; index < snapshot.Run.StorageInstanceIds.Count; index++)
            {
                if (snapshot.Run.StorageInstanceIds[index] == instanceId)
                    snapshot.Run.StorageInstanceIds[index] = string.Empty;
            }
        }

        private static BuqiRunItemQuality AdvanceQuality(BuqiRunItemQuality quality)
        {
            if (quality == BuqiRunItemQuality.Common)
                return BuqiRunItemQuality.Improved;
            if (quality == BuqiRunItemQuality.Improved)
                return BuqiRunItemQuality.Finalized;
            return BuqiRunItemQuality.Finalized;
        }

        private static bool HasDefinedQualities(BuqiRunEconomySnapshot snapshot)
        {
            foreach (BuqiRunItemInstance item in snapshot.Items.Values)
            {
                if (!IsDefinedQuality(item.Quality))
                    return false;
            }

            return true;
        }

        private static bool IsDefinedQuality(BuqiRunItemQuality quality)
        {
            return quality == BuqiRunItemQuality.Common
                || quality == BuqiRunItemQuality.Improved
                || quality == BuqiRunItemQuality.Finalized;
        }

        private static bool IsPositiveSize(int size)
        {
            return size >= 1 && size <= 3;
        }

        private static bool IsNonNegativePrice(int price)
        {
            return price >= 0;
        }

        private static BuqiRunEconomyResult Success(BuqiRunEconomySnapshot snapshot, string affectedInstanceId)
        {
            return new BuqiRunEconomyResult
            {
                Success = true,
                Snapshot = snapshot,
                AffectedInstanceId = affectedInstanceId,
            };
        }

        private static BuqiRunEconomyResult Fail(BuqiRunEconomySnapshot source, string reason)
        {
            return new BuqiRunEconomyResult
            {
                Success = false,
                FailureReason = reason,
                Snapshot = source.Clone(),
            };
        }
    }
}
