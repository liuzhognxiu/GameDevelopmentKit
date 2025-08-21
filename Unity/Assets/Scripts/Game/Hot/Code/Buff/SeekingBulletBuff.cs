namespace Game.Hot
{
    public class SeekingBulletBuff : Buff
    {
        public override void Apply(Entity target, float duration)
        {
            base.Apply(target, duration);

            MyAircraft aircraft = target as MyAircraft;
            if (aircraft == null)
            {
                return;
            }

            foreach (var weapon in aircraft.Weapons)
            {
                weapon.OverrideBulletType(BulletType.Seeking);
            }
        }

        public override void End()
        {
            MyAircraft aircraft = Target as MyAircraft;
            if (aircraft == null)
            {
                return;
            }

            foreach (var weapon in aircraft.Weapons)
            {
                weapon.OverrideBulletType(null); // Revert to default
            }
        }
    }
}
