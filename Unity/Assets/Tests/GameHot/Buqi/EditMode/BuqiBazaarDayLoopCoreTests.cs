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
        public void DayNinePvpSettlement_EntersExistingTribulationRoute()
        {
            var controller = CreateHourSixController(BuqiRunRules.RunDayCount, lives: 1, omen: 2);

            BuqiRunTransitionResult result = controller.SettleBattle(
                "day-9-pvp",
                0,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.PlayerWin);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(controller.State.Day, Is.EqualTo(BuqiRunRules.RunDayCount));
            Assert.That(controller.State.Period, Is.EqualTo(BuqiRunPeriod.Hour6Pvp));
            Assert.That(controller.State.Phase, Is.EqualTo(BuqiRunPhase.TribulationRoute));
            Assert.That(controller.State.Outcome, Is.EqualTo(BuqiRunOutcome.None));
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
        public void SaveCodec_MigratesV3NightStateWithoutReapplyingSettlement()
        {
            BuqiRunState source = CreateHourSixController(day: 4, lives: 2, omen: 1).State;
            source.Wins = 2;
            source.DaoSeals = 2;
            source.AppliedSettlementIds.Add("already-applied");
            BuqiRunSaveData legacy = BuqiRunSaveCodec.FromState(source);
            legacy.SaveVersion = BuqiRunSaveData.PreviousSaveVersion;
            legacy.RuleVersion = BuqiRunState.PreviousRuleVersion;
            legacy.EncounterIndex = 2;
            legacy.Period = 3;

            string json = BuqiRunSaveCodec.ToJson(legacy);

            Assert.That(
                BuqiRunSaveCodec.TryFromJson(json, out BuqiRunSaveData migrated, out string migrationError),
                Is.True,
                migrationError);
            Assert.That(migrated.SaveVersion, Is.EqualTo(BuqiRunSaveData.CurrentSaveVersion));
            Assert.That(migrated.RuleVersion, Is.EqualTo(BuqiRunState.CurrentRuleVersion));
            Assert.That(
                BuqiRunSaveCodec.TryToState(migrated, out BuqiRunState loaded, out string stateError),
                Is.True,
                stateError);
            Assert.That(loaded.Period, Is.EqualTo(BuqiRunPeriod.Hour6Pvp));
            Assert.That(loaded.EncounterIndex, Is.EqualTo(BuqiRunRules.OperationsPerDay));
            Assert.That(loaded.AppliedSettlementIds, Does.Contain("already-applied"));

            var controller = new BuqiRunController(loaded);
            BuqiRunTransitionResult replay = controller.SettleBattle(
                "already-applied",
                loaded.Revision,
                BuqiRunBattleKind.Pvp,
                BuqiRunRawBattleOutcome.OpponentWin);
            Assert.That(replay.Success, Is.True);
            Assert.That(replay.Replayed, Is.True);
            Assert.That(controller.State.Revision, Is.EqualTo(loaded.Revision));
            Assert.That(controller.State.Lives, Is.EqualTo(loaded.Lives));
        }

        [Test]
        public void SaveCodec_MigratesV3PendingPveWithoutApplyingItDuringLoad()
        {
            BuqiRunState source = BuqiRunState.CreateInitial(105L, "content-v1");
            source.Revision = 7;
            source.EncounterIndex = BuqiRunRules.OperationsBeforePve;
            source.Period = BuqiRunPeriod.Hour3Pve;
            source.Phase = BuqiRunPhase.PveBattle;
            var pending = new BuqiRunPendingSettlement
            {
                SettlementId = "pending-pve",
                ExpectedRevision = source.Revision,
                BattleKind = (int)BuqiRunBattleKind.Pve,
                RawOutcome = (int)BuqiRunRawBattleOutcome.OpponentWin,
                BattleLogHash = "pending-hash",
                Summary = new BuqiRunBattleSummary
                {
                    RawOutcome = BattleOutcome.RightWin,
                    BattleLogHash = "pending-hash",
                },
            };
            BuqiRunSaveData legacy = BuqiRunSaveCodec.FromState(source, pendingSettlement: pending);
            legacy.SaveVersion = BuqiRunSaveData.PreviousSaveVersion;
            legacy.RuleVersion = BuqiRunState.PreviousRuleVersion;
            legacy.EncounterIndex = 2;
            legacy.Period = 2;

            Assert.That(
                BuqiRunSaveCodec.TryFromJson(
                    BuqiRunSaveCodec.ToJson(legacy),
                    out BuqiRunSaveData migrated,
                    out string migrationError),
                Is.True,
                migrationError);
            Assert.That(
                BuqiRunSaveCodec.TryToState(migrated, out BuqiRunState loaded, out string stateError),
                Is.True,
                stateError);
            Assert.That(migrated.PendingSettlement, Is.Not.Null);
            Assert.That(migrated.PendingSettlement.SettlementId, Is.EqualTo("pending-pve"));
            Assert.That(loaded.Revision, Is.EqualTo(source.Revision));
            Assert.That(loaded.Wins, Is.EqualTo(source.Wins));
            Assert.That(loaded.Lives, Is.EqualTo(source.Lives));
            Assert.That(loaded.AppliedSettlementIds, Does.Not.Contain("pending-pve"));
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
