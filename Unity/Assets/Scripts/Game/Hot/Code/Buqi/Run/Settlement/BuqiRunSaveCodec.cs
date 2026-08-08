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
            BuqiRunPendingSettlement pendingSettlement = null)
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
                Phase = (int)state.Phase,
                Outcome = (int)state.Outcome,
                Coins = state.Coins,
                Wins = state.Wins,
                Lives = state.Lives,
                BoardInstanceIds = CopyList(state.BoardInstanceIds),
                StorageInstanceIds = CopyList(state.StorageInstanceIds),
                AppliedCommandIds = SortIds(state.AppliedCommandIds),
                AppliedSettlementIds = SortIds(state.AppliedSettlementIds),
                EconomyPayload = Normalize(economyPayload),
                EncounterPayload = Normalize(encounterPayload),
                BattlePayload = Normalize(battlePayload),
                HasPendingSettlement = pendingSettlement != null,
                PendingSettlement = ClonePendingSettlement(pendingSettlement),
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
            saveData = null!;
            error = string.Empty;

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
                saveData.EncounterIndex > BuqiRunRules.EncountersPerDay ||
                saveData.Coins < 0 ||
                saveData.Wins < 0 ||
                saveData.Wins > BuqiRunRules.WinsToVictory ||
                saveData.Lives < 0 ||
                saveData.Lives > BuqiRunRules.StartingLives)
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

            BuqiRunPhase phase = (BuqiRunPhase)saveData.Phase;
            BuqiRunOutcome outcome = (BuqiRunOutcome)saveData.Outcome;

            if (phase == BuqiRunPhase.Encounter)
            {
                if (saveData.EncounterIndex >= BuqiRunRules.EncountersPerDay)
                {
                    error = "Encounter phase index is invalid.";
                    return false;
                }
            }
            else if (saveData.EncounterIndex != BuqiRunRules.EncountersPerDay)
            {
                error = "Encounter index does not match the current phase.";
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

            if (outcome == BuqiRunOutcome.Victory && saveData.Wins != BuqiRunRules.WinsToVictory)
            {
                error = "Victory save must be capped at the win target.";
                return false;
            }

            if (outcome == BuqiRunOutcome.Defeat && saveData.Lives != 0)
            {
                error = "Defeat save must have zero lives.";
                return false;
            }

            if (outcome != BuqiRunOutcome.Victory && saveData.Wins >= BuqiRunRules.WinsToVictory)
            {
                error = "Non-victory save cannot already be at the win target.";
                return false;
            }

            if (outcome != BuqiRunOutcome.Defeat && saveData.Lives <= 0)
            {
                error = "Non-defeat save must keep at least one life.";
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

            saveData.PendingSettlement = pendingSettlement;

            state = new BuqiRunState
            {
                ContentVersion = saveData.ContentVersion,
                RuleVersion = saveData.RuleVersion,
                RunSeed = saveData.RunSeed,
                RngCursor = saveData.RngCursor,
                Revision = saveData.Revision,
                Day = saveData.Day,
                EncounterIndex = saveData.EncounterIndex,
                Phase = phase,
                Outcome = outcome,
                Coins = saveData.Coins,
                Wins = saveData.Wins,
                Lives = saveData.Lives,
                BoardInstanceIds = CopyList(saveData.BoardInstanceIds),
                StorageInstanceIds = CopyList(saveData.StorageInstanceIds),
                AppliedCommandIds = commandIds,
                AppliedSettlementIds = settlementIds,
            };

            return true;
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
