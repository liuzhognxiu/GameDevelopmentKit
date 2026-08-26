using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.Run.Battle;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Integration;
using Game.Hot.Buqi.Run.Settlement;
using NUnit.Framework;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;
using BattleOutcome = Game.Hot.Buqi.Battle.BattleOutcome;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunDayLoopIntegrationTests
    {
        [Test]
        public void Create_StartsAtPeriodTransitionThenShowsDeterministicOperationChoice()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(new MemoryRunStore()),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);

            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            BuqiUIDemoCommandResult continued = controller.Execute(
                new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.NextPhase });
            Assert.That(continued.Accepted, Is.True, continued.Reason);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.OperationChoice));
            Assert.That(controller.View.Choices.Count, Is.EqualTo(3));
            Assert.That(controller.View.Phase, Is.Not.EqualTo(BuqiUIDemoPhase.StarterSelection));
            Assert.That(controller.View.Phase, Is.Not.EqualTo(BuqiUIDemoPhase.OpponentIntel));
            Assert.That(controller.View.Phase, Is.Not.EqualTo(BuqiUIDemoPhase.Prediction));
            Assert.That(controller.View.BoardSlots.Count(slot => !slot.Empty), Is.EqualTo(1));
            Assert.That(controller.View.StorageSlots.Count, Is.EqualTo(BuqiRunRules.StorageSlotCount));
            Assert.That(controller.View.StorageSlots.All(slot => slot.Empty), Is.True);
        }

        [Test]
        public void BuqiNineDay_FullDayRunsMorningNoonDuskNightThenAdvancesWithoutForcedRecordPage()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);

            var seenPhases = new List<BuqiUIDemoPhase> { controller.View.Phase };
            int guard = 0;
            while (controller.View.Round == 1 && guard++ < 24)
            {
                BuqiUIDemoCommand command = SelectProgressCommand(controller.View);
                BuqiUIDemoCommandResult result = controller.Execute(command);
                Assert.That(result.Accepted, Is.True, result.Reason);
                seenPhases.Add(controller.View.Phase);
            }

            Assert.That(controller.View.Round, Is.EqualTo(2));
            Assert.That(seenPhases.Count(phase => phase == BuqiUIDemoPhase.OperationChoice), Is.EqualTo(4));
            Assert.That(seenPhases.Count(phase => phase == BuqiUIDemoPhase.PeriodTransition), Is.GreaterThanOrEqualTo(4));
            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.PveSelection), Is.True);
            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.BattleReplay), Is.True);
            Assert.That(seenPhases.Count(phase => phase == BuqiUIDemoPhase.BattleSummary), Is.EqualTo(2));
            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.RoundSettlement), Is.False);
            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.StarterSelection), Is.False);
            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.OpponentIntel), Is.False);
            Assert.That(seenPhases.Contains(BuqiUIDemoPhase.Prediction), Is.False);
        }

        [Test]
        public void NinthPvpWin_OpensTribulationAtTheNextHourSixGate()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateWinningController(store);
            int guard = 0;

            while ((controller.View.Wins < BuqiRunRules.WinsToVictory - 1 ||
                    controller.View.Phase != BuqiUIDemoPhase.BattleSummary) && guard++ < 512)
            {
                BuqiUIDemoCommandResult result = controller.Execute(SelectProgressCommand(controller.View));
                Assert.That(result.Accepted, Is.True, result.Reason);
            }

            BuqiRunSaveData ninthWin = ReadSave(store);
            Assert.That(
                guard,
                Is.LessThan(512),
                $"wins={controller.View.Wins}, day={controller.View.Round}, period={controller.View.Period}, phase={controller.View.Phase}");
            Assert.That(ninthWin.Wins, Is.EqualTo(BuqiRunRules.WinsToVictory - 1));
            Assert.That(ninthWin.Day, Is.EqualTo(BuqiRunRules.WinsToVictory));
            Assert.That(ninthWin.Period, Is.EqualTo((int)BuqiRunPeriod.Hour1Operation));
            Assert.That(ninthWin.Phase, Is.EqualTo((int)BuqiRunPhase.Encounter));
            Assert.That(ninthWin.BattlePayload, Is.Not.Empty);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleSummary));

            BuqiUIDemoCommandResult confirmed = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.ContinueBattleResult,
            });

            Assert.That(confirmed.Accepted, Is.True, confirmed.Reason);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.RewardSelection));
            while (controller.View.Phase != BuqiUIDemoPhase.PeriodTransition)
            {
                BuqiUIDemoCommandResult rewardStep = controller.Execute(SelectProgressCommand(controller.View));
                Assert.That(rewardStep.Accepted, Is.True, rewardStep.Reason);
            }
            Assert.That(ReadSave(store).BattlePayload, Is.Empty);

            guard = 0;
            while (controller.View.Phase != BuqiUIDemoPhase.TribulationRoute && guard++ < 64)
            {
                BuqiUIDemoCommandResult result = controller.Execute(SelectProgressCommand(controller.View));
                Assert.That(result.Accepted, Is.True, result.Reason);
            }
            Assert.That(guard, Is.LessThan(64));
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.TribulationRoute));
            Assert.That(controller.View.Period, Is.EqualTo(BuqiRunPeriod.Hour6Pvp));

            BuqiUIDemoCommandResult route = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectTribulationRoute,
                PrimaryId = "face-thunder",
            });
            Assert.That(route.Accepted, Is.True, route.Reason);

            for (int stage = 1; stage <= BuqiRunRules.TribulationStageCount; stage++)
            {
                Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.TribulationStage));
                Assert.That(controller.View.TribulationStage, Is.EqualTo(stage));
                BuqiUIDemoCommandResult resolved = controller.Execute(new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.ResolveTribulationStage,
                });
                Assert.That(resolved.Accepted, Is.True, resolved.Reason);
            }

            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.RunTerminal));
            Assert.That(controller.View.PrimaryCommandLabel, Is.EqualTo("重新开始"));
            Assert.That(ReadSave(store).Phase, Is.EqualTo((int)BuqiRunPhase.RunTerminal));

            BuqiUIDemoController restored = CreateWinningController(store);
            Assert.That(restored.View.Phase, Is.EqualTo(BuqiUIDemoPhase.RunTerminal));
            Assert.That(restored.View.Round, Is.EqualTo(BuqiRunRules.WinsToVictory));
        }

        [Test]
        public void TryCreate_CorruptSaveFailsClosedAndPreservesStoredBytes()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore("{broken-json");

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.False);

            Assert.That(controller, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(store.CurrentJson, Is.EqualTo("{broken-json"));
        }

        [Test]
        public void EventGrant_DoesNotSpendCoinsAndAdvancesImmediately()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store, runSeed: 2L),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);
            SelectOperation(controller, "event");
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Event));
            int coinsBefore = controller.View.Coins;

            Assert.That(controller.View.Choices.Any(choice => choice.Id == "event-item"), Is.True);
            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectChoice,
                PrimaryId = "event-item",
            });

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(controller.View.Coins, Is.EqualTo(coinsBefore));
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
        }

        [Test]
        public void FailedStoreWrite_DuringPurchaseLeavesStateUnchanged()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateControllerOnPhase(store, BuqiUIDemoPhase.Shop);
            RunFingerprint before = CaptureRuntime(controller);
            string jsonBefore = store.CurrentJson;
            store.FailNextWrite("purchase write failed");

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.BuyOffer,
                PrimaryId = controller.View.ShopOffers[0].Id,
            });

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("操作未完成，请重试。"));
            Assert.That(CaptureRuntime(controller), Is.EqualTo(before));
            Assert.That(store.CurrentJson, Is.EqualTo(jsonBefore));
        }

        [Test]
        public void FailedStoreWrite_DuringEventLeavesStateUnchanged()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateControllerOnPhase(store, BuqiUIDemoPhase.Event);
            RunFingerprint before = CaptureRuntime(controller);
            string jsonBefore = store.CurrentJson;
            store.FailNextWrite("event write failed");

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectChoice,
                PrimaryId = controller.View.Choices[0].Id,
            });

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("操作未完成，请重试。"));
            Assert.That(CaptureRuntime(controller), Is.EqualTo(before));
            Assert.That(store.CurrentJson, Is.EqualTo(jsonBefore));
        }

        [Test]
        public void DeploymentCommand_DuringPveSelectionRefreshesSnapshotAndAllowsBattle()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            AdvanceUntilPhase(controller, BuqiUIDemoPhase.PveSelection);
            string[] choiceIds = controller.View.Choices.Select(choice => choice.Id).ToArray();
            string instanceId = controller.View.BoardSlots.First(slot => !slot.Empty).Id;
            var board = Enumerable.Repeat(string.Empty, BuqiRunRules.BoardSlotCount).ToList();
            board[3] = instanceId;

            BuqiUIDemoCommandResult deployment = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.ApplyDeployment,
                Deployment = new BuqiDeploymentSnapshot(
                    board,
                    Enumerable.Repeat(string.Empty, BuqiRunRules.StorageSlotCount).ToList()),
            });

            Assert.That(deployment.Accepted, Is.True, deployment.Reason);
            Assert.That(controller.View.Choices.Select(choice => choice.Id), Is.EqualTo(choiceIds));

            BuqiUIDemoCommandResult selected = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectPveDifficulty,
                PrimaryId = choiceIds[0],
            });
            Assert.That(selected.Accepted, Is.True, selected.Reason);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleReplay));
        }

        [Test]
        public void FailedStoreWrite_DuringBattleGenerationLeavesStateUnchanged()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            AdvanceUntilPhase(controller, BuqiUIDemoPhase.BattleSummary, summaryCountTarget: 1);
            RunFingerprint before = CaptureRuntime(controller);
            string jsonBefore = store.CurrentJson;
            store.FailNextWrite("battle generation write failed");

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.NextPhase,
            });

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("操作未完成，请重试。"));
            Assert.That(CaptureRuntime(controller), Is.EqualTo(before));
            Assert.That(store.CurrentJson, Is.EqualTo(jsonBefore));
        }

        [Test]
        public void FailedStoreWrite_DuringCompleteDayLeavesStateUnchanged()
        {
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            AdvanceUntilPhase(controller, BuqiUIDemoPhase.BattleSummary, summaryCountTarget: 2);
            RunFingerprint before = CaptureRuntime(controller);
            string jsonBefore = store.CurrentJson;
            store.FailNextWrite("day completion write failed");

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.NextPhase,
            });

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("操作未完成，请重试。"));
            Assert.That(CaptureRuntime(controller), Is.EqualTo(before));
            Assert.That(store.CurrentJson, Is.EqualTo(jsonBefore));
        }

        [Test]
        public void FailedStoreWrite_DoesNotAdvanceRuntimeOrPersistedState()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);

            RunFingerprint before = CaptureRuntime(controller);
            string jsonBefore = store.CurrentJson;
            BuqiUIDemoCommand command = SelectProgressCommand(controller.View);
            store.FailNextWrite("simulated write failure");

            BuqiUIDemoCommandResult failed = controller.Execute(command);

            Assert.That(failed.Accepted, Is.False);
            Assert.That(failed.Reason, Is.EqualTo("操作未完成，请重试。"));
            Assert.That(CaptureRuntime(controller), Is.EqualTo(before));
            Assert.That(store.CurrentJson, Is.EqualTo(jsonBefore));

            BuqiUIDemoCommandResult retried = controller.Execute(command);

            Assert.That(retried.Accepted, Is.True, retried.Reason);
            Assert.That(CaptureRuntime(controller), Is.Not.EqualTo(before));
            Assert.That(store.CurrentJson, Is.Not.EqualTo(jsonBefore));
        }

        [Test]
        public void TryCreate_ContentVersionMismatchStartsFreshRunAndSkipsPendingSettlement()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);
            AdvanceUntilPhase(controller, BuqiUIDemoPhase.BattleReplay);

            BuqiRunSaveData save = ReadSave(store);
            BuqiRunBattleSummary summary = BuildSummary(controller);
            save.ContentVersion = "mismatched-content-version";
            save.PendingSettlement = CreatePendingSettlement(summary, save.Revision, BuqiRunBattleKind.Pve, controller.CurrentReplay.Result.Outcome);
            save.HasPendingSettlement = true;
            store.SetJson(BuqiRunSaveCodec.ToJson(save));
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out error),
                Is.True,
                error);

            Assert.That(reloaded.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            BuqiRunSaveData freshSave = ReadSave(store);
            Assert.That(freshSave.ContentVersion, Is.EqualTo("test-content-v1"));
            Assert.That(freshSave.SaveVersion, Is.EqualTo(BuqiRunSaveData.CurrentSaveVersion));
            Assert.That(freshSave.PendingSettlement, Is.Null);
            Assert.That(store.Deletes, Is.EqualTo(0));
            Assert.That(reloaded.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.NextPhase,
            }).Accepted, Is.True);
            Assert.That(reloaded.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectOperation,
                PrimaryId = "meditate",
            }).Accepted, Is.True);
        }

        [TestCase("buqi-run-save-v1")]
        [TestCase("buqi-run-save-v99")]
        public void TryCreate_UnsupportedSaveSchemaStartsFreshRunAndWritesCurrentSchema(string unsupportedVersion)
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            CreateController(store);
            BuqiRunSaveData save = ReadSave(store);
            save.SaveVersion = unsupportedVersion;
            store.SetJson(BuqiRunSaveCodec.ToJson(save));

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out string error),
                Is.True,
                error);

            Assert.That(reloaded.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Assert.That(ReadSave(store).SaveVersion, Is.EqualTo(BuqiRunSaveData.CurrentSaveVersion));
            Assert.That(ReadSave(store).RunSeed, Is.EqualTo(1L));
            Assert.That(store.Deletes, Is.EqualTo(0));
        }

        [Test]
        public void TryCreate_MissingSaveSchemaFailsClosedAndPreservesStoredBytes()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            CreateController(store);
            BuqiRunSaveData save = ReadSave(store);
            save.SaveVersion = string.Empty;
            string originalJson = BuqiRunSaveCodec.ToJson(save);
            store.SetJson(originalJson);
            int writesBeforeReload = store.Writes;

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out string error),
                Is.False);

            Assert.That(reloaded, Is.Null);
            Assert.That(error, Does.Contain("存档校验失败"));
            Assert.That(store.CurrentJson, Is.EqualTo(originalJson));
            Assert.That(store.Writes, Is.EqualTo(writesBeforeReload));
            Assert.That(store.Deletes, Is.EqualTo(0));
        }

        [Test]
        public void TryCreate_UnsupportedV3SaveStartsFreshRun()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            CreateController(store);
            BuqiRunSaveData save = ReadSave(store);
            save.Day = 7;
            save.Coins = 1;
            save.SaveVersion = BuqiRunSaveData.PreviousSaveVersion;
            save.RuleVersion = BuqiRunState.PreviousRuleVersion;
            store.SetJson(BuqiRunSaveCodec.ToJson(save));

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out string error),
                Is.True,
                error);

            BuqiRunSaveData restarted = ReadSave(store);
            Assert.That(reloaded.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Assert.That(restarted.SaveVersion, Is.EqualTo(BuqiRunSaveData.CurrentSaveVersion));
            Assert.That(restarted.RuleVersion, Is.EqualTo(BuqiRunState.CurrentRuleVersion));
            Assert.That(restarted.Day, Is.EqualTo(1));
            Assert.That(restarted.Coins, Is.EqualTo(BuqiRunRules.StartingCoins));
        }

        [Test]
        public void TryCreate_CurrentValidSaveRestoresProgressWithoutRewriting()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            SelectOperation(controller, "meditate");
            int writesBeforeReload = store.Writes;
            string jsonBeforeReload = store.CurrentJson;

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out string error),
                Is.True,
                error);

            Assert.That(reloaded.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Assert.That(store.Writes, Is.EqualTo(writesBeforeReload));
            Assert.That(store.CurrentJson, Is.EqualTo(jsonBeforeReload));
        }

        [Test]
        public void TryCreate_ContentReplacementWriteFailurePreservesOldSave()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            CreateController(store);
            BuqiRunSaveData save = ReadSave(store);
            save.ContentVersion = "buqi-effects-cv1";
            string originalJson = BuqiRunSaveCodec.ToJson(save);
            store.SetJson(originalJson);
            store.FailNextWrite("disk is full");

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out string error),
                Is.False);

            Assert.That(reloaded, Is.Null);
            Assert.That(error, Does.Contain("旧存档"));
            Assert.That(store.CurrentJson, Is.EqualTo(originalJson));
        }

        [Test]
        public void TryCreate_ReadIoFailurePreservesExistingSaveAndDoesNotWrite()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            store.FailNextRead("permission denied");

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.False);

            Assert.That(controller, Is.Null);
            Assert.That(error, Does.Contain("读取存档失败"));
            Assert.That(store.Writes, Is.EqualTo(0));
        }

        [Test]
        public void TryCreate_PendingSettlementReadFailurePreservesPendingSave()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            AdvanceUntilPhase(controller, BuqiUIDemoPhase.BattleReplay);
            BuqiRunSaveData save = ReadSave(store);
            BuqiRunBattleSummary summary = BuildSummary(controller);
            save.PendingSettlement = CreatePendingSettlement(summary, save.Revision, BuqiRunBattleKind.Pve,
                controller.CurrentReplay.Result.Outcome);
            save.HasPendingSettlement = true;
            store.SetJson(BuqiRunSaveCodec.ToJson(save));
            string originalJson = store.CurrentJson;
            store.FailReadOnAttempt(2, "temporary read failure");

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out string error),
                Is.False);

            Assert.That(reloaded, Is.Null);
            Assert.That(error, Does.Contain("待结算"));
            Assert.That(store.CurrentJson, Is.EqualTo(originalJson));
        }

        [Test]
        public void TryCreate_InvalidEconomyPayloadFailsClosed()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);

            BuqiRunSaveData save = ReadSave(store);
            string starterInstanceId = save.BoardInstanceIds.Single(id => !string.IsNullOrEmpty(id));
            save.EconomyPayload =
                "{\"NextItemOrdinal\":2,\"Items\":[{\"InstanceId\":\"" + starterInstanceId +
                "\",\"DefinitionId\":\"item-01\",\"Quality\":99,\"RefinementId\":\"\"}]}";
            string originalJson = BuqiRunSaveCodec.ToJson(save);
            store.SetJson(originalJson);

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out error),
                Is.False);

            Assert.That(reloaded, Is.Null);
            Assert.That(error, Does.Contain("存档校验失败"));
            Assert.That(store.CurrentJson, Is.EqualTo(originalJson));
        }

        [Test]
        public void TryCreateNewRun_ExplicitErrorRestartOverwritesInvalidPayload()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            BuqiUIDemoController controller = CreateController(store);
            BuqiRunSaveData save = ReadSave(store);
            save.EconomyPayload = "{invalid-economy";
            string invalidJson = BuqiRunSaveCodec.ToJson(save);
            store.SetJson(invalidJson);

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out _,
                    out _),
                Is.False);

            Assert.That(
                BuqiUIDemoController.TryCreateNewRun(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController restarted,
                    out string restartError),
                Is.True,
                restartError);

            Assert.That(restarted.View.Phase, Is.EqualTo(BuqiUIDemoPhase.PeriodTransition));
            Assert.That(store.CurrentJson, Is.Not.EqualTo(invalidJson));
            Assert.That(ReadSave(store).ContentVersion, Is.EqualTo("test-content-v1"));
        }

        [Test]
        public void TryCreate_InvalidEncounterPayloadFailsClosed()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);

            BuqiRunSaveData save = ReadSave(store);
            save.EncounterPayload =
                "{\"EncounterId\":\"enc-bad\",\"Kind\":0,\"Day\":99,\"EncounterIndex\":1,\"NextRngCursor\":7,\"Resolved\":false,\"ResolutionId\":\"\",\"SelectedChoiceId\":\"\",\"CandidateIds\":[\"item-02\",\"item-02\"]}";
            string originalJson = BuqiRunSaveCodec.ToJson(save);
            store.SetJson(originalJson);

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out error),
                Is.False);

            Assert.That(reloaded, Is.Null);
            Assert.That(error, Does.Contain("存档校验失败"));
            Assert.That(store.CurrentJson, Is.EqualTo(originalJson));
        }

        [Test]
        public void TryCreate_IncompleteBattlePayloadFailsClosed()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);
            AdvanceUntilPhase(controller, BuqiUIDemoPhase.BattleReplay);

            BuqiRunSaveData save = ReadSave(store);
            save.BattlePayload = "{\"BattleId\":\"battle-only\"}";
            string originalJson = BuqiRunSaveCodec.ToJson(save);
            store.SetJson(originalJson);

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out error),
                Is.False);

            Assert.That(reloaded, Is.Null);
            Assert.That(error, Does.Contain("存档校验失败"));
            Assert.That(store.CurrentJson, Is.EqualTo(originalJson));
        }

        [Test]
        public void ReloadAfterSettledPve_ReturnsToBattleSummaryBeforePvpWithoutResimulation()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);

            AdvanceUntilPhase(controller, BuqiUIDemoPhase.BattleSummary);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleSummary));
            string persistedJson = store.CurrentJson;
            string titleBefore = controller.View.ContextTitle;
            string bodyBefore = controller.View.ContextBody;
            string[] factsBefore = controller.View.Facts.Select(fact => fact.Body).ToArray();
            string opponentBefore = controller.CurrentReplay.RightName;

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out error),
                Is.True,
                error);

            Assert.That(reloaded.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleSummary));
            Assert.That(reloaded.View.ContextTitle, Is.EqualTo(titleBefore));
            Assert.That(reloaded.View.ContextBody, Is.EqualTo(bodyBefore));
            Assert.That(reloaded.View.Facts.Select(fact => fact.Body), Is.EqualTo(factsBefore));
            Assert.That(reloaded.CurrentReplay.RightName, Is.EqualTo(opponentBefore));
            Assert.That(store.CurrentJson, Is.EqualTo(persistedJson));
        }

        [Test]
        public void ReloadAfterSettledPvp_ReturnsToBattleSummaryWithoutResimulation()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);

            AdvanceUntilPhase(controller, BuqiUIDemoPhase.BattleSummary, summaryCountTarget: 2);
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleSummary));
            string persistedJson = store.CurrentJson;
            string titleBefore = controller.View.ContextTitle;
            string bodyBefore = controller.View.ContextBody;
            string[] factsBefore = controller.View.Facts.Select(fact => fact.Body).ToArray();
            string opponentBefore = controller.CurrentReplay.RightName;

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController reloaded,
                    out error),
                Is.True,
                error);

            Assert.That(reloaded.View.Phase, Is.EqualTo(BuqiUIDemoPhase.BattleSummary));
            Assert.That(reloaded.View.ContextTitle, Is.EqualTo(titleBefore));
            Assert.That(reloaded.View.ContextBody, Is.EqualTo(bodyBefore));
            Assert.That(reloaded.View.Facts.Select(fact => fact.Body), Is.EqualTo(factsBefore));
            Assert.That(reloaded.CurrentReplay.RightName, Is.EqualTo(opponentBefore));
            Assert.That(store.CurrentJson, Is.EqualTo(persistedJson));
        }

        [Test]
        public void ApplyDeployment_RejectsDuplicateOrDroppedInstances()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);

            string boardInstanceId = controller.View.BoardSlots.Single(slot => !slot.Empty).Id;
            var board = Slots(BuqiRunRules.BoardSlotCount);
            var storage = Slots(BuqiRunRules.StorageSlotCount);
            board[0] = boardInstanceId;
            board[1] = boardInstanceId;
            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.ApplyDeployment,
                Deployment = new BuqiDeploymentSnapshot(board, storage),
            });

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("操作未完成，请重试。"));
        }

        [Test]
        public void EventWithGrantedRefinementId_FailsClosedInsteadOfSilentlyIgnoring()
        {
            BuqiUIDemoCatalog catalog = CreateCatalog();
            var store = new MemoryRunStore();
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store, runSeed: 2L),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);
            SelectOperation(controller, "event");
            Assert.That(controller.View.Phase, Is.EqualTo(BuqiUIDemoPhase.Event));

            BuqiRunSaveData save = ReadSave(store);
            save.EncounterPayload =
                "{\"EncounterId\":\"enc-refine\",\"Kind\":1,\"Day\":1,\"EncounterIndex\":0,\"NextRngCursor\":1,\"Resolved\":false,\"ResolutionId\":\"\",\"SelectedChoiceId\":\"\",\"CandidateIds\":[\"event-refine\"]}";
            store.SetJson(BuqiRunSaveCodec.ToJson(save));

            Assert.That(
                BuqiUIDemoController.TryCreate(
                    catalog,
                    CreateOptions(store, runSeed: 2L),
                    out controller,
                    out error),
                Is.True,
                error);

            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectChoice,
                PrimaryId = "event-refine",
            });

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("操作未完成，请重试。"));
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
            {
                return new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.SelectOperation,
                    PrimaryId = "meditate",
                };
            }

            if (view.Phase == BuqiUIDemoPhase.PveSelection)
            {
                return new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.SelectPveDifficulty,
                    PrimaryId = view.Choices[0].Id,
                };
            }

            if (view.Phase == BuqiUIDemoPhase.TribulationRoute)
            {
                return new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.SelectTribulationRoute,
                    PrimaryId = "face-thunder",
                    Slot = 0,
                };
            }

            if (view.Phase == BuqiUIDemoPhase.TribulationStage)
                return new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.ResolveTribulationStage };

            if (view.Phase == BuqiUIDemoPhase.Shop)
            {
                return new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.NextPhase };
            }

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

        private static void AdvanceUntilPhase(
            BuqiUIDemoController controller,
            BuqiUIDemoPhase targetPhase,
            int summaryCountTarget = 1)
        {
            int summaryCount = controller.View.Phase == BuqiUIDemoPhase.BattleSummary ? 1 : 0;
            int guard = 0;
            while (guard++ < 64)
            {
                if (controller.View.Phase == targetPhase &&
                    (targetPhase != BuqiUIDemoPhase.BattleSummary || summaryCount >= summaryCountTarget))
                {
                    return;
                }

                BuqiUIDemoCommandResult step = controller.Execute(SelectProgressCommand(controller.View));
                Assert.That(step.Accepted, Is.True, step.Reason);
                if (controller.View.Phase == BuqiUIDemoPhase.BattleSummary)
                    summaryCount++;
            }

            Assert.Fail("Target phase was not reached.");
        }

        private static BuqiDeploymentSnapshot BuildDeploymentSnapshot(BuqiUIDemoController controller)
        {
            var board = controller.View.BoardSlots.Select(slot => slot.Empty ? string.Empty : slot.Id).ToList();
            var storage = controller.View.StorageSlots.Select(slot => slot.Empty ? string.Empty : slot.Id).ToList();
            return new BuqiDeploymentSnapshot(board, storage);
        }

        private static BuqiUIDemoController CreateController(MemoryRunStore store)
        {
            Assert.That(
                BuqiUIDemoController.TryCreate(
                    CreateCatalog(),
                    CreateOptions(store),
                    out BuqiUIDemoController controller,
                    out string error),
                Is.True,
                error);
            return controller;
        }

        private static BuqiUIDemoController CreateWinningController(MemoryRunStore store)
        {
            BuqiConfigCatalog source = CreateSourceCatalog();
            source.Items.Add(new BuqiItemConfigRow
            {
                DefinitionId = "item-00-guaranteed-win",
                DisplayName = "Guaranteed Win",
                Size = BattleSize.S,
                BasePrice = 1,
                BaseCooldownTicks = 1,
                Effects =
                {
                    new BuqiEffectConfigRow
                    {
                        Trigger = Game.Hot.Buqi.Battle.BuqiTrigger.OnUse,
                        Effect = Game.Hot.Buqi.Battle.BuqiEffect.Damage,
                        Target = Game.Hot.Buqi.Battle.BuqiTarget.EnemyExecution,
                        Amount = 200,
                        ReasonCode = "test-guaranteed-win",
                    },
                },
            });
            Assert.That(BuqiUIDemoCatalog.TryCreate(
                source, out BuqiUIDemoCatalog catalog, out string catalogError), Is.True, catalogError);
            Assert.That(BuqiUIDemoController.TryCreate(
                catalog,
                CreateOptions(store),
                out BuqiUIDemoController controller,
                out string error), Is.True, error);
            return controller;
        }

        private static BuqiUIDemoController CreateControllerOnPhase(MemoryRunStore store, BuqiUIDemoPhase phase)
        {
            for (int seed = 1; seed <= 64; seed++)
            {
                store.Reset();
                if (!BuqiUIDemoController.TryCreate(
                        CreateCatalog(),
                        CreateOptions(store, seed),
                        out BuqiUIDemoController controller,
                        out string error))
                {
                    Assert.Fail(error);
                }

                if (phase == BuqiUIDemoPhase.Shop || phase == BuqiUIDemoPhase.Event)
                {
                    SelectOperation(controller, phase == BuqiUIDemoPhase.Shop ? "bazaar" : "event");
                }

                if (controller.View.Phase == phase)
                    return controller;
            }

            Assert.Fail($"Unable to find seed for phase {phase}.");
            return null;
        }

        private static BuqiUIDemoControllerOptions CreateOptions(MemoryRunStore store, long runSeed = 1L)
        {
            return new BuqiUIDemoControllerOptions
            {
                Store = store,
                RunSeed = runSeed,
                PveOpponentIds = new[] { "pve-a", "pve-b", "pve-c" },
                PvpOpponentIds = new[] { "pvp-a", "pvp-b" },
            };
        }

        private static BuqiUIDemoCatalog CreateCatalog()
        {
            BuqiConfigCatalog source = CreateSourceCatalog();
            Assert.That(BuqiUIDemoCatalog.TryCreate(source, out BuqiUIDemoCatalog catalog, out string error), Is.True, error);
            return catalog;
        }

        private static BuqiConfigCatalog CreateSourceCatalog()
        {
            var catalog = new BuqiConfigCatalog
            {
                Global = new BuqiGlobalConfigRow
                {
                    ContentVersion = "test-content-v1",
                    BoardSlotCount = BuqiRunRules.BoardSlotCount,
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
                    BaseCooldownTicks = 10 + index,
                });
            }

            for (int index = 1; index <= 3; index++)
            {
                catalog.Refinements.Add(new BuqiRefinementConfigRow
                {
                    RefinementId = $"A-0{index}",
                    DisplayName = $"Refine {index}",
                    Summary = $"Refine summary {index}",
                });
            }

            catalog.Echoes.Add(CreateEcho("pve-a", "PVE Alpha", "item-02", "item-03"));
            catalog.Echoes.Add(CreateEcho("pve-b", "PVE Beta", "item-03", "item-04"));
            catalog.Echoes.Add(CreateEcho("pve-c", "PVE Gamma", "item-04", "item-05"));
            catalog.Echoes.Add(CreateEcho("pvp-a", "PVP Alpha", "item-05", "item-06"));
            catalog.Echoes.Add(CreateEcho("pvp-b", "PVP Beta", "item-07", "item-08"));
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

        private static BuqiRunSaveData ReadSave(MemoryRunStore store)
        {
            Assert.That(BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData saveData, out string error), Is.True, error);
            return saveData;
        }

        private static void SelectOperation(BuqiUIDemoController controller, string operationId)
        {
            if (controller.View.Phase == BuqiUIDemoPhase.PeriodTransition)
            {
                BuqiUIDemoCommandResult continued = controller.Execute(
                    new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.NextPhase });
                Assert.That(continued.Accepted, Is.True, continued.Reason);
            }
            BuqiUIDemoCommandResult result = controller.Execute(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.SelectOperation,
                PrimaryId = operationId,
            });
            Assert.That(result.Accepted, Is.True, result.Reason);
        }

        private static BuqiRunBattleSummary BuildSummary(BuqiUIDemoController controller)
        {
            return BuqiRunBattleSummaryBuilder.Build(controller.CurrentReplay.Result, controller.CurrentReplay.Log);
        }

        private static BuqiRunPendingSettlement CreatePendingSettlement(
            BuqiRunBattleSummary summary,
            int revision,
            BuqiRunBattleKind battleKind,
            BattleOutcome outcome)
        {
            return new BuqiRunPendingSettlement
            {
                SettlementId = "settlement:pending-test",
                ExpectedRevision = revision,
                BattleKind = (int)battleKind,
                RawOutcome = (int)MapRawOutcome(outcome),
                BattleLogHash = summary.BattleLogHash,
                Summary = summary,
            };
        }

        private static BuqiRunRawBattleOutcome MapRawOutcome(BattleOutcome outcome)
        {
            switch (outcome)
            {
                case BattleOutcome.LeftWin:
                    return BuqiRunRawBattleOutcome.PlayerWin;
                case BattleOutcome.RightWin:
                    return BuqiRunRawBattleOutcome.OpponentWin;
                case BattleOutcome.Draw:
                    return BuqiRunRawBattleOutcome.Draw;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }
        }

        private static List<string> Slots(int count)
        {
            var result = new List<string>(count);
            for (int index = 0; index < count; index++)
                result.Add(string.Empty);
            return result;
        }

        private static RunFingerprint CaptureRuntime(BuqiUIDemoController controller)
        {
            object orchestrator = typeof(BuqiUIDemoController)
                .GetField("m_Orchestrator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(controller);
            object state = orchestrator.GetType().GetProperty("State").GetValue(orchestrator, null);
            object economy = state.GetType().GetField("Economy").GetValue(state);
            BuqiRunEconomySnapshot snapshot = (BuqiRunEconomySnapshot)economy;
            return new RunFingerprint
            {
                Phase = snapshot.Run.Phase,
                Day = snapshot.Run.Day,
                EncounterIndex = snapshot.Run.EncounterIndex,
                RngCursor = snapshot.Run.RngCursor,
                Revision = snapshot.Run.Revision,
                Coins = snapshot.Run.Coins,
                Wins = snapshot.Run.Wins,
                Lives = snapshot.Run.Lives,
                Board = string.Join("|", snapshot.Run.BoardInstanceIds),
                Storage = string.Join("|", snapshot.Run.StorageInstanceIds),
                ViewPhase = controller.View.Phase,
            };
        }

        private sealed class MemoryRunStore : IBuqiRunStore
        {
            public MemoryRunStore(string currentJson = null)
            {
                CurrentJson = currentJson;
            }

            public string CurrentJson { get; private set; }
            public int Writes { get; private set; }
            public int Deletes { get; private set; }
            private string NextWriteError { get; set; }
            private string NextReadError { get; set; }
            private int m_ReadAttempt;
            private int m_FailReadAttempt = -1;
            private string m_FailReadAttemptError;

            public bool TryRead(out string json, out string error)
            {
                m_ReadAttempt++;
                if (!string.IsNullOrEmpty(NextReadError))
                {
                    json = string.Empty;
                    error = NextReadError;
                    NextReadError = null;
                    return false;
                }

                if (m_ReadAttempt == m_FailReadAttempt)
                {
                    json = string.Empty;
                    error = m_FailReadAttemptError;
                    m_FailReadAttempt = -1;
                    return false;
                }

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
                if (!string.IsNullOrEmpty(NextWriteError))
                {
                    error = NextWriteError;
                    NextWriteError = null;
                    return false;
                }

                CurrentJson = json;
                Writes++;
                error = string.Empty;
                return true;
            }

            public bool TryDelete(out string error)
            {
                Deletes++;
                CurrentJson = null;
                error = string.Empty;
                return true;
            }

            public void FailNextWrite(string error)
            {
                NextWriteError = error;
            }

            public void FailNextRead(string error)
            {
                NextReadError = error;
            }

            public void FailReadOnAttempt(int attempt, string error)
            {
                m_FailReadAttempt = m_ReadAttempt + attempt;
                m_FailReadAttemptError = error;
            }

            public void SetJson(string json)
            {
                CurrentJson = json;
            }

            public void Reset()
            {
                CurrentJson = null;
                NextWriteError = null;
                NextReadError = null;
                m_ReadAttempt = 0;
                m_FailReadAttempt = -1;
                m_FailReadAttemptError = null;
                Writes = 0;
                Deletes = 0;
            }
        }

        private sealed class RunFingerprint
        {
            public BuqiRunPhase Phase;
            public int Day;
            public int EncounterIndex;
            public int RngCursor;
            public int Revision;
            public int Coins;
            public int Wins;
            public int Lives;
            public string Board = string.Empty;
            public string Storage = string.Empty;
            public BuqiUIDemoPhase ViewPhase;

            public override bool Equals(object obj)
            {
                if (obj is not RunFingerprint other)
                    return false;

                return Phase == other.Phase &&
                       Day == other.Day &&
                       EncounterIndex == other.EncounterIndex &&
                       RngCursor == other.RngCursor &&
                       Revision == other.Revision &&
                       Coins == other.Coins &&
                       Wins == other.Wins &&
                       Lives == other.Lives &&
                       string.Equals(Board, other.Board, StringComparison.Ordinal) &&
                       string.Equals(Storage, other.Storage, StringComparison.Ordinal) &&
                       ViewPhase == other.ViewPhase;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (int)Phase;
                    hashCode = (hashCode * 397) ^ Day;
                    hashCode = (hashCode * 397) ^ EncounterIndex;
                    hashCode = (hashCode * 397) ^ RngCursor;
                    hashCode = (hashCode * 397) ^ Revision;
                    hashCode = (hashCode * 397) ^ Coins;
                    hashCode = (hashCode * 397) ^ Wins;
                    hashCode = (hashCode * 397) ^ Lives;
                    hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Board);
                    hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Storage);
                    hashCode = (hashCode * 397) ^ (int)ViewPhase;
                    return hashCode;
                }
            }
        }
    }
}
