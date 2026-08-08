namespace Game.Hot.Buqi.Run.Core
{
    public static class BuqiRunRules
    {
        public const int RunDayCount = 9;
        public const int OperationsPerDay = 2;
        public const int TribulationStageCount = 3;
        public const int MaxBattleWins = RunDayCount * 2;
        public const int MaxDaoSeals = MaxBattleWins;
        public const int MaxOmen = MaxBattleWins;
        public const int WinsToVictory = MaxBattleWins;
        public const int StartingLives = 3;
        public const int EncountersPerDay = OperationsPerDay;
        public const int BoardSlotCount = 8;
        public const int StorageSlotCount = 8;
        public const int StartingCoins = 12;
    }
}
