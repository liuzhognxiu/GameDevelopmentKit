using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Core
{
    public sealed class BuqiRunState
    {
        public const string CurrentRuleVersion = "buqi-day-run-rule-v1";

        public string ContentVersion = string.Empty;
        public string RuleVersion = CurrentRuleVersion;
        public long RunSeed;
        public int RngCursor;
        public int Revision;
        public int Day;
        public int EncounterIndex;
        public BuqiRunPhase Phase;
        public BuqiRunOutcome Outcome;
        public int Coins;
        public int Wins;
        public int Lives;
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
                Phase = BuqiRunPhase.Encounter,
                Outcome = BuqiRunOutcome.None,
                Coins = BuqiRunRules.StartingCoins,
                Wins = 0,
                Lives = BuqiRunRules.StartingLives,
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
                Phase = Phase,
                Outcome = Outcome,
                Coins = Coins,
                Wins = Wins,
                Lives = Lives,
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
