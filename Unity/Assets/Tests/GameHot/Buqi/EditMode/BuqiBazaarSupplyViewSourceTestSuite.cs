using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.Tests
{
    public static class BuqiBazaarSupplyViewSourceTestSuite
    {
        private const int ContractCount = 6;

        public static List<string> RunAll()
        {
            var failures = new List<string>();
            Run("catalog-contract", CatalogContract, failures);
            Run("constrained-shelf-contract", ConstrainedShelfContract, failures);
            Run("preference-contract", PreferenceContract, failures);
            Run("refresh-contract", RefreshContract, failures);
            Run("purchase-view-contract", PurchaseViewContract, failures);
            Run("restore-rollback-contract", RestoreRollbackContract, failures);
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
            Require(offers.Count == 4 && offers.Distinct(StringComparer.Ordinal).Count() == 4,
                "A merchant shelf must contain four distinct offers.");
            Require(source.TryGetCurrentSupply(out BuqiBazaarSupplyView view),
                "The opened shelf must be visible to the Demo view source.");
            BuqiMerchantConfigRow merchant = catalog.Merchants.Single(row => row.MerchantId == view.MerchantId);
            Require(offers.All(merchant.PoolItemIds.Contains),
                "Merchant offers must stay inside the configured constrained pool.");
            Require(view.OfferRoles.Count == 4 && offers.All(id => view.OfferRoles.ContainsKey(id)),
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

        private static BuqiConfigCatalog CreateCatalog()
        {
            var catalog = new BuqiConfigCatalog();
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
                        Size = (Game.Hot.Buqi.Battle.BuqiSize)(1 + (ordinal % 3)),
                        UnlockDay = role == "finisher" || role == "economy" ? 7 : role == "core" ? 4 : 1,
                        BasePrice = 2,
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
            return catalog;
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
