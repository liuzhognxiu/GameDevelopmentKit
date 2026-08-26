using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Integration;
using Game.Hot.Buqi.Run.Settlement;
using NUnit.Framework;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunMainFlowUIContractTests
    {
        [Test]
        public void EventAndTraining_RestoreTheirFormalStageAcrossPeriodTransitions()
        {
            var store = new MemoryStore();
            BuqiUIDemoController controller = CreateController(store);

            Assert.That(controller.View.RouteNodes, Has.Count.EqualTo(3));
            Assert.That(controller.View.RouteNodes.All(node =>
                !string.IsNullOrWhiteSpace(node.Benefit) &&
                !string.IsNullOrWhiteSpace(node.Cost) &&
                !string.IsNullOrWhiteSpace(node.Condition)), Is.True);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Execute(controller, BuqiUIDemoCommandType.NextPhase);

            Execute(controller, BuqiUIDemoCommandType.SelectRouteNode, "event");
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Event));

            controller = CreateController(store);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Event));
            Assert.That(controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectOperation,
                PrimaryId = "bazaar",
            }).Accepted, Is.False);

            Execute(controller, BuqiUIDemoCommandType.SelectChoice, controller.View.Choices[0].Id);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Assert.That(ReadSave(store).PeriodTransitionVisible, Is.True);

            controller = CreateController(store);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Execute(controller, BuqiUIDemoCommandType.NextPhase);
            Execute(controller, BuqiUIDemoCommandType.SelectRouteNode, "training");
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Training));
            Assert.That(controller.View.Choices, Is.Not.Empty);

            controller = CreateController(store);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Training));
            BuqiDemoChoiceView training = controller.View.Choices.First(choice => !choice.Disabled);
            Execute(controller, BuqiUIDemoCommandType.ExecuteTraining, training.Id, training.TargetId);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Assert.That(ReadSave(store).OperationRuntime.TemporaryModifiers, Is.Not.Empty);
        }

        [Test]
        public void BattleResultPauseAndReward_AreBlockingRecoverableAndIdempotent()
        {
            var store = new MemoryStore();
            BuqiUIDemoController controller = CreateController(store);
            CompleteMeditationPeriod(controller);
            CompleteMeditationPeriod(controller);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PveSelection));

            Execute(controller, BuqiUIDemoCommandType.SelectPveDifficulty, controller.View.Choices[0].Id);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleReplay));
            Execute(controller, BuqiUIDemoCommandType.NextPhase);
            Assert.That(controller.View.BattleResultVisible, Is.True);
            Assert.That(controller.View.BattleResultLabel, Is.Not.Empty);
            Assert.That(controller.View.InputLocked, Is.True);

            Execute(controller, BuqiUIDemoCommandType.PauseRun);
            Assert.That(controller.View.IsPaused, Is.True);
            Assert.That(controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.OpenDragDeploy,
            }).Accepted, Is.False);

            controller = CreateController(store);
            Assert.That(controller.View.IsPaused, Is.True);
            Assert.That(controller.View.BattleResultVisible, Is.True);
            Execute(controller, BuqiUIDemoCommandType.ResumeRun);
            Execute(controller, BuqiUIDemoCommandType.ContinueBattleResult);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.RewardSelection));
            Assert.That(controller.View.Rewards, Has.Count.EqualTo(4));
            Assert.That(controller.View.PrimaryCommandLabel, Is.Empty);

            BuqiDemoRewardView reward = controller.View.Rewards[0];
            int coinsBefore = controller.View.Coins;
            Execute(controller, BuqiUIDemoCommandType.PreviewReward, reward.Id);
            Assert.That(controller.View.Coins, Is.EqualTo(coinsBefore));
            Execute(controller, BuqiUIDemoCommandType.ClaimReward, reward.Id, reward.TargetId);
            int coinsAfter = controller.View.Coins;
            Assert.That(controller.View.PrimaryCommandLabel, Is.EqualTo("继续"));
            Execute(controller, BuqiUIDemoCommandType.ClaimReward, reward.Id, reward.TargetId);
            Assert.That(controller.View.Coins, Is.EqualTo(coinsAfter));

            controller = CreateController(store);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.RewardSelection));
            Assert.That(controller.View.Rewards.All(candidate => candidate.Claimed), Is.True);
            Execute(controller, BuqiUIDemoCommandType.NextPhase);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));

            Execute(controller, BuqiUIDemoCommandType.PauseRun);
            Execute(controller, BuqiUIDemoCommandType.ExitRun);
            Assert.That(controller.View.ExitRequested, Is.True);
            Assert.That(ReadSave(store).ExitRequested, Is.False);
            controller = CreateController(store);
            Assert.That(controller.View.ExitRequested, Is.False);
        }

        [Test]
        public void TrainingRoute_WithNoAvailableOffer_FallsBackWithoutEnteringAnEmptyStage()
        {
            var store = new MemoryStore();
            BuqiUIDemoController controller = CreateController(store, trainingMinDay: 7);

            Execute(controller, BuqiUIDemoCommandType.NextPhase);
            Execute(controller, BuqiUIDemoCommandType.SelectRouteNode, "training");

            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            controller = CreateController(store, trainingMinDay: 7);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Execute(controller, BuqiUIDemoCommandType.NextPhase);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.OperationChoice));
        }

        [Test]
        public void StaleBattleSettlementReplay_ReloadsProgressedSaveWithoutOverwritingIt()
        {
            var store = new MemoryStore();
            BuqiUIDemoController staleController = CreateController(store);
            CompleteMeditationPeriod(staleController);
            CompleteMeditationPeriod(staleController);
            Execute(
                staleController,
                BuqiUIDemoCommandType.SelectPveDifficulty,
                staleController.View.Choices[0].Id);
            Assert.That(staleController.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleReplay));

            BuqiUIDemoController progressedController = CreateController(store);
            Execute(progressedController, BuqiUIDemoCommandType.NextPhase);
            Execute(progressedController, BuqiUIDemoCommandType.ContinueBattleResult);
            BuqiDemoRewardView reward = progressedController.View.Rewards[0];
            Execute(progressedController, BuqiUIDemoCommandType.PreviewReward, reward.Id);
            Execute(progressedController, BuqiUIDemoCommandType.ClaimReward, reward.Id, reward.TargetId);
            Execute(progressedController, BuqiUIDemoCommandType.NextPhase);
            string progressedJson = store.Json;
            BuqiUIDemoPhase progressedPhase = progressedController.View.Phase;

            Execute(staleController, BuqiUIDemoCommandType.NextPhase);

            Assert.That(store.Json, Is.EqualTo(progressedJson));
            Assert.That(staleController.View.Phase, Is.EqualTo(progressedPhase));
            Assert.That(staleController.View.BattleResultVisible, Is.False);
        }

        private static void CompleteMeditationPeriod(BuqiUIDemoController controller)
        {
            if (controller.View.Phase == BuqiUIDemoPhase.PeriodTransition)
                Execute(controller, BuqiUIDemoCommandType.NextPhase);
            Execute(controller, BuqiUIDemoCommandType.SelectOperation, "meditate");
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Execute(controller, BuqiUIDemoCommandType.NextPhase);
        }

        private static void Execute(
            BuqiUIDemoController controller,
            BuqiUIDemoCommandType type,
            string primaryId = "",
            string secondaryId = "")
        {
            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = type,
                PrimaryId = primaryId,
                SecondaryId = secondaryId,
            });
            Assert.That(result.Accepted, Is.True, result.Reason);
        }

        private static BuqiUIDemoController CreateController(MemoryStore store, int trainingMinDay = 1)
        {
            Assert.That(BuqiUIDemoController.TryCreate(
                CreateCatalog(trainingMinDay),
                new BuqiUIDemoControllerOptions
                {
                    RunSeed = 23,
                    Store = store,
                    RewardCandidateCount = 4,
                    PveOpponentIds = new[] { "pve-a", "pve-b", "pve-c" },
                    PvpOpponentIds = new[] { "pvp-a", "pvp-b" },
                },
                out BuqiUIDemoController controller,
                out string error), Is.True, error);
            return controller;
        }

        private static BuqiUIDemoCatalog CreateCatalog(int trainingMinDay = 1)
        {
            var source = new BuqiConfigCatalog
            {
                Global = new BuqiGlobalConfigRow
                {
                    ContentVersion = "main-flow-ui-v1",
                    BoardSlotCount = BuqiRunRules.BoardSlotCount,
                    InitialExecution = 100,
                },
            };
            for (int index = 1; index <= 8; index++)
            {
                source.Items.Add(new BuqiItemConfigRow
                {
                    DefinitionId = $"item-{index:00}",
                    DisplayName = $"Item {index}",
                    Size = BattleSize.S,
                    BasePrice = index + 1,
                    BaseCooldownTicks = 10 + index,
                    Tags = new List<string> { index == 1 ? "attack" : "support" },
                });
            }
            for (int index = 1; index <= 3; index++)
            {
                source.Refinements.Add(new BuqiRefinementConfigRow
                {
                    RefinementId = $"refinement-{index}",
                    DisplayName = $"改造 {index}",
                    Summary = "测试用通用改造。",
                });
            }
            source.TrainingProjects.Add(new BuqiTrainingProjectConfigRow
            {
                ProjectId = "training-haste",
                DisplayName = "Training Haste",
                MinDay = trainingMinDay,
                MaxDay = 9,
                Cost = 2,
                RequiredTag = "attack",
                EffectKind = "OpeningHaste",
                Amount = 1000,
                Duration = 30,
                MaxPerRun = 1,
            });
            source.Events.Add(new BuqiEventConfigRow { EventId = "event-main", Weight = 1, MinDay = 1, MaxDay = 9 });
            for (int index = 0; index < 3; index++)
            {
                source.EventOptions.Add(new BuqiEventOptionConfigRow
                {
                    EventId = "event-main",
                    OptionId = $"event-option-{index}",
                    Order = index,
                    DisplayName = $"Option {index}",
                    Summary = "Configured outcome",
                    Outcomes =
                    {
                        new BuqiEventOutcomeConfigRow
                        {
                            Kind = "Coins",
                            Amount = index + 1,
                            ReasonCode = "event-coins",
                        },
                    },
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

        private static BuqiEchoConfigRow Echo(string id, string definitionId)
        {
            var snapshot = new BuqiBuildSnapshotConfigRow { SnapshotId = id + "-snapshot", ArchetypeId = id };
            snapshot.Items.Add(new BuqiItemInstanceConfigRow
            {
                InstanceId = id + "-item",
                DefinitionId = definitionId,
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

            public bool TryWrite(string json, out string error)
            {
                Json = json;
                error = string.Empty;
                return true;
            }

            public bool TryDelete(out string error)
            {
                Json = null;
                error = string.Empty;
                return true;
            }
        }
    }
}
