using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.Run.Integration;
using Game.Hot.Buqi.Run.Settlement;

namespace Game.Hot.Buqi.Tests
{
    public static class BuqiBazaarSupplyRunIntegrationTestSuite
    {
        private const int ContractCount = 3;

        public static int Main()
        {
            List<string> failures = RunAll();
            foreach (string failure in failures)
                Console.Error.WriteLine(failure);
            Console.WriteLine($"bazaar-run-contracts={ContractCount - failures.Count}/{ContractCount}");
            return failures.Count == 0 ? 0 : 1;
        }

        public static List<string> RunAll()
        {
            var failures = new List<string>();
            Run("run-refresh-purchase-contract", RunRefreshPurchaseContract, failures);
            Run("restart-reset-contract", RestartResetContract, failures);
            Run("persistence-rollback-contract", PersistenceRollbackContract, failures);
            return failures;
        }

        private static void RunRefreshPurchaseContract()
        {
            BuqiConfigCatalog sourceCatalog = BuqiBazaarSupplyViewSourceTestSuite.CreateCatalog();
            Require(BuqiUIDemoCatalog.TryCreate(
                sourceCatalog, out BuqiUIDemoCatalog catalog, out string error), error);
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                sourceCatalog, out BuqiBazaarSupplyViewSource supply, out error), error);
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(catalog, supply, store);
            EnterOperationChoice(controller);

            Require(Execute(controller, BuqiUIDemoCommandType.SelectOperation, "bazaar").Accepted,
                "The bazaar operation must open.");
            Require(supply.TryGetCurrentSupply(out BuqiBazaarSupplyView opened),
                "Opening the bazaar must initialize the production supply source.");
            string[] openingOffers = controller.View.ShopOffers.Select(offer => offer.Id).ToArray();
            Require(openingOffers.SequenceEqual(opened.OfferIds),
                "Demo offers must come from the constrained merchant shelf.");

            int openingCoins = controller.View.Coins;
            BuqiUIDemoCommandResult refresh = Execute(
                controller, BuqiUIDemoCommandType.RefreshShop, string.Empty);
            Require(refresh.Accepted, refresh.Reason);
            Require(controller.View.Coins == openingCoins - 2,
                "The first production refresh must deduct two coins.");
            Require(supply.TryGetCurrentSupply(out BuqiBazaarSupplyView refreshed),
                "Refreshed supply metadata must remain visible.");
            Require(refreshed.RefreshCount == 1 && refreshed.Balance == controller.View.Coins,
                "Refresh count and authoritative balance must stay synchronized.");
            string[] refreshedOffers = controller.View.ShopOffers.Select(offer => offer.Id).ToArray();
            Require(refreshedOffers.SequenceEqual(refreshed.OfferIds) &&
                    !refreshedOffers.SequenceEqual(openingOffers),
                "A refresh must replace the Demo shelf with the runtime shelf.");

            string purchasedId = controller.View.ShopOffers
                .First(offer => offer.Price <= controller.View.Coins)
                .Id;
            BuqiUIDemoCommandResult purchase = Execute(
                controller, BuqiUIDemoCommandType.BuyOffer, purchasedId);
            Require(purchase.Accepted, purchase.Reason);
            Require(supply.TryGetCurrentSupply(out BuqiBazaarSupplyView purchased),
                "Purchase metadata must remain visible.");
            Require(purchased.PurchasedOfferIds.Contains(purchasedId) &&
                    purchased.Balance == controller.View.Coins,
                "Purchase sold-state and balance must flow back into the supply view.");

            Require(BuqiBazaarSupplyViewSource.TryCreate(
                sourceCatalog, out BuqiBazaarSupplyViewSource restoredSupply, out error), error);
            BuqiUIDemoController restoredController = CreateController(catalog, restoredSupply, store);
            Require(restoredSupply.TryGetCurrentSupply(out BuqiBazaarSupplyView restored),
                "Loading an active bazaar save must restore the production supply view.");
            Require(restoredController.View.ShopOffers.Select(offer => offer.Id).SequenceEqual(restored.OfferIds) &&
                    restored.PurchasedOfferIds.Contains(purchasedId) && restored.RefreshCount == 1,
                "A new ViewSource must replay refresh and purchase state from the saved encounter.");
        }

        private static void RestartResetContract()
        {
            BuqiConfigCatalog sourceCatalog = BuqiBazaarSupplyViewSourceTestSuite.CreateCatalog();
            Require(BuqiUIDemoCatalog.TryCreate(
                sourceCatalog, out BuqiUIDemoCatalog catalog, out string error), error);
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                sourceCatalog, out BuqiBazaarSupplyViewSource supply, out error), error);
            BuqiUIDemoController controller = CreateController(catalog, supply, new MemoryRunStore());
            EnterOperationChoice(controller);

            Require(Execute(controller, BuqiUIDemoCommandType.SelectOperation, "bazaar").Accepted,
                "The first bazaar must open.");
            Require(Execute(controller, BuqiUIDemoCommandType.RefreshShop, string.Empty).Accepted,
                "The first bazaar must refresh.");
            Require(Execute(controller, BuqiUIDemoCommandType.Restart, string.Empty).Accepted,
                "Restart must succeed.");
            EnterOperationChoice(controller);
            Require(Execute(controller, BuqiUIDemoCommandType.SelectOperation, "bazaar").Accepted,
                "The restarted bazaar must open.");
            Require(supply.TryGetCurrentSupply(out BuqiBazaarSupplyView restarted),
                "The restarted bazaar must expose supply metadata.");
            Require(restarted.RefreshCount == 0,
                "Restarting the same deterministic run must not reuse the previous refresh cursor.");
        }

        private static void PersistenceRollbackContract()
        {
            BuqiConfigCatalog sourceCatalog = BuqiBazaarSupplyViewSourceTestSuite.CreateCatalog();
            Require(BuqiUIDemoCatalog.TryCreate(
                sourceCatalog, out BuqiUIDemoCatalog catalog, out string error), error);
            Require(BuqiBazaarSupplyViewSource.TryCreate(
                sourceCatalog, out BuqiBazaarSupplyViewSource supply, out error), error);
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(catalog, supply, store);
            EnterOperationChoice(controller);
            Require(Execute(controller, BuqiUIDemoCommandType.SelectOperation, "bazaar").Accepted,
                "The rollback bazaar must open.");
            Require(supply.TryGetCurrentSupply(out BuqiBazaarSupplyView before),
                "The rollback contract requires an open supply view.");

            store.RejectWrites = true;
            BuqiUIDemoCommandResult refresh = Execute(
                controller, BuqiUIDemoCommandType.RefreshShop, string.Empty);
            Require(!refresh.Accepted, "A refresh must fail when the save store rejects the commit.");
            Require(supply.TryGetCurrentSupply(out BuqiBazaarSupplyView afterRefreshFailure),
                "The supply view must survive a failed refresh commit.");
            Require(afterRefreshFailure.RefreshCount == before.RefreshCount &&
                    afterRefreshFailure.Balance == before.Balance &&
                    afterRefreshFailure.OfferIds.SequenceEqual(before.OfferIds),
                "A failed refresh commit must roll back shelf, cursor, and balance.");

            string offerId = controller.View.ShopOffers.First().Id;
            BuqiUIDemoCommandResult purchase = Execute(
                controller, BuqiUIDemoCommandType.BuyOffer, offerId);
            Require(!purchase.Accepted, "A purchase must fail when the save store rejects the commit.");
            Require(supply.TryGetCurrentSupply(out BuqiBazaarSupplyView afterPurchaseFailure),
                "The supply view must survive a failed purchase commit.");
            Require(afterPurchaseFailure.Balance == before.Balance &&
                    !afterPurchaseFailure.PurchasedOfferIds.Contains(offerId),
                "A failed purchase commit must roll back balance and sold-state.");
        }

        private static BuqiUIDemoController CreateController(
            BuqiUIDemoCatalog catalog,
            IBuqiBazaarSupplyRuntime supply,
            IBuqiRunStore store)
        {
            var options = new BuqiUIDemoControllerOptions
            {
                RunSeed = 1949,
                Store = store,
                BazaarSupply = supply,
            };
            Require(BuqiUIDemoController.TryCreate(
                catalog, options, out BuqiUIDemoController controller, out string error), error);
            return controller;
        }

        private static BuqiUIDemoCommandResult Execute(
            BuqiUIDemoController controller,
            BuqiUIDemoCommandType type,
            string primaryId)
        {
            return controller.Execute(new BuqiUIDemoCommand
            {
                Type = type,
                PrimaryId = primaryId,
            });
        }

        private static void EnterOperationChoice(BuqiUIDemoController controller)
        {
            if (controller.View.Phase == BuqiUIDemoPhase.OperationChoice)
                return;
            Require(Execute(controller, BuqiUIDemoCommandType.NextPhase, string.Empty).Accepted,
                "The operation period must begin before choosing a destination.");
            Require(controller.View.Phase == BuqiUIDemoPhase.OperationChoice,
                "The operation period must expose operation choices.");
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

        private sealed class MemoryRunStore : IBuqiRunStore
        {
            private string m_Json;
            public bool RejectWrites { get; set; }

            public bool TryRead(out string json, out string error)
            {
                json = m_Json ?? string.Empty;
                error = m_Json == null ? "Save file does not exist." : string.Empty;
                return m_Json != null;
            }

            public bool TryWrite(string json, out string error)
            {
                if (RejectWrites)
                {
                    error = "Injected persistence failure.";
                    return false;
                }
                m_Json = json;
                error = string.Empty;
                return true;
            }

            public bool TryDelete(out string error)
            {
                m_Json = null;
                error = string.Empty;
                return true;
            }
        }
    }
}
