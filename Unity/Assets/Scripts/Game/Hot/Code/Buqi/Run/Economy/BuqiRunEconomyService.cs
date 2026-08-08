using System;
using System.Collections.Generic;
using System.Text;
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

        public BuqiRunEconomyResult GrantFreeItem(BuqiRunEconomySnapshot source, string definitionId)
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

            string mergeInstanceId = FindMergeTarget(working, definitionId, BuqiRunItemQuality.Common);
            if (!string.IsNullOrEmpty(mergeInstanceId))
            {
                BuqiRunItemInstance mergedItem = working.Items[mergeInstanceId];
                if (mergedItem.Quality == BuqiRunItemQuality.Finalized)
                    return Fail(source, "Item is already finalized.");

                mergedItem.Quality = AdvanceQuality(mergedItem.Quality);
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

        public BuqiRunSellQuote QuoteBoardSale(BuqiRunEconomySnapshot source, string instanceId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!HasDefinedQualities(source))
                return RejectQuote("Item quality is invalid.");
            if (!IsOnBoard(source, instanceId))
                return RejectQuote("Board item instance was not found.");
            if (!TryResolveItem(source, instanceId, out BuqiRunItemInstance item, out BuqiRunItemDefinition definition))
                return RejectQuote("Item instance was not found.");
            if (!string.Equals(item.InstanceId, instanceId, StringComparison.Ordinal))
                return RejectQuote("Item instance identity is invalid.");
            if (!IsPositiveSize(definition.Size))
                return RejectQuote("Item definition size must be positive.");
            if (!IsNonNegativePrice(definition.SellPrice))
                return RejectQuote("Sell price must be non-negative.");

            return new BuqiRunSellQuote
            {
                Success = true,
                InstanceId = instanceId,
                ExpectedRefund = definition.SellPrice,
                DefinitionId = item.DefinitionId,
                Quality = item.Quality,
                RefinementId = item.RefinementId,
                SnapshotToken = CreateSnapshotToken(source),
            };
        }

        public BuqiRunEconomyResult SellQuoted(BuqiRunEconomySnapshot source, BuqiRunSellQuote quote)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (quote == null || !quote.Success)
                return Fail(source, "Sell quote was not accepted.");
            if (!string.Equals(quote.SnapshotToken, CreateSnapshotToken(source), StringComparison.Ordinal))
                return Fail(source, "Sell quote is stale.");
            if (!IsOnBoard(source, quote.InstanceId))
                return Fail(source, "Sell quote is stale.");
            if (!TryResolveItem(
                    source,
                    quote.InstanceId,
                    out BuqiRunItemInstance item,
                    out BuqiRunItemDefinition definition))
            {
                return Fail(source, "Sell quote is stale.");
            }
            if (!string.Equals(item.InstanceId, quote.InstanceId, StringComparison.Ordinal)
                || !string.Equals(item.DefinitionId, quote.DefinitionId, StringComparison.Ordinal)
                || item.Quality != quote.Quality
                || !string.Equals(item.RefinementId, quote.RefinementId, StringComparison.Ordinal)
                || definition.SellPrice != quote.ExpectedRefund)
            {
                return Fail(source, "Sell quote is stale.");
            }

            return Sell(source, quote.InstanceId);
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
                && m_Catalog.TryGet(definitionId, out BuqiRunItemDefinition resolved)
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
                && snapshot.Items.TryGetValue(instanceId, out BuqiRunItemInstance resolvedItem)
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
                if (snapshot.Items.TryGetValue(instanceId, out BuqiRunItemInstance item)
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
                if (snapshot.Items.TryGetValue(instanceId, out BuqiRunItemInstance item)
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

        private static bool IsOnBoard(BuqiRunEconomySnapshot snapshot, string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return false;

            for (int index = 0; index < snapshot.Run.BoardInstanceIds.Count; index++)
            {
                if (string.Equals(snapshot.Run.BoardInstanceIds[index], instanceId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string CreateSnapshotToken(BuqiRunEconomySnapshot snapshot)
        {
            var builder = new StringBuilder(512);
            AppendTokenPart(builder, snapshot.Run.ContentVersion);
            AppendTokenPart(builder, snapshot.Run.RuleVersion);
            AppendTokenPart(builder, snapshot.Run.RunSeed.ToString());
            AppendTokenPart(builder, snapshot.Run.RngCursor.ToString());
            AppendTokenPart(builder, snapshot.Run.Revision.ToString());
            AppendTokenPart(builder, snapshot.Run.Day.ToString());
            AppendTokenPart(builder, snapshot.Run.EncounterIndex.ToString());
            AppendTokenPart(builder, ((int)snapshot.Run.Phase).ToString());
            AppendTokenPart(builder, ((int)snapshot.Run.Outcome).ToString());
            AppendTokenPart(builder, snapshot.Run.Coins.ToString());
            AppendTokenPart(builder, snapshot.Run.Wins.ToString());
            AppendTokenPart(builder, snapshot.Run.Lives.ToString());
            AppendTokenPart(builder, snapshot.NextItemOrdinal.ToString());
            AppendTokenParts(builder, snapshot.Run.BoardInstanceIds);
            AppendTokenParts(builder, snapshot.Run.StorageInstanceIds);
            AppendSortedTokenParts(builder, snapshot.Run.AppliedCommandIds);
            AppendSortedTokenParts(builder, snapshot.Run.AppliedSettlementIds);

            var instanceIds = new List<string>(snapshot.Items.Keys);
            instanceIds.Sort(StringComparer.Ordinal);
            foreach (string instanceId in instanceIds)
            {
                AppendTokenPart(builder, instanceId);
                BuqiRunItemInstance item = snapshot.Items[instanceId];
                if (item == null)
                {
                    AppendTokenPart(builder, string.Empty);
                    continue;
                }

                AppendTokenPart(builder, item.InstanceId);
                AppendTokenPart(builder, item.DefinitionId);
                AppendTokenPart(builder, ((int)item.Quality).ToString());
                AppendTokenPart(builder, item.RefinementId);
            }

            return builder.ToString();
        }

        private static void AppendTokenParts(StringBuilder builder, IEnumerable<string> values)
        {
            foreach (string value in values)
                AppendTokenPart(builder, value);
        }

        private static void AppendSortedTokenParts(StringBuilder builder, IEnumerable<string> values)
        {
            var sorted = new List<string>(values);
            sorted.Sort(StringComparer.Ordinal);
            AppendTokenParts(builder, sorted);
        }

        private static void AppendTokenPart(StringBuilder builder, string value)
        {
            string safeValue = value ?? string.Empty;
            builder.Append(safeValue.Length).Append(':').Append(safeValue).Append('|');
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

        private static BuqiRunSellQuote RejectQuote(string reason)
        {
            return new BuqiRunSellQuote
            {
                Success = false,
                FailureReason = reason,
            };
        }
    }
}
