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
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.OperationChoice));
        }

        private static BuqiUIDemoController CreateController(MemoryRunStore store)
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
                    },
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);
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

        private static BuqiUIDemoCommand SelectProgressCommand(BuqiUIDemoView view)
        {
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
                    BasePrice = index + 1,
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
    }
}
