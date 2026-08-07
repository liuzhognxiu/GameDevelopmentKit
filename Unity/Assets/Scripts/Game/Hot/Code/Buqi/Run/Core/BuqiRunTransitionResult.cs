namespace Game.Hot.Buqi.Run.Core
{
    public sealed class BuqiRunTransitionResult
    {
        public bool Success;
        public bool Replayed;
        public string FailureReason = string.Empty;
        public BuqiRunState State = null!;
    }
}
