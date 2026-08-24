using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Settlement;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiFinalFlowIntegrationTests
    {
        [Test]
        public void NewRun_ShowsStandaloneOperationChoiceWithBoardAndThreeActions()
        {
            var store = new MemoryStore();
            BuqiUIDemoController controller = CreateController(store);

            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.OperationChoice));
            Assert.That(controller.View.Choices.Select(choice => choice.Id),
                Is.EqualTo(new[] { "bazaar", "event", "training" }));
            Assert.That(controller.View.BoardSlots.Any(slot => !slot.Empty), Is.True);
            Assert.That(ReadSave(store).EncounterPayload, Is.Empty);
        }

        [Test]
        public void PveSelection_IsPersistedAsEmptyBattleUntilDifficultyIsChosen()
        {
            var store = new MemoryStore();
            BuqiUIDemoController controller = CreateController(store);
            SelectOperation(controller, "meditate");
            SelectOperation(controller, "meditate");

            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PveSelection));
            Assert.That(controller.View.Choices.Count, Is.EqualTo(3));
            Assert.That(ReadSave(store).BattlePayload, Is.Empty);
            string[] choiceIds = controller.View.Choices.Select(choice => choice.Id).ToArray();

            controller = CreateController(store);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PveSelection));
            Assert.That(controller.View.Choices.Select(choice => choice.Id), Is.EqualTo(choiceIds));

            BuqiUIDemoCommandResult selected = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectPveDifficulty,
                PrimaryId = choiceIds[0],
            });
            Assert.That(selected.Accepted, Is.True, selected.Reason);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleReplay));
            Assert.That(ReadSave(store).BattlePayload, Is.Not.Empty);
        }

        [Test]
        public void OperationChoice_CanEnterExplicitBazaarWithoutRandomKind()
        {
            BuqiUIDemoController controller = CreateController(new MemoryStore());
            SelectOperation(controller, "bazaar");

            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Shop));
            Assert.That(controller.View.ShopOffers, Is.Not.Empty);
        }

        private static void SelectOperation(BuqiUIDemoController controller, string id)
        {
            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectOperation,
                PrimaryId = id,
            });
            Assert.That(result.Accepted, Is.True, result.Reason);
            if (controller.View.Phase == BuqiUIDemoPhase.PeriodTransition)
            {
                result = controller.Execute(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.NextPhase });
                Assert.That(result.Accepted, Is.True, result.Reason);
            }
        }

        private static BuqiUIDemoController CreateController(MemoryStore store)
        {
            Assert.That(BuqiUIDemoController.TryCreate(
                CreateCatalog(),
                new BuqiUIDemoControllerOptions
                {
                    RunSeed = 17,
                    Store = store,
                    PveOpponentIds = new[] { "pve-a", "pve-b", "pve-c" },
                    PvpOpponentIds = new[] { "pvp-a", "pvp-b" },
                },
                out BuqiUIDemoController controller,
                out string error), Is.True, error);
            if (controller.View.Phase == BuqiUIDemoPhase.PeriodTransition)
            {
                BuqiUIDemoCommandResult continued = controller.Execute(
                    new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.NextPhase });
                Assert.That(continued.Accepted, Is.True, continued.Reason);
            }
            return controller;
        }

        private static BuqiUIDemoCatalog CreateCatalog()
        {
            var source = new BuqiConfigCatalog
            {
                Global = new BuqiGlobalConfigRow
                {
                    ContentVersion = "final-flow-v1",
                    BoardSlotCount = BuqiRunRules.BoardSlotCount,
                },
            };
            for (int index = 1; index <= 8; index++)
            {
                source.Items.Add(new BuqiItemConfigRow
                {
                    DefinitionId = $"item-{index:00}",
                    DisplayName = $"Item {index}",
                    Size = Game.Hot.Buqi.Battle.BuqiSize.S,
                    BasePrice = index + 1,
                    BaseCooldownTicks = 10 + index,
                });
            }
            for (int index = 1; index <= 3; index++)
            {
                source.Refinements.Add(new BuqiRefinementConfigRow
                {
                    RefinementId = $"A-0{index}",
                    DisplayName = $"Refine {index}",
                    Summary = $"Refine {index}",
                });
            }

            source.Echoes.Add(Echo("pve-a", "item-02"));
            source.Echoes.Add(Echo("pve-b", "item-03"));
            source.Echoes.Add(Echo("pve-c", "item-04"));
            source.Echoes.Add(Echo("pvp-a", "item-05"));
            source.Echoes.Add(Echo("pvp-b", "item-06"));
            Assert.That(BuqiUIDemoCatalog.TryCreate(source, out BuqiUIDemoCatalog catalog, out string error), Is.True, error);
            return catalog;
        }

        private static BuqiEchoConfigRow Echo(string id, string itemId)
        {
            var snapshot = new BuqiBuildSnapshotConfigRow { SnapshotId = id + "-snapshot", ArchetypeId = id };
            snapshot.Items.Add(new BuqiItemInstanceConfigRow
            {
                InstanceId = id + "-item",
                DefinitionId = itemId,
                AnchorSlot = 0,
            });
            return new BuqiEchoConfigRow { EchoId = id, DisplayName = id, Build = id, Snapshot = snapshot };
        }

        private static BuqiRunSaveData ReadSave(MemoryStore store)
        {
            Assert.That(BuqiRunSaveCodec.TryFromJson(store.Json, out BuqiRunSaveData save, out string error), Is.True, error);
            return save;
        }

        private sealed class MemoryStore : IBuqiRunStore
        {
            public string Json;
            public bool TryRead(out string json, out string error)
            {
                json = Json ?? string.Empty;
                error = Json == null ? "Save file does not exist." : string.Empty;
                return Json != null;
            }
            public bool TryWrite(string json, out string error) { Json = json; error = string.Empty; return true; }
            public bool TryDelete(out string error) { Json = null; error = string.Empty; return true; }
        }
    }
}
