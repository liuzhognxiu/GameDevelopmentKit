using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Battle
{
    public sealed class BuqiRunBattleSession
    {
        public string BattleId = string.Empty;
        public BuqiRunBattleKind Kind;
        public BuqiPveDifficulty? PveDifficulty;
        public string OpponentId = string.Empty;
        public int NextRngCursor;
        public BattleRequest Request;
        public BattleResult Result;
        public List<BattleEvent> Log = new List<BattleEvent>();
        public BattleReplayData Replay;
        public BuqiRunRawBattleOutcome RawOutcome;
    }
}
