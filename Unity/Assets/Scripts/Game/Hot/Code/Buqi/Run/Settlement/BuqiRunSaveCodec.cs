using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Core;
#if BUQI_HEADLESS
using System.Text.Json;
#endif
using UnityEngine;

namespace Game.Hot.Buqi.Run.Settlement
{
    public static class BuqiRunSaveCodec
    {
        private const int LegacyV2OperationsBeforeBattle = 3;
        private const int LegacyV2MigratableOpeningOperations = 2;
        private const int PreviousV3OperationsPerDay = 2;
        private const int PreviousV3PvePeriod = 2;
        private const int PreviousV3PvpPeriod = 3;

#if BUQI_HEADLESS
        private static readonly JsonSerializerOptions HeadlessJsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
        };
#endif

        public static BuqiRunSaveData FromState(
            BuqiRunState state,
            string economyPayload = "",
            string encounterPayload = "",
            string battlePayload = "",
            BuqiRunPendingSettlement pendingSettlement = null,
            BuqiRunPendingSettlement lastAppliedSettlement = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new BuqiRunSaveData
            {
                SaveVersion = BuqiRunSaveData.CurrentSaveVersion,
                ContentVersion = Normalize(state.ContentVersion),
                RuleVersion = Normalize(state.RuleVersion),
                RunSeed = state.RunSeed,
                RngCursor = state.RngCursor,
                Revision = state.Revision,
                Day = state.Day,
                EncounterIndex = state.EncounterIndex,
                Period = (int)state.Period,
                Phase = (int)state.Phase,
                Outcome = (int)state.Outcome,
                HeroId = state.HeroId,
                Coins = state.Coins,
                Wins = state.Wins,
                DaoSeals = state.DaoSeals,
                CurrentOmen = state.CurrentOmen,
                Cultivation = state.Cultivation,
                Realm = state.Realm,
                LifePool = state.LifePool,
                InTribulationTrial = state.InTribulationTrial,
                HeartTrialUsed = state.HeartTrialUsed,
                TribulationRoute = (int)state.TribulationRoute,
                TribulationDaoSealsSpent = state.TribulationDaoSealsSpent,
                TribulationStage = state.TribulationStage,
                TribulationSuccesses = state.TribulationSuccesses,
                BoardInstanceIds = CopyList(state.BoardInstanceIds),
                StorageInstanceIds = CopyList(state.StorageInstanceIds),
                AppliedCommandIds = SortIds(state.AppliedCommandIds),
                AppliedSettlementIds = SortIds(state.AppliedSettlementIds),
                EconomyPayload = Normalize(economyPayload),
                EncounterPayload = Normalize(encounterPayload),
                BattlePayload = Normalize(battlePayload),
                HasPendingSettlement = pendingSettlement != null,
                PendingSettlement = ClonePendingSettlement(pendingSettlement),
                HasLastAppliedSettlement = lastAppliedSettlement != null,
                LastAppliedSettlement = ClonePendingSettlement(lastAppliedSettlement),
            };
        }

        public static string ToJson(BuqiRunSaveData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

#if BUQI_HEADLESS
            return JsonSerializer.Serialize(saveData, HeadlessJsonOptions);
#else
            return JsonUtility.ToJson(saveData);
#endif
        }

        public static bool TryFromJson(string json, out BuqiRunSaveData saveData, out string error)
        {
            return TryFromJson(json, out saveData, out error, out _, out _);
        }

        public static bool TryFromJson(
            string json,
            out BuqiRunSaveData saveData,
            out string error,
            out BuqiRunSaveFailureKind failureKind)
        {
            return TryFromJson(json, out saveData, out error, out failureKind, out _);
        }

        public static bool TryFromJson(
            string json,
            out BuqiRunSaveData saveData,
            out string error,
            out BuqiRunSaveFailureKind failureKind,
            out bool wasMigrated)
        {
            saveData = null!;
            error = string.Empty;
            failureKind = BuqiRunSaveFailureKind.InvalidData;
            wasMigrated = false;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Save json is empty.";
                return false;
            }

            try
            {
#if BUQI_HEADLESS
                saveData = JsonSerializer.Deserialize<BuqiRunSaveData>(json, HeadlessJsonOptions);
#else
                saveData = JsonUtility.FromJson<BuqiRunSaveData>(json);
#endif
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (saveData == null)
            {
                error = "Save json produced no data.";
                return false;
            }

            bool isCurrent = string.Equals(saveData.SaveVersion, BuqiRunSaveData.CurrentSaveVersion, StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(saveData.SaveVersion))
            {
                error = "Save schema version is required.";
                saveData = null!;
                return false;
            }

            if (!isCurrent)
            {
                error = "Save schema version is unsupported.";
                failureKind = BuqiRunSaveFailureKind.UnsupportedVersion;
                saveData = null!;
                return false;
            }

            if (!TryToState(saveData, out _, out error))
            {
                saveData = null!;
                return false;
            }

            return true;
        }

        public static bool TryToState(BuqiRunSaveData saveData, out BuqiRunState state, out string error)
        {
            state = null!;
            error = string.Empty;

            if (saveData == null)
            {
                error = "Save data is missing.";
                return false;
            }

            if (!string.Equals(
                    saveData.SaveVersion,
                    BuqiRunSaveData.CurrentSaveVersion,
                    StringComparison.Ordinal))
            {
                error = "Save version is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(saveData.ContentVersion))
            {
                error = "Content version is required.";
                return false;
            }

            if (!string.Equals(
                    saveData.RuleVersion,
                    BuqiRunState.CurrentRuleVersion,
                    StringComparison.Ordinal))
            {
                error = "Rule version is invalid.";
                return false;
            }

            if (saveData.RngCursor < 0 ||
                saveData.Revision < 0 ||
                saveData.Day < 1 ||
                saveData.EncounterIndex < 0 ||
                saveData.EncounterIndex > BuqiRunRules.OperationsPerDay ||
                saveData.Coins < 0 ||
                saveData.Wins < 0 ||
                saveData.Wins > BuqiRunRules.MaxBattleWins ||
                saveData.DaoSeals < 0 ||
                saveData.DaoSeals > BuqiRunRules.MaxDaoSeals ||
                saveData.DaoSeals > saveData.Wins ||
                saveData.CurrentOmen < 0 ||
                saveData.CurrentOmen > BuqiRunRules.MaxOmen ||
                saveData.HeroId < 0 ||
                saveData.HeroId > 4 ||
                saveData.Cultivation < 0 ||
                saveData.Realm < 0 ||
                saveData.Realm >= BuqiRunRules.RealmCount ||
                saveData.Realm != BuqiRunProgression.GetRealm(saveData.Cultivation) ||
                saveData.LifePool < 0 ||
                saveData.LifePool > BuqiRunRules.StartingLifePool ||
                saveData.TribulationDaoSealsSpent < 0 ||
                saveData.TribulationDaoSealsSpent > BuqiRunRules.MaxDaoSeals ||
                saveData.DaoSeals + saveData.TribulationDaoSealsSpent > saveData.Wins ||
                saveData.TribulationStage < 0 ||
                saveData.TribulationStage > BuqiRunRules.TribulationStageCount ||
                saveData.TribulationSuccesses < 0 ||
                saveData.TribulationSuccesses > BuqiRunRules.TribulationStageCount)
            {
                error = "Save counters are out of range.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BuqiRunPhase), saveData.Phase))
            {
                error = "Run phase is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BuqiRunOutcome), saveData.Outcome))
            {
                error = "Run outcome is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BuqiRunPeriod), saveData.Period))
            {
                error = "Run period is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BuqiTribulationRoute), saveData.TribulationRoute))
            {
                error = "Tribulation route is invalid.";
                return false;
            }

            BuqiRunPhase phase = (BuqiRunPhase)saveData.Phase;
            BuqiRunOutcome outcome = (BuqiRunOutcome)saveData.Outcome;
            BuqiRunPeriod period = (BuqiRunPeriod)saveData.Period;
            BuqiTribulationRoute tribulationRoute = (BuqiTribulationRoute)saveData.TribulationRoute;

            if (saveData.DaoSeals + saveData.TribulationDaoSealsSpent != saveData.Wins)
            {
                error = "Dao seal totals do not match battle wins.";
                return false;
            }

            int omenBeforeTribulationSpend = saveData.CurrentOmen + saveData.TribulationDaoSealsSpent;
            if (omenBeforeTribulationSpend > BuqiRunRules.MaxOmen)
            {
                error = "Omen totals do not match tribulation spending.";
                return false;
            }

            if (phase == BuqiRunPhase.Encounter)
            {
                if (!TryGetOperationPeriod(saveData.EncounterIndex, out BuqiRunPeriod expectedPeriod) ||
                    period != expectedPeriod)
                {
                    error = "Operation phase does not match its period or index.";
                    return false;
                }
            }
            else if (phase == BuqiRunPhase.PveBattle)
            {
                if (saveData.EncounterIndex != BuqiRunRules.OperationsBeforePve)
                {
                    error = "Operation index does not match the PVE phase.";
                    return false;
                }
            }
            else if (phase == BuqiRunPhase.RunTerminal)
            {
                bool validTerminalIndex =
                    (period == BuqiRunPeriod.Hour3Pve &&
                     saveData.EncounterIndex == BuqiRunRules.OperationsBeforePve) ||
                    (period == BuqiRunPeriod.Hour6Pvp &&
                     saveData.EncounterIndex == BuqiRunRules.OperationsPerDay);
                if (!validTerminalIndex)
                {
                    error = "Operation index does not match the terminal phase.";
                    return false;
                }
            }
            else if (saveData.EncounterIndex != BuqiRunRules.OperationsPerDay)
            {
                error = "Operation index does not match the current phase.";
                return false;
            }

            if ((phase == BuqiRunPhase.PveBattle && period != BuqiRunPeriod.Hour3Pve) ||
                ((phase == BuqiRunPhase.PvpBattle || phase == BuqiRunPhase.DaySettlement ||
                  phase == BuqiRunPhase.TribulationRoute || phase == BuqiRunPhase.TribulationStage) &&
                 period != BuqiRunPeriod.Hour6Pvp) ||
                (phase == BuqiRunPhase.RunTerminal &&
                 period != BuqiRunPeriod.Hour3Pve && period != BuqiRunPeriod.Hour6Pvp))
            {
                error = "Run period does not match the current phase.";
                return false;
            }

            bool isTribulation = phase == BuqiRunPhase.TribulationRoute ||
                                 phase == BuqiRunPhase.TribulationStage ||
                                 (phase == BuqiRunPhase.RunTerminal &&
                                  tribulationRoute != BuqiTribulationRoute.None);
            if (isTribulation && saveData.Wins != BuqiRunRules.WinsToVictory - 1 &&
                !(phase == BuqiRunPhase.RunTerminal && outcome == BuqiRunOutcome.Victory &&
                  saveData.Wins == BuqiRunRules.WinsToVictory))
            {
                error = "Tribulation requires nine wins, or ten wins after victory.";
                return false;
            }

            if (phase == BuqiRunPhase.RunTerminal)
            {
                if (outcome == BuqiRunOutcome.None)
                {
                    error = "Terminal phase requires a terminal outcome.";
                    return false;
                }
            }
            else if (outcome != BuqiRunOutcome.None)
            {
                error = "Non-terminal phase cannot carry a terminal outcome.";
                return false;
            }

            if (phase == BuqiRunPhase.TribulationRoute)
            {
                if (tribulationRoute != BuqiTribulationRoute.None ||
                    saveData.TribulationDaoSealsSpent != 0 ||
                    saveData.TribulationStage != 0 ||
                    saveData.TribulationSuccesses != 0)
                {
                    error = "Route choice phase cannot contain a selected tribulation route.";
                    return false;
                }
            }
            else if (phase == BuqiRunPhase.TribulationStage)
            {
                if (tribulationRoute == BuqiTribulationRoute.None ||
                    saveData.TribulationStage < 1 ||
                    saveData.TribulationSuccesses >= saveData.TribulationStage)
                {
                    error = "Active tribulation stage fields are inconsistent.";
                    return false;
                }
            }
            else if (phase == BuqiRunPhase.RunTerminal)
            {
                bool earlyDefeat = tribulationRoute == BuqiTribulationRoute.None;
                if (earlyDefeat &&
                    (outcome != BuqiRunOutcome.Defeat || saveData.LifePool != 0 ||
                     !saveData.HeartTrialUsed || saveData.InTribulationTrial ||
                     saveData.TribulationDaoSealsSpent != 0 || saveData.TribulationStage != 0 ||
                     saveData.TribulationSuccesses != 0))
                {
                    error = "Early terminal state must be a life-depletion defeat.";
                    return false;
                }

                bool validTribulationVictory =
                    outcome == BuqiRunOutcome.Victory &&
                    saveData.TribulationStage == BuqiRunRules.TribulationStageCount &&
                    saveData.TribulationSuccesses == BuqiRunRules.TribulationStageCount;
                bool validTribulationDefeat =
                    outcome == BuqiRunOutcome.Defeat &&
                    saveData.TribulationStage >= 1 &&
                    saveData.TribulationStage <= BuqiRunRules.TribulationStageCount &&
                    saveData.TribulationSuccesses == saveData.TribulationStage - 1;
                if (!earlyDefeat && !validTribulationVictory && !validTribulationDefeat)
                {
                    error = "Terminal tribulation fields do not match the outcome.";
                    return false;
                }
            }
            else if (tribulationRoute != BuqiTribulationRoute.None ||
                     saveData.TribulationDaoSealsSpent != 0 ||
                     saveData.TribulationStage != 0 ||
                     saveData.TribulationSuccesses != 0)
            {
                error = "Pre-tribulation phase cannot contain tribulation progress.";
                return false;
            }

            if (saveData.InTribulationTrial && !saveData.HeartTrialUsed)
            {
                error = "Heart trial flags are inconsistent.";
                return false;
            }

            if (phase != BuqiRunPhase.RunTerminal &&
                saveData.LifePool == 0 && !saveData.InTribulationTrial)
            {
                error = "Life depletion requires a heart trial or terminal defeat.";
                return false;
            }

            if (isTribulation && (saveData.LifePool == 0 || saveData.InTribulationTrial))
            {
                error = "Tribulation cannot overlap the heart trial.";
                return false;
            }

            if (tribulationRoute != BuqiTribulationRoute.QuestionHeart &&
                saveData.TribulationDaoSealsSpent != 0)
            {
                error = "Only Question Heart can spend Dao seals during route choice.";
                return false;
            }

            if (!TryValidateSlots(
                    saveData.BoardInstanceIds,
                    "board",
                    saveData.StorageInstanceIds,
                    "storage",
                    out error))
            {
                return false;
            }

            if (!TryBuildIdSet(
                    saveData.AppliedCommandIds,
                    "command",
                    out HashSet<string> commandIds,
                    out error))
            {
                return false;
            }

            if (!TryBuildIdSet(
                    saveData.AppliedSettlementIds,
                    "settlement",
                    out HashSet<string> settlementIds,
                    out error))
            {
                return false;
            }

            if (!TryNormalizePendingSettlement(
                    saveData.HasPendingSettlement,
                    saveData.PendingSettlement,
                    out BuqiRunPendingSettlement pendingSettlement,
                    out error))
            {
                return false;
            }

            if (!TryValidatePendingSettlement(
                    pendingSettlement,
                    saveData.Revision,
                    phase,
                    outcome,
                    settlementIds,
                    out error))
            {
                return false;
            }

            if (!TryNormalizePendingSettlement(
                    saveData.HasLastAppliedSettlement,
                    saveData.LastAppliedSettlement,
                    out BuqiRunPendingSettlement lastAppliedSettlement,
                    out error))
            {
                return false;
            }

            if (!TryValidateLastAppliedSettlement(
                    lastAppliedSettlement,
                    saveData.Revision,
                    settlementIds,
                    out error))
            {
                return false;
            }

            saveData.PendingSettlement = pendingSettlement;
            saveData.LastAppliedSettlement = lastAppliedSettlement;

            state = new BuqiRunState
            {
                ContentVersion = saveData.ContentVersion,
                RuleVersion = saveData.RuleVersion,
                RunSeed = saveData.RunSeed,
                RngCursor = saveData.RngCursor,
                Revision = saveData.Revision,
                Day = saveData.Day,
                EncounterIndex = saveData.EncounterIndex,
                Period = period,
                Phase = phase,
                Outcome = outcome,
                HeroId = saveData.HeroId,
                Coins = saveData.Coins,
                Wins = saveData.Wins,
                DaoSeals = saveData.DaoSeals,
                CurrentOmen = saveData.CurrentOmen,
                Cultivation = saveData.Cultivation,
                Realm = saveData.Realm,
                LifePool = saveData.LifePool,
                InTribulationTrial = saveData.InTribulationTrial,
                HeartTrialUsed = saveData.HeartTrialUsed,
                TribulationRoute = tribulationRoute,
                TribulationDaoSealsSpent = saveData.TribulationDaoSealsSpent,
                TribulationStage = saveData.TribulationStage,
                TribulationSuccesses = saveData.TribulationSuccesses,
                BoardInstanceIds = CopyList(saveData.BoardInstanceIds),
                StorageInstanceIds = CopyList(saveData.StorageInstanceIds),
                AppliedCommandIds = commandIds,
                AppliedSettlementIds = settlementIds,
            };

            return true;
        }

        private static bool TryMigrateLegacyV2(BuqiRunSaveData saveData, out string error)
        {
            error = string.Empty;
            if (!string.Equals(saveData.RuleVersion, "buqi-day-run-rule-v1", StringComparison.Ordinal))
            {
                error = "Legacy rule version is invalid.";
                return false;
            }

            if (saveData.Day < 1 || saveData.Day > BuqiRunRules.RunDayCount ||
                !Enum.IsDefined(typeof(BuqiRunPhase), saveData.Phase) ||
                saveData.Wins >= 9)
            {
                error = "Legacy save cannot be migrated to the nine-day rules.";
                return false;
            }

            BuqiRunPhase phase = (BuqiRunPhase)saveData.Phase;
            switch (phase)
            {
                case BuqiRunPhase.Encounter:
                    if (saveData.EncounterIndex < 0 ||
                        saveData.EncounterIndex >= LegacyV2MigratableOpeningOperations)
                    {
                        error = "Legacy third operation cannot be migrated safely.";
                        return false;
                    }

                    saveData.Period = saveData.EncounterIndex == 0
                        ? (int)BuqiRunPeriod.Hour1Operation
                        : (int)BuqiRunPeriod.Hour2Operation;
                    break;

                case BuqiRunPhase.PveBattle:
                    if (saveData.EncounterIndex != LegacyV2OperationsBeforeBattle)
                    {
                        error = "Legacy PVE operation index is invalid.";
                        return false;
                    }

                    saveData.EncounterIndex = BuqiRunRules.OperationsBeforePve;
                    saveData.Period = (int)BuqiRunPeriod.Hour3Pve;
                    break;

                case BuqiRunPhase.PvpBattle:
                case BuqiRunPhase.DaySettlement:
                    if (saveData.EncounterIndex != LegacyV2OperationsBeforeBattle ||
                        (phase == BuqiRunPhase.DaySettlement && saveData.Day == BuqiRunRules.RunDayCount))
                    {
                        error = "Legacy night state cannot be migrated safely.";
                        return false;
                    }

                    saveData.EncounterIndex = BuqiRunRules.OperationsPerDay;
                    saveData.Period = (int)BuqiRunPeriod.Hour6Pvp;
                    break;

                case BuqiRunPhase.RunTerminal:
                default:
                    error = "Legacy early terminal state cannot be migrated to the fixed nine-day run.";
                    return false;
            }

            saveData.DaoSeals = saveData.Wins;
            saveData.CurrentOmen = Math.Max(0, BuqiRunRules.StartingLifePool - saveData.LifePool);
            saveData.TribulationRoute = (int)BuqiTribulationRoute.None;
            saveData.TribulationDaoSealsSpent = 0;
            saveData.TribulationStage = 0;
            saveData.TribulationSuccesses = 0;
            bool hasPendingPayload = saveData.PendingSettlement != null &&
                                     !IsEmptyPendingSettlementPlaceholder(saveData.PendingSettlement);
            saveData.HasPendingSettlement = hasPendingPayload;
            saveData.HasLastAppliedSettlement = false;
            saveData.LastAppliedSettlement = null;
            saveData.SaveVersion = BuqiRunSaveData.CurrentSaveVersion;
            saveData.RuleVersion = BuqiRunState.CurrentRuleVersion;
            return true;
        }

        private static bool TryMigratePreviousV3(BuqiRunSaveData saveData, out string error)
        {
            error = string.Empty;
            if (!string.Equals(saveData.RuleVersion, BuqiRunState.PreviousRuleVersion, StringComparison.Ordinal))
            {
                error = "Previous rule version is invalid.";
                return false;
            }

            if (saveData.Day < 1 || saveData.Day > BuqiRunRules.RunDayCount ||
                !Enum.IsDefined(typeof(BuqiRunPhase), saveData.Phase))
            {
                error = "Previous save cannot be migrated to the six-hour rules.";
                return false;
            }

            BuqiRunPhase phase = (BuqiRunPhase)saveData.Phase;
            switch (phase)
            {
                case BuqiRunPhase.Encounter:
                    if (saveData.EncounterIndex < 0 ||
                        saveData.EncounterIndex >= PreviousV3OperationsPerDay ||
                        saveData.Period != saveData.EncounterIndex)
                    {
                        error = "Previous operation state cannot be migrated safely.";
                        return false;
                    }

                    saveData.Period = saveData.EncounterIndex == 0
                        ? (int)BuqiRunPeriod.Hour1Operation
                        : (int)BuqiRunPeriod.Hour2Operation;
                    break;

                case BuqiRunPhase.PveBattle:
                    if (saveData.EncounterIndex != PreviousV3OperationsPerDay ||
                        saveData.Period != PreviousV3PvePeriod)
                    {
                        error = "Previous PVE state cannot be migrated safely.";
                        return false;
                    }

                    saveData.EncounterIndex = BuqiRunRules.OperationsBeforePve;
                    saveData.Period = (int)BuqiRunPeriod.Hour3Pve;
                    break;

                case BuqiRunPhase.PvpBattle:
                case BuqiRunPhase.DaySettlement:
                case BuqiRunPhase.TribulationRoute:
                case BuqiRunPhase.TribulationStage:
                    if (saveData.EncounterIndex != PreviousV3OperationsPerDay ||
                        saveData.Period != PreviousV3PvpPeriod)
                    {
                        error = "Previous night state cannot be migrated safely.";
                        return false;
                    }

                    saveData.EncounterIndex = BuqiRunRules.OperationsPerDay;
                    saveData.Period = (int)BuqiRunPeriod.Hour6Pvp;
                    break;

                case BuqiRunPhase.RunTerminal:
                    if (saveData.EncounterIndex != PreviousV3OperationsPerDay ||
                        (saveData.Period != PreviousV3PvePeriod &&
                         saveData.Period != PreviousV3PvpPeriod))
                    {
                        error = "Previous terminal state cannot be migrated safely.";
                        return false;
                    }

                    bool endedDuringPve = saveData.Period == PreviousV3PvePeriod;
                    saveData.EncounterIndex = endedDuringPve
                        ? BuqiRunRules.OperationsBeforePve
                        : BuqiRunRules.OperationsPerDay;
                    saveData.Period = endedDuringPve
                        ? (int)BuqiRunPeriod.Hour3Pve
                        : (int)BuqiRunPeriod.Hour6Pvp;
                    break;

                default:
                    error = "Previous phase cannot be migrated safely.";
                    return false;
            }

            saveData.SaveVersion = BuqiRunSaveData.CurrentSaveVersion;
            saveData.RuleVersion = BuqiRunState.CurrentRuleVersion;
            return true;
        }

        private static bool TryGetOperationPeriod(int encounterIndex, out BuqiRunPeriod period)
        {
            switch (encounterIndex)
            {
                case 0:
                    period = BuqiRunPeriod.Hour1Operation;
                    return true;
                case 1:
                    period = BuqiRunPeriod.Hour2Operation;
                    return true;
                case 2:
                    period = BuqiRunPeriod.Hour4Operation;
                    return true;
                case 3:
                    period = BuqiRunPeriod.Hour5Operation;
                    return true;
                default:
                    period = default;
                    return false;
            }
        }

        private static string Normalize(string value)
        {
            return value ?? string.Empty;
        }

        private static List<string> CopyList(List<string> values)
        {
            return values == null ? new List<string>() : new List<string>(values);
        }

        private static List<string> SortIds(IEnumerable<string> ids)
        {
            var list = new List<string>();
            if (ids != null)
            {
                foreach (string id in ids)
                {
                    list.Add(id ?? string.Empty);
                }
            }

            list.Sort(StringComparer.Ordinal);
            return list;
        }

        private static bool TryValidateSlots(
            List<string> boardSlots,
            string boardLabel,
            List<string> storageSlots,
            string storageLabel,
            out string error)
        {
            error = string.Empty;
            if (boardSlots == null || boardSlots.Count != BuqiRunRules.BoardSlotCount)
            {
                error = "Board slot count is invalid.";
                return false;
            }

            if (storageSlots == null || storageSlots.Count != BuqiRunRules.StorageSlotCount)
            {
                error = "Storage slot count is invalid.";
                return false;
            }

            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            if (!TryValidateSlotList(boardSlots, boardLabel, knownIds, out error))
            {
                return false;
            }

            return TryValidateSlotList(storageSlots, storageLabel, knownIds, out error);
        }

        private static bool TryValidateSlotList(
            List<string> slots,
            string label,
            HashSet<string> knownIds,
            out string error)
        {
            error = string.Empty;
            for (int index = 0; index < slots.Count; index++)
            {
                string instanceId = slots[index];
                if (instanceId == null)
                {
                    error = BuqiText.Format("{0} slot {1} is null.", label, index);
                    return false;
                }

                if (instanceId.Length == 0)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    error = BuqiText.Format("{0} slot {1} contains whitespace only.", label, index);
                    return false;
                }

                if (!knownIds.Add(instanceId))
                {
                    error = BuqiText.Format("Duplicate instance id '{0}' appears in saved slots.", instanceId);
                    return false;
                }
            }

            return true;
        }

        private static bool TryBuildIdSet(
            List<string> ids,
            string label,
            out HashSet<string> set,
            out string error)
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            error = string.Empty;

            if (ids == null)
            {
                error = BuqiText.Format("Applied {0} ids are missing.", label);
                return false;
            }

            for (int index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                if (string.IsNullOrWhiteSpace(id))
                {
                    error = BuqiText.Format("Applied {0} id at index {1} is invalid.", label, index);
                    return false;
                }

                if (!set.Add(id))
                {
                    error = BuqiText.Format("Applied {0} id '{1}' is duplicated.", label, id);
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidatePendingSettlement(
            BuqiRunPendingSettlement pendingSettlement,
            int revision,
            BuqiRunPhase phase,
            BuqiRunOutcome outcome,
            HashSet<string> appliedSettlementIds,
            out string error)
        {
            error = string.Empty;
            if (pendingSettlement == null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(pendingSettlement.SettlementId))
            {
                error = "Pending settlement id is required.";
                return false;
            }

            if (pendingSettlement.ExpectedRevision != revision)
            {
                error = "Pending settlement revision does not match the save revision.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BuqiRunBattleKind), pendingSettlement.BattleKind))
            {
                error = "Pending settlement battle kind is invalid.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BuqiRunRawBattleOutcome), pendingSettlement.RawOutcome))
            {
                error = "Pending settlement raw outcome is invalid.";
                return false;
            }

            BuqiRunBattleKind battleKind = (BuqiRunBattleKind)pendingSettlement.BattleKind;
            BuqiRunPhase expectedPhase = battleKind == BuqiRunBattleKind.Pve
                ? BuqiRunPhase.PveBattle
                : BuqiRunPhase.PvpBattle;
            if (phase != expectedPhase || outcome != BuqiRunOutcome.None)
            {
                error = "Pending settlement does not match the saved phase.";
                return false;
            }

            if (appliedSettlementIds.Contains(pendingSettlement.SettlementId))
            {
                error = "Pending settlement has already been applied.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(pendingSettlement.BattleLogHash))
            {
                error = "Pending settlement battle log hash is required.";
                return false;
            }

            if (pendingSettlement.Summary == null)
            {
                error = "Pending settlement summary is missing.";
                return false;
            }

            if (!Enum.IsDefined(typeof(BattleOutcome), pendingSettlement.Summary.RawOutcome))
            {
                error = "Pending settlement summary outcome is invalid.";
                return false;
            }

            if (!string.Equals(
                    pendingSettlement.Summary.BattleLogHash,
                    pendingSettlement.BattleLogHash,
                    StringComparison.Ordinal))
            {
                error = "Pending settlement summary hash does not match the pending hash.";
                return false;
            }

            if (pendingSettlement.Summary.FactLines == null)
            {
                error = "Pending settlement fact lines are missing.";
                return false;
            }

            if (!TryValidateSummaryOutcomeConsistency(
                    (BuqiRunRawBattleOutcome)pendingSettlement.RawOutcome,
                    pendingSettlement.Summary.RawOutcome))
            {
                error = "Pending settlement summary outcome does not match the raw outcome.";
                return false;
            }

            if (pendingSettlement.Summary.TopContribution < 0)
            {
                error = "Pending settlement top contribution cannot be negative.";
                return false;
            }

            if (pendingSettlement.Summary.OverloadLoss < 0)
            {
                error = "Pending settlement overload loss cannot be negative.";
                return false;
            }

            for (int index = 0; index < pendingSettlement.Summary.FactLines.Count; index++)
            {
                if (pendingSettlement.Summary.FactLines[index] == null)
                {
                    error = BuqiText.Format("Pending settlement fact line {0} is null.", index);
                    return false;
                }
            }

            return true;
        }

        private static bool TryNormalizePendingSettlement(
            bool hasPendingSettlement,
            BuqiRunPendingSettlement pendingSettlement,
            out BuqiRunPendingSettlement normalized,
            out string error)
        {
            normalized = pendingSettlement;
            error = string.Empty;
            if (hasPendingSettlement)
            {
                if (pendingSettlement != null)
                    return true;

                error = "Pending settlement payload is missing.";
                return false;
            }

            if (pendingSettlement != null && !IsEmptyPendingSettlementPlaceholder(pendingSettlement))
            {
                error = BuqiText.Format(
                    "Pending settlement presence flag does not match its payload: {0}",
                    JsonUtility.ToJson(pendingSettlement));
                return false;
            }

            normalized = null;
            return true;
        }

        private static bool TryValidateLastAppliedSettlement(
            BuqiRunPendingSettlement receipt,
            int revision,
            HashSet<string> appliedSettlementIds,
            out string error)
        {
            error = string.Empty;
            if (receipt == null)
                return true;

            if (string.IsNullOrWhiteSpace(receipt.SettlementId) ||
                !appliedSettlementIds.Contains(receipt.SettlementId))
            {
                error = "Last applied settlement receipt is not present in applied ids.";
                return false;
            }

            if (receipt.ExpectedRevision + 1 != revision ||
                !Enum.IsDefined(typeof(BuqiRunBattleKind), receipt.BattleKind) ||
                !Enum.IsDefined(typeof(BuqiRunRawBattleOutcome), receipt.RawOutcome) ||
                string.IsNullOrWhiteSpace(receipt.BattleLogHash) ||
                receipt.Summary == null ||
                !Enum.IsDefined(typeof(BattleOutcome), receipt.Summary.RawOutcome) ||
                !string.Equals(receipt.Summary.BattleLogHash, receipt.BattleLogHash, StringComparison.Ordinal) ||
                !TryValidateSummaryOutcomeConsistency(
                    (BuqiRunRawBattleOutcome)receipt.RawOutcome,
                    receipt.Summary.RawOutcome) ||
                receipt.Summary.TopContribution < 0 ||
                receipt.Summary.OverloadLoss < 0 ||
                receipt.Summary.FactLines == null)
            {
                error = "Last applied settlement receipt is invalid.";
                return false;
            }

            for (int index = 0; index < receipt.Summary.FactLines.Count; index++)
            {
                if (receipt.Summary.FactLines[index] == null)
                {
                    error = BuqiText.Format("Last applied settlement fact line {0} is null.", index);
                    return false;
                }
            }

            return true;
        }

        private static bool IsEmptyPendingSettlementPlaceholder(BuqiRunPendingSettlement pendingSettlement)
        {
            if (!string.IsNullOrEmpty(pendingSettlement.SettlementId) ||
                pendingSettlement.ExpectedRevision != 0 ||
                pendingSettlement.BattleKind != 0 ||
                pendingSettlement.RawOutcome != 0 ||
                !string.IsNullOrEmpty(pendingSettlement.BattleLogHash))
            {
                return false;
            }

            BuqiRunBattleSummary summary = pendingSettlement.Summary;
            return summary == null ||
                   (summary.RawOutcome == BattleOutcome.Draw &&
                    string.IsNullOrEmpty(summary.BattleLogHash) &&
                    string.IsNullOrEmpty(summary.TopSourceInstanceId) &&
                    summary.TopContribution == 0 &&
                    string.IsNullOrEmpty(summary.KeyInterruptionReason) &&
                    summary.OverloadLoss == 0 &&
                    (summary.FactLines == null || summary.FactLines.Count == 0));
        }

        private static bool TryValidateSummaryOutcomeConsistency(
            BuqiRunRawBattleOutcome rawOutcome,
            BattleOutcome summaryOutcome)
        {
            switch (rawOutcome)
            {
                case BuqiRunRawBattleOutcome.PlayerWin:
                    return summaryOutcome == BattleOutcome.LeftWin;
                case BuqiRunRawBattleOutcome.OpponentWin:
                    return summaryOutcome == BattleOutcome.RightWin;
                case BuqiRunRawBattleOutcome.Draw:
                    return summaryOutcome == BattleOutcome.Draw;
                default:
                    return false;
            }
        }

        private static BuqiRunPendingSettlement ClonePendingSettlement(BuqiRunPendingSettlement pendingSettlement)
        {
            if (pendingSettlement == null)
            {
                return null;
            }

            return new BuqiRunPendingSettlement
            {
                SettlementId = Normalize(pendingSettlement.SettlementId),
                ExpectedRevision = pendingSettlement.ExpectedRevision,
                BattleKind = pendingSettlement.BattleKind,
                RawOutcome = pendingSettlement.RawOutcome,
                BattleLogHash = Normalize(pendingSettlement.BattleLogHash),
                Summary = CloneSummary(pendingSettlement.Summary),
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
                BattleLogHash = Normalize(summary.BattleLogHash),
                TopSourceInstanceId = Normalize(summary.TopSourceInstanceId),
                TopContribution = summary.TopContribution,
                KeyInterruptionReason = Normalize(summary.KeyInterruptionReason),
                OverloadLoss = summary.OverloadLoss,
                FactLines = summary.FactLines == null
                    ? new List<string>()
                    : new List<string>(summary.FactLines),
            };
        }
    }
}
