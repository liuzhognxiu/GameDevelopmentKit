using System;

namespace Game.Hot.Buqi.Run.Encounter
{
    public static class BuqiRunEventTransitions
    {
        public static BuqiRunEventRuntimeState AfterBattle(BuqiRunEventRuntimeState source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BuqiRunEventRuntimeState working = source.Clone();
            for (int index = working.TemporaryModifiers.Count - 1; index >= 0; index--)
            {
                BuqiRunTemporaryModifier modifier = working.TemporaryModifiers[index];
                modifier.RemainingBattles--;
                if (modifier.RemainingBattles <= 0)
                    working.TemporaryModifiers.RemoveAt(index);
            }
            return working;
        }

        public static BuqiRunEventRuntimeState RemoveExpiredReturns(BuqiRunEventRuntimeState source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BuqiRunEventRuntimeState working = source.Clone();
            bool changed = false;
            for (int index = working.ScheduledReturns.Count - 1; index >= 0; index--)
            {
                if (working.ScheduledReturns[index].LatestDay < working.Economy.Run.Day)
                {
                    working.ScheduledReturns.RemoveAt(index);
                    changed = true;
                }
            }

            if (changed)
                working.Economy.Run.Revision++;
            return working;
        }
    }
}
