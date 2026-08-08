using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Settlement
{
    [Serializable]
    public sealed class BuqiRunSaveData
    {
        public const string LegacySaveVersion = "buqi-run-save-v2";
        public const string CurrentSaveVersion = "buqi-run-save-v3";

        public string SaveVersion = CurrentSaveVersion;
        public string ContentVersion = string.Empty;
        public string RuleVersion = string.Empty;
        public long RunSeed;
        public int RngCursor;
        public int Revision;
        public int Day;
        public int EncounterIndex;
        public int Period;
        public int Phase;
        public int Outcome;
        public int Coins;
        public int Wins;
        public int DaoSeals;
        public int CurrentOmen;
        public int Lives;
        public int TribulationRoute;
        public int TribulationDaoSealsSpent;
        public int TribulationStage;
        public int TribulationSuccesses;
        public List<string> BoardInstanceIds = new List<string>();
        public List<string> StorageInstanceIds = new List<string>();
        public List<string> AppliedCommandIds = new List<string>();
        public List<string> AppliedSettlementIds = new List<string>();
        public string EconomyPayload = string.Empty;
        public string EncounterPayload = string.Empty;
        public string BattlePayload = string.Empty;
        public bool HasPendingSettlement;
        public BuqiRunPendingSettlement PendingSettlement;
        public bool HasLastAppliedSettlement;
        public BuqiRunPendingSettlement LastAppliedSettlement;
    }
}
