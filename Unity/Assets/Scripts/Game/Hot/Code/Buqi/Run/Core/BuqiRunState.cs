using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Core
{
    public sealed class BuqiRunState
    {
        public const string PreviousRuleVersion = "buqi-nine-day-run-rule-v3";
        public const string CurrentRuleVersion = "buqi-infinite-run-rule-v1";

        public string ContentVersion = string.Empty;
        public string RuleVersion = CurrentRuleVersion;
        public long RunSeed;
        public int RngCursor;
        public int Revision;
        public int Day;
        public int EncounterIndex;
        public BuqiRunPeriod Period;
        public BuqiRunPhase Phase;
        public BuqiRunOutcome Outcome;
        public int HeroId;
        public int Coins;
        public int Wins;
        public int DaoSeals;
        public int CurrentOmen;
        public int Cultivation;
        public int Realm;
        public int LifePool;
        public bool InTribulationTrial;
        public bool HeartTrialUsed;
        public int Lives
        {
            get => LifePool;
            set => LifePool = value;
        }
        public BuqiTribulationRoute TribulationRoute;
        public int TribulationDaoSealsSpent;
        public int TribulationStage;
        public int TribulationSuccesses;
        public List<string> BoardInstanceIds = new List<string>();
        public List<string> StorageInstanceIds = new List<string>();
        public HashSet<string> AppliedCommandIds = new HashSet<string>();
        public HashSet<string> AppliedSettlementIds = new HashSet<string>();

        public static BuqiRunState CreateInitial(long runSeed, string contentVersion = "")
        {
            return new BuqiRunState
            {
                ContentVersion = contentVersion ?? string.Empty,
                RunSeed = runSeed,
                Day = 1,
                EncounterIndex = 0,
                Period = BuqiRunPeriod.Hour1Operation,
                Phase = BuqiRunPhase.Encounter,
                Outcome = BuqiRunOutcome.None,
                HeroId = 0,
                Coins = BuqiRunRules.StartingCoins,
                Wins = 0,
                DaoSeals = 0,
                CurrentOmen = 0,
                Cultivation = 0,
                Realm = 0,
                LifePool = BuqiRunRules.StartingLifePool,
                InTribulationTrial = false,
                HeartTrialUsed = false,
                TribulationRoute = BuqiTribulationRoute.None,
                TribulationDaoSealsSpent = 0,
                TribulationStage = 0,
                TribulationSuccesses = 0,
                BoardInstanceIds = CreateEmptySlots(BuqiRunRules.BoardSlotCount),
                StorageInstanceIds = CreateEmptySlots(BuqiRunRules.StorageSlotCount),
            };
        }

        public BuqiRunState Clone()
        {
            return new BuqiRunState
            {
                ContentVersion = ContentVersion,
                RuleVersion = RuleVersion,
                RunSeed = RunSeed,
                RngCursor = RngCursor,
                Revision = Revision,
                Day = Day,
                EncounterIndex = EncounterIndex,
                Period = Period,
                Phase = Phase,
                Outcome = Outcome,
                HeroId = HeroId,
                Coins = Coins,
                Wins = Wins,
                DaoSeals = DaoSeals,
                CurrentOmen = CurrentOmen,
                Cultivation = Cultivation,
                Realm = Realm,
                LifePool = LifePool,
                InTribulationTrial = InTribulationTrial,
                HeartTrialUsed = HeartTrialUsed,
                TribulationRoute = TribulationRoute,
                TribulationDaoSealsSpent = TribulationDaoSealsSpent,
                TribulationStage = TribulationStage,
                TribulationSuccesses = TribulationSuccesses,
                BoardInstanceIds = new List<string>(BoardInstanceIds),
                StorageInstanceIds = new List<string>(StorageInstanceIds),
                AppliedCommandIds = new HashSet<string>(AppliedCommandIds),
                AppliedSettlementIds = new HashSet<string>(AppliedSettlementIds),
            };
        }

        private static List<string> CreateEmptySlots(int count)
        {
            var slots = new List<string>(count);
            for (int index = 0; index < count; index++)
            {
                slots.Add(string.Empty);
            }

            return slots;
        }
    }
}
