using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public sealed class BattleReplayFilter
    {
        public bool KeyOnly;
        public string SourceInstanceId = string.Empty;
        public string TargetInstanceId = string.Empty;
        public string ChainId = string.Empty;
        public string ReasonCode = string.Empty;
    }

    public sealed class BattleReplayLogRow
    {
        public BattleEvent Event;
        public string Summary = string.Empty;
    }

    public sealed class BattleReplayLogPage
    {
        public int PageIndex;
        public int PageCount;
        public IReadOnlyList<BattleReplayLogRow> Rows = Array.Empty<BattleReplayLogRow>();
    }

    public sealed class BattleReplayFact
    {
        public string Kind = string.Empty;
        public string Summary = string.Empty;
        public IReadOnlyList<int> EventSequences = Array.Empty<int>();
    }
}
