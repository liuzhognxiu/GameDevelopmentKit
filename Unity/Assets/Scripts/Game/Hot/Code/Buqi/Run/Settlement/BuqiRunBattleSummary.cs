using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;

namespace Game.Hot.Buqi.Run.Settlement
{
    [Serializable]
    public sealed class BuqiRunBattleSummary
    {
        public BattleOutcome RawOutcome = BattleOutcome.Draw;
        public string BattleLogHash = string.Empty;
        public string TopSourceInstanceId = string.Empty;
        public int TopContribution;
        public string KeyInterruptionReason = string.Empty;
        public int OverloadLoss;
        public List<string> FactLines = new List<string>();
    }

    [Serializable]
    public sealed class BuqiRunPendingSettlement
    {
        public string SettlementId = string.Empty;
        public int ExpectedRevision;
        public int BattleKind;
        public int RawOutcome;
        public string BattleLogHash = string.Empty;
        public BuqiRunBattleSummary Summary = new BuqiRunBattleSummary();
    }
}
