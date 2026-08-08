namespace Game.Hot.Buqi.Battle
{
    public enum BattleReplayFeedbackKind
    {
        Attack,
        Damage,
        Guard,
        Heal,
    }

    public enum BattleReplayFeedbackSide
    {
        Left,
        Right,
    }

    public sealed class BattleReplayFeedbackEvent
    {
        public BattleReplayFeedbackEvent(
            int sequence,
            BattleReplayFeedbackKind kind,
            BattleReplayFeedbackSide side,
            int slot,
            int value,
            float startSeconds,
            float durationSeconds)
        {
            Sequence = sequence;
            Kind = kind;
            Side = side;
            Slot = slot;
            Value = value;
            StartSeconds = startSeconds;
            DurationSeconds = durationSeconds;
        }

        public readonly int Sequence;
        public readonly BattleReplayFeedbackKind Kind;
        public readonly BattleReplayFeedbackSide Side;
        public readonly int Slot;
        public readonly int Value;
        public readonly float StartSeconds;
        public readonly float DurationSeconds;
    }
}
