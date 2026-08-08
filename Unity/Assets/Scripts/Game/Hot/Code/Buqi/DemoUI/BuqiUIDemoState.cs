using System.Collections.Generic;

namespace Game.Hot.Buqi.DemoUI
{
    internal sealed class BuqiUIDemoState
    {
        public BuqiUIDemoPhase Phase = BuqiUIDemoPhase.StarterSelection;
        public int Coins = 12;
        public int Wins;
        public int Lives = 3;
        public int Round = 1;
        public bool ShopLocked;
        public int ShopRefreshCount;
        public bool PredictionSubmitted;
        public string SelectedId = string.Empty;
        public string SelectedBoardSourceId = string.Empty;
        public string Prediction = string.Empty;
        public List<string> Board = EmptySlots(8);
        public List<string> Storage = EmptySlots(8);
        public HashSet<string> SoldOffers = new HashSet<string>();
        public List<BuqiUIDemoPhase> Visited = new List<BuqiUIDemoPhase> { BuqiUIDemoPhase.StarterSelection };

        public BuqiUIDemoState Clone()
        {
            return new BuqiUIDemoState
            {
                Phase = Phase,
                Coins = Coins,
                Wins = Wins,
                Lives = Lives,
                Round = Round,
                ShopLocked = ShopLocked,
                ShopRefreshCount = ShopRefreshCount,
                PredictionSubmitted = PredictionSubmitted,
                SelectedId = SelectedId,
                SelectedBoardSourceId = SelectedBoardSourceId,
                Prediction = Prediction,
                Board = new List<string>(Board),
                Storage = new List<string>(Storage),
                SoldOffers = new HashSet<string>(SoldOffers),
                Visited = new List<BuqiUIDemoPhase>(Visited),
            };
        }

        private static List<string> EmptySlots(int count)
        {
            var slots = new List<string>(count);
            for (int index = 0; index < count; index++)
                slots.Add(string.Empty);
            return slots;
        }
    }
}
