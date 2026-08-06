using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Battle
{
    public sealed class BattleReplayItemFrame
    {
        public string InstanceId = string.Empty;
        public string DefinitionId = string.Empty;
        public int AnchorSlot;
        public int Size;
        public int Charge;
        public int FrozenTicks;
        public float Cooldown01;
    }

    public sealed class BattleReplaySideFrame
    {
        public int Execution;
        public int MaxExecution;
        public int Buffer;
        public int Noise;
        public IReadOnlyList<BattleReplayItemFrame> Items = Array.Empty<BattleReplayItemFrame>();
        public IReadOnlyList<string> Slots = Array.Empty<string>();
    }

    public sealed class BattleReplayFrame
    {
        public int Tick;
        public BattleReplaySideFrame Left;
        public BattleReplaySideFrame Right;
        public BattleEvent CurrentEvent;
        public bool IsFinished;
        public string Error = string.Empty;
    }
}
