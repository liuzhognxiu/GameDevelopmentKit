using System;
using System.Collections.Generic;
using System.IO;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Settlement;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunSettlementTests
    {
        [Test]
        public void BuildSummary_UsesStableAggregationFromRealBattleEvents()
        {
            BattleResult result = CreateBattleResult(BattleOutcome.Draw, "hash-summary");
            var log = new List<BattleEvent>
            {
                CreateEvent(5, 9, BuqiEventType.Effect, 9, "gamma", "target-3", "StormDamage"),
                CreateEvent(2, 3, BuqiEventType.Truncate, 0, string.Empty, string.Empty, "ChainBreak"),
                CreateEvent(1, 2, BuqiEventType.Effect, 5, "beta", "target-1", "Damage"),
                CreateEvent(0, 1, BuqiEventType.Declare, 0, "alpha", string.Empty, "Declare"),
                CreateEvent(4, 7, BuqiEventType.Effect, 7, "alpha", "target-2", "Damage"),
                CreateEvent(3, 5, BuqiEventType.Effect, -3, "beta", "target-2", "Damage"),
                CreateEvent(6, 11, BuqiEventType.Effect, 7, "beta", "target-4", "Damage"),
            };

            BuqiRunBattleSummary summary = BuqiRunBattleSummaryBuilder.Build(result, log);

            Assert.That(summary.RawOutcome, Is.EqualTo(BattleOutcome.Draw));
            Assert.That(summary.BattleLogHash, Is.EqualTo("hash-summary"));
            Assert.That(summary.TopSourceInstanceId, Is.EqualTo("beta"));
            Assert.That(summary.TopContribution, Is.EqualTo(12));
            Assert.That(summary.KeyInterruptionReason, Is.EqualTo("ChainBreak"));
            Assert.That(summary.OverloadLoss, Is.EqualTo(9));
            Assert.That(summary.FactLines, Is.EqualTo(new[]
            {
                "主要贡献：beta 累计 12",
                "关键中断：ChainBreak",
                "风险损失：9",
            }));
        }

        [Test]
        public void BuildSummary_BreaksTopContributionTiesByOrdinalSourceId()
        {
            BattleResult result = CreateBattleResult(BattleOutcome.LeftWin, "hash-tie");
            var log = new List<BattleEvent>
            {
                CreateEvent(1, 2, BuqiEventType.Effect, 4, "source-b", "target-1", "Damage"),
                CreateEvent(0, 1, BuqiEventType.Effect, 4, "source-a", "target-2", "Damage"),
                CreateEvent(3, 4, BuqiEventType.Effect, 6, "source-b", "target-3", "Damage"),
                CreateEvent(2, 3, BuqiEventType.Effect, 6, "source-a", "target-4", "Damage"),
            };

            BuqiRunBattleSummary summary = BuqiRunBattleSummaryBuilder.Build(result, log);

            Assert.That(summary.TopSourceInstanceId, Is.EqualTo("source-a"));
            Assert.That(summary.TopContribution, Is.EqualTo(10));
        }

        [Test]
        public void BuildSummary_ReturnsNoFactLinesWhenBattleLogIsEmpty()
        {
            BattleResult result = CreateBattleResult(BattleOutcome.RightWin, "hash-empty");

            BuqiRunBattleSummary summary =
                BuqiRunBattleSummaryBuilder.Build(result, Array.Empty<BattleEvent>());

            Assert.That(summary.RawOutcome, Is.EqualTo(BattleOutcome.RightWin));
            Assert.That(summary.BattleLogHash, Is.EqualTo("hash-empty"));
            Assert.That(summary.TopSourceInstanceId, Is.Empty);
            Assert.That(summary.TopContribution, Is.EqualTo(0));
            Assert.That(summary.KeyInterruptionReason, Is.Empty);
            Assert.That(summary.OverloadLoss, Is.EqualTo(0));
            Assert.That(summary.FactLines, Is.Empty);
        }

        [Test]
        public void SaveCodec_RoundTripsCoreFieldsOpaquePayloadsAndPendingSettlement()
        {
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PvpBattle, revision: 6, wins: 4, lives: 2);
            state.RunSeed = 9001L;
            state.RngCursor = 13;
            state.Day = 5;
            state.Coins = 27;
            state.BoardInstanceIds = CreateSlots("board-a", "board-b");
            state.StorageInstanceIds = CreateSlots("storage-a", "storage-b", "storage-c");
            state.AppliedCommandIds.Add("cmd-20");
            state.AppliedCommandIds.Add("cmd-03");
            state.AppliedCommandIds.Add("cmd-11");
            state.AppliedSettlementIds.Add("settle-02");
            state.AppliedSettlementIds.Add("settle-01");

            BuqiRunPendingSettlement pending = CreatePendingSettlement(
                "settle-pending",
                state.Revision,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.Draw,
                "pending-hash");

            BuqiRunSaveData save = BuqiRunSaveCodec.FromState(
                state,
                "economy-json",
                "encounter-json",
                "battle-json",
                pending);

            Assert.That(save.AppliedCommandIds, Is.EqualTo(new[] { "cmd-03", "cmd-11", "cmd-20" }));
            Assert.That(save.AppliedSettlementIds, Is.EqualTo(new[] { "settle-01", "settle-02" }));

            string json = BuqiRunSaveCodec.ToJson(save);

            Assert.That(BuqiRunSaveCodec.TryFromJson(json, out BuqiRunSaveData parsed, out string parseError), Is.True, parseError);
            Assert.That(BuqiRunSaveCodec.TryToState(parsed, out BuqiRunState roundTripped, out string stateError), Is.True, stateError);

            AssertStateEquals(state, roundTripped);
            Assert.That(parsed.EconomyPayload, Is.EqualTo("economy-json"));
            Assert.That(parsed.EncounterPayload, Is.EqualTo("encounter-json"));
            Assert.That(parsed.BattlePayload, Is.EqualTo("battle-json"));
            Assert.That(parsed.HasPendingSettlement, Is.True);
            Assert.That(parsed.PendingSettlement, Is.Not.Null);
            Assert.That(parsed.PendingSettlement!.SettlementId, Is.EqualTo("settle-pending"));
            Assert.That(parsed.PendingSettlement.Summary.BattleLogHash, Is.EqualTo("pending-hash"));
            Assert.That(parsed.PendingSettlement.Summary.FactLines, Is.EqualTo(new[] { "主要贡献：source-x 累计 9" }));
        }

        [Test]
        public void SaveCodec_RoundTripsWithoutPendingSettlement()
        {
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 2, wins: 1, lives: 3);
            BuqiRunSaveData save = BuqiRunSaveCodec.FromState(state);

            string json = BuqiRunSaveCodec.ToJson(save);

            Assert.That(BuqiRunSaveCodec.TryFromJson(json, out BuqiRunSaveData parsed, out string error), Is.True, error);
            Assert.That(parsed.HasPendingSettlement, Is.False);
            Assert.That(parsed.PendingSettlement, Is.Null);
        }

        [Test]
        public void Tribulation_SaveCodecRoundTripsRouteStageAndOmenFields()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(901L, "content-2026-08-07");
            state.Day = BuqiRunRules.RunDayCount;
            state.EncounterIndex = BuqiRunRules.OperationsPerDay;
            state.Period = BuqiRunPeriod.NightPvp;
            state.Phase = BuqiRunPhase.TribulationStage;
            state.Wins = BuqiRunRules.WinsToVictory - 1;
            state.DaoSeals = 6;
            state.CurrentOmen = 1;
            state.TribulationRoute = BuqiTribulationRoute.QuestionHeart;
            state.TribulationDaoSealsSpent = 3;
            state.TribulationStage = 2;
            state.TribulationSuccesses = 1;

            string json = BuqiRunSaveCodec.ToJson(BuqiRunSaveCodec.FromState(state));

            Assert.That(BuqiRunSaveCodec.TryFromJson(json, out BuqiRunSaveData parsed, out string parseError), Is.True, parseError);
            Assert.That(BuqiRunSaveCodec.TryToState(parsed, out BuqiRunState loaded, out string stateError), Is.True, stateError);
            Assert.That(loaded.Period, Is.EqualTo(BuqiRunPeriod.NightPvp));
            Assert.That(loaded.DaoSeals, Is.EqualTo(6));
            Assert.That(loaded.CurrentOmen, Is.EqualTo(1));
            Assert.That(loaded.TribulationRoute, Is.EqualTo(BuqiTribulationRoute.QuestionHeart));
            Assert.That(loaded.TribulationDaoSealsSpent, Is.EqualTo(3));
            Assert.That(loaded.TribulationStage, Is.EqualTo(2));
            Assert.That(loaded.TribulationSuccesses, Is.EqualTo(1));
        }

        [Test]
        public void TryFromJson_RejectsLegacyAndUnknownVersions()
        {
            BuqiRunState legacyState = CreateBattleState(BuqiRunPhase.PvpBattle, revision: 6, wins: 4, lives: 2);
            legacyState.Day = 5;
            BuqiRunSaveData legacy = BuqiRunSaveCodec.FromState(legacyState);
            legacy.SaveVersion = "buqi-run-save-v2";
            legacy.RuleVersion = "buqi-day-run-rule-v1";
            legacy.EncounterIndex = 3;

            Assert.That(BuqiRunSaveCodec.TryFromJson(
                BuqiRunSaveCodec.ToJson(legacy),
                out _,
                out _,
                out BuqiRunSaveFailureKind legacyFailure), Is.False);
            Assert.That(legacyFailure, Is.EqualTo(BuqiRunSaveFailureKind.UnsupportedVersion));

            legacy.SaveVersion = "buqi-run-save-v0";
            Assert.That(BuqiRunSaveCodec.TryFromJson(
                BuqiRunSaveCodec.ToJson(legacy),
                out _,
                out _,
                out BuqiRunSaveFailureKind unknownFailure), Is.False);
            Assert.That(unknownFailure, Is.EqualTo(BuqiRunSaveFailureKind.UnsupportedVersion));
        }

        [Test]
        public void TryToState_RejectsImpossibleSealRelationshipsAndOmenRange()
        {
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 2, wins: 4, lives: 2);
            BuqiRunSaveData impossibleSeals = BuqiRunSaveCodec.FromState(state);
            impossibleSeals.DaoSeals--;
            AssertRejected(impossibleSeals);

            BuqiRunSaveData impossibleOmen = BuqiRunSaveCodec.FromState(state);
            impossibleOmen.CurrentOmen = BuqiRunRules.MaxOmen + 1;
            AssertRejected(impossibleOmen);
        }

        [Test]
        public void TryFromJson_RejectsEmptyAndMalformedPayloads()
        {
            Assert.That(BuqiRunSaveCodec.TryFromJson(string.Empty, out _, out string emptyError), Is.False);
            Assert.That(emptyError, Is.Not.Empty);

            Assert.That(BuqiRunSaveCodec.TryFromJson("{not-json", out _, out string malformedError), Is.False);
            Assert.That(malformedError, Is.Not.Empty);
        }

        [Test]
        public void TryToState_RejectsVersionSlotDuplicateAndTerminalMismatches()
        {
            BuqiRunSaveData wrongSaveVersion = CreateValidSaveData();
            wrongSaveVersion.SaveVersion = "wrong-save-version";
            AssertRejected(wrongSaveVersion);

            BuqiRunSaveData wrongRuleVersion = CreateValidSaveData();
            wrongRuleVersion.RuleVersion = "wrong-rule-version";
            AssertRejected(wrongRuleVersion);

            BuqiRunSaveData emptyContentVersion = CreateValidSaveData();
            emptyContentVersion.ContentVersion = string.Empty;
            AssertRejected(emptyContentVersion);

            BuqiRunSaveData shortBoard = CreateValidSaveData();
            shortBoard.BoardInstanceIds.RemoveAt(shortBoard.BoardInstanceIds.Count - 1);
            AssertRejected(shortBoard);

            BuqiRunSaveData duplicateItem = CreateValidSaveData();
            duplicateItem.StorageInstanceIds[0] = duplicateItem.BoardInstanceIds[0];
            AssertRejected(duplicateItem);

            BuqiRunSaveData terminalMismatch = CreateValidSaveData();
            terminalMismatch.Phase = (int)BuqiRunPhase.RunTerminal;
            terminalMismatch.Outcome = (int)BuqiRunOutcome.None;
            AssertRejected(terminalMismatch);

            BuqiRunSaveData hiddenPendingSettlement = CreateValidSaveData();
            hiddenPendingSettlement.HasPendingSettlement = false;
            AssertRejected(hiddenPendingSettlement);
        }

        [Test]
        public void TryToState_RejectsTamperedPendingSummaryOutcomeMappings()
        {
            BuqiRunSaveData playerWinButRightWinSummary = CreateValidSaveData();
            playerWinButRightWinSummary.PendingSettlement.RawOutcome = (int)BuqiRunRawBattleOutcome.PlayerWin;
            playerWinButRightWinSummary.PendingSettlement.Summary.RawOutcome = BattleOutcome.RightWin;
            AssertRejected(playerWinButRightWinSummary);

            BuqiRunSaveData drawButInvalidBuildSummary = CreateValidSaveData();
            drawButInvalidBuildSummary.PendingSettlement.RawOutcome = (int)BuqiRunRawBattleOutcome.Draw;
            drawButInvalidBuildSummary.PendingSettlement.Summary.RawOutcome = BattleOutcome.InvalidBuild;
            AssertRejected(drawButInvalidBuildSummary);

            BuqiRunSaveData drawButAbortedSummary = CreateValidSaveData();
            drawButAbortedSummary.PendingSettlement.RawOutcome = (int)BuqiRunRawBattleOutcome.Draw;
            drawButAbortedSummary.PendingSettlement.Summary.RawOutcome = BattleOutcome.Aborted;
            AssertRejected(drawButAbortedSummary);
        }

        [Test]
        public void TryToState_RejectsNegativeSummaryValuesAndNullFactLines()
        {
            BuqiRunSaveData negativeContribution = CreateValidSaveData();
            negativeContribution.PendingSettlement.Summary.TopContribution = -1;
            AssertRejected(negativeContribution);

            BuqiRunSaveData negativeOverloadLoss = CreateValidSaveData();
            negativeOverloadLoss.PendingSettlement.Summary.OverloadLoss = -2;
            AssertRejected(negativeOverloadLoss);

            BuqiRunSaveData nullFactLine = CreateValidSaveData();
            nullFactLine.PendingSettlement.Summary.FactLines.Add(null);
            AssertRejected(nullFactLine);
        }

        [Test]
        public void FileRunStore_RequiresAbsolutePathAndReplacesContentsViaTempFile()
        {
            Assert.That(() => new BuqiFileRunStore("relative-save.json"), Throws.TypeOf<ArgumentException>());

            string path = Path.Combine(Path.GetTempPath(), $"buqi-run-store-{Guid.NewGuid():N}.json");
            try
            {
                var store = new BuqiFileRunStore(path);

                Assert.That(store.TryWrite("{\"value\":1}", out string firstWriteError), Is.True, firstWriteError);
                Assert.That(File.Exists(path), Is.True);
                Assert.That(File.Exists(path + ".tmp"), Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo("{\"value\":1}"));

                Assert.That(store.TryWrite("{\"value\":2}", out string secondWriteError), Is.True, secondWriteError);
                Assert.That(File.ReadAllText(path), Is.EqualTo("{\"value\":2}"));

                Assert.That(store.TryRead(out string json, out string readError), Is.True, readError);
                Assert.That(json, Is.EqualTo("{\"value\":2}"));

                Assert.That(store.TryDelete(out string deleteError), Is.True, deleteError);
                Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                string tmpPath = path + ".tmp";
                if (File.Exists(tmpPath))
                {
                    File.Delete(tmpPath);
                }
            }
        }

        [Test]
        public void Coordinator_PersistsPendingResultBeforeCallingCoreAndClearsItAfterSuccess()
        {
            var store = new SpyRunStore();
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 4, wins: 2, lives: 3);
            bool observedPendingBeforeCore = false;

            var coordinator = new BuqiRunSettlementCoordinator(
                store,
                (controller, settlementId, expectedRevision, battleKind, rawOutcome) =>
                {
                    Assert.That(store.Writes, Has.Count.EqualTo(1));
                    Assert.That(
                        BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData pendingSave, out string error),
                        Is.True,
                        error);
                    observedPendingBeforeCore =
                        pendingSave.PendingSettlement != null &&
                        pendingSave.PendingSettlement.SettlementId == settlementId &&
                        pendingSave.PendingSettlement.BattleKind == (int)battleKind &&
                        pendingSave.PendingSettlement.RawOutcome == (int)rawOutcome;
                    return controller.SettleBattle(settlementId, expectedRevision, battleKind, rawOutcome);
                });

            BuqiRunSettlementResult result = coordinator.SettleBattle(
                state,
                "settle-win",
                CreateBattleResult(BattleOutcome.LeftWin, "hash-core-order"),
                CreateSummaryLog(),
                "eco-payload",
                "enc-payload",
                "battle-payload");

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(observedPendingBeforeCore, Is.True);
            Assert.That(result.State.Wins, Is.EqualTo(2));
            Assert.That(result.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
            Assert.That(result.State.Period, Is.EqualTo(BuqiRunPeriod.Hour4Operation));
            Assert.That(result.Summary.TopSourceInstanceId, Is.EqualTo("source-x"));
            Assert.That(store.Writes, Has.Count.EqualTo(2));
            Assert.That(
                BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData finalSave, out string finalError),
                Is.True,
                finalError);
            Assert.That(finalSave.PendingSettlement, Is.Null);
            Assert.That(finalSave.AppliedSettlementIds, Does.Contain("settle-win"));
            Assert.That(finalSave.BattlePayload, Is.EqualTo("battle-payload"));
        }

        [Test]
        public void Coordinator_WhenInitialWriteFails_DoesNotCallCoreAndLeavesStateUntouched()
        {
            var store = new SpyRunStore { FailWriteAtCall = 0 };
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 5, wins: 3, lives: 2);
            bool coreCalled = false;

            var coordinator = new BuqiRunSettlementCoordinator(
                store,
                (controller, settlementId, expectedRevision, battleKind, rawOutcome) =>
                {
                    coreCalled = true;
                    return controller.SettleBattle(settlementId, expectedRevision, battleKind, rawOutcome);
                });

            BuqiRunSettlementResult result = coordinator.SettleBattle(
                state,
                "settle-fail-write",
                CreateBattleResult(BattleOutcome.RightWin, "hash-fail-write"),
                CreateSummaryLog(),
                "eco",
                "enc",
                "battle");

            Assert.That(result.Success, Is.False);
            Assert.That(coreCalled, Is.False);
            Assert.That(store.Writes, Is.Empty);
            AssertStateEquals(state, result.State);
        }

        [Test]
        public void Coordinator_ResumesPendingSettlementExactlyOnceAfterFinalWriteFailure()
        {
            var store = new SpyRunStore { FailWriteAtCall = 1 };
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 7, wins: 0, lives: 3);
            var coordinator = new BuqiRunSettlementCoordinator(store);

            BuqiRunSettlementResult initial = coordinator.SettleBattle(
                state,
                "settle-resume",
                CreateBattleResult(BattleOutcome.LeftWin, "hash-resume"),
                CreateSummaryLog(),
                "eco-before",
                "enc-before",
                "battle-before");

            Assert.That(initial.Success, Is.False);
            Assert.That(store.Writes, Has.Count.EqualTo(1));
            Assert.That(
                BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData pendingSave, out string pendingError),
                Is.True,
                pendingError);
            Assert.That(pendingSave.PendingSettlement, Is.Not.Null);
            Assert.That(pendingSave.PendingSettlement!.SettlementId, Is.EqualTo("settle-resume"));

            store.FailWriteAtCall = -1;

            BuqiRunSettlementResult resumed = coordinator.ResumePendingSettlement();

            Assert.That(resumed.Success, Is.True, resumed.FailureReason);
            Assert.That(resumed.Replayed, Is.False);
            Assert.That(resumed.State.Wins, Is.Zero);
            Assert.That(resumed.State.AppliedSettlementIds, Does.Contain("settle-resume"));
            Assert.That(
                BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData finalSave, out string finalError),
                Is.True,
                finalError);
            Assert.That(finalSave.PendingSettlement, Is.Null);
            Assert.That(finalSave.AppliedSettlementIds, Does.Contain("settle-resume"));

            BuqiRunSettlementResult secondResume = coordinator.ResumePendingSettlement();

            Assert.That(secondResume.Success, Is.False);
            Assert.That(secondResume.FailureReason, Does.Contain("No pending settlement"));
            Assert.That(resumed.State.Wins, Is.Zero);
        }

        [Test]
        public void Coordinator_PersistsHeartTrialDefeatAndClearsPendingSettlement()
        {
            var store = new SpyRunStore();
            BuqiRunState state = CreateBattleState(
                BuqiRunPhase.PvpBattle,
                revision: 7,
                wins: 3,
                lives: 0);
            state.Day = 8;
            state.InTribulationTrial = true;
            state.HeartTrialUsed = true;
            var coordinator = new BuqiRunSettlementCoordinator(store);

            BuqiRunSettlementResult result = coordinator.SettleBattle(
                state,
                "heart-trial-defeat",
                CreateBattleResult(BattleOutcome.RightWin, "heart-trial-defeat-hash"),
                CreateSummaryLog(),
                "eco-heart-trial",
                "enc-heart-trial",
                "battle-heart-trial");

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.State.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));
            Assert.That(result.State.Outcome, Is.EqualTo(BuqiRunOutcome.Defeat));
            Assert.That(result.State.InTribulationTrial, Is.False);
            Assert.That(
                BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData save, out string error),
                Is.True,
                error);
            Assert.That(save.PendingSettlement, Is.Null);
            Assert.That(save.InTribulationTrial, Is.False);
        }

        [Test]
        public void Coordinator_PreservesDrawOutcomeInSummaryAndPayloadButAwardsPlayerWinInCore()
        {
            var store = new SpyRunStore();
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 2, wins: 7, lives: 3);
            var coordinator = new BuqiRunSettlementCoordinator(store);

            BuqiRunSettlementResult result = coordinator.SettleBattle(
                state,
                "settle-draw",
                CreateBattleResult(BattleOutcome.Draw, "hash-draw"),
                CreateSummaryLog(),
                "eco-draw",
                "enc-draw",
                "raw-draw-payload");

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.RawOutcome, Is.EqualTo(BuqiRunRawBattleOutcome.Draw));
            Assert.That(result.Summary.RawOutcome, Is.EqualTo(BattleOutcome.Draw));
            Assert.That(result.State.Wins, Is.EqualTo(7));
            Assert.That(result.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
            Assert.That(result.State.Period, Is.EqualTo(BuqiRunPeriod.Hour4Operation));
            Assert.That(
                BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData save, out string error),
                Is.True,
                error);
            Assert.That(save.BattlePayload, Is.EqualTo("raw-draw-payload"));
        }

        [Test]
        public void CoordinatorReplaysAppliedNinthPvpWinExactlyOnce()
        {
            var store = new SpyRunStore();
            var coordinator = new BuqiRunSettlementCoordinator(store);
            BuqiRunState state = CreateBattleState(
                BuqiRunPhase.PvpBattle,
                revision: 10,
                wins: BuqiRunRules.WinsToVictory - 2,
                lives: 2);
            state.Day = BuqiRunRules.RunDayCount;
            state.Period = BuqiRunPeriod.NightPvp;
            BattleResult battle = CreateBattleResult(BattleOutcome.LeftWin, "day-nine-hash");

            BuqiRunSettlementResult first = coordinator.SettleBattle(
                state,
                "settlement:day-nine-pvp",
                battle,
                CreateSummaryLog(),
                "eco",
                string.Empty,
                "battle");
            Assert.That(first.Success, Is.True, first.FailureReason);

            BuqiRunSettlementResult replay = coordinator.SettleBattle(
                first.State,
                "settlement:day-nine-pvp",
                battle,
                CreateSummaryLog(),
                "eco",
                string.Empty,
                "battle");

            Assert.That(first.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
            Assert.That(first.State.Wins, Is.EqualTo(BuqiRunRules.WinsToVictory - 1));
            Assert.That(replay.Success, Is.True, replay.FailureReason);
            Assert.That(replay.Replayed, Is.True);
            Assert.That(replay.State.Revision, Is.EqualTo(first.State.Revision));
            Assert.That(replay.State.Wins, Is.EqualTo(first.State.Wins));
            Assert.That(replay.State.DaoSeals, Is.EqualTo(first.State.DaoSeals));
        }

        [Test]
        public void BuqiNineDay_CoordinatorStaleReplayCannotOverwriteLaterProgress()
        {
            var store = new SpyRunStore();
            var coordinator = new BuqiRunSettlementCoordinator(store);
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 10, wins: 5, lives: 2);

            BuqiRunSettlementResult dusk = coordinator.SettleBattle(
                state,
                "settlement:dusk",
                CreateBattleResult(BattleOutcome.LeftWin, "dusk-hash"),
                CreateSummaryLog(),
                "eco-dusk",
                "enc-dusk",
                "battle-dusk");
            var run = new BuqiRunController(dusk.State);
            Assert.That(run.ResolveEncounter("hour-4", dusk.State.Revision).Success, Is.True);
            Assert.That(run.ResolveEncounter("hour-5", run.State.Revision).Success, Is.True);
            BuqiRunSettlementResult night = coordinator.SettleBattle(
                run.State,
                "settlement:night",
                CreateBattleResult(BattleOutcome.LeftWin, "night-hash"),
                CreateSummaryLog(),
                "eco-night",
                "enc-night",
                "battle-night");
            int writesBeforeReplay = store.Writes.Count;

            BuqiRunSettlementResult replay = coordinator.SettleBattle(
                dusk.State,
                "settlement:dusk",
                CreateBattleResult(BattleOutcome.LeftWin, "dusk-hash"),
                CreateSummaryLog(),
                "eco-dusk",
                "enc-dusk",
                "battle-dusk");

            Assert.That(replay.Success, Is.True, replay.FailureReason);
            Assert.That(replay.Replayed, Is.True);
            Assert.That(replay.State.Revision, Is.EqualTo(night.State.Revision));
            Assert.That(replay.State.AppliedSettlementIds, Does.Contain("settlement:night"));
            Assert.That(store.Writes, Has.Count.EqualTo(writesBeforeReplay));
            Assert.That(BuqiRunSaveCodec.TryFromJson(store.CurrentJson, out BuqiRunSaveData saved, out _), Is.True);
            Assert.That(saved.Revision, Is.EqualTo(night.State.Revision));
            Assert.That(saved.BattlePayload, Is.EqualTo("battle-night"));
        }

        [Test]
        public void BuqiNineDay_CoordinatorRejectsConflictingImmediateReplayPayload()
        {
            var store = new SpyRunStore();
            var coordinator = new BuqiRunSettlementCoordinator(store);
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 10, wins: 5, lives: 2);
            BuqiRunSettlementResult first = coordinator.SettleBattle(
                state,
                "settlement:dusk",
                CreateBattleResult(BattleOutcome.LeftWin, "dusk-hash"),
                CreateSummaryLog(),
                "eco",
                "enc",
                "battle-dusk");
            int writesBeforeReplay = store.Writes.Count;

            BuqiRunSettlementResult replay = coordinator.SettleBattle(
                first.State,
                "settlement:dusk",
                CreateBattleResult(BattleOutcome.RightWin, "conflicting-hash"),
                CreateSummaryLog(),
                "eco",
                "enc",
                "battle-dusk");

            Assert.That(replay.Success, Is.False);
            Assert.That(replay.FailureReason, Does.Contain("does not match"));
            Assert.That(store.Writes, Has.Count.EqualTo(writesBeforeReplay));
        }

        [Test]
        public void Coordinator_TransientReplayReadFailureFailsClosedWithoutWriting()
        {
            var store = new SpyRunStore();
            var coordinator = new BuqiRunSettlementCoordinator(store);
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 10, wins: 5, lives: 2);
            BuqiRunSettlementResult first = coordinator.SettleBattle(
                state,
                "settlement:read-failure",
                CreateBattleResult(BattleOutcome.LeftWin, "read-failure-hash"),
                CreateSummaryLog(),
                "eco",
                "enc",
                "battle");
            Assert.That(first.Success, Is.True, first.FailureReason);
            int writesBeforeReplay = store.Writes.Count;
            store.FailNextRead = true;

            BuqiRunSettlementResult replay = coordinator.SettleBattle(
                state,
                "settlement:read-failure",
                CreateBattleResult(BattleOutcome.LeftWin, "read-failure-hash"),
                CreateSummaryLog(),
                "eco",
                "enc",
                "battle");

            Assert.That(replay.Success, Is.False);
            Assert.That(replay.FailureReason, Does.Contain("read failed"));
            Assert.That(store.Writes, Has.Count.EqualTo(writesBeforeReplay));
        }

        [Test]
        public void Tribulation_SaveCodecPersistsTerminalStateAndLoadedStateCannotAdvance()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(909L, "content-2026-08-07");
            state.Day = BuqiRunRules.RunDayCount;
            state.EncounterIndex = BuqiRunRules.OperationsPerDay;
            state.Period = BuqiRunPeriod.NightPvp;
            state.Phase = BuqiRunPhase.TribulationRoute;
            state.Wins = BuqiRunRules.WinsToVictory - 1;
            state.DaoSeals = state.Wins;
            var controller = new BuqiRunController(state);
            Assert.That(controller.SelectTribulationRoute(
                "route", 0, BuqiTribulationRoute.ShatterArtifact, 0).Success, Is.True);
            Assert.That(controller.ResolveTribulationStage("stage-1", 1, true).Success, Is.True);
            Assert.That(controller.ResolveTribulationStage("stage-2", 2, true).Success, Is.True);
            Assert.That(controller.ResolveTribulationStage("stage-3", 3, true).Success, Is.True);

            string json = BuqiRunSaveCodec.ToJson(BuqiRunSaveCodec.FromState(controller.State));
            Assert.That(
                BuqiRunSaveCodec.TryFromJson(json, out BuqiRunSaveData save, out string saveError),
                Is.True,
                saveError);
            Assert.That(BuqiRunSaveCodec.TryToState(save, out BuqiRunState loaded, out string stateError), Is.True, stateError);
            Assert.That(loaded.Outcome, Is.EqualTo(BuqiRunOutcome.Victory));
            Assert.That(loaded.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));

            controller = new BuqiRunController(loaded);
            Assert.That(controller.ResolveTribulationStage("late-stage", loaded.Revision, true).Success, Is.False);
            Assert.That(
                controller.SettleBattle(
                    "late-settlement",
                    loaded.Revision,
                    BuqiRunBattleKind.Pvp,
                    BuqiRunRawBattleOutcome.PlayerWin).Success,
                Is.False);
        }

        private static void AssertRejected(BuqiRunSaveData save)
        {
            Assert.That(BuqiRunSaveCodec.TryToState(save, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        private static void AssertStateEquals(BuqiRunState expected, BuqiRunState actual)
        {
            Assert.That(actual.ContentVersion, Is.EqualTo(expected.ContentVersion));
            Assert.That(actual.RuleVersion, Is.EqualTo(expected.RuleVersion));
            Assert.That(actual.RunSeed, Is.EqualTo(expected.RunSeed));
            Assert.That(actual.RngCursor, Is.EqualTo(expected.RngCursor));
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.Day, Is.EqualTo(expected.Day));
            Assert.That(actual.EncounterIndex, Is.EqualTo(expected.EncounterIndex));
            Assert.That(actual.Period, Is.EqualTo(expected.Period));
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
            Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
            Assert.That(actual.Coins, Is.EqualTo(expected.Coins));
            Assert.That(actual.Wins, Is.EqualTo(expected.Wins));
            Assert.That(actual.DaoSeals, Is.EqualTo(expected.DaoSeals));
            Assert.That(actual.CurrentOmen, Is.EqualTo(expected.CurrentOmen));
            Assert.That(actual.Lives, Is.EqualTo(expected.Lives));
            Assert.That(actual.TribulationRoute, Is.EqualTo(expected.TribulationRoute));
            Assert.That(actual.TribulationDaoSealsSpent, Is.EqualTo(expected.TribulationDaoSealsSpent));
            Assert.That(actual.TribulationStage, Is.EqualTo(expected.TribulationStage));
            Assert.That(actual.TribulationSuccesses, Is.EqualTo(expected.TribulationSuccesses));
            Assert.That(actual.BoardInstanceIds, Is.EqualTo(expected.BoardInstanceIds));
            Assert.That(actual.StorageInstanceIds, Is.EqualTo(expected.StorageInstanceIds));
            Assert.That(actual.AppliedCommandIds, Is.EquivalentTo(expected.AppliedCommandIds));
            Assert.That(actual.AppliedSettlementIds, Is.EquivalentTo(expected.AppliedSettlementIds));
        }

        private static BuqiRunSaveData CreateValidSaveData()
        {
            BuqiRunState state = CreateBattleState(BuqiRunPhase.PveBattle, revision: 4, wins: 2, lives: 3);
            state.BoardInstanceIds = CreateSlots("board-1", "board-2");
            state.StorageInstanceIds = CreateSlots("storage-1", "storage-2");
            state.AppliedCommandIds.Add("cmd-a");
            state.AppliedSettlementIds.Add("settle-a");

            return BuqiRunSaveCodec.FromState(
                state,
                "eco",
                "enc",
                "battle",
                CreatePendingSettlement("settle-pending", state.Revision, BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.PlayerWin, "valid-hash"));
        }

        private static BuqiRunPendingSettlement CreatePendingSettlement(
            string settlementId,
            int expectedRevision,
            BuqiRunBattleKind battleKind,
            BuqiRunRawBattleOutcome rawOutcome,
            string battleLogHash)
        {
            BattleOutcome summaryOutcome = rawOutcome switch
            {
                BuqiRunRawBattleOutcome.PlayerWin => BattleOutcome.LeftWin,
                BuqiRunRawBattleOutcome.OpponentWin => BattleOutcome.RightWin,
                _ => BattleOutcome.Draw,
            };

            return new BuqiRunPendingSettlement
            {
                SettlementId = settlementId,
                ExpectedRevision = expectedRevision,
                BattleKind = (int)battleKind,
                RawOutcome = (int)rawOutcome,
                BattleLogHash = battleLogHash,
                Summary = new BuqiRunBattleSummary
                {
                    RawOutcome = summaryOutcome,
                    BattleLogHash = battleLogHash,
                    TopSourceInstanceId = "source-x",
                    TopContribution = 9,
                    FactLines = new List<string> { "主要贡献：source-x 累计 9" },
                },
            };
        }

        private static BattleResult CreateBattleResult(BattleOutcome outcome, string battleLogHash)
        {
            return new BattleResult
            {
                RuleVersion = "battle-rule-v1",
                SimulationVersion = "simulation-v1",
                ContentVersion = "content-2026-08-07",
                BattleSeed = 1234UL,
                RoundIndex = 2,
                Outcome = outcome,
                DurationTicks = 321,
                LeftExecution = 88,
                RightExecution = 12,
                LeftBuffer = 5,
                RightBuffer = 0,
                LeftNoise = 2,
                RightNoise = 10,
                TerminationReason = "Normal",
                BattleLogHash = battleLogHash,
                LeftSnapshotHash = "left-hash",
                RightSnapshotHash = "right-hash",
            };
        }

        private static List<BattleEvent> CreateSummaryLog()
        {
            return new List<BattleEvent>
            {
                CreateEvent(0, 1, BuqiEventType.Effect, 9, "source-x", "target-a", "Damage"),
                CreateEvent(1, 2, BuqiEventType.Truncate, 0, string.Empty, string.Empty, "ChainBreak"),
                CreateEvent(2, 3, BuqiEventType.Effect, 4, "source-y", "target-b", "NoiseAccident"),
            };
        }

        private static BattleEvent CreateEvent(
            int sequence,
            int tick,
            BuqiEventType type,
            int amount,
            string sourceInstanceId,
            string targetInstanceId,
            string reasonCode)
        {
            return new BattleEvent
            {
                Sequence = sequence,
                Tick = tick,
                Phase = BuqiEventPhase.Resolve,
                ChainDepth = 0,
                ChainId = $"chain-{sequence}",
                ActorInstanceId = sourceInstanceId,
                SourceInstanceId = sourceInstanceId,
                TargetInstanceId = targetInstanceId,
                Type = type,
                Amount = amount,
                EffectId = $"effect-{sequence}",
                ReasonCode = reasonCode,
            };
        }

        private static BuqiRunState CreateBattleState(
            BuqiRunPhase phase,
            int revision,
            int wins,
            int lives)
        {
            BuqiRunState state = BuqiRunState.CreateInitial(701L, "content-2026-08-07");
            state.RuleVersion = BuqiRunState.CurrentRuleVersion;
            state.RngCursor = 11;
            state.Revision = revision;
            state.Day = 3;
            state.EncounterIndex = phase == BuqiRunPhase.Encounter
                ? 0
                : phase == BuqiRunPhase.PveBattle
                    ? BuqiRunRules.OperationsBeforePve
                    : BuqiRunRules.OperationsPerDay;
            state.Period = phase == BuqiRunPhase.Encounter
                ? BuqiRunPeriod.MorningOperation
                : phase == BuqiRunPhase.PveBattle
                    ? BuqiRunPeriod.DuskPve
                    : BuqiRunPeriod.NightPvp;
            state.Phase = phase;
            state.Outcome = BuqiRunOutcome.None;
            state.Coins = 18;
            state.Wins = wins;
            state.DaoSeals = wins;
            state.CurrentOmen = 0;
            state.Lives = lives;
            state.BoardInstanceIds = CreateSlots("board-main");
            state.StorageInstanceIds = CreateSlots("storage-main");
            return state;
        }

        private static List<string> CreateSlots(params string[] occupied)
        {
            var slots = new List<string>(BuqiRunRules.BoardSlotCount);
            for (int index = 0; index < BuqiRunRules.BoardSlotCount; index++)
            {
                slots.Add(index < occupied.Length ? occupied[index] : string.Empty);
            }

            return slots;
        }

        private sealed class SpyRunStore : IBuqiRunStore
        {
            public readonly List<string> Writes = new List<string>();

            public int FailWriteAtCall = -1;

            public bool FailNextRead;

            public string CurrentJson = string.Empty;

            public bool HasSave;

            public bool TryRead(out string json, out string error)
            {
                if (FailNextRead)
                {
                    FailNextRead = false;
                    json = string.Empty;
                    error = "read failed";
                    return false;
                }
                if (!HasSave)
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
                if (Writes.Count == FailWriteAtCall)
                {
                    error = $"write-{FailWriteAtCall} failed";
                    return false;
                }

                CurrentJson = json;
                HasSave = true;
                Writes.Add(json);
                error = string.Empty;
                return true;
            }

            public bool TryDelete(out string error)
            {
                CurrentJson = string.Empty;
                HasSave = false;
                error = string.Empty;
                return true;
            }
        }
    }
}
