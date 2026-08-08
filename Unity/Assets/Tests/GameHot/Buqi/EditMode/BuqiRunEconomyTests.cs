using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Config;
using NUnit.Framework;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunEconomyTests
    {
        [Test]
        public void CreateInitialUsesCoreEightSlotStorageAndNoSharedCollections()
        {
            BuqiRunEconomySnapshot snapshot = BuqiRunEconomySnapshot.CreateInitial(700);
            BuqiRunEconomySnapshot clone = snapshot.Clone();

            Assert.That(snapshot.Run.StorageInstanceIds, Has.Count.EqualTo(8));
            Assert.That(snapshot.Items, Is.Empty);
            clone.Run.StorageInstanceIds[0] = "changed";
            Assert.That(snapshot.Run.StorageInstanceIds[0], Is.Empty);
        }

        [Test]
        public void ItemCopiesHaveStableUniqueInstanceIds()
        {
            BuqiRunEconomySnapshot snapshot = BuqiRunEconomySnapshot.CreateInitial(701);
            string first = snapshot.CreateInstanceId();
            string second = snapshot.CreateInstanceId();

            Assert.That(first, Is.EqualTo("run-701-item-1"));
            Assert.That(second, Is.EqualTo("run-701-item-2"));
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void CreateInitialPassesOptionalContentVersionToCoreState()
        {
            BuqiRunEconomySnapshot snapshot = BuqiRunEconomySnapshot.CreateInitial(702, "content-v2");

            Assert.That(snapshot.Run.ContentVersion, Is.EqualTo("content-v2"));
        }

        [Test]
        public void PurchaseDeductsCoinsAndAddsUniqueInstanceToFirstStorageSlot()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(800);
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedInstanceId, Is.EqualTo("run-800-item-1"));
            Assert.That(result.Snapshot.Run.Coins, Is.EqualTo(8));
            Assert.That(result.Snapshot.Run.StorageInstanceIds[0], Is.EqualTo("run-800-item-1"));
            Assert.That(result.Snapshot.Items["run-800-item-1"].DefinitionId, Is.EqualTo("blade"));
            Assert.That(result.Snapshot.Items["run-800-item-1"].Quality, Is.EqualTo(BuqiRunItemQuality.Common));
            Assert.That(state.Run.Coins, Is.EqualTo(12));
            Assert.That(state.Run.StorageInstanceIds[0], Is.Empty);
            Assert.That(state.Items, Is.Empty);
        }

        [Test]
        public void PurchasePrefersLowestSlotMatchingInstanceAndPreservesItsIdentity()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(803);
            PutInStorage(state, 3, "late-blade", "blade", BuqiRunItemQuality.Common);
            PutInStorage(state, 1, "early-blade", "blade", BuqiRunItemQuality.Common);
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedInstanceId, Is.EqualTo("early-blade"));
            Assert.That(result.Snapshot.Items["early-blade"].Quality, Is.EqualTo(BuqiRunItemQuality.Improved));
            Assert.That(result.Snapshot.Run.StorageInstanceIds[1], Is.EqualTo("early-blade"));
            Assert.That(result.Snapshot.Items.ContainsKey("run-803-item-1"), Is.False);
            Assert.That(result.Snapshot.Run.StorageInstanceIds[3], Is.EqualTo("late-blade"));
        }

        [Test]
        public void FullStorageAllowsPurchaseOnlyWhenNewCopyImmediatelyMerges()
        {
            BuqiRunEconomySnapshot state = FilledStorageWithCommonBlade(801);
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedInstanceId, Is.EqualTo("storage-blade"));
            Assert.That(result.Snapshot.Items[result.AffectedInstanceId].Quality,
                Is.EqualTo(BuqiRunItemQuality.Improved));
            Assert.That(result.Snapshot.Run.StorageInstanceIds, Has.Count.EqualTo(8));
            Assert.That(result.Snapshot.Items, Has.Count.EqualTo(8));
        }

        [Test]
        public void PurchaseDoesNotMergeIntoHigherQualityCopy()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(806);
            PutInStorage(state, 0, "improved-blade", "blade", BuqiRunItemQuality.Improved);
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedInstanceId, Is.EqualTo("run-806-item-1"));
            Assert.That(result.Snapshot.Items["improved-blade"].Quality, Is.EqualTo(BuqiRunItemQuality.Improved));
            Assert.That(result.Snapshot.Run.StorageInstanceIds[1], Is.EqualTo("run-806-item-1"));
            Assert.That(result.Snapshot.Items["run-806-item-1"].Quality, Is.EqualTo(BuqiRunItemQuality.Common));
        }

        [Test]
        public void RejectedPurchaseNeverChangesCoinsOrInventory()
        {
            BuqiRunEconomySnapshot state = FilledStorageWithoutMerge(802);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void PurchaseRejectsUnknownDefinitionsWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(804);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "unknown");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void PurchaseRejectsInsufficientCoinsWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(805);
            state.Run.Coins = 3;
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void PurchaseRejectsCatalogEntriesThatResolveToNullDefinitionWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(807);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.ReturningNull("blade"));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void PurchaseRejectsNegativeBuyPriceWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(808);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.WithPrices("blade", 1, -4, 2, 4, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void PurchaseRejectsNonPositiveSizeWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(809);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.WithPrices("blade", 0, 4, 2, 4, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void PurchaseRejectsUndefinedExistingQualityWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(810);
            PutInStorage(state, 0, "broken-blade", "blade", (BuqiRunItemQuality)99);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void PurchaseSkipsOccupiedInstanceIdsWhenOrdinalFallsBehind()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(811);
            PutInStorage(state, 0, "run-811-item-1", "shield", BuqiRunItemQuality.Common);
            PutInStorage(state, 1, "run-811-item-2", "orb", BuqiRunItemQuality.Common);
            state.NextItemOrdinal = 1;
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Purchase(state, "blade");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedInstanceId, Is.EqualTo("run-811-item-3"));
            Assert.That(result.Snapshot.Run.StorageInstanceIds[2], Is.EqualTo("run-811-item-3"));
            Assert.That(result.Snapshot.Items.ContainsKey("run-811-item-1"), Is.True);
            Assert.That(result.Snapshot.Items.ContainsKey("run-811-item-2"), Is.True);
        }

        [Test]
        public void ServiceRejectsUnsupportedSizeOutsideBoardRangeWithoutMutatingState()
        {
            BuqiRunEconomySnapshot purchaseState = BuqiRunEconomySnapshot.CreateInitial(812);
            BuqiRunEconomySnapshot purchaseExpected = purchaseState.Clone();
            BuqiRunEconomySnapshot instanceState = BuqiRunEconomySnapshot.CreateInitial(813);
            PutInStorage(instanceState, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot instanceExpected = instanceState.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.WithPrices("blade", 4, 4, 2, 4, 4));

            BuqiRunEconomyResult purchase = service.Purchase(purchaseState, "blade");
            BuqiRunEconomyResult sell = service.Sell(instanceState, "storage-blade");
            BuqiRunEconomyResult upgrade = service.Upgrade(instanceState, "storage-blade");
            BuqiRunEconomyResult refine = service.Refine(instanceState, "storage-blade", "A-01");

            Assert.That(purchase.Success, Is.False);
            Assert.That(sell.Success, Is.False);
            Assert.That(upgrade.Success, Is.False);
            Assert.That(refine.Success, Is.False);
            AssertSnapshotsEqual(purchase.Snapshot, purchaseExpected);
            AssertSnapshotsEqual(purchaseState, purchaseExpected);
            AssertSnapshotsEqual(sell.Snapshot, instanceExpected);
            AssertSnapshotsEqual(upgrade.Snapshot, instanceExpected);
            AssertSnapshotsEqual(refine.Snapshot, instanceExpected);
            AssertSnapshotsEqual(instanceState, instanceExpected);
        }

        [Test]
        public void SellRemovesExactInstanceFromItemsAndBoardAndAddsConfiguredPrice()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(900);
            state.Run.Coins = 5;
            PutOnBoard(state, 0, "board-blade", "blade", BuqiRunItemQuality.Common);
            state.Run.BoardInstanceIds[3] = "board-blade";
            PutInStorage(state, 4, "storage-blade", "blade", BuqiRunItemQuality.Common);
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Sell(state, "board-blade");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedInstanceId, Is.EqualTo("board-blade"));
            Assert.That(result.Snapshot.Run.Coins, Is.EqualTo(7));
            Assert.That(result.Snapshot.Items.ContainsKey("board-blade"), Is.False);
            Assert.That(result.Snapshot.Run.BoardInstanceIds[0], Is.Empty);
            Assert.That(result.Snapshot.Run.BoardInstanceIds[3], Is.Empty);
            Assert.That(result.Snapshot.Run.StorageInstanceIds[4], Is.EqualTo("storage-blade"));
        }

        [Test]
        public void SellRejectsUnknownInstanceWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(901);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Sell(state, "missing");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void SellRejectsCatalogEntriesThatResolveToNullDefinitionWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(908);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.ReturningNull("blade"));

            BuqiRunEconomyResult result = service.Sell(state, "storage-blade");

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void SellRejectsNegativeSellPriceWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(909);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.WithPrices("blade", 1, 4, -2, 4, 4));

            BuqiRunEconomyResult result = service.Sell(state, "storage-blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void SellRejectsUndefinedItemQualityWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(910);
            PutInStorage(state, 0, "storage-blade", "blade", (BuqiRunItemQuality)99);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Sell(state, "storage-blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void UpgradeDeductsConfiguredPriceAndAdvancesOneQualityTier()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(902);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Upgrade(state, "storage-blade");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedInstanceId, Is.EqualTo("storage-blade"));
            Assert.That(result.Snapshot.Run.Coins, Is.EqualTo(8));
            Assert.That(result.Snapshot.Items["storage-blade"].Quality, Is.EqualTo(BuqiRunItemQuality.Improved));
            Assert.That(result.Snapshot.Run.StorageInstanceIds[0], Is.EqualTo("storage-blade"));
        }

        [Test]
        public void UpgradeRejectsFinalizedItemsWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(903);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Finalized);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Upgrade(state, "storage-blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void UpgradeRejectsInsufficientCoinsWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(904);
            state.Run.Coins = 3;
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Upgrade(state, "storage-blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void UpgradeRejectsCatalogEntriesThatResolveToNullDefinitionWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(911);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.ReturningNull("blade"));

            BuqiRunEconomyResult result = service.Upgrade(state, "storage-blade");

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void UpgradeRejectsNegativeUpgradePriceWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(912);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.WithPrices("blade", 1, 4, 2, -4, 4));

            BuqiRunEconomyResult result = service.Upgrade(state, "storage-blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void UpgradeRejectsUndefinedItemQualityWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(913);
            PutInStorage(state, 0, "storage-blade", "blade", (BuqiRunItemQuality)99);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Upgrade(state, "storage-blade");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void RefineDeductsConfiguredPriceAndStoresRequestedRefinementId()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(905);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Refine(state, "storage-blade", "A-01");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AffectedInstanceId, Is.EqualTo("storage-blade"));
            Assert.That(result.Snapshot.Run.Coins, Is.EqualTo(8));
            Assert.That(result.Snapshot.Items["storage-blade"].RefinementId, Is.EqualTo("A-01"));
            Assert.That(result.Snapshot.Run.StorageInstanceIds[0], Is.EqualTo("storage-blade"));
        }

        [Test]
        public void RefineRejectsSecondRefinementWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(906);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common, "A-01");
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Refine(state, "storage-blade", "A-02");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void RefineRejectsEmptyRefinementIdsWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(907);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Refine(state, "storage-blade", " ");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void RefineRejectsCatalogEntriesThatResolveToNullDefinitionWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(914);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.ReturningNull("blade"));

            BuqiRunEconomyResult result = service.Refine(state, "storage-blade", "A-01");

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.Not.Empty);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void RefineRejectsNegativeRefinementPriceWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(915);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.WithPrices("blade", 1, 4, 2, 4, -4));

            BuqiRunEconomyResult result = service.Refine(state, "storage-blade", "A-01");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void RefineRejectsUndefinedItemQualityWithoutMutatingState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(916);
            PutInStorage(state, 0, "storage-blade", "blade", (BuqiRunItemQuality)99);
            BuqiRunEconomySnapshot expected = state.Clone();
            var service = new BuqiRunEconomyService(TestCatalog.With("blade", 1, 4));

            BuqiRunEconomyResult result = service.Refine(state, "storage-blade", "A-01");

            Assert.That(result.Success, Is.False);
            AssertSnapshotsEqual(result.Snapshot, expected);
            AssertSnapshotsEqual(state, expected);
        }

        [Test]
        public void CatalogAdapterMapsBasePriceSizeAndDerivedFallbackPrices()
        {
            BuqiConfigCatalog catalog = CreateConfigCatalog(
                CreateConfigItem("blade", BattleSize.M, 5),
                CreateConfigItem("seed", BattleSize.S, 1));
            var adapter = new BuqiRunItemCatalogAdapter(catalog);

            bool foundBlade = adapter.TryGet("blade", out BuqiRunItemDefinition blade);
            bool foundSeed = adapter.TryGet("seed", out BuqiRunItemDefinition seed);

            Assert.That(foundBlade, Is.True);
            Assert.That(blade.DefinitionId, Is.EqualTo("blade"));
            Assert.That(blade.Size, Is.EqualTo(2));
            Assert.That(blade.BuyPrice, Is.EqualTo(5));
            Assert.That(blade.SellPrice, Is.EqualTo(2));
            Assert.That(blade.UpgradePrice, Is.EqualTo(5));
            Assert.That(blade.RefinementPrice, Is.EqualTo(5));

            Assert.That(foundSeed, Is.True);
            Assert.That(seed.SellPrice, Is.EqualTo(1));
            Assert.That(seed.UpgradePrice, Is.EqualTo(1));
            Assert.That(seed.RefinementPrice, Is.EqualTo(1));

            blade.BuyPrice = 99;
            Assert.That(adapter.TryGet("blade", out BuqiRunItemDefinition copiedBlade), Is.True);
            Assert.That(copiedBlade.BuyPrice, Is.EqualTo(5));
        }

        [Test]
        public void CatalogAdapterRejectsDuplicateDefinitionIdsExplicitly()
        {
            BuqiConfigCatalog catalog = CreateConfigCatalog(
                CreateConfigItem("blade", BattleSize.S, 4),
                CreateConfigItem("blade", BattleSize.M, 6));

            Assert.That(
                () => new BuqiRunItemCatalogAdapter(catalog),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void CatalogAdapterReturnsFalseForMissingDefinitions()
        {
            var adapter = new BuqiRunItemCatalogAdapter(CreateConfigCatalog(CreateConfigItem("blade", BattleSize.S, 4)));

            bool found = adapter.TryGet("missing", out BuqiRunItemDefinition definition);

            Assert.That(found, Is.False);
            Assert.That(definition, Is.Null);
        }

        [TestCase((BattleSize)0, 4)]
        [TestCase((BattleSize)99, 4)]
        [TestCase(BattleSize.S, -1)]
        public void CatalogAdapterRejectsInvalidSizeOrNegativeBasePriceExplicitly(BattleSize size, int basePrice)
        {
            BuqiConfigCatalog catalog = CreateConfigCatalog(CreateConfigItem("blade", size, basePrice));

            Assert.That(
                () => new BuqiRunItemCatalogAdapter(catalog),
                Throws.TypeOf<ArgumentException>());
        }

        private static BuqiRunEconomySnapshot FilledStorageWithCommonBlade(long seed)
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(seed);
            PutInStorage(state, 0, "storage-blade", "blade", BuqiRunItemQuality.Common);
            PutInStorage(state, 1, "storage-filler-1", "shield", BuqiRunItemQuality.Common);
            PutInStorage(state, 2, "storage-filler-2", "orb", BuqiRunItemQuality.Common);
            PutInStorage(state, 3, "storage-filler-3", "hammer", BuqiRunItemQuality.Common);
            PutInStorage(state, 4, "storage-filler-4", "helm", BuqiRunItemQuality.Common);
            PutInStorage(state, 5, "storage-filler-5", "ring", BuqiRunItemQuality.Common);
            PutInStorage(state, 6, "storage-filler-6", "boots", BuqiRunItemQuality.Common);
            PutInStorage(state, 7, "storage-filler-7", "cloak", BuqiRunItemQuality.Common);
            return state;
        }

        private static BuqiRunEconomySnapshot FilledStorageWithoutMerge(long seed)
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(seed);
            PutInStorage(state, 0, "storage-filler-0", "shield", BuqiRunItemQuality.Common);
            PutInStorage(state, 1, "storage-filler-1", "orb", BuqiRunItemQuality.Common);
            PutInStorage(state, 2, "storage-filler-2", "hammer", BuqiRunItemQuality.Common);
            PutInStorage(state, 3, "storage-filler-3", "helm", BuqiRunItemQuality.Common);
            PutInStorage(state, 4, "storage-filler-4", "ring", BuqiRunItemQuality.Common);
            PutInStorage(state, 5, "storage-filler-5", "boots", BuqiRunItemQuality.Common);
            PutInStorage(state, 6, "storage-filler-6", "cloak", BuqiRunItemQuality.Common);
            PutInStorage(state, 7, "storage-filler-7", "amulet", BuqiRunItemQuality.Common);
            return state;
        }

        private static void PutInStorage(
            BuqiRunEconomySnapshot state,
            int slotIndex,
            string instanceId,
            string definitionId,
            BuqiRunItemQuality quality,
            string refinementId = "")
        {
            state.Run.StorageInstanceIds[slotIndex] = instanceId;
            state.Items[instanceId] = new BuqiRunItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                Quality = quality,
                RefinementId = refinementId,
            };
        }

        private static BuqiConfigCatalog CreateConfigCatalog(params BuqiItemConfigRow[] items)
        {
            var catalog = new BuqiConfigCatalog();
            catalog.Items.AddRange(items);
            return catalog;
        }

        private static BuqiItemConfigRow CreateConfigItem(string definitionId, BattleSize size, int basePrice)
        {
            return new BuqiItemConfigRow
            {
                DefinitionId = definitionId,
                DisplayName = definitionId,
                Size = size,
                BasePrice = basePrice,
            };
        }

        private static void PutOnBoard(
            BuqiRunEconomySnapshot state,
            int slotIndex,
            string instanceId,
            string definitionId,
            BuqiRunItemQuality quality,
            string refinementId = "")
        {
            state.Run.BoardInstanceIds[slotIndex] = instanceId;
            state.Items[instanceId] = new BuqiRunItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                Quality = quality,
                RefinementId = refinementId,
            };
        }

        private static void AssertSnapshotsEqual(BuqiRunEconomySnapshot actual, BuqiRunEconomySnapshot expected)
        {
            Assert.That(actual.Run.RunSeed, Is.EqualTo(expected.Run.RunSeed));
            Assert.That(actual.Run.Day, Is.EqualTo(expected.Run.Day));
            Assert.That(actual.Run.EncounterIndex, Is.EqualTo(expected.Run.EncounterIndex));
            Assert.That(actual.Run.Phase, Is.EqualTo(expected.Run.Phase));
            Assert.That(actual.Run.Outcome, Is.EqualTo(expected.Run.Outcome));
            Assert.That(actual.Run.Coins, Is.EqualTo(expected.Run.Coins));
            Assert.That(actual.Run.Wins, Is.EqualTo(expected.Run.Wins));
            Assert.That(actual.Run.Lives, Is.EqualTo(expected.Run.Lives));
            Assert.That(actual.Run.BoardInstanceIds, Is.EqualTo(expected.Run.BoardInstanceIds));
            Assert.That(actual.Run.StorageInstanceIds, Is.EqualTo(expected.Run.StorageInstanceIds));
            Assert.That(actual.NextItemOrdinal, Is.EqualTo(expected.NextItemOrdinal));
            Assert.That(actual.Items.Keys, Is.EquivalentTo(expected.Items.Keys));

            foreach (KeyValuePair<string, BuqiRunItemInstance> pair in expected.Items)
            {
                Assert.That(actual.Items.ContainsKey(pair.Key), Is.True, pair.Key);
                BuqiRunItemInstance actualItem = actual.Items[pair.Key];
                Assert.That(actualItem.InstanceId, Is.EqualTo(pair.Value.InstanceId), pair.Key);
                Assert.That(actualItem.DefinitionId, Is.EqualTo(pair.Value.DefinitionId), pair.Key);
                Assert.That(actualItem.Quality, Is.EqualTo(pair.Value.Quality), pair.Key);
                Assert.That(actualItem.RefinementId, Is.EqualTo(pair.Value.RefinementId), pair.Key);
            }
        }

        private sealed class TestCatalog : IBuqiRunItemCatalog
        {
            private readonly Dictionary<string, BuqiRunItemDefinition> m_Definitions;

            private TestCatalog(Dictionary<string, BuqiRunItemDefinition> definitions)
            {
                m_Definitions = definitions;
            }

            public static TestCatalog With(string definitionId, int size, int buyPrice)
            {
                return new TestCatalog(
                    new Dictionary<string, BuqiRunItemDefinition>(StringComparer.Ordinal)
                    {
                        [definitionId] = CreateDefinition(definitionId, size, buyPrice),
                    });
            }

            public static TestCatalog WithPrices(
                string definitionId,
                int size,
                int buyPrice,
                int sellPrice,
                int upgradePrice,
                int refinementPrice)
            {
                return new TestCatalog(
                    new Dictionary<string, BuqiRunItemDefinition>(StringComparer.Ordinal)
                    {
                        [definitionId] = new BuqiRunItemDefinition
                        {
                            DefinitionId = definitionId,
                            Size = size,
                            BuyPrice = buyPrice,
                            SellPrice = sellPrice,
                            UpgradePrice = upgradePrice,
                            RefinementPrice = refinementPrice,
                        },
                    });
            }

            public static TestCatalog ReturningNull(string definitionId)
            {
                return new TestCatalog(new Dictionary<string, BuqiRunItemDefinition>(StringComparer.Ordinal), definitionId);
            }

            public bool TryGet(string definitionId, out BuqiRunItemDefinition definition)
            {
                if (definitionId == m_NullDefinitionId)
                {
                    definition = null!;
                    return true;
                }

                if (m_Definitions.TryGetValue(definitionId, out BuqiRunItemDefinition? existing)
                    && existing != null)
                {
                    definition = CloneDefinition(existing);
                    return true;
                }

                definition = null!;
                return false;
            }

            private readonly string m_NullDefinitionId = string.Empty;

            private static BuqiRunItemDefinition CreateDefinition(string definitionId, int size, int buyPrice)
            {
                return new BuqiRunItemDefinition
                {
                    DefinitionId = definitionId,
                    Size = size,
                    BuyPrice = buyPrice,
                    SellPrice = Math.Max(1, buyPrice / 2),
                    UpgradePrice = Math.Max(1, buyPrice),
                    RefinementPrice = Math.Max(1, buyPrice),
                };
            }

            private TestCatalog(Dictionary<string, BuqiRunItemDefinition> definitions, string nullDefinitionId)
            {
                m_Definitions = definitions;
                m_NullDefinitionId = nullDefinitionId;
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
}
