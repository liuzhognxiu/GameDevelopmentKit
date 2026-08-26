namespace Game.Hot.Buqi.Run.Core
{
    public static class BuqiRunRules
    {
        public const int OperationsBeforePve = 2;
        public const int OperationsAfterPve = 2;
        public const int OperationsPerDay = OperationsBeforePve + OperationsAfterPve;
        public const int PeriodsPerDay = 6;
        public const int TribulationStageCount = 3;
        public const int WinsToVictory = 10;
        public const int MaxBattleWins = WinsToVictory;
        public const int MaxDaoSeals = MaxBattleWins;
        public const int MaxOmen = MaxBattleWins;
        public const int StartingLifePool = 20;
        public const int StartingLives = StartingLifePool;
        public const int RealmCount = 9;
        public const int ContentScheduleDayCount = 9;
        public const int RunDayCount = ContentScheduleDayCount;
        public const int EncountersPerDay = OperationsPerDay;
        public const int BoardSlotCount = 10;
        public const int StorageSlotCount = 10;
        public const int StartingCoins = 12;

        public static int GetContentScheduleDay(int runDay)
        {
            if (runDay < 1)
                throw new System.ArgumentOutOfRangeException(nameof(runDay));
            return System.Math.Min(runDay, ContentScheduleDayCount);
        }
    }

    public static class BuqiRunProgression
    {
        private static readonly int[] s_RealmThresholds = { 0, 8, 18, 30, 44, 60, 78, 98, 120 };

        public static int GetRealm(int cultivation)
        {
            if (cultivation < 0)
                throw new System.ArgumentOutOfRangeException(nameof(cultivation));

            int realm = 0;
            for (int index = 1; index < s_RealmThresholds.Length; index++)
            {
                if (cultivation < s_RealmThresholds[index])
                    break;
                realm = index;
            }
            return realm;
        }

        public static int GetBattleReward(BuqiRunBattleKind kind, BuqiRunRawBattleOutcome outcome)
        {
            if (outcome == BuqiRunRawBattleOutcome.PlayerWin)
                return kind == BuqiRunBattleKind.Pve ? 3 : 2;
            return 1;
        }
    }
}
