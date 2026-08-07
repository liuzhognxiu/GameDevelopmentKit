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
        private const string RunEnded = "Run has already ended.";

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
            if (next.EncounterIndex >= BuqiRunRules.EncountersPerDay)
            {
                next.Phase = BuqiRunPhase.PveBattle;
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
            if (rawOutcome == BuqiRunRawBattleOutcome.OpponentWin)
            {
                next.Lives--;
            }
            else
            {
                next.Wins++;
            }

            next.AppliedSettlementIds.Add(settlementId);
            if (next.Wins >= BuqiRunRules.WinsToVictory)
            {
                next.Wins = BuqiRunRules.WinsToVictory;
                next.Outcome = BuqiRunOutcome.Victory;
                next.Phase = BuqiRunPhase.RunTerminal;
            }
            else if (next.Lives <= 0)
            {
                next.Lives = 0;
                next.Outcome = BuqiRunOutcome.Defeat;
                next.Phase = BuqiRunPhase.RunTerminal;
            }
            else
            {
                next.Phase = battleKind == BuqiRunBattleKind.Pve
                    ? BuqiRunPhase.PvpBattle
                    : BuqiRunPhase.DaySettlement;
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
            next.Phase = BuqiRunPhase.Encounter;
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
