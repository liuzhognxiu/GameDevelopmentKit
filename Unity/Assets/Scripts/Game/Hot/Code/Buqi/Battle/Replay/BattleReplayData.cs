using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public sealed class BattleReplayEffectInfo
    {
        public string EffectId = string.Empty;
        public BuqiEffect Effect;
        public BuqiTarget Target;
    }

    public sealed class BattleReplayData
    {
        public string Title = string.Empty;
        public string LeftName = string.Empty;
        public string RightName = string.Empty;
        public BuildSnapshot LeftBuild;
        public BuildSnapshot RightBuild;
        public BattleResult Result;
        public IReadOnlyList<BattleEvent> Log = Array.Empty<BattleEvent>();
        public IItemDefinitionProvider Definitions;
        public IReadOnlyDictionary<string, BattleReplayEffectInfo> Effects =
            new Dictionary<string, BattleReplayEffectInfo>(StringComparer.Ordinal);
    }
}
