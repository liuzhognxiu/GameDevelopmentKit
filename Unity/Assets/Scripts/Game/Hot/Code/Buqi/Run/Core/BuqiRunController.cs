using System;

namespace Game.Hot.Buqi.Run.Core
{
    public sealed class BuqiRunController
    {
        private const string RequiredCommandId = "Command id is required.";
        private const string RequiredSettlementId = "Settlement id is required.";
        private const string RevisionMismatch = "State revision mismatch.";
        private const string InvalidPhase = "Command is not valid in the current phase.";
        private const string InvalidBattleKindValue = "Battle kind is invalid.";
        private const string InvalidBattleKind = "Battle kind does not match current phase.";
        private const string InvalidBattleOutcome = "Battle outcome is invalid.";
        private const string InvalidTribulationRoute = "Tribulation route is invalid.";
        private const string InvalidTribulationSpend = "Tribulation seal spend is invalid.";
        private const string InvalidTribulationStage = "Tribulation stage is invalid.";
        private const string RunEnded = "Run has already ended.";
        private const string InvalidOperationPeriod = "Operation period is invalid.";

        private BuqiRunState m_State;

        public BuqiRunController(BuqiRunState initialState)
        {
            if (initialState == null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            m_State = initialState.Clone();
        }

        public BuqiRunState State => m_State.Clone();

        public BuqiRunTransitionResult ResolveEncounter(string commandId, int expectedRevision)
        {
            if (!TryValidateCommand(commandId, expectedRevision, BuqiRunPhase.Encounter, out BuqiRunTransitionResult failure))
            {
                return failure;
            }

            BuqiRunState next = m_State.Clone();
            next.EncounterIndex++;
            switch (m_State.Period)
            {
                case BuqiRunPeriod.Hour1Operation:
                    next.Period = BuqiRunPeriod.Hour2Operation;
                    break;
                case BuqiRunPeriod.Hour2Operation:
                    next.Period = BuqiRunPeriod.Hour3Pve;
                    next.Phase = BuqiRunPhase.PveBattle;
                    break;
                case BuqiRunPeriod.Hour4Operation:
                    next.Period = BuqiRunPeriod.Hour5Operation;
                    break;
                case BuqiRunPeriod.Hour5Operation:
                    next.Period = BuqiRunPeriod.Hour6Pvp;
                    next.Phase = BuqiRunPhase.PvpBattle;
                    break;
                default:
                    return Rejected(InvalidOperationPeriod);
            }

            ApplyCommand(next, commandId);
            return Commit(next);
        }

        public BuqiRunTransitionResult SettleBattle(
            string settlementId,
            int expectedRevision,
            BuqiRunBattleKind battleKind,
            BuqiRunRawBattleOutcome rawOutcome)
        {
            if (string.IsNullOrEmpty(settlementId))
            {
                return Rejected(RequiredSettlementId);
            }

            if (m_State.AppliedSettlementIds.Contains(settlementId))
            {
                return Accepted(true);
            }

            if (m_State.Phase == BuqiRunPhase.RunTerminal)
            {
                return Rejected(RunEnded);
            }

            if (m_State.Revision != expectedRevision)
            {
                return Rejected(RevisionMismatch);
            }

            if (!Enum.IsDefined(typeof(BuqiRunBattleKind), battleKind))
            {
                return Rejected(InvalidBattleKindValue);
            }

            if (!Enum.IsDefined(typeof(BuqiRunRawBattleOutcome), rawOutcome))
            {
                return Rejected(InvalidBattleOutcome);
            }

            BuqiRunPhase expectedPhase = battleKind == BuqiRunBattleKind.Pve
                ? BuqiRunPhase.PveBattle
                : BuqiRunPhase.PvpBattle;
            if (m_State.Phase != expectedPhase)
            {
                return Rejected(InvalidBattleKind);
            }

            BuqiRunState next = m_State.Clone();
            bool isPvpLoss = battleKind == BuqiRunBattleKind.Pvp &&
                             rawOutcome == BuqiRunRawBattleOutcome.OpponentWin;
            if (isPvpLoss)
            {
                next.Lives = Math.Max(0, next.Lives - 1);
                next.CurrentOmen = Math.Min(BuqiRunRules.MaxOmen, next.CurrentOmen + 1);
            }
            else if (rawOutcome != BuqiRunRawBattleOutcome.OpponentWin)
            {
                next.Wins++;
                next.DaoSeals++;
            }

            next.AppliedSettlementIds.Add(settlementId);
            if (next.Lives <= 0)
            {
                next.Lives = 0;
                next.Outcome = BuqiRunOutcome.Defeat;
                next.Phase = BuqiRunPhase.RunTerminal;
            }
            else if (battleKind == BuqiRunBattleKind.Pve)
            {
                next.Period = BuqiRunPeriod.Hour4Operation;
                next.Phase = BuqiRunPhase.Encounter;
            }
            else if (next.Day == BuqiRunRules.RunDayCount)
            {
                next.Period = BuqiRunPeriod.Hour6Pvp;
                next.Phase = BuqiRunPhase.TribulationRoute;
            }
            else
            {
                next.Day++;
                next.EncounterIndex = 0;
                next.Period = BuqiRunPeriod.Hour1Operation;
                next.Phase = BuqiRunPhase.Encounter;
            }

            next.Revision++;
            return Commit(next);
        }

        public BuqiRunTransitionResult CompleteDay(string commandId, int expectedRevision)
        {
            if (!TryValidateCommand(commandId, expectedRevision, BuqiRunPhase.DaySettlement, out BuqiRunTransitionResult failure))
            {
                return failure;
            }

            BuqiRunState next = m_State.Clone();
            next.Day++;
            next.EncounterIndex = 0;
            next.Period = BuqiRunPeriod.Hour1Operation;
            next.Phase = BuqiRunPhase.Encounter;
            ApplyCommand(next, commandId);
            return Commit(next);
        }

        public BuqiRunTransitionResult SelectTribulationRoute(
            string commandId,
            int expectedRevision,
            BuqiTribulationRoute route,
            int daoSealsToSpend)
        {
            if (!TryValidateCommand(commandId, expectedRevision, BuqiRunPhase.TribulationRoute, out BuqiRunTransitionResult failure))
                return failure;

            if (!Enum.IsDefined(typeof(BuqiTribulationRoute), route) || route == BuqiTribulationRoute.None)
                return Rejected(InvalidTribulationRoute);

            if (daoSealsToSpend < 0 || daoSealsToSpend > m_State.DaoSeals ||
                (route != BuqiTribulationRoute.QuestionHeart && daoSealsToSpend != 0) ||
                (route == BuqiTribulationRoute.QuestionHeart && daoSealsToSpend > m_State.CurrentOmen))
            {
                return Rejected(InvalidTribulationSpend);
            }

            BuqiRunState next = m_State.Clone();
            next.TribulationRoute = route;
            next.TribulationDaoSealsSpent = daoSealsToSpend;
            next.DaoSeals -= daoSealsToSpend;
            next.CurrentOmen -= daoSealsToSpend;
            next.TribulationStage = 1;
            next.TribulationSuccesses = 0;
            next.Phase = BuqiRunPhase.TribulationStage;
            ApplyCommand(next, commandId);
            return Commit(next);
        }

        public BuqiRunTransitionResult ResolveTribulationStage(
            string commandId,
            int expectedRevision,
            bool survived)
        {
            if (!TryValidateCommand(commandId, expectedRevision, BuqiRunPhase.TribulationStage, out BuqiRunTransitionResult failure))
                return failure;

            if (m_State.TribulationStage < 1 || m_State.TribulationStage > BuqiRunRules.TribulationStageCount)
                return Rejected(InvalidTribulationStage);

            BuqiRunState next = m_State.Clone();
            if (survived)
                next.TribulationSuccesses++;

            if (next.TribulationStage >= BuqiRunRules.TribulationStageCount)
            {
                next.TribulationStage = BuqiRunRules.TribulationStageCount;
                next.Outcome = next.TribulationSuccesses == BuqiRunRules.TribulationStageCount
                    ? BuqiRunOutcome.Victory
                    : BuqiRunOutcome.Defeat;
                next.Phase = BuqiRunPhase.RunTerminal;
            }
            else
            {
                next.TribulationStage++;
            }

            ApplyCommand(next, commandId);
            return Commit(next);
        }

        private bool TryValidateCommand(
            string commandId,
            int expectedRevision,
            BuqiRunPhase requiredPhase,
            out BuqiRunTransitionResult failure)
        {
            failure = null!;
            if (string.IsNullOrEmpty(commandId))
            {
                failure = Rejected(RequiredCommandId);
            }
            else if (m_State.AppliedCommandIds.Contains(commandId))
            {
                failure = Accepted(true);
            }
            else if (m_State.Phase == BuqiRunPhase.RunTerminal)
            {
                failure = Rejected(RunEnded);
            }
            else if (m_State.Revision != expectedRevision)
            {
                failure = Rejected(RevisionMismatch);
            }
            else if (m_State.Phase != requiredPhase)
            {
                failure = Rejected(InvalidPhase);
            }

            return failure == null;
        }

        private static void ApplyCommand(BuqiRunState state, string commandId)
        {
            state.AppliedCommandIds.Add(commandId);
            state.Revision++;
        }

        private BuqiRunTransitionResult Commit(BuqiRunState next)
        {
            m_State = next;
            return Accepted(false);
        }

        private BuqiRunTransitionResult Accepted(bool replayed)
        {
            return new BuqiRunTransitionResult
            {
                Success = true,
                Replayed = replayed,
                State = State,
            };
        }

        private BuqiRunTransitionResult Rejected(string reason)
        {
            return new BuqiRunTransitionResult
            {
                Success = false,
                FailureReason = reason,
                State = State,
            };
        }
    }
}
