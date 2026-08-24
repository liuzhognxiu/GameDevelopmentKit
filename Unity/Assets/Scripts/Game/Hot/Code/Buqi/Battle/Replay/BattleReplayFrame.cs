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
        public int FrozenTicks;
        internal long FreezeEndTick;
        public int AmmoCapacity;
        public int AmmoRemaining;
        public bool IsEnabled = true;
        public float Cooldown01;
    }

    public sealed class BattleReplaySideFrame
    {
        public int Execution;
        public int MaxExecution;
        public int Buffer;
        public int Noise;
        public int Rage;
        public int EnragedTicks;
        internal long EnrageEndTick;
        public bool IsFlying;
        public int FlyingTicks;
        internal long FlightEndTick;
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
