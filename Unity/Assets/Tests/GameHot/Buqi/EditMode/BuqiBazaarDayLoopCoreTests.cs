using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Battle;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Settlement;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiBazaarDayLoopCoreTests
    {
        [Test]
        public void InitialState_UsesApprovedUnlimitedRunBaseline()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(240824L, "content-v1");

            Assert.That(BuqiRunRules.WinsToVictory, Is.EqualTo(10));
            Assert.That(BuqiRunRules.StartingLifePool, Is.EqualTo(20));
            Assert.That(BuqiRunRules.BoardSlotCount, Is.EqualTo(10));
            Assert.That(BuqiRunRules.StorageSlotCount, Is.EqualTo(10));
            Assert.That(state.Day, Is.EqualTo(1));
            Assert.That(state.LifePool, Is.EqualTo(20));
            Assert.That(state.Cultivation, Is.Zero);
            Assert.That(state.Realm, Is.Zero);
            Assert.That(state.InTribulationTrial, Is.False);
            Assert.That(state.HeartTrialUsed, Is.False);
            Assert.That(state.BoardInstanceIds, Has.Count.EqualTo(10));
            Assert.That(state.StorageInstanceIds, Has.Count.EqualTo(10));
        }

        [TestCase(0, 0)]
        [TestCase(7, 0)]
        [TestCase(8, 1)]
        [TestCase(119, 7)]
        [TestCase(120, 8)]
        public void RealmProgression_UsesApprovedThresholds(int cultivation, int expectedRealm)
        {
            Assert.That(BuqiRunProgression.GetRealm(cultivation), Is.EqualTo(expectedRealm));
        }

        [TestCase(BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.PlayerWin, 3)]
        [TestCase(BuqiRunBattleKind.Pve, BuqiRunRawBattleOutcome.OpponentWin, 1)]
        [TestCase(BuqiRunBattleKind.Pvp, BuqiRunRawBattleOutcome.PlayerWin, 2)]
        [TestCase(BuqiRunBattleKind.Pvp, BuqiRunRawBattleOutcome.OpponentWin, 1)]
        public void BattleCultivationReward_IsExplicit(
            BuqiRunBattleKind kind,
            BuqiRunRawBattleOutcome outcome,
            int expected)
        {
            Assert.That(BuqiRunProgression.GetBattleReward(kind, outcome), Is.EqualTo(expected));
        }

        [Test]
        public void DailyLoop_PveLossKeepsLivesAndOpensTwoPostBattleOperations()
        {
            var controller = new BuqiRunController(BuqiRunState.CreateInitial(101L, "content-v1"));

            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour1Operation));
            Assert.That(controller.ResolveEncounter("hour-1", 0).Success, Is.True);
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour2Operation));
            Assert.That(controller.ResolveEncounter("hour-2", 1).Success, Is.True);
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour3Pve));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PveBattle));

            BuqiRunTransitionResult pveLoss = controller.SettleBattle(
                "pve-loss",
                2,
                BuqiRunBattleKind.Pve,
                BuqiRunRawBattleOutcome.OpponentWin);

            Assert.That(pveLoss.Success, Is.True, pveLoss.FailureReason);
            Assert.That(controller.State.Lives, Is.EqualTo(BuqiRunRules.StartingLives));
            Assert.That(controller.State.CurrentOmen, Is.Zero);
            Assert.That(controller.State.Wins, Is.Zero);
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour4Operation));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));

            Assert.That(controller.ResolveEncounter("hour-4", 3).Success, Is.True);
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour5Operation));
            Assert.That(controller.ResolveEncounter("hour-5", 4).Success, Is.True);
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour6Pvp));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.PvpBattle));
            Assert.That(controller.State.EncounterIndex, Is.EqualTo(BuqiRunRules.OperationsPerDay));
        }

        [Test]
        public void PvpLoss_IsTheOnlyBattleLossThatCostsLifeAndImmediatelyStartsNextDay()
        {
            var controller = CreateHourSixController(day: 1, lives: 3, omen: 0);

            BuqiRunTransitionResult first = controller.SettleBattle(
                "day-1-pvp",
                0,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.OpponentWin);

            Assert.That(first.Success, Is.True, first.FailureReason);
            Assert.That(controller.State.Day, Is.EqualTo(2));
            Assert.That(controller.State.Lives, Is.EqualTo(2));
            Assert.That(controller.State.CurrentOmen, Is.EqualTo(1));
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour1Operation));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
            Assert.That(controller.State.EncounterIndex, Is.Zero);

            BuqiRunTransitionResult replay = controller.SettleBattle(
                "day-1-pvp",
                0,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.OpponentWin);

            Assert.That(replay.Success, Is.True);
            Assert.That(replay.Replayed, Is.True);
            Assert.That(controller.State.Day, Is.EqualTo(2));
            Assert.That(controller.State.Lives, Is.EqualTo(2));
            Assert.That(controller.State.CurrentOmen, Is.EqualTo(1));
        }

        [Test]
        public void PveVictory_AwardsCultivationWithoutCountingPvpProgress()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(106L, "content-v1");
            state.Period = BuqiRunPeriod.Hour3Pve;
            state.Phase = BuqiRunPhase.PveBattle;
            var controller = new BuqiRunController(state);

            BuqiRunTransitionResult result = controller.SettleBattle(
                "pve-win",
                0,
                BuqiRunBattleKind.Pve,
                BuqiRunRawBattleOutcome.PlayerWin);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(controller.State.Cultivation, Is.EqualTo(3));
            Assert.That(controller.State.Realm, Is.Zero);
            Assert.That(controller.State.Wins, Is.Zero);
            Assert.That(controller.State.DaoSeals, Is.Zero);
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour4Operation));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
        }

        [Test]
        public void PvpVictory_AwardsCultivationAndStartsNextUnlimitedDay()
        {
            var controller = CreateHourSixController(day: 12, lives: 8, omen: 2);

            BuqiRunTransitionResult result = controller.SettleBattle(
                "day-12-pvp-win",
                0,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.PlayerWin);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(controller.State.Day, Is.EqualTo(13));
            Assert.That(controller.State.Cultivation, Is.EqualTo(2));
            Assert.That(controller.State.Wins, Is.EqualTo(1));
            Assert.That(controller.State.DaoSeals, Is.EqualTo(1));
            Assert.That(controller.State.LifePool, Is.EqualTo(8));
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour1Operation));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
        }

        [Test]
        public void PvpLoss_DeductsCurrentDayFromLifePool()
        {
            var controller = CreateHourSixController(day: 4, lives: 20, omen: 0);

            BuqiRunTransitionResult result = controller.SettleBattle(
                "day-4-pvp-loss",
                0,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.OpponentWin);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(controller.State.Day, Is.EqualTo(5));
            Assert.That(controller.State.LifePool, Is.EqualTo(16));
            Assert.That(controller.State.Cultivation, Is.EqualTo(1));
            Assert.That(controller.State.CurrentOmen, Is.EqualTo(1));
            Assert.That(controller.State.InTribulationTrial, Is.False);
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.None));
        }

        [Test]
        public void FirstLifeDepletion_EntersFinalHeartTrialAndContinues()
        {
            var controller = CreateHourSixController(day: 4, lives: 4, omen: 0);

            BuqiRunTransitionResult result = controller.SettleBattle(
                "enter-heart-trial",
                0,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.OpponentWin);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(controller.State.Day, Is.EqualTo(5));
            Assert.That(controller.State.LifePool, Is.Zero);
            Assert.That(controller.State.InTribulationTrial, Is.True);
            Assert.That(controller.State.HeartTrialUsed, Is.True);
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.None));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.Encounter));
        }

        [Test]
        public void HeartTrialVictory_RecoversCurrentDayLifeAndCountsTheWin()
        {
            var controller = CreateHourSixController(day: 5, lives: 0, omen: 1);
            BuqiRunState state = controller.State;
            state.InTribulationTrial = true;
            state.HeartTrialUsed = true;
            state.Wins = 2;
            state.DaoSeals = 2;
            controller = new BuqiRunController(state);

            BuqiRunTransitionResult result = controller.SettleBattle(
                "survive-heart-trial",
                0,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.PlayerWin);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(controller.State.Day, Is.EqualTo(6));
            Assert.That(controller.State.LifePool, Is.EqualTo(5));
            Assert.That(controller.State.InTribulationTrial, Is.False);
            Assert.That(controller.State.HeartTrialUsed, Is.True);
            Assert.That(controller.State.Wins, Is.EqualTo(3));
            Assert.That(controller.State.DaoSeals, Is.EqualTo(3));
        }

        [Test]
        public void HeartTrialLoss_EndsTheRun()
        {
            var controller = CreateHourSixController(day: 5, lives: 0, omen: 1);
            BuqiRunState state = controller.State;
            state.InTribulationTrial = true;
            state.HeartTrialUsed = true;
            controller = new BuqiRunController(state);

            BuqiRunTransitionResult result = controller.SettleBattle(
                "fail-heart-trial",
                0,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.OpponentWin);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(controller.State.LifePool, Is.Zero);
            Assert.That(controller.State.InTribulationTrial, Is.True);
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.Defeat));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));
        }

        [Test]
        public void LifeDepletionAfterHeartTrialWasUsed_EndsTheRunImmediately()
        {
            var controller = CreateHourSixController(day: 7, lives: 7, omen: 2);
            BuqiRunState state = controller.State;
            state.HeartTrialUsed = true;
            controller = new BuqiRunController(state);

            BuqiRunTransitionResult result = controller.SettleBattle(
                "deplete-after-heart-trial",
                0,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.OpponentWin);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(controller.State.LifePool, Is.Zero);
            Assert.That(controller.State.InTribulationTrial, Is.False);
            Assert.That(controller.State.HeartTrialUsed, Is.True);
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.Defeat));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));
        }

        [Test]
        public void NineWins_ChangesTheNextHourSixIntoTribulationChoice()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(107L, "content-v1");
            state.Day = 14;
            state.Wins = BuqiRunRules.WinsToVictory - 1;
            state.DaoSeals = state.Wins;
            state.EncounterIndex = BuqiRunRules.OperationsPerDay - 1;
            state.Period = BuqiRunPeriod.Hour5Operation;
            state.Phase = BuqiRunPhase.Encounter;
            var controller = new BuqiRunController(state);

            BuqiRunTransitionResult result = controller.ResolveEncounter("hour-5", 0);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(controller.State.Day, Is.EqualTo(14));
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour6Pvp));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.TribulationRoute));
        }

        [Test]
        public void ThreeTribulationSuccesses_RecordTheTenthWin()
        {
            BuqiRunState state = BuqiRunState.CreateInitial(108L, "content-v1");
            state.Wins = BuqiRunRules.WinsToVictory - 1;
            state.DaoSeals = state.Wins;
            state.Period = BuqiRunPeriod.Hour6Pvp;
            state.Phase = BuqiRunPhase.TribulationRoute;
            var controller = new BuqiRunController(state);

            Assert.That(controller.SelectTribulationRoute(
                "choose-route", 0, BuqiTribulationRoute.FaceThunder, 0).Success, Is.True);
            Assert.That(controller.ResolveTribulationStage("trib-1", 1, true).Success, Is.True);
            Assert.That(controller.ResolveTribulationStage("trib-2", 2, true).Success, Is.True);
            Assert.That(controller.ResolveTribulationStage("trib-3", 3, true).Success, Is.True);

            Assert.That(controller.State.Wins, Is.EqualTo(BuqiRunRules.WinsToVictory));
            Assert.That(controller.State.DaoSeals, Is.EqualTo(BuqiRunRules.WinsToVictory));
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.Victory));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.RunTerminal));
        }

        [Test]
        public void PveRewardPolicy_ExposesIncreasingArtifactGoldAndExperienceChoices()
        {
            BuqiPveRewardProfile initial = BuqiPveRewardPolicy.Get(BuqiPveDifficulty.Initial);
            BuqiPveRewardProfile intermediate = BuqiPveRewardPolicy.Get(BuqiPveDifficulty.Intermediate);
            BuqiPveRewardProfile dangerous = BuqiPveRewardPolicy.Get(BuqiPveDifficulty.Dangerous);

            Assert.That(initial.Rank, Is.EqualTo(1));
            Assert.That(intermediate.Rank, Is.EqualTo(2));
            Assert.That(dangerous.Rank, Is.EqualTo(3));
            Assert.That(intermediate.ArtifactChoiceCount, Is.GreaterThan(initial.ArtifactChoiceCount));
            Assert.That(dangerous.ArtifactChoiceCount, Is.GreaterThan(intermediate.ArtifactChoiceCount));
            Assert.That(intermediate.GoldOptionAmount, Is.GreaterThan(initial.GoldOptionAmount));
            Assert.That(dangerous.GoldOptionAmount, Is.GreaterThan(intermediate.GoldOptionAmount));
            Assert.That(intermediate.ExperienceOptionAmount, Is.GreaterThan(initial.ExperienceOptionAmount));
            Assert.That(dangerous.ExperienceOptionAmount, Is.GreaterThan(intermediate.ExperienceOptionAmount));
            Assert.That(initial.DefeatExperienceAmount, Is.LessThan(initial.ExperienceOptionAmount));
            Assert.That(intermediate.DefeatExperienceAmount, Is.LessThan(intermediate.ExperienceOptionAmount));
            Assert.That(dangerous.DefeatExperienceAmount, Is.LessThan(dangerous.ExperienceOptionAmount));
        }

        [Test]
        public void SaveV5_RoundTripsNewRunFieldsAndTenSlots()
        {
            BuqiRunState source = BuqiRunState.CreateInitial(44L, "content-v1");
            source.HeroId = 2;
            source.Cultivation = 44;
            source.Realm = 4;
            source.LifePool = 7;
            source.InTribulationTrial = false;
            source.HeartTrialUsed = true;

            Assert.That(
                BuqiRunSaveCodec.TryFromJson(
                    BuqiRunSaveCodec.ToJson(BuqiRunSaveCodec.FromState(source)),
                    out BuqiRunSaveData data,
                    out string error),
                Is.True,
                error);
            Assert.That(
                BuqiRunSaveCodec.TryToState(data, out BuqiRunState loaded, out error),
                Is.True,
                error);
            Assert.That(loaded.HeroId, Is.EqualTo(2));
            Assert.That(loaded.Cultivation, Is.EqualTo(44));
            Assert.That(loaded.Realm, Is.EqualTo(4));
            Assert.That(loaded.LifePool, Is.EqualTo(7));
            Assert.That(loaded.InTribulationTrial, Is.False);
            Assert.That(loaded.HeartTrialUsed, Is.True);
            Assert.That(loaded.BoardInstanceIds, Has.Count.EqualTo(10));
            Assert.That(loaded.StorageInstanceIds, Has.Count.EqualTo(10));
        }

        [Test]
        public void SaveV4_IsReportedAsUnsupportedSoInitializationCanReplaceIt()
        {
            BuqiRunSaveData old = BuqiRunSaveCodec.FromState(
                BuqiRunState.CreateInitial(45L, "content-v1"));
            old.SaveVersion = "buqi-run-save-v4";

            Assert.That(
                BuqiRunSaveCodec.TryFromJson(
                    BuqiRunSaveCodec.ToJson(old),
                    out _,
                    out _,
                    out BuqiRunSaveFailureKind kind),
                Is.False);
            Assert.That(kind, Is.EqualTo(BuqiRunSaveFailureKind.UnsupportedVersion));
        }

        private static BuqiRunController CreateHourSixController(int day, int lives, int omen)
        {
            BuqiRunState state = BuqiRunState.CreateInitial(103L, "content-v1");
            state.Day = day;
            state.EncounterIndex = BuqiRunRules.OperationsPerDay;
            state.Period = BuqiRunPeriod.Hour6Pvp;
            state.Phase = BuqiRunPhase.PvpBattle;
            state.Lives = lives;
            state.CurrentOmen = omen;
            return new BuqiRunController(state);
        }
    }
}
