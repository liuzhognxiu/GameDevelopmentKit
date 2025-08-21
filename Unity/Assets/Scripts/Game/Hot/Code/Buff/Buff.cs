namespace Game.Hot
{
    public abstract class Buff
    {
        public float Duration { get; protected set; }
        public float TimeRemaining { get; protected set; }
        public Entity Target { get; protected set; }

        public virtual void Apply(Entity target, float duration)
        {
            Target = target;
            Duration = duration;
            TimeRemaining = duration;
        }

        public virtual void Tick(float elapseSeconds)
        {
            if (TimeRemaining > 0)
            {
                TimeRemaining -= elapseSeconds;
            }
        }

        public abstract void End();
    }
}
