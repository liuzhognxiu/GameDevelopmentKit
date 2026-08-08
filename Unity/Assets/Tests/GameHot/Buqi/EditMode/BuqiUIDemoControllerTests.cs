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
        public void Create_StartsInEncounterWithoutLegacyTopLevelPhases()
        {
            BuqiUIDemoController controller = CreateController(new MemoryRunStore());

            Assert.That(controller.View.Phase, Is.AnyOf(BuqiUIDemoPhase.Shop, BuqiUIDemoPhase.Event));
            Assert.That(controller.View.Phase, Is.Not.EqualTo(BuqiUIDemoPhase.StarterSelection));
            Assert.That(controller.View.Phase, Is.Not.EqualTo(BuqiUIDemoPhase.OpponentIntel));
            Assert.That(controller.View.Phase, Is.Not.EqualTo(BuqiUIDemoPhase.Prediction));
            Assert.That(controller.View.BoardSlots.Count, Is.EqualTo(8));
            Assert.That(controller.View.StorageSlots.Count, Is.EqualTo(8));
        }

        [Test]
        public void OpenDragDeploy_IsAcceptedInEncounterAndRejectedDuringBattleReplay()
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
        public void ApplyDeployment_PersistsAnchorOnlyBoardSlots()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            string instanceId = controller.View.BoardSlots.First(slot => !slot.Empty).Id;
            var board = EmptySlots(8);
            var storage = EmptySlots(8);
            board[3] = instanceId;
            board[4] = instanceId;

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.ApplyDeployment,
                Deployment = new BuqiDeploymentSnapshot(board, storage),
            });

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData saveData, out string error), Is.True, error);
            Assert.That(saveData.BoardInstanceIds[3], Is.EqualTo(instanceId));
            Assert.That(saveData.BoardInstanceIds[4], Is.Empty);
            Assert.That(controller.View.BoardSlots[3].Id, Is.EqualTo(instanceId));
            Assert.That(controller.View.BoardSlots[4].Id, Is.EqualTo(instanceId));
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

            Assert.That(seenPhases, Does.Not.Contain(BuqiUIDemoPhase.StarterSelection));
            Assert.That(seenPhases, Does.Not.Contain(BuqiUIDemoPhase.OpponentIntel));
            Assert.That(seenPhases, Does.Not.Contain(BuqiUIDemoPhase.Prediction));
            Assert.That(seenPhases, Does.Not.Contain(BuqiUIDemoPhase.BoardEditor));
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
                        PveOpponentIds = new[] { "pve-a", "pve-b" },
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
            catalog.Echoes.Add(CreateEcho("pvp-a", "PVP A", "item-05", "item-06"));
            catalog.Echoes.Add(CreateEcho("pvp-b", "PVP B", "item-07", "item-08"));
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
