using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Settlement
{
    public sealed class BuqiRunSettlementResult
    {
        public bool Success;
        public bool Replayed;
        public string FailureReason = string.Empty;
        public BuqiRunState State = null!;
        public BuqiRunBattleSummary Summary = new BuqiRunBattleSummary();
        public BuqiRunRawBattleOutcome RawOutcome;
    }

    public sealed class BuqiRunSettlementCoordinator
    {
        private readonly IBuqiRunStore m_Store;
        private readonly Func<
            BuqiRunController,
            string,
            int,
            BuqiRunBattleKind,
            BuqiRunRawBattleOutcome,
            BuqiRunTransitionResult> m_SettlementExecutor;

        public BuqiRunSettlementCoordinator(
            IBuqiRunStore store,
            Func<
                BuqiRunController,
                string,
                int,
                BuqiRunBattleKind,
                BuqiRunRawBattleOutcome,
                BuqiRunTransitionResult> settlementExecutor = null)
        {
            m_Store = store ?? throw new ArgumentNullException(nameof(store));
            m_SettlementExecutor = settlementExecutor ?? ExecuteSettlement;
        }

        public BuqiRunSettlementResult SettleBattle(
            BuqiRunState state,
            string settlementId,
            BattleResult battleResult,
            IReadOnlyList<BattleEvent> battleLog,
            string economyPayload,
            string encounterPayload,
            string battlePayload)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (battleResult == null)
            {
                throw new ArgumentNullException(nameof(battleResult));
            }

            if (string.IsNullOrWhiteSpace(settlementId))
            {
                return Rejected("Settlement id is required.", state);
            }

            if (!TryMapPhaseToBattleKind(state.Phase, out BuqiRunBattleKind battleKind))
            {
                return Rejected("State phase is not settleable.", state);
            }

            if (!TryMapOutcome(battleResult.Outcome, out BuqiRunRawBattleOutcome rawOutcome))
            {
                return Rejected("Battle outcome cannot be settled.", state);
            }

            BuqiRunBattleSummary summary = BuqiRunBattleSummaryBuilder.Build(battleResult, battleLog);

            if (state.AppliedSettlementIds.Contains(settlementId))
            {
                if (!TryWriteState(state, economyPayload, encounterPayload, battlePayload, null, out string replayWriteError))
                {
                    return Rejected(replayWriteError, state, summary, rawOutcome);
                }

                return Succeeded(state, summary, rawOutcome, true);
            }

            var pendingSettlement = new BuqiRunPendingSettlement
            {
                SettlementId = settlementId,
                ExpectedRevision = state.Revision,
                BattleKind = (int)battleKind,
                RawOutcome = (int)rawOutcome,
                BattleLogHash = summary.BattleLogHash,
                Summary = CloneSummary(summary),
            };

            if (!TryWriteState(
                    state,
                    economyPayload,
                    encounterPayload,
                    battlePayload,
                    pendingSettlement,
                    out string pendingWriteError))
            {
                return Rejected(pendingWriteError, state, summary, rawOutcome);
            }

            var controller = new BuqiRunController(state);
            BuqiRunTransitionResult transition = m_SettlementExecutor(
                controller,
                settlementId,
                state.Revision,
                battleKind,
                rawOutcome);

            if (!transition.Success)
            {
                TryWriteState(state, economyPayload, encounterPayload, battlePayload, null, out _);
                return Rejected(transition.FailureReason, state, summary, rawOutcome);
            }

            if (!TryWriteState(
                    transition.State,
                    economyPayload,
                    encounterPayload,
                    battlePayload,
                    null,
                    out string finalWriteError))
            {
                return Rejected(finalWriteError, state, summary, rawOutcome);
            }

            return Succeeded(transition.State, summary, rawOutcome, transition.Replayed);
        }

        public BuqiRunSettlementResult ResumePendingSettlement()
        {
            if (!m_Store.TryRead(out string json, out string readError))
            {
                return Rejected(readError);
            }

            if (!BuqiRunSaveCodec.TryFromJson(json, out BuqiRunSaveData saveData, out string parseError))
            {
                return Rejected(parseError);
            }

            if (!BuqiRunSaveCodec.TryToState(saveData, out BuqiRunState state, out string stateError))
            {
                return Rejected(stateError);
            }

            if (saveData.PendingSettlement == null)
            {
                return Rejected("No pending settlement is available.", state);
            }

            BuqiRunPendingSettlement pendingSettlement = saveData.PendingSettlement;
            BuqiRunRawBattleOutcome rawOutcome = (BuqiRunRawBattleOutcome)pendingSettlement.RawOutcome;
            BuqiRunBattleKind battleKind = (BuqiRunBattleKind)pendingSettlement.BattleKind;

            var controller = new BuqiRunController(state);
            BuqiRunTransitionResult transition = m_SettlementExecutor(
                controller,
                pendingSettlement.SettlementId,
                pendingSettlement.ExpectedRevision,
                battleKind,
                rawOutcome);

            if (!transition.Success)
            {
                return Rejected(
                    transition.FailureReason,
                    state,
                    pendingSettlement.Summary,
                    rawOutcome);
            }

            if (!TryWriteState(
                    transition.State,
                    saveData.EconomyPayload,
                    saveData.EncounterPayload,
                    saveData.BattlePayload,
                    null,
                    out string finalWriteError))
            {
                return Rejected(
                    finalWriteError,
                    state,
                    pendingSettlement.Summary,
                    rawOutcome);
            }

            return Succeeded(
                transition.State,
                pendingSettlement.Summary,
                rawOutcome,
                transition.Replayed);
        }

        private bool TryWriteState(
            BuqiRunState state,
            string economyPayload,
            string encounterPayload,
            string battlePayload,
            BuqiRunPendingSettlement pendingSettlement,
            out string error)
        {
            try
            {
                BuqiRunSaveData saveData = BuqiRunSaveCodec.FromState(
                    state,
                    economyPayload,
                    encounterPayload,
                    battlePayload,
                    pendingSettlement);
                if (!BuqiRunSaveCodec.TryToState(saveData, out _, out error))
                {
                    return false;
                }

                string json = BuqiRunSaveCodec.ToJson(saveData);
                return m_Store.TryWrite(json, out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static BuqiRunTransitionResult ExecuteSettlement(
            BuqiRunController controller,
            string settlementId,
            int expectedRevision,
            BuqiRunBattleKind battleKind,
            BuqiRunRawBattleOutcome rawOutcome)
        {
            return controller.SettleBattle(
                settlementId,
                expectedRevision,
                battleKind,
                rawOutcome);
        }

        private static bool TryMapPhaseToBattleKind(BuqiRunPhase phase, out BuqiRunBattleKind battleKind)
        {
            switch (phase)
            {
                case BuqiRunPhase.PveBattle:
                    battleKind = BuqiRunBattleKind.Pve;
                    return true;
                case BuqiRunPhase.PvpBattle:
                    battleKind = BuqiRunBattleKind.Pvp;
                    return true;
                default:
                    battleKind = default;
                    return false;
            }
        }

        private static bool TryMapOutcome(BattleOutcome outcome, out BuqiRunRawBattleOutcome rawOutcome)
        {
            switch (outcome)
            {
                case BattleOutcome.LeftWin:
                    rawOutcome = BuqiRunRawBattleOutcome.PlayerWin;
                    return true;
                case BattleOutcome.RightWin:
                    rawOutcome = BuqiRunRawBattleOutcome.OpponentWin;
                    return true;
                case BattleOutcome.Draw:
                    rawOutcome = BuqiRunRawBattleOutcome.Draw;
                    return true;
                default:
                    rawOutcome = default;
                    return false;
            }
        }

        private static BuqiRunSettlementResult Rejected(
            string reason,
            BuqiRunState state = null,
            BuqiRunBattleSummary summary = null,
            BuqiRunRawBattleOutcome rawOutcome = default)
        {
            return new BuqiRunSettlementResult
            {
                Success = false,
                Replayed = false,
                FailureReason = reason ?? string.Empty,
                State = state == null ? null! : state.Clone(),
                Summary = CloneSummary(summary),
                RawOutcome = rawOutcome,
            };
        }

        private static BuqiRunSettlementResult Succeeded(
            BuqiRunState state,
            BuqiRunBattleSummary summary,
            BuqiRunRawBattleOutcome rawOutcome,
            bool replayed)
        {
            return new BuqiRunSettlementResult
            {
                Success = true,
                Replayed = replayed,
                FailureReason = string.Empty,
                State = state.Clone(),
                Summary = CloneSummary(summary),
                RawOutcome = rawOutcome,
            };
        }

        private static BuqiRunBattleSummary CloneSummary(BuqiRunBattleSummary summary)
        {
            if (summary == null)
            {
                return new BuqiRunBattleSummary();
            }

            return new BuqiRunBattleSummary
            {
                RawOutcome = summary.RawOutcome,
                BattleLogHash = summary.BattleLogHash ?? string.Empty,
                TopSourceInstanceId = summary.TopSourceInstanceId ?? string.Empty,
                TopContribution = summary.TopContribution,
                KeyInterruptionReason = summary.KeyInterruptionReason ?? string.Empty,
                OverloadLoss = summary.OverloadLoss,
                FactLines = summary.FactLines == null
                    ? new List<string>()
                    : new List<string>(summary.FactLines),
            };
        }
    }
}
