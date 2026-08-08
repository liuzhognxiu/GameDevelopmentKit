using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Settlement
{
    [Serializable]
    public sealed class BuqiRunSaveData
    {
        public const string CurrentSaveVersion = "buqi-run-save-v1";

        public string SaveVersion = CurrentSaveVersion;
        public string ContentVersion = string.Empty;
        public string RuleVersion = string.Empty;
        public long RunSeed;
        public int RngCursor;
        public int Revision;
        public int Day;
        public int EncounterIndex;
        public int Phase;
        public int Outcome;
        public int Coins;
        public int Wins;
        public int Lives;
        public List<string> BoardInstanceIds = new List<string>();
        public List<string> StorageInstanceIds = new List<string>();
        public List<string> AppliedCommandIds = new List<string>();
        public List<string> AppliedSettlementIds = new List<string>();
        public string EconomyPayload = string.Empty;
        public string EncounterPayload = string.Empty;
        public string BattlePayload = string.Empty;
        public BuqiRunPendingSettlement PendingSettlement;
    }
}
