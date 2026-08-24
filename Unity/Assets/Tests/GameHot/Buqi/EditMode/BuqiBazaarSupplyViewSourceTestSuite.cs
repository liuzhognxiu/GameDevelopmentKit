using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Supply;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;

namespace Game.Hot.Buqi.Tests
{
    public static class BuqiBazaarSupplyViewSourceTestSuite
    {
        private const int ContractCount = 18;

        public static List<string> RunAll()
        {
            var failures = new List<string>();
            Run("catalog-contract", CatalogContract, failures);
            Run("complete-merchant-coverage-contract", CompleteMerchantCoverageContract, failures);
            Run("constrained-shelf-contract", ConstrainedShelfContract, failures);
            Run("preference-contract", PreferenceContract, failures);
            Run("refresh-contract", RefreshContract, failures);
            Run("restore-contract", RestoreContract, failures);
            Run("merchant-availability-contract", MerchantAvailabilityContract, failures);
            Run("merchant-slot-constraint-contract", MerchantSlotConstraintContract, failures);
            Run("purchase-view-contract", PurchaseViewContract, failures);
            Run("purchase-hole-contract", PurchaseHoleContract, failures);
            Run("refresh-repack-contract", RefreshRepackContract, failures);
            Run("snapshot-restore-contract", SnapshotRestoreContract, failures);
            Run("snapshot-integrity-contract", SnapshotIntegrityContract, failures);
            Run("failure-rollback-contract", FailureRollbackContract, failures);
            Run("open-failure-rollback-contract", OpenFailureRollbackContract, failures);
            Run("restore-rollback-contract", RestoreRollbackContract, failures);
            Run("invalid-category-contract", InvalidCategoryContract, failures);
            Run("non-weapon-specialty-contract", NonWeaponSpecialtyContract, failures);
            return failures;
        }

        public static int Main()
        {
            List<string> failures = RunAll();
            foreach (string failure in failures)
                Console.Error.WriteLine(failure);
            Console.WriteLine($"bazaar-supply-contracts={ContractCount - failures.Count}/{ContractCount}");
            return failures.Count == 0 ? 0 : 1;
        }

        private static void CatalogContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                catalog, out BuqiBazaarSupplyViewSource source, out string error), error);
            Require(source.MerchantIds.Count == 8, "All eight configured merchants must be projected.");
            Require(source.MerchantIds.Distinct(StringComparer.Ordinal).Count() == 8,
                "Projected merchant ids must remain unique.");

            catalog.Merchants[7].MerchantId = catalog.Merchants[0].MerchantId;
            Require(!BuqiBazaarSupplyViewSource.TryCreate(catalog, out _, out _),
                "Duplicate merchant ids must fail closed.");
        }

        private static void ConstrainedShelfContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                catalog, out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(4101, 5, 0, 30, "fast-01", "fast-02");

            Require(source.TryOpen(context, out IReadOnlyList<string> offers, out error), error);
            Require(offers.Count > 0 && offers.Distinct(StringComparer.Ordinal).Count() == offers.Count,
                "A merchant shelf must contain distinct offers.");
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView view),
                "The opened shelf must be visible to the Demo view source.");
            Require(view.ShelfSlotCount == BuqiSupplyService.MerchantShelfSlotCount,
                "Production merchant shelves must expose ten logical slots.");
            AssertCompactLayout(view);
            BuqiMerchantConfigRow merchant = catalog.Merchants.Single(row => row.MerchantId == view.MerchantId);
            Require(offers.All(merchant.PoolItemIds.Contains),
                "Merchant offers must stay inside the configured constrained pool.");
            Require(view.OfferRoles.Count == offers.Count && offers.All(id => view.OfferRoles.ContainsKey(id)),
                "Every offer must expose its configured slot role.");

            Require(source.TryOpen(context, out IReadOnlyList<string> frozen, out error), error);
            Require(frozen.SequenceEqual(offers), "Reopening the same encounter must keep the frozen shelf.");
        }

        private static void PreferenceContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            string[] healOwned = catalog.Items
                .Where(item => item.ArchetypeId == "heal")
                .Take(3)
                .Select(item => item.DefinitionId)
                .Concat(catalog.Items.Where(item => item.ArchetypeId == "fast")
                    .Take(1)
                    .Select(item => item.DefinitionId))
                .ToArray();
            string[] fastOwned = catalog.Items
                .Where(item => item.ArchetypeId == "fast")
                .Take(3)
                .Select(item => item.DefinitionId)
                .Concat(catalog.Items.Where(item => item.ArchetypeId == "heal")
                    .Take(1)
                    .Select(item => item.DefinitionId))
                .ToArray();

            int healPreferredOffers = 0;
            int fastPreferredOffers = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                Require(BuqiBazaarSupplyViewSource.TryCreate(
                    catalog, out BuqiBazaarSupplyViewSource healSource, out string error), error);
                Require(healSource.TryOpen(
                    Context(seed, 4, 1, 24, healOwned),
                    out IReadOnlyList<string> healOffers,
                    out error), error);
                Require(healSource.TryGetCurrentSupply(out BuqiBazaarSupplyView healView),
                    "Preference metadata must be available after opening a shelf.");
                Require(healView.PreferredArchetypeId == "heal",
                    "The dominant owned archetype must become the supply preference.");
                healPreferredOffers += healOffers.Count(id =>
                    id.StartsWith("heal-", StringComparison.Ordinal));

                Require(BuqiBazaarSupplyViewSource.TryCreate(
                    catalog, out BuqiBazaarSupplyViewSource fastSource, out error), error);
                Require(fastSource.TryOpen(
                    Context(seed, 4, 1, 24, fastOwned),
                    out IReadOnlyList<string> fastOffers,
                    out error), error);
                fastPreferredOffers += fastOffers.Count(id =>
                    id.StartsWith("heal-", StringComparison.Ordinal));
            }

            Require(healPreferredOffers > fastPreferredOffers,
                "Heal preference must increase heal offers across deterministic matched seeds.");
        }

        private static void RefreshContract()
        {
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                CreateCatalog(), out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(7331, 7, 0, 50, "buffer-01", "buffer-02");
            Require(source.TryOpen(context, out IReadOnlyList<string> initial, out error), error);

            int balance = context.Balance;
            int[] expectedCosts = { 2, 3, 4 };
            for (int index = 0; index < expectedCosts.Length; index++)
            {
                context.Balance = balance;
                Require(source.TryRefresh(
                    context, out IReadOnlyList<string> refreshed, out int cost, out error), error);
                Require(cost == expectedCosts[index], "Refresh price must progress as 2/3/4.");
                Require(!refreshed.SequenceEqual(initial), "A refresh must replace the frozen shelf.");
                balance -= cost;
                initial = refreshed;
            }

            context.Balance = balance;
            Require(!source.TryRefresh(context, out _, out _, out _),
                "A fourth refresh must be rejected by the production adapter.");
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView view),
                "Exhausted refresh metadata must remain available.");
            Require(view.RefreshCount == 3 && !view.CanRefresh && view.Balance == balance,
                "Refresh count, availability, and remaining balance must be exposed to Demo.");
        }

        private static void PurchaseViewContract()
        {
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                CreateCatalog(), out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(1138, 3, 1, 20, "fast-01");
            Require(source.TryOpen(context, out IReadOnlyList<string> offers, out error), error);

            string purchased = offers[0];
            Require(source.RecordPurchase(purchased, 16, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView view),
                "Purchase metadata must remain visible.");
            Require(view.Balance == 16 && view.PurchasedOfferIds.SequenceEqual(new[] { purchased }),
                "Demo supply data must expose the authoritative balance and purchased offer ids.");
            Require(!source.RecordPurchase(purchased, 12, out _),
                "The same frozen offer cannot be recorded twice.");

            string rejected = offers.First(offer => offer != purchased);
            string beforeFailure = LayoutSignature(view);
            Require(!source.RecordPurchase(rejected, -1, out _),
                "A purchase with an invalid resulting balance must fail.");
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView afterFailure),
                "A failed purchase must keep the current shelf available.");
            Require(LayoutSignature(afterFailure) == beforeFailure &&
                    afterFailure.Balance == view.Balance &&
                    afterFailure.PurchasedOfferIds.SequenceEqual(view.PurchasedOfferIds),
                "A failed purchase must not change balance, holes, purchases, or RNG.");
        }

        private static void PurchaseHoleContract()
        {
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                CreateCatalog(), out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(9128, 5, 0, 30, "fast-01");
            Require(source.TryOpen(context, out IReadOnlyList<string> offers, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView before),
                "Opening supply must expose shelf layout.");

            BuqiBazaarOfferLayoutView purchased = before.Offers[before.Offers.Count / 2];
            var anchors = before.Offers.ToDictionary(
                offer => offer.OfferId, offer => offer.AnchorSlot, StringComparer.Ordinal);
            Require(source.RecordPurchase(purchased.OfferId, 26, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView after),
                "Purchased supply must expose shelf layout.");

            Require(after.Offers.Single(offer => offer.OfferId == purchased.OfferId).Purchased,
                "The purchased offer must be marked as removed from the visible shelf.");
            Require(Enumerable.Range(purchased.AnchorSlot, purchased.Size)
                    .All(after.EmptyShelfSlots.Contains),
                "Purchasing an offer must leave its occupied shelf slots empty.");
            Require(after.Offers.All(offer => anchors[offer.OfferId] == offer.AnchorSlot),
                "Purchasing must not move or refill the remaining shelf offers.");
            Require(after.OfferIds.SequenceEqual(offers),
                "The frozen offer identity list must remain available for persistence.");
        }

        private static void RefreshRepackContract()
        {
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                CreateCatalog(), out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(18311, 7, 1, 50, "buffer-08");
            Require(source.TryOpen(context, out _, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView initial),
                "Opening supply must expose shelf layout.");
            BuqiBazaarOfferLayoutView purchased = initial.Offers[0];
            Require(source.RecordPurchase(purchased.OfferId, 48, out error), error);

            context.Balance = 48;
            Require(source.TryRefresh(context, out _, out _, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView refreshed),
                "Refreshed supply must expose shelf layout.");
            Require(refreshed.Offers.All(offer => !offer.Purchased) &&
                    refreshed.PurchasedOfferIds.Count == 0,
                "A refresh must replace the purchased-hole state with a new shelf.");
            AssertCompactLayout(refreshed);
        }

        private static void SnapshotRestoreContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                catalog, out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(55128, 6, 0, 40, "heal-13", "heal-14");
            Require(source.TryOpen(context, out _, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView opened),
                "Opening supply must expose shelf layout.");
            Require(source.RecordPurchase(opened.Offers[1].OfferId, 36, out error), error);
            context.Balance = 36;
            IBuqiBazaarSupplyPersistence persistence = source;
            Require(persistence.TryCaptureSnapshot(
                out BuqiBazaarSupplySnapshot snapshot, out error), error);

            Require(BuqiBazaarSupplyViewSource.TryCreate(
                catalog, out BuqiBazaarSupplyViewSource restoredSource, out error), error);
            var restoreContext = Context(55128, 6, 0, 36, "heal-13", "heal-14");
            restoreContext.PurchasedOfferIds = snapshot.Offers
                .Where(offer => offer.Purchased)
                .Select(offer => offer.OfferId)
                .ToArray();
            IBuqiBazaarSupplyPersistence restoredPersistence = restoredSource;
            Require(restoredPersistence.TryRestore(restoreContext, snapshot, out error), error);
            Require(restoredSource.TryGetCurrentSupply(out BuqiBazaarSupplyView restored),
                "Restored snapshot must expose shelf layout.");

            Require(LayoutSignature(restored) == LayoutSignature(source),
                "Restore must preserve anchors, empty slots, purchases, and offer identity.");
            Require(restored.SupplyRngCursor == snapshot.SupplyState.Cursor &&
                    restored.SupplyGeneration == snapshot.SupplyState.Generation,
                "Restore must preserve the supply RNG cursor and generation.");

            BuqiBazaarSupplySnapshot invalid = snapshot.Clone();
            invalid.EmptyShelfSlots.Clear();
            Require(!restoredSource.TryRestore(restoreContext, invalid, out _),
                "An invalid supply snapshot must be rejected.");
            Require(restoredSource.TryGetCurrentSupply(out BuqiBazaarSupplyView afterFailure),
                "Failed snapshot restore must keep the previous shelf available.");
            Require(LayoutSignature(afterFailure) == LayoutSignature(restored),
                "Failed snapshot restore must not partially mutate the runtime.");
        }

        private static void FailureRollbackContract()
        {
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                CreateCatalog(), out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(9918, 4, 1, 30, "chain-19");
            Require(source.TryOpen(context, out _, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView before),
                "Opening supply must expose shelf layout.");

            context.Balance = 0;
            Require(!source.TryRefresh(context, out _, out _, out _),
                "An unaffordable refresh must fail.");
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView after),
                "Failed refresh must keep shelf layout available.");
            Require(LayoutSignature(before) == LayoutSignature(after) &&
                    before.Balance == after.Balance &&
                    before.SupplyRngCursor == after.SupplyRngCursor,
                "Failed refresh must not change balance, layout, purchases, or RNG.");
        }

        private static void SnapshotIntegrityContract()
        {
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                CreateCatalog(), out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(90417, 4, 0, 30, "fast-01");
            Require(source.TryOpen(context, out _, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView before),
                "Snapshot integrity fixture must expose a shelf.");
            Require(source.TryCaptureSnapshot(out BuqiBazaarSupplySnapshot snapshot, out error), error);

            BuqiBazaarSupplySnapshot invalidRng = snapshot.Clone();
            invalidRng.SupplyState.Generation = -1;
            Require(!source.TryRestore(context, invalidRng, out _),
                "A snapshot with an invalid RNG generation must be rejected.");

            BuqiBazaarSupplySnapshot invalidPrice = snapshot.Clone();
            invalidPrice.NextRefreshPrice++;
            Require(!source.TryRestore(context, invalidPrice, out _),
                "A snapshot with forged refresh prices must be rejected.");

            BuqiBazaarSupplySnapshot invalidAnchors = snapshot.Clone();
            invalidAnchors.Offers[0].Purchased = true;
            invalidAnchors.Offers[1].Purchased = true;
            invalidAnchors.Offers[1].AnchorSlot = invalidAnchors.Offers[0].AnchorSlot;
            invalidAnchors.EmptyShelfSlots = Enumerable.Range(
                    0, BuqiSupplyService.MerchantShelfSlotCount)
                .Where(slot => !invalidAnchors.Offers.Any(offer =>
                    !offer.Purchased && slot >= offer.AnchorSlot &&
                    slot < offer.AnchorSlot + offer.Size))
                .ToList();
            Require(!source.TryRestore(context, invalidAnchors, out _),
                "A snapshot with overlapping purchased anchors must be rejected.");

            BuqiBazaarSupplySnapshot invalidPurchases = snapshot.Clone();
            invalidPurchases.Offers[0].Purchased = true;
            invalidPurchases.EmptyShelfSlots = Enumerable.Range(
                    0, BuqiSupplyService.MerchantShelfSlotCount)
                .Where(slot => !invalidPurchases.Offers.Any(offer =>
                    !offer.Purchased && slot >= offer.AnchorSlot &&
                    slot < offer.AnchorSlot + offer.Size))
                .ToList();
            Require(!source.TryRestore(context, invalidPurchases, out _),
                "Snapshot purchases must match the authoritative restore context.");
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView after),
                "Rejected snapshot metadata must keep the previous shelf available.");
            Require(LayoutSignature(after) == LayoutSignature(before) &&
                    after.SupplyRngCursor == before.SupplyRngCursor &&
                    after.SupplyGeneration == before.SupplyGeneration,
                "Rejected snapshot metadata must not mutate layout or RNG state.");
        }

        private static void OpenFailureRollbackContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            foreach (BuqiMerchantConfigRow merchant in catalog.Merchants)
            {
                merchant.MinDay = 1;
                merchant.MaxDay = 1;
            }
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                catalog, out BuqiBazaarSupplyViewSource source, out string error), error);
            Require(source.TryOpen(Context(7001, 1, 0, 20), out _, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView before),
                "Open rollback fixture must expose an initial shelf.");

            Require(!source.TryOpen(Context(7001, 2, 0, 20), out _, out _),
                "Opening an encounter without an eligible merchant must fail.");
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView after),
                "A failed open must keep the previous shelf available.");
            Require(LayoutSignature(after) == LayoutSignature(before) &&
                    after.MerchantId == before.MerchantId &&
                    after.SupplyRngCursor == before.SupplyRngCursor,
                "A failed open must not partially mutate merchant, layout, or RNG state.");
        }

        private static void RestoreRollbackContract()
        {
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                CreateCatalog(), out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(7119, 6, 0, 30, "heal-13", "heal-14");
            Require(source.TryOpen(context, out IReadOnlyList<string> initial, out error), error);
            Require(source.TryRefresh(context, out IReadOnlyList<string> refreshed, out _, out error), error);
            Require(!refreshed.SequenceEqual(initial), "Fixture must reach a different refreshed shelf.");

            Require(source.TryRestore(context, initial, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView restored),
                "Restored supply metadata must remain available.");
            Require(restored.OfferIds.SequenceEqual(initial),
                "Restore must return to the authoritative frozen shelf.");
            Require(restored.RefreshCount == 0,
                "Restoring the opening shelf must reset the refresh count.");
        }

        private static void CompleteMerchantCoverageContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            var unassigned = new BuqiItemConfigRow
            {
                DefinitionId = "unassigned-item",
                DisplayName = "Unassigned item",
                ArchetypeId = "fast",
                Role = "starter",
                Size = BattleSize.S,
                UnlockDay = 1,
                BasePrice = 2,
                BaseCooldownTicks = 30,
                Category = Game.Hot.BuqiItemCategory.NonWeapon,
            };
            unassigned.Tags.Add("fast");
            unassigned.Tags.Add("starter");
            catalog.Items.Add(unassigned);

            Require(BuqiBazaarSupplyViewSource.TryCreate(catalog, out _, out string error), error);
        }

        private static void RestoreContract()
        {
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                CreateCatalog(), out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(8821, 6, 1, 30, "heal-13", "heal-14");
            Require(source.TryOpen(context, out IReadOnlyList<string> initial, out error), error);

            Require(source.TryRefresh(context, out IReadOnlyList<string> refreshed, out int cost, out error), error);
            Require(!refreshed.SequenceEqual(initial), "The test seed must produce a different refreshed shelf.");
            context.Balance -= cost;

            Require(source.TryRestore(context, initial, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView restored),
                "A restored shelf must remain visible.");
            Require(restored.OfferIds.SequenceEqual(initial) && restored.RefreshCount == 0,
                "Restoring an earlier saved shelf must reset the deterministic refresh sequence.");
            Require(restored.Balance == context.Balance,
                "Restoration must preserve the authoritative persisted balance.");
        }

        private static void MerchantAvailabilityContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            BuqiMerchantConfigRow constrained = catalog.Merchants[0];
            constrained.Weight = 100000;
            List<string> originalPoolIds = new List<string>(constrained.PoolItemIds);
            constrained.PoolItemIds.Clear();
            List<BuqiItemConfigRow> sparsePool = catalog.Items
                .Where(item => originalPoolIds.Contains(item.DefinitionId))
                .Take(4)
                .ToList();
            foreach (BuqiItemConfigRow item in sparsePool)
            {
                item.UnlockDay = 1;
                item.Size = BattleSize.L;
            }
            constrained.PoolItemIds.AddRange(sparsePool.Select(item => item.DefinitionId));

            for (int seed = 1; seed <= 64; seed++)
            {
                Require(BuqiBazaarSupplyViewSource.TryCreate(
                    catalog, out BuqiBazaarSupplyViewSource source, out string error), error);
                Require(source.TryOpen(
                    Context(seed, 1, 0, 20), out IReadOnlyList<string> offers, out error), error);
                Require(offers.Count == 3,
                    "A sparse large-item pool must stop at nine occupied shelf slots.");
                Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView view),
                    "The replacement merchant must expose view metadata.");
                Require(view.MerchantId == constrained.MerchantId &&
                        view.EmptyShelfSlots.SequenceEqual(new[] { 9 }),
                    "A legal sparse merchant must remain eligible and expose its empty slot.");
            }
        }

        private static void MerchantSlotConstraintContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            BuqiMerchantConfigRow constrained = catalog.Merchants[0];
            constrained.Weight = 100000;
            string allowedId = constrained.PoolItemIds[0];
            catalog.Items.Single(item => item.DefinitionId == allowedId).Tags.Add("slot-exclusive");
            foreach (BuqiMerchantSlotConfigRow slot in constrained.Slots)
            {
                slot.RequiredTag = "slot-exclusive";
                slot.Count = 1;
            }

            Require(BuqiBazaarSupplyViewSource.TryCreate(
                catalog, out BuqiBazaarSupplyViewSource source, out string error), error);
            Require(source.TryOpen(Context(48012, 1, 0, 20),
                out IReadOnlyList<string> offers, out error), error);
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView view),
                "Constrained merchant shelf must expose layout metadata.");
            Require(view.MerchantId == constrained.MerchantId &&
                    offers.SequenceEqual(new[] { allowedId }),
                "Merchant generation must leave legal empty slots instead of bypassing slot filters.");
            Require(view.EmptyShelfSlots.Count ==
                    BuqiSupplyService.MerchantShelfSlotCount - view.Offers[0].Size,
                "A constrained short pool must expose the unused legal shelf tail.");
        }

        private static void InvalidCategoryContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            catalog.Items[0].Category = Game.Hot.BuqiItemCategory.Unknown;
            Require(!BuqiBazaarSupplyViewSource.TryCreate(catalog, out _, out _),
                "Bazaar supply must reject an item without a formal category.");

            catalog = CreateCatalog();
            catalog.Merchants[0].Specialty = (Game.Hot.BuqiMerchantSpecialty)99;
            Require(!BuqiBazaarSupplyViewSource.TryCreate(catalog, out _, out _),
                "Bazaar supply must reject an unknown merchant specialty.");
        }

        private static void NonWeaponSpecialtyContract()
        {
            BuqiConfigCatalog catalog = CreateCatalog();
            foreach (BuqiItemConfigRow item in catalog.Items)
            {
                item.Category = item.DefinitionId.EndsWith("1", StringComparison.Ordinal)
                    ? Game.Hot.BuqiItemCategory.Weapon
                    : Game.Hot.BuqiItemCategory.NonWeapon;
            }
            BuqiMerchantConfigRow specialist = catalog.Merchants.Single(row =>
                row.MerchantId == "merchant-measure");
            specialist.Specialty = Game.Hot.BuqiMerchantSpecialty.NonWeaponOnly;
            specialist.Weight = 1000000;
            specialist.PoolItemIds.RemoveAll(id =>
                catalog.Items.Single(item => item.DefinitionId == id).Category ==
                Game.Hot.BuqiItemCategory.Weapon);

            Require(BuqiBazaarSupplyViewSource.TryCreate(
                catalog, out BuqiBazaarSupplyViewSource source, out string error), error);
            BuqiBazaarSupplyContext context = Context(97531, 7, 0, 100);
            Require(source.TryOpen(context, out _, out error), error);
            AssertNonWeaponShelf(source, specialist.MerchantId);
            for (int refresh = 0; refresh < BuqiSupplyService.MaximumRefreshCount; refresh++)
            {
                Require(source.TryRefresh(context, out _, out int cost, out error), error);
                context.Balance -= cost;
                AssertNonWeaponShelf(source, specialist.MerchantId);
            }
            Require(source.TryCaptureSnapshot(out BuqiBazaarSupplySnapshot snapshot, out error), error);
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                catalog, out BuqiBazaarSupplyViewSource restored, out error), error);
            Require(restored.TryRestore(context, snapshot, out error), error);
            AssertNonWeaponShelf(restored, specialist.MerchantId);

            BuqiConfigCatalog invalid = CreateCatalog();
            BuqiMerchantConfigRow invalidSpecialist = invalid.Merchants[3];
            invalidSpecialist.Specialty = Game.Hot.BuqiMerchantSpecialty.NonWeaponOnly;
            BuqiItemConfigRow weapon = invalid.Items.Single(item =>
                item.DefinitionId == invalidSpecialist.PoolItemIds[0]);
            weapon.Category = Game.Hot.BuqiItemCategory.Weapon;
            Require(!BuqiBazaarSupplyViewSource.TryCreate(invalid, out _, out _),
                "A non-weapon specialist must reject a weapon in its configured pool.");
        }

        private static void AssertNonWeaponShelf(
            BuqiBazaarSupplyViewSource source,
            string merchantId)
        {
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView view),
                "Specialist shelf view is unavailable.");
            Require(view.MerchantId == merchantId && view.MerchantNonWeaponOnly,
                "The deterministic test must select the configured non-weapon specialist.");
            Require(view.MerchantSpecialty.Contains("非武器专营"),
                "The specialist view must expose its non-weapon-only label.");
            Require(view.Offers.Count > 0 && view.Offers.All(offer =>
                    offer.Category == Game.Hot.BuqiItemCategory.NonWeapon),
                "A non-weapon specialist exposed a weapon offer.");
        }

        private static void AssertCompactLayout(BuqiBazaarSupplyView view)
        {
            int expectedAnchor = 0;
            foreach (BuqiBazaarOfferLayoutView offer in view.Offers)
            {
                Require(offer.AnchorSlot == expectedAnchor,
                    "A newly generated shelf must be compact from the left edge.");
                Require(offer.Size >= 1 && offer.Size <= 3 &&
                        offer.AnchorSlot + offer.Size <= view.ShelfSlotCount,
                    "A generated offer has an invalid shelf span.");
                expectedAnchor += offer.Size;
            }
            Require(view.EmptyShelfSlots.SequenceEqual(
                    Enumerable.Range(expectedAnchor, view.ShelfSlotCount - expectedAnchor)),
                "Only the compact shelf tail may be empty after generation or refresh.");
        }

        private static string LayoutSignature(BuqiBazaarSupplyViewSource source)
        {
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView view),
                "Supply layout is unavailable.");
            return LayoutSignature(view);
        }

        private static string LayoutSignature(BuqiBazaarSupplyView view)
        {
            return string.Join("|", view.Offers.Select(offer =>
                       $"{offer.OfferId}:{offer.AnchorSlot}:{offer.Size}:{offer.Purchased}")) +
                   ";empty=" + string.Join(",", view.EmptyShelfSlots) +
                   $";refresh={view.RefreshCount};cursor={view.SupplyRngCursor};generation={view.SupplyGeneration}";
        }

        private static BuqiBazaarSupplyContext Context(
            long seed,
            int day,
            int encounterIndex,
            int balance,
            params string[] ownedDefinitionIds)
        {
            return new BuqiBazaarSupplyContext
            {
                RunSeed = seed,
                Day = day,
                EncounterIndex = encounterIndex,
                Balance = balance,
                OwnedDefinitionIds = ownedDefinitionIds,
            };
        }

        internal static BuqiConfigCatalog CreateCatalog()
        {
            var catalog = new BuqiConfigCatalog
            {
                Global = new BuqiGlobalConfigRow
                {
                    ContentVersion = "bazaar-supply-contract-v1",
                    BoardSlotCount = BuqiRunRules.BoardSlotCount,
                },
            };
            string[] builds = { "fast", "buffer", "heal", "chain", "poison", "burn", "shared" };
            string[] roles = { "starter", "core", "bridge", "counter", "economy", "finisher" };
            int ordinal = 1;
            foreach (string build in builds)
            {
                foreach (string role in roles)
                {
                    string id = $"{build}-{ordinal++:00}";
                    var item = new BuqiItemConfigRow
                    {
                        DefinitionId = id,
                        DisplayName = id,
                        ArchetypeId = build,
                        Role = role,
                        Size = (BattleSize)(1 + (ordinal % 3)),
                        UnlockDay = role == "finisher" || role == "economy" ? 7 : role == "core" ? 4 : 1,
                        BasePrice = 2,
                        BaseCooldownTicks = 20 + ordinal,
                        Category = Game.Hot.BuqiItemCategory.NonWeapon,
                    };
                    item.Tags.Add(build);
                    item.Tags.Add(role);
                    catalog.Items.Add(item);
                }
            }

            AddMerchant(catalog, "merchant-edge", 1, 9, 110, "fast", "chain", "shared");
            AddMerchant(catalog, "merchant-bastion", 1, 9, 110, "buffer", "heal", "shared");
            AddMerchant(catalog, "merchant-spring", 1, 9, 110, "heal", "buffer", "shared");
            AddMerchant(catalog, "merchant-measure", 1, 9, 85, "fast", "buffer", "heal", "shared");
            AddMerchant(catalog, "merchant-gap", 4, 9, 80, "fast", "buffer", "heal", "poison");
            AddMerchant(catalog, "merchant-ledger", 4, 9, 70, "fast", "buffer", "heal", "shared");
            AddMerchant(catalog, "merchant-grades", 4, 9, 65, "fast", "buffer", "heal", "chain");
            AddMerchant(catalog, "merchant-summit", 7, 9, 55, "fast", "buffer", "heal", "burn");

            for (int index = 1; index <= 3; index++)
            {
                catalog.Refinements.Add(new BuqiRefinementConfigRow
                {
                    RefinementId = $"refinement-{index}",
                    DisplayName = $"Refinement {index}",
                    Summary = $"Refinement summary {index}",
                });
            }

            string[] opponentArchetypes =
            {
                "fast", "buffer", "chain", "heal", "poison", "burn", "freeze", "overload",
            };
            List<BuqiItemConfigRow> opponentItems = catalog.Items
                .Where(item => item.Size == BattleSize.S)
                .Take(2)
                .ToList();
            foreach (string archetype in opponentArchetypes)
            {
                catalog.Echoes.Add(Echo($"echo-{archetype}-lesson", archetype, opponentItems));
                catalog.Echoes.Add(Echo($"echo-{archetype}-early", archetype, opponentItems));
            }
            return catalog;
        }

        private static BuqiEchoConfigRow Echo(
            string echoId,
            string archetypeId,
            IReadOnlyList<BuqiItemConfigRow> items)
        {
            var snapshot = new BuqiBuildSnapshotConfigRow
            {
                SnapshotId = echoId + "-snapshot",
                ArchetypeId = archetypeId,
            };
            for (int index = 0; index < items.Count; index++)
            {
                snapshot.Items.Add(new BuqiItemInstanceConfigRow
                {
                    InstanceId = $"{echoId}-item-{index + 1}",
                    DefinitionId = items[index].DefinitionId,
                    AnchorSlot = index * 2,
                });
            }
            return new BuqiEchoConfigRow
            {
                EchoId = echoId,
                DisplayName = echoId,
                Build = archetypeId,
                Snapshot = snapshot,
            };
        }

        private static void AddMerchant(
            BuqiConfigCatalog catalog,
            string merchantId,
            int minDay,
            int maxDay,
            int weight,
            params string[] builds)
        {
            var merchant = new BuqiMerchantConfigRow
            {
                MerchantId = merchantId,
                DisplayName = merchantId,
                LocalizationKey = merchantId,
                MinDay = minDay,
                MaxDay = maxDay,
                Weight = weight,
            };
            merchant.PoolItemIds.AddRange(catalog.Items
                .Where(item => builds.Contains(item.ArchetypeId))
                .Select(item => item.DefinitionId));
            merchant.Slots.Add(Slot(merchantId + "-main", "Archetype", builds[0], "S+M+L", "Normal+Improved", builds[0], minDay, maxDay, 120, 2));
            merchant.Slots.Add(Slot(merchantId + "-bridge", "Bridge", string.Join("+", builds), "S+M+L", "Normal+Improved", "bridge", minDay, maxDay, 100, 1));
            merchant.Slots.Add(Slot(merchantId + "-counter", "Counter", string.Join("+", builds), "S+M+L", "Normal+Improved", "counter", minDay, maxDay, 90, 1));
            merchant.Slots.Add(Slot(merchantId + "-wild", "Quality", string.Join("+", builds), "S+M+L", "Normal+Improved+Fixed", builds[0], minDay, maxDay, 80, 1));
            catalog.Merchants.Add(merchant);
        }

        private static BuqiMerchantSlotConfigRow Slot(
            string id,
            string kind,
            string builds,
            string sizes,
            string qualities,
            string tag,
            int minDay,
            int maxDay,
            int weight,
            int count)
        {
            return new BuqiMerchantSlotConfigRow
            {
                SlotId = id,
                SlotKind = kind,
                BuildFilter = builds,
                SizeFilter = sizes,
                QualityFilter = qualities,
                RequiredTag = tag,
                MinUnlockDay = minDay,
                MaxUnlockDay = maxDay,
                Weight = weight,
                Count = count,
            };
        }

        private static void Run(string name, Action contract, List<string> failures)
        {
            try
            {
                contract();
            }
            catch (Exception exception)
            {
                failures.Add($"{name}: {exception.Message}");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
