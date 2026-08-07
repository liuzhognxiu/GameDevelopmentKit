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
            Assert.That(BuqiRunRules.WinsToVictory, Is.EqualTo(9));
            Assert.That(BuqiRunRules.StartingLives, Is.EqualTo(3));
            Assert.That(BuqiRunRules.EncountersPerDay, Is.EqualTo(3));
            Assert.That(BuqiRunRules.BoardSlotCount, Is.EqualTo(8));
            Assert.That(BuqiRunRules.StorageSlotCount, Is.EqualTo(8));
            Assert.That(BuqiRunRules.StartingCoins, Is.EqualTo(12));
        }

        [Test]
        public void CreateInitialStartsAtFirstEncounterWithEightSlotStorage()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(812345L);

            Assert.That(state.RunSeed, Is.EqualTo(812345L));
            Assert.That(state.Day, Is.EqualTo(1));
            Assert.That(state.EncounterIndex, Is.EqualTo(0));
            Assert.That(state.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
            Assert.That(state.Coins, Is.EqualTo(12));
            Assert.That(state.Wins, Is.EqualTo(0));
            Assert.That(state.Lives, Is.EqualTo(3));
            Assert.That(state.BoardInstanceIds, Has.Count.EqualTo(8));
            Assert.That(state.StorageInstanceIds, Has.Count.EqualTo(8));
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
        public void ThreeResolvedEncountersAdvanceToPveThenPvpThenNextDay()
        {
            var controller = new BuqiRunController(BuqiRunState.CreateInitial(10));

            Assert.That(controller.ResolveEncounter("enc-1", 0).Success, Is.True);
            Assert.That(controller.State.EncounterIndex, Is.EqualTo(1));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));

            Assert.That(controller.ResolveEncounter("enc-2", 1).Success, Is.True);
            Assert.That(controller.ResolveEncounter("enc-3", 2).Success, Is.True);
            Assert.That(controller.State.EncounterIndex, Is.EqualTo(3));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PveBattle));

            Assert.That(
                controller.SettleBattle("pve-1", 3, BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.PlayerWin).Success,
                Is.True);
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PvpBattle));

            Assert.That(
                controller.SettleBattle("pvp-1", 4, BuqiRunBattleKind.Pvp, BuqiRunRawBattleOutcome.OpponentWin).Success,
                Is.True);
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.DaySettlement));

            Assert.That(controller.CompleteDay("day-1", 5).Success, Is.True);
            Assert.That(controller.State.Day, Is.EqualTo(2));
            Assert.That(controller.State.EncounterIndex, Is.EqualTo(0));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
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
        public void DrawCountsAsPlayerWinAndNineWinsStopsImmediately()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(30);
            state.Phase = BuqiRunPhase.PveBattle;
            state.Wins = 8;
            var controller = new BuqiRunController(state);

            BuqiRunTransitionResult result =
                controller.SettleBattle("draw-terminal", 0, BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.Draw);

            Assert.That(result.Success, Is.True);
            Assert.That(controller.State.Wins, Is.EqualTo(9));
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.Victory));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));
        }

        [Test]
        public void ThirdLossStopsImmediatelyAndSkipsRemainingFlow()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(40);
            state.Phase = BuqiRunPhase.PvpBattle;
            state.Lives = 1;
            var controller = new BuqiRunController(state);

            controller.SettleBattle("loss-terminal", 0, BuqiRunBattleKind.Pvp, BuqiRunRawBattleOutcome.OpponentWin);

            Assert.That(controller.State.Lives, Is.EqualTo(0));
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.Defeat));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));
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
            Assert.That(controller.State.Wins, Is.EqualTo(1));
            Assert.That(controller.State.Revision, Is.EqualTo(1));
        }

        [Test]
        public void TerminalRunRejectsNewCommandsAndSettlements()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(60);
            state.Phase = BuqiRunPhase.PveBattle;
            state.Wins = 8;
            var controller = new BuqiRunController(state);
            Assert.That(
                controller.SettleBattle("terminal", 0, BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.Draw).Success,
                Is.True);

            BuqiRunTransitionResult encounter = controller.ResolveEncounter("late-encounter", 1);
            BuqiRunTransitionResult completeDay = controller.CompleteDay("late-day", 1);
            BuqiRunTransitionResult settlement =
                controller.SettleBattle("late-settlement", 1, BuqiRunBattleKind.Pvp, BuqiRunRawBattleOutcome.PlayerWin);

            Assert.That(encounter.Success, Is.False);
            Assert.That(completeDay.Success, Is.False);
            Assert.That(settlement.Success, Is.False);
            Assert.That(encounter.FailureReason, Is.EqualTo("Run has already ended."));
            Assert.That(completeDay.FailureReason, Is.EqualTo("Run has already ended."));
            Assert.That(settlement.FailureReason, Is.EqualTo("Run has already ended."));
            Assert.That(controller.State.Revision, Is.EqualTo(1));
        }
    }
}
