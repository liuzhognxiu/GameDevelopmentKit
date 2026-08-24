using System;
using NUnit.Framework;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunCoreTests
    {
        [Test]
        public void RulesMatchApprovedDemoContract()
        {
            Assert.That(BuqiRunRules.ContentScheduleDayCount, Is.EqualTo(9));
            Assert.That(BuqiRunRules.OperationsPerDay, Is.EqualTo(4));
            Assert.That(BuqiRunRules.TribulationStageCount, Is.EqualTo(3));
            Assert.That(BuqiRunRules.WinsToVictory, Is.EqualTo(10));
            Assert.That(BuqiRunRules.StartingLifePool, Is.EqualTo(20));
            Assert.That(BuqiRunRules.BoardSlotCount, Is.EqualTo(10));
            Assert.That(BuqiRunRules.StorageSlotCount, Is.EqualTo(10));
            Assert.That(BuqiRunRules.StartingCoins, Is.EqualTo(12));
        }

        [Test]
        public void CreateInitialStartsAtFirstEncounterWithTenSlotStorage()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(812345L);

            Assert.That(state.RunSeed, Is.EqualTo(812345L));
            Assert.That(state.Day, Is.EqualTo(1));
            Assert.That(state.EncounterIndex, Is.EqualTo(0));
            Assert.That(state.Period, Is.EqualTo(BuqiRunPeriod.MorningOperation));
            Assert.That(state.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
            Assert.That(state.Coins, Is.EqualTo(12));
            Assert.That(state.Wins, Is.EqualTo(0));
            Assert.That(state.LifePool, Is.EqualTo(20));
            Assert.That(state.BoardInstanceIds, Has.Count.EqualTo(10));
            Assert.That(state.StorageInstanceIds, Has.Count.EqualTo(10));
            Assert.That(state.Outcome, Is.EqualTo(BuqiRunOutcome.None));
            Assert.That(state.Revision, Is.EqualTo(0));
        }

        [Test]
        public void CreateInitial_AcceptsContentVersionAndClonePreservesIt()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(7L, "content-2026-08-07");

            Assert.That(state.ContentVersion, Is.EqualTo("content-2026-08-07"));

            BuqiRunState clone = state.Clone();
            Assert.That(clone.ContentVersion, Is.EqualTo("content-2026-08-07"));
        }

        [TestCase(1L, 0, 3, 1, 1)]
        [TestCase(1L, 1, 3, 2, 2)]
        [TestCase(812345L, 0, 7, 4, 1)]
        [TestCase(812345L, 4, 7, 3, 5)]
        [TestCase(-9L, 2, 8, 0, 3)]
        [TestCase(123456789L, 10, 97, 84, 11)]
        [TestCase(0L, 0, 2, 1, 1)]
        public void RandomNext_UsesStableLockedVectors(
            long seed,
            int startingCursor,
            int maxExclusive,
            int expectedValue,
            int expectedCursor)
        {
            int cursor = startingCursor;

            int value = BuqiRunRandom.Next(seed, ref cursor, maxExclusive);

            Assert.That(value, Is.EqualTo(expectedValue));
            Assert.That(cursor, Is.EqualTo(expectedCursor));
        }

        [Test]
        public void RandomNext_RejectsNonPositiveRangeWithoutAdvancingCursor()
        {
            int cursor = 6;

            Assert.That(
                () => BuqiRunRandom.Next(99L, ref cursor, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(cursor, Is.EqualTo(6));
        }

        [Test]
        public void RandomNext_ReplaysSameSequenceForSameSeedAndCursor()
        {
            int firstCursor = 0;
            int secondCursor = 0;

            for (int index = 0; index < 12; index++)
            {
                int first = BuqiRunRandom.Next(123456L, ref firstCursor, 17);
                int second = BuqiRunRandom.Next(123456L, ref secondCursor, 17);

                Assert.That(first, Is.EqualTo(second), $"Mismatch at draw {index}.");
            }

            Assert.That(firstCursor, Is.EqualTo(secondCursor));
        }

        [Test]
        public void RandomNext_RejectionStillAdvancesPublicCursorExactlyOnce()
        {
            int cursor = 1;

            int value = BuqiRunRandom.Next(-2000L, ref cursor, 27479);

            Assert.That(value, Is.EqualTo(7121));
            Assert.That(cursor, Is.EqualTo(2));
        }

        [Test]
        public void SixPeriodsAdvanceThroughPveAndFourOperationsBeforeNextDay()
        {
            var controller = new BuqiRunController(BuqiRunState.CreateInitial(10));

            Assert.That(controller.ResolveEncounter("enc-1", 0).Success, Is.True);
            Assert.That(controller.State.EncounterIndex, Is.EqualTo(1));
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.NoonOperation));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));

            Assert.That(controller.ResolveEncounter("enc-2", 1).Success, Is.True);
            Assert.That(controller.State.EncounterIndex, Is.EqualTo(2));
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.DuskPve));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PveBattle));

            Assert.That(
                controller.SettleBattle("pve-1", 2, BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.PlayerWin).Success,
                Is.True);
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour4Operation));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));

            Assert.That(controller.ResolveEncounter("enc-3", 3).Success, Is.True);
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour5Operation));
            Assert.That(controller.ResolveEncounter("enc-4", 4).Success, Is.True);
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour6Pvp));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PvpBattle));

            Assert.That(
                controller.SettleBattle("pvp-1", 5, BuqiRunBattleKind.Pvp, BuqiRunRawBattleOutcome.OpponentWin).Success,
                Is.True);
            Assert.That(controller.State.Day, Is.EqualTo(2));
            Assert.That(controller.State.EncounterIndex, Is.EqualTo(0));
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.MorningOperation));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
        }

        [Test]
        public void NinePvpWinsMakeTheFollowingHourSixATribulationChoice()
        {
            var controller = new BuqiRunController(BuqiRunState.CreateInitial(90));

            for (int day = 1; day <= BuqiRunRules.WinsToVictory - 1; day++)
            {
                Assert.That(controller.ResolveEncounter($"hour-1-{day}", controller.State.Revision).Success, Is.True);
                Assert.That(controller.ResolveEncounter($"hour-2-{day}", controller.State.Revision).Success, Is.True);
                Assert.That(controller.SettleBattle(
                    $"pve-{day}", controller.State.Revision, BuqiRunBattleKind.Pve,
                    BuqiRunRawBattleOutcome.PlayerWin).Success, Is.True);
                Assert.That(controller.ResolveEncounter($"hour-4-{day}", controller.State.Revision).Success, Is.True);
                Assert.That(controller.ResolveEncounter($"hour-5-{day}", controller.State.Revision).Success, Is.True);
                Assert.That(controller.SettleBattle(
                    $"pvp-{day}", controller.State.Revision, BuqiRunBattleKind.Pvp,
                    BuqiRunRawBattleOutcome.PlayerWin).Success, Is.True);
            }

            Assert.That(controller.State.Wins, Is.EqualTo(9));
            Assert.That(controller.State.Day, Is.EqualTo(10));
            Assert.That(controller.ResolveEncounter("final-hour-1", controller.State.Revision).Success, Is.True);
            Assert.That(controller.ResolveEncounter("final-hour-2", controller.State.Revision).Success, Is.True);
            Assert.That(controller.SettleBattle(
                "final-pve", controller.State.Revision, BuqiRunBattleKind.Pve,
                BuqiRunRawBattleOutcome.PlayerWin).Success, Is.True);
            Assert.That(controller.ResolveEncounter("final-hour-4", controller.State.Revision).Success, Is.True);
            Assert.That(controller.ResolveEncounter("final-hour-5", controller.State.Revision).Success, Is.True);

            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour6Pvp));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.TribulationRoute));
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.None));
            Assert.That(controller.State.DaoSeals, Is.EqualTo(9));
        }

        [Test]
        public void InvalidPhaseAndStaleRevisionDoNotMutateState()
        {
            var controller = new BuqiRunController(BuqiRunState.CreateInitial(20));

            BuqiRunTransitionResult invalid = controller.CompleteDay("day-early", 0);
            Assert.That(invalid.Success, Is.False);
            Assert.That(controller.State.Revision, Is.EqualTo(0));

            Assert.That(controller.ResolveEncounter("enc-1", 0).Success, Is.True);
            BuqiRunTransitionResult stale = controller.ResolveEncounter("enc-stale", 0);
            Assert.That(stale.Success, Is.False);
            Assert.That(controller.State.EncounterIndex, Is.EqualTo(1));
            Assert.That(controller.State.Revision, Is.EqualTo(1));
        }

        [Test]
        public void RepeatingCommandIdReturnsOriginalSuccessWithoutDoubleAdvance()
        {
            var controller = new BuqiRunController(BuqiRunState.CreateInitial(21));

            Assert.That(controller.ResolveEncounter("enc-1", 0).Success, Is.True);

            BuqiRunTransitionResult replay = controller.ResolveEncounter("enc-1", 0);

            Assert.That(replay.Success, Is.True);
            Assert.That(replay.Replayed, Is.True);
            Assert.That(controller.State.EncounterIndex, Is.EqualTo(1));
            Assert.That(controller.State.Revision, Is.EqualTo(1));
        }

        [Test]
        public void DrawAwardsCultivationWithoutWinOrDaoSeal()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(30);
            state.Phase = BuqiRunPhase.PveBattle;
            state.Period = BuqiRunPeriod.DuskPve;
            state.EncounterIndex = BuqiRunRules.OperationsPerDay;
            state.Wins = 8;
            state.DaoSeals = 8;
            var controller = new BuqiRunController(state);

            BuqiRunTransitionResult result =
                controller.SettleBattle("draw-terminal", 0, BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.Draw);

            Assert.That(result.Success, Is.True);
            Assert.That(controller.State.Wins, Is.EqualTo(8));
            Assert.That(controller.State.DaoSeals, Is.EqualTo(8));
            Assert.That(controller.State.Cultivation, Is.EqualTo(1));
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.None));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour4Operation));
        }

        [Test]
        public void FirstLifeDepletionStartsHeartTrialInsteadOfEndingRun()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(40);
            state.Phase = BuqiRunPhase.PvpBattle;
            state.Period = BuqiRunPeriod.NightPvp;
            state.EncounterIndex = BuqiRunRules.OperationsPerDay;
            state.Lives = 1;
            var controller = new BuqiRunController(state);

            controller.SettleBattle("loss-terminal", 0, BuqiRunBattleKind.Pvp, BuqiRunRawBattleOutcome.OpponentWin);

            Assert.That(controller.State.Lives, Is.EqualTo(0));
            Assert.That(controller.State.CurrentOmen, Is.EqualTo(1));
            Assert.That(controller.State.InTribulationTrial, Is.True);
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.None));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour1Operation));
        }

        [Test]
        public void RepeatingSettlementIdReturnsOriginalSuccessWithoutDoubleReward()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(50);
            state.Phase = BuqiRunPhase.PveBattle;
            var controller = new BuqiRunController(state);

            Assert.That(
                controller.SettleBattle("same", 0, BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.PlayerWin).Success,
                Is.True);
            BuqiRunTransitionResult replay =
                controller.SettleBattle("same", 0, BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.PlayerWin);

            Assert.That(replay.Success, Is.True);
            Assert.That(replay.Replayed, Is.True);
            Assert.That(controller.State.Wins, Is.Zero);
            Assert.That(controller.State.Cultivation, Is.EqualTo(3));
            Assert.That(controller.State.Revision, Is.EqualTo(1));
        }

        [Test]
        public void InvalidBattleKind_IsRejectedWithoutMutatingState()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(51);
            state.Phase = BuqiRunPhase.PvpBattle;
            state.Wins = 2;
            state.Lives = 2;
            var controller = new BuqiRunController(state);

            BuqiRunTransitionResult result =
                controller.SettleBattle("invalid-kind", 0, (BuqiRunBattleKind)99, BuqiRunRawBattleOutcome.PlayerWin);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo("Battle kind is invalid."));
            Assert.That(controller.State.Wins, Is.EqualTo(2));
            Assert.That(controller.State.Lives, Is.EqualTo(2));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PvpBattle));
            Assert.That(controller.State.Revision, Is.EqualTo(0));
        }

        [Test]
        public void InvalidBattleOutcome_IsRejectedWithoutMutatingState()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(52);
            state.Phase = BuqiRunPhase.PveBattle;
            state.Wins = 3;
            state.Lives = 1;
            var controller = new BuqiRunController(state);

            BuqiRunTransitionResult result =
                controller.SettleBattle("invalid-outcome", 0, BuqiRunBattleKind.Pve, (BuqiRunRawBattleOutcome)77);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo("Battle outcome is invalid."));
            Assert.That(controller.State.Wins, Is.EqualTo(3));
            Assert.That(controller.State.Lives, Is.EqualTo(1));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PveBattle));
            Assert.That(controller.State.Revision, Is.EqualTo(0));
        }

        [Test]
        public void Tribulation_QuestionHeartConsumesSealsToReduceCurrentOmen()
        {
            BuqiRunState state = CreateTribulationRouteState(60);
            state.DaoSeals = 3;
            state.CurrentOmen = 2;
            var controller = new BuqiRunController(state);

            BuqiRunTransitionResult selected = controller.SelectTribulationRoute(
                "question-heart", 0, BuqiTribulationRoute.QuestionHeart, 2);

            Assert.That(selected.Success, Is.True);
            Assert.That(controller.State.TribulationRoute, Is.EqualTo(BuqiTribulationRoute.QuestionHeart));
            Assert.That(controller.State.TribulationDaoSealsSpent, Is.EqualTo(2));
            Assert.That(controller.State.DaoSeals, Is.EqualTo(1));
            Assert.That(controller.State.CurrentOmen, Is.EqualTo(0));
            Assert.That(controller.State.TribulationStage, Is.EqualTo(1));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.TribulationStage));
        }

        [Test]
        public void Tribulation_ThreeStagesProduceTerminalOutcomeAndReplayIsIdempotent()
        {
            var controller = new BuqiRunController(CreateTribulationRouteState(61));
            Assert.That(controller.SelectTribulationRoute(
                "face-thunder", 0, BuqiTribulationRoute.FaceThunder, 0).Success, Is.True);

            Assert.That(controller.ResolveTribulationStage("stage-1", 1, true).Success, Is.True);
            BuqiRunTransitionResult replay = controller.ResolveTribulationStage("stage-1", 1, true);
            Assert.That(replay.Success, Is.True);
            Assert.That(replay.Replayed, Is.True);
            Assert.That(controller.State.TribulationStage, Is.EqualTo(2));
            Assert.That(controller.State.TribulationSuccesses, Is.EqualTo(1));

            Assert.That(controller.ResolveTribulationStage("stage-2", 2, true).Success, Is.True);
            Assert.That(controller.ResolveTribulationStage("stage-3", 3, true).Success, Is.True);
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.Victory));
            Assert.That(controller.State.Wins, Is.EqualTo(BuqiRunRules.WinsToVictory));

            BuqiRunTransitionResult late = controller.ResolveTribulationStage("stage-late", 4, true);
            Assert.That(late.Success, Is.False);
            Assert.That(late.FailureReason, Is.EqualTo("Run has already ended."));
        }

        private static BuqiRunState CreateTribulationRouteState(long seed)
        {
            BuqiRunState state = BuqiRunState.CreateInitial(seed);
            state.Day = BuqiRunRules.RunDayCount;
            state.EncounterIndex = BuqiRunRules.OperationsPerDay;
            state.Period = BuqiRunPeriod.NightPvp;
            state.Phase = BuqiRunPhase.TribulationRoute;
            state.Wins = BuqiRunRules.WinsToVictory - 1;
            state.DaoSeals = state.Wins;
            return state;
        }
    }
}
