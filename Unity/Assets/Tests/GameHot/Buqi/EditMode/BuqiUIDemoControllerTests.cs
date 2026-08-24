using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.Run.Settlement;
using NUnit.Framework;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiUIDemoControllerTests
    {
        [Test]
        public void RestartDispatch_UsesSameCommandForErrorButtonAndTerminalShortcut()
        {
            int dispatchCount = 0;
            Action restart = () => dispatchCount++;

            Assert.That(
                BuqiRestartPolicy.TryDispatch(true, BuqiUIDemoPhase.OperationChoice, restart),
                Is.True);
            Assert.That(
                BuqiRestartPolicy.TryDispatch(false, BuqiUIDemoPhase.RunTerminal, restart),
                Is.True);

            Assert.That(dispatchCount, Is.EqualTo(2));
        }

        [Test]
        public void RestartDispatch_IgnoresShortcutDuringNormalOperation()
        {
            int dispatchCount = 0;

            Assert.That(
                BuqiRestartPolicy.TryDispatch(false, BuqiUIDemoPhase.OperationChoice, () => dispatchCount++),
                Is.False);

            Assert.That(dispatchCount, Is.EqualTo(0));
        }

        [Test]
        public void Create_StartsInOperationChoiceWithoutLegacyTopLevelPhases()
        {
            BuqiUIDemoController controller = CreateController(new MemoryRunStore());

            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.OperationChoice));
            Assert.That(controller.View.Choices.Count, Is.EqualTo(3));
            Assert.That(controller.View.Phase, Is.Not.EqualTo(BuqiUIDemoPhase.StarterSelection));
            Assert.That(controller.View.Phase, Is.Not.EqualTo(BuqiUIDemoPhase.OpponentIntel));
            Assert.That(controller.View.Phase, Is.Not.EqualTo(BuqiUIDemoPhase.Prediction));
            Assert.That(controller.View.BoardSlots.Count, Is.EqualTo(8));
            Assert.That(controller.View.StorageSlots.Count, Is.EqualTo(8));
        }

        [Test]
        public void OpenDragDeploy_IsAcceptedDuringOperationAndRejectedDuringBattleReplay()
        {
            BuqiUIDemoController controller = CreateController(new MemoryRunStore());

            BuqiUIDemoCommandResult accepted = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.OpenDragDeploy,
            });

            Assert.That(accepted.Accepted, Is.True, accepted.Reason);

            AdvanceUntil(controller, BuqiUIDemoPhase.BattleReplay);
            BuqiUIDemoCommandResult rejected = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.OpenDragDeploy,
            });

            Assert.That(rejected.Accepted, Is.False);
        }

        [Test]
        public void DeploymentAvailability_HasOnePolicyForEditableAndLockedPhases()
        {
            System.Reflection.MethodInfo policy = typeof(BuqiUIDemoController).GetMethod(
                "CanConfigureDeployment",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.That(policy, Is.Not.Null);

            Assert.That(CanConfigure(BuqiUIDemoPhase.OperationChoice), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.Shop), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.Event), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.Training), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.PveSelection), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.TribulationRoute), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.TribulationStage), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.BattleReplay), Is.False);
            Assert.That(CanConfigure(BuqiUIDemoPhase.BattleSummary), Is.False);
            Assert.That(CanConfigure(BuqiUIDemoPhase.RoundSettlement), Is.False);
            Assert.That(CanConfigure(BuqiUIDemoPhase.RunTerminal), Is.False);

            bool CanConfigure(BuqiUIDemoPhase phase)
            {
                return (bool)policy.Invoke(null, new object[] { phase });
            }
        }

        [Test]
        public void OpenDragDeploy_IsAvailableBeforeBattleAndLockedDuringPlaybackAndSettlement()
        {
            BuqiUIDemoController controller = CreateController(new MemoryRunStore());
            SelectOperation(controller, "meditate");
            SelectOperation(controller, "meditate");
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PveSelection));

            BuqiUIDemoCommandResult preparation = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.OpenDragDeploy,
            });
            Assert.That(preparation.Accepted, Is.True, preparation.Reason);

            BuqiUIDemoCommandResult selected = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectPveDifficulty,
                PrimaryId = controller.View.Choices[0].Id,
            });
            Assert.That(selected.Accepted, Is.True, selected.Reason);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleReplay));
            AssertDeploymentLocked(controller);

            BuqiUIDemoCommandResult settled = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.NextPhase,
            });
            Assert.That(settled.Accepted, Is.True, settled.Reason);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleSummary));
            AssertDeploymentLocked(controller);
        }

        [Test]
        public void Bazaar_AllowsMultiplePurchasesAndRefreshesOffersInventoryAndCoins()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            SelectOperation(controller, "bazaar");
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Shop));
            Assert.That(controller.View.ShopOffers.Count, Is.GreaterThanOrEqualTo(2));
            string firstOfferId = controller.View.ShopOffers[0].Id;
            string secondOfferId = controller.View.ShopOffers[1].Id;
            string openingItems = ItemFingerprint(controller.View);
            int openingCoins = controller.View.Coins;

            BuqiUIDemoCommandResult first = Buy(controller, firstOfferId);

            Assert.That(first.Accepted, Is.True, first.Reason);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Shop));
            Assert.That(controller.View.Coins, Is.LessThan(openingCoins));
            Assert.That(controller.View.ShopOffers.Single(offer => offer.Id == firstOfferId).Sold, Is.True);
            Assert.That(ItemFingerprint(controller.View), Is.Not.EqualTo(openingItems));
            int afterFirstCoins = controller.View.Coins;

            BuqiUIDemoCommandResult second = Buy(controller, secondOfferId);

            Assert.That(second.Accepted, Is.True, second.Reason);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Shop));
            Assert.That(controller.View.Coins, Is.LessThan(afterFirstCoins));
            Assert.That(controller.View.ShopOffers.Count(offer => offer.Sold), Is.EqualTo(2));

            int afterSecondCoins = controller.View.Coins;
            string afterSecondItems = ItemFingerprint(controller.View);
            BuqiUIDemoCommandResult duplicate = Buy(controller, firstOfferId);
            Assert.That(duplicate.Accepted, Is.False);
            Assert.That(controller.View.Coins, Is.EqualTo(afterSecondCoins));
            Assert.That(ItemFingerprint(controller.View), Is.EqualTo(afterSecondItems));

            controller = CreateController(store);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Shop));
            Assert.That(controller.View.Coins, Is.EqualTo(afterSecondCoins));
            Assert.That(controller.View.ShopOffers.Count(offer => offer.Sold), Is.EqualTo(2));
        }

        [Test]
        public void Bazaar_DragPurchasePlacesOfferOnRequestedBoardSlotAndPersistsOnce()
        {
            var store = new MemoryRunStore();
            var supply = new FakeBazaarSupplyRuntime();
            BuqiUIDemoController controller = CreateController(store, supply);
            SelectOperation(controller, "bazaar");
            int openingCoins = controller.View.Coins;
            string expectedName = controller.View.ShopOffers
                .Single(offer => offer.Id == "item-02")
                .Item.Name;

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.BuyOffer,
                PrimaryId = "item-02",
                Slot = 3,
            });

            Assert.That(result.Accepted, Is.True, result.Reason);
            BuqiDemoItemView placed = controller.View.BoardSlots[3];
            Assert.That(placed.Empty, Is.False);
            Assert.That(placed.Name, Is.EqualTo(expectedName));
            Assert.That(controller.View.StorageSlots.Any(item => item.Id == placed.Id), Is.False);
            Assert.That(controller.View.Coins, Is.EqualTo(openingCoins - 2));
            Assert.That(controller.View.ShopOffers.Single(offer => offer.Id == "item-02").Sold, Is.True);

            controller = CreateController(store, new FakeBazaarSupplyRuntime());
            Assert.That(controller.View.BoardSlots[3].Id, Is.EqualTo(placed.Id));
            Assert.That(controller.View.ShopOffers.Single(offer => offer.Id == "item-02").Sold, Is.True);
        }

        [Test]
        public void Bazaar_DragPurchaseRejectsOccupiedBoardTargetWithoutChargingOrSellingOffer()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store, new FakeBazaarSupplyRuntime());
            SelectOperation(controller, "bazaar");
            int openingCoins = controller.View.Coins;
            string openingItems = ItemFingerprint(controller.View);
            string openingSave = store.CurrentJson;

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.BuyOffer,
                PrimaryId = "item-03",
                Slot = 0,
            });

            Assert.That(result.Accepted, Is.False);
            Assert.That(controller.View.Coins, Is.EqualTo(openingCoins));
            Assert.That(ItemFingerprint(controller.View), Is.EqualTo(openingItems));
            Assert.That(controller.View.ShopOffers.Single(offer => offer.Id == "item-03").Sold, Is.False);
            Assert.That(store.CurrentJson, Is.EqualTo(openingSave));
        }

        [Test]
        public void BazaarSupplyRuntime_OpensPurchasesRefreshesAndRestoresAuthoritativeShelf()
        {
            var store = new MemoryRunStore();
            var supply = new FakeBazaarSupplyRuntime();
            BuqiUIDemoController controller = CreateController(store, supply);

            SelectOperation(controller, "bazaar");

            Assert.That(supply.OpenCount, Is.EqualTo(1));
            Assert.That(controller.View.ShopOffers.Select(offer => offer.Id),
                Is.EqualTo(supply.InitialOffers));
            int openingCoins = controller.View.Coins;

            BuqiUIDemoCommandResult purchased = Buy(controller, supply.InitialOffers[0]);
            Assert.That(purchased.Accepted, Is.True, purchased.Reason);
            Assert.That(supply.PurchasedOfferIds, Is.EqualTo(new[] { supply.InitialOffers[0] }));
            Assert.That(supply.Balance, Is.EqualTo(controller.View.Coins));

            BuqiUIDemoCommandResult refreshed = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.RefreshShop,
            });
            Assert.That(refreshed.Accepted, Is.True, refreshed.Reason);
            Assert.That(controller.View.Coins, Is.EqualTo(openingCoins - 2 - 2));
            Assert.That(controller.View.ShopOffers.Select(offer => offer.Id),
                Is.EqualTo(supply.RefreshedOffers));

            var restoredSupply = new FakeBazaarSupplyRuntime();
            controller = CreateController(store, restoredSupply);
            Assert.That(restoredSupply.RestoreCount, Is.EqualTo(1));
            Assert.That(controller.View.ShopOffers.Select(offer => offer.Id),
                Is.EqualTo(restoredSupply.RefreshedOffers));
            Assert.That(restoredSupply.Balance, Is.EqualTo(controller.View.Coins));
        }

        [Test]
        public void BazaarSupplyRuntime_DiscardsIncompatibleLegacyShopSave()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController legacy = CreateController(store);
            SelectOperation(legacy, "bazaar");

            var productionSupply = new FakeBazaarSupplyRuntime { RejectRestore = true };
            BuqiUIDemoController recovered = CreateController(store, productionSupply);

            Assert.That(productionSupply.RestoreCount, Is.EqualTo(1));
            Assert.That(recovered.View.Phase, Is.EqualTo(BuqiUIDemoPhase.OperationChoice));
            Assert.That(recovered.View.Round, Is.EqualTo(1));
        }

        [Test]
        public void Bazaar_SaleRefreshesBoardAndCoinBalanceWithoutLeavingShop()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            SelectOperation(controller, "bazaar");
            BuqiDemoItemView soldItem = controller.View.BoardSlots.First(item => !item.Empty);
            int openingCoins = controller.View.Coins;
            Assert.That(Enum.TryParse("SellItem", out BuqiUIDemoCommandType sellCommandType), Is.True,
                "The shell needs a sell command so a completed drag can persist and refresh its resource chips.");

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = sellCommandType,
                PrimaryId = soldItem.Id,
            });

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Shop));
            Assert.That(controller.View.Coins, Is.GreaterThan(openingCoins));
            Assert.That(controller.View.BoardSlots.Any(item => item.Id == soldItem.Id), Is.False);

            controller = CreateController(store);
            Assert.That(controller.View.Coins, Is.EqualTo(result.View.Coins));
            Assert.That(controller.View.BoardSlots.Any(item => item.Id == soldItem.Id), Is.False);
        }

        [Test]
        public void ApplyDeployment_PersistsAnchorOnlyBoardSlotsDuringOperation()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            BuqiDemoItemView source = controller.View.BoardSlots.First(slot => !slot.Empty);
            string instanceId = source.Id;
            var board = EmptySlots(8);
            var storage = EmptySlots(8);
            board[3] = instanceId;
            for (int offset = 1; offset < source.Size; offset++)
                board[3 + offset] = instanceId;

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.ApplyDeployment,
                Deployment = new BuqiDeploymentSnapshot(board, storage),
            });

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData saveData, out string error), Is.True, error);
            Assert.That(saveData.BoardInstanceIds[3], Is.EqualTo(instanceId));
            Assert.That(saveData.BoardInstanceIds[4], Is.Empty);
            for (int offset = 0; offset < source.Size; offset++)
                Assert.That(controller.View.BoardSlots[3 + offset].Id, Is.EqualTo(instanceId));
        }

        [Test]
        public void FirstDay_NeverEntersLegacyIntelPredictionOrBoardEditorPhases()
        {
            BuqiUIDemoController controller = CreateController(new MemoryRunStore());
            var seenPhases = new List<BuqiUIDemoPhase> { controller.View.Phase };
            int guard = 0;
            while (controller.View.Round == 1 && guard++ < 24)
            {
                BuqiUIDemoCommandResult step = controller.Execute(SelectProgressCommand(controller.View));
                Assert.That(step.Accepted, Is.True, step.Reason);
                seenPhases.Add(controller.View.Phase);
            }

            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.StarterSelection), Is.False);
            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.OpponentIntel), Is.False);
            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.Prediction), Is.False);
            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.BoardEditor), Is.False);
        }

        [Test]
        public void TryCreate_NullOpponentIdsUseDefaultOpponentPool()
        {
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    CreateDemoCatalog(),
                    new BuqiUIDemoControllerOptions
                    {
                        Store = new MemoryRunStore(),
                        RunSeed = 1L,
                    },
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
        }

        private static BuqiUIDemoController CreateController(
            MemoryRunStore store,
            IBuqiBazaarSupplyRuntime supplyRuntime = null)
        {
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    CreateDemoCatalog(),
                    new BuqiUIDemoControllerOptions
                    {
                        Store = store,
                        RunSeed = 1L,
                        PveOpponentIds = new[] { "pve-a", "pve-b", "pve-c" },
                        PvpOpponentIds = new[] { "pvp-a", "pvp-b" },
                        BazaarSupplyRuntime = supplyRuntime,
                    },
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);
            if (controller.View.Phase == BuqiUIDemoPhase.PeriodTransition)
            {
                BuqiUIDemoCommandResult continued = controller.Execute(
                    new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.NextPhase });
                Assert.That(continued.Accepted, Is.True, continued.Reason);
            }
            return controller;
        }

        private static void AdvanceUntil(BuqiUIDemoController controller, BuqiUIDemoPhase target)
        {
            int guard = 0;
            while (controller.View.Phase != target && guard++ < 32)
            {
                BuqiUIDemoCommandResult step = controller.Execute(SelectProgressCommand(controller.View));
                Assert.That(step.Accepted, Is.True, step.Reason);
            }

            Assert.That(controller.View.Phase, Is.EqualTo(target));
        }

        private static BuqiUIDemoCommandResult Buy(BuqiUIDemoController controller, string offerId)
        {
            return controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.BuyOffer,
                PrimaryId = offerId,
            });
        }

        private static void SelectOperation(BuqiUIDemoController controller, string operationId)
        {
            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectOperation,
                PrimaryId = operationId,
            });
            Assert.That(result.Accepted, Is.True, result.Reason);
            if (controller.View.Phase == BuqiUIDemoPhase.PeriodTransition)
            {
                result = controller.Execute(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.NextPhase });
                Assert.That(result.Accepted, Is.True, result.Reason);
            }
        }

        private static void AssertDeploymentLocked(BuqiUIDemoController controller)
        {
            BuqiUIDemoView before = controller.View;
            BuqiUIDemoCommandResult open = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.OpenDragDeploy,
            });
            BuqiUIDemoCommandResult apply = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.ApplyDeployment,
                Deployment = new BuqiDeploymentSnapshot(
                    before.BoardSlots.Select(item => item.Empty ? string.Empty : item.Id).ToList(),
                    before.StorageSlots.Select(item => item.Empty ? string.Empty : item.Id).ToList()),
            });

            Assert.That(open.Accepted, Is.False);
            Assert.That(apply.Accepted, Is.False);
            Assert.That(controller.View, Is.SameAs(before));
        }

        private static string ItemFingerprint(BuqiUIDemoView view)
        {
            return string.Join("|", view.BoardSlots.Concat(view.StorageSlots)
                .Select(item => item.Empty ? "empty" : $"{item.Id}:{item.Description}"));
        }

        private static BuqiUIDemoCommand SelectProgressCommand(BuqiUIDemoView view)
        {
            if (view.BattleResultVisible)
                return new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.ContinueBattleResult };
            if (view.Phase == BuqiUIDemoPhase.RewardSelection)
            {
                BuqiDemoRewardView reward = view.Rewards[0];
                if (!reward.Selected)
                    return new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.PreviewReward, PrimaryId = reward.Id };
                if (!reward.Claimed)
                {
                    return new BuqiUIDemoCommand
                    {
                        Type = BuqiUIDemoCommandType.ClaimReward,
                        PrimaryId = reward.Id,
                        SecondaryId = reward.TargetId,
                    };
                }
            }
            if (view.Phase == BuqiUIDemoPhase.OperationChoice)
                return new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.SelectOperation, PrimaryId = "meditate" };
            if (view.Phase == BuqiUIDemoPhase.PveSelection)
                return new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.SelectPveDifficulty, PrimaryId = view.Choices[0].Id };
            if (view.Phase == BuqiUIDemoPhase.TribulationRoute)
                return new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.SelectTribulationRoute, PrimaryId = "face-thunder" };
            if (view.Phase == BuqiUIDemoPhase.TribulationStage)
                return new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.ResolveTribulationStage };
            if (view.Phase == BuqiUIDemoPhase.Event)
            {
                return new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.SelectChoice,
                    PrimaryId = view.Choices[0].Id,
                };
            }

            return new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.NextPhase };
        }

        private static List<string> EmptySlots(int count)
        {
            var slots = new List<string>(count);
            for (int index = 0; index < count; index++)
                slots.Add(string.Empty);
            return slots;
        }

        private static BuqiUIDemoCatalog CreateDemoCatalog()
        {
            Assert.That(BuqiUIDemoCatalog.TryCreate(CreateSourceCatalog(), out BuqiUIDemoCatalog catalog, out string error), Is.True, error);
            return catalog;
        }

        private static BuqiConfigCatalog CreateSourceCatalog()
        {
            var catalog = new BuqiConfigCatalog
            {
                Global = new BuqiGlobalConfigRow
                {
                    ContentVersion = "test-content-v1",
                    BoardSlotCount = 8,
                },
            };

            for (int index = 1; index <= 8; index++)
            {
                catalog.Items.Add(new BuqiItemConfigRow
                {
                    DefinitionId = $"item-{index:00}",
                    DisplayName = $"Item {index}",
                    Size = index == 1 ? BattleSize.M : BattleSize.S,
                    BasePrice = 2,
                    BaseCooldownTicks = 20 + index,
                });
            }

            for (int index = 1; index <= 3; index++)
            {
                catalog.Refinements.Add(new BuqiRefinementConfigRow
                {
                    RefinementId = $"mod-{index:00}",
                    DisplayName = $"Mod {index}",
                    Summary = $"Mod summary {index}",
                });
            }

            catalog.Echoes.Add(CreateEcho("pve-a", "PVE A", "item-02", "item-03"));
            catalog.Echoes.Add(CreateEcho("pve-b", "PVE B", "item-03", "item-04"));
            catalog.Echoes.Add(CreateEcho("pve-c", "PVE C", "item-04", "item-05"));
            catalog.Echoes.Add(CreateEcho("pvp-a", "PVP A", "item-05", "item-06"));
            catalog.Echoes.Add(CreateEcho("pvp-b", "PVP B", "item-07", "item-08"));
            string[] defaultArchetypes =
            {
                "fast", "buffer", "chain", "heal", "poison", "burn", "freeze", "overload",
            };
            for (int index = 0; index < defaultArchetypes.Length; index++)
            {
                string archetype = defaultArchetypes[index];
                string firstItem = $"item-{index % 8 + 1:00}";
                string secondItem = $"item-{(index + 2) % 8 + 1:00}";
                catalog.Echoes.Add(CreateEcho(
                    $"echo-{archetype}-lesson",
                    $"{archetype} lesson",
                    firstItem,
                    secondItem));
                catalog.Echoes.Add(CreateEcho(
                    $"echo-{archetype}-early",
                    $"{archetype} early",
                    firstItem,
                    secondItem));
            }
            return catalog;
        }

        private static BuqiEchoConfigRow CreateEcho(string echoId, string displayName, string firstItemId, string secondItemId)
        {
            var snapshot = new BuqiBuildSnapshotConfigRow
            {
                SnapshotId = echoId + "-snapshot",
                ArchetypeId = echoId + "-build",
            };
            snapshot.Items.Add(new BuqiItemInstanceConfigRow
            {
                InstanceId = echoId + "-item-1",
                DefinitionId = firstItemId,
                AnchorSlot = 0,
            });
            snapshot.Items.Add(new BuqiItemInstanceConfigRow
            {
                InstanceId = echoId + "-item-2",
                DefinitionId = secondItemId,
                AnchorSlot = 3,
            });

            return new BuqiEchoConfigRow
            {
                EchoId = echoId,
                DisplayName = displayName,
                Build = snapshot.ArchetypeId,
                Snapshot = snapshot,
            };
        }

        private sealed class MemoryRunStore : IBuqiRunStore
        {
            public string CurrentJson { get; private set; }

            public bool TryRead(out string json, out string error)
            {
                if (CurrentJson == null)
                {
                    json = string.Empty;
                    error = "Save file does not exist.";
                    return false;
                }

                json = CurrentJson;
                error = string.Empty;
                return true;
            }

            public bool TryWrite(string json, out string error)
            {
                CurrentJson = json;
                error = string.Empty;
                return true;
            }

            public bool TryDelete(out string error)
            {
                CurrentJson = null;
                error = string.Empty;
                return true;
            }
        }

        private sealed class FakeBazaarSupplyRuntime : IBuqiBazaarSupplyRuntime
        {
            public readonly string[] InitialOffers =
                { "item-01", "item-02", "item-03", "item-04", "item-05", "item-06", "item-07", "item-08" };
            public readonly string[] RefreshedOffers =
                { "item-08", "item-07", "item-06", "item-05", "item-04", "item-03", "item-02", "item-01" };

            public int OpenCount { get; private set; }
            public int RestoreCount { get; private set; }
            public int Balance { get; private set; }
            public bool RejectRestore { get; set; }
            public List<string> PurchasedOfferIds { get; } = new List<string>();

            private IReadOnlyList<string> m_Offers;

            public void Reset()
            {
                Balance = 0;
                PurchasedOfferIds.Clear();
                m_Offers = null;
            }

            public bool TryOpen(
                BuqiBazaarSupplyContext context,
                out IReadOnlyList<string> offerDefinitionIds,
                out string error)
            {
                OpenCount++;
                Balance = context.Balance;
                m_Offers = InitialOffers;
                offerDefinitionIds = m_Offers;
                error = string.Empty;
                return true;
            }

            public bool TryRestore(
                BuqiBazaarSupplyContext context,
                IReadOnlyList<string> offerDefinitionIds,
                out string error)
            {
                RestoreCount++;
                if (RejectRestore)
                {
                    error = "旧商店货架无法恢复。";
                    return false;
                }
                Balance = context.Balance;
                PurchasedOfferIds.Clear();
                PurchasedOfferIds.AddRange(context.PurchasedOfferIds);
                m_Offers = offerDefinitionIds.ToArray();
                error = string.Empty;
                return true;
            }

            public bool TryRefresh(
                BuqiBazaarSupplyContext context,
                out IReadOnlyList<string> offerDefinitionIds,
                out int cost,
                out string error)
            {
                cost = 2;
                Balance = context.Balance - cost;
                PurchasedOfferIds.Clear();
                m_Offers = RefreshedOffers;
                offerDefinitionIds = m_Offers;
                error = string.Empty;
                return true;
            }

            public bool RecordPurchase(string offerDefinitionId, int balance, out string error)
            {
                Balance = balance;
                PurchasedOfferIds.Add(offerDefinitionId);
                error = string.Empty;
                return true;
            }

            public bool TryGetCurrentSupply(out BuqiBazaarSupplyView supply)
            {
                supply = new BuqiBazaarSupplyView
                {
                    Balance = Balance,
                    CanRefresh = true,
                    RefreshPrice = 2,
                    RefreshPriceLabel = "刷新 2 金币",
                    OfferIds = m_Offers ?? Array.Empty<string>(),
                    PurchasedOfferIds = PurchasedOfferIds.ToArray(),
                };
                return m_Offers != null;
            }
        }
    }
}
