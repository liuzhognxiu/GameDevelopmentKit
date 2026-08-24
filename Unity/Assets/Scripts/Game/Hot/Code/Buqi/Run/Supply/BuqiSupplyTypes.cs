using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Supply
{
    [Flags]
    public enum BuqiSupplySource
    {
        None = 0,
        Merchant = 1,
        Event = 2,
        Pve = 4,
        All = Merchant | Event | Pve,
    }

    public enum BuqiSupplyQuality
    {
        Common = 0,
        Improved = 1,
        Finalized = 2,
    }

    public enum BuqiSupplyProductRole
    {
        Mainline = 0,
        Bridge = 1,
        Counter = 2,
        Economy = 3,
        Wildcard = 4,
    }

    public enum BuqiSupplySlotPurpose
    {
        Mainline = 0,
        Bridge = 1,
        CounterOrEconomy = 2,
        Wildcard = 3,
    }

    public sealed class BuqiSupplyDefinition
    {
        public string DefinitionId = string.Empty;
        public string ArchetypeId = string.Empty;
        public Game.Hot.BuqiItemCategory Category = Game.Hot.BuqiItemCategory.NonWeapon;
        public BuqiSupplyProductRole Role;
        public int MinimumDay = 1;
        public int MaximumDay = 9;
        public int Size = 1;
        public BuqiSupplyQuality Quality;
        public BuqiSupplySource Sources = BuqiSupplySource.All;
        public int BaseWeight = 100;
        public string RefinementId = string.Empty;
        public int AnchorSlot = -1;
        public List<string> Tags = new List<string>();
        public List<string> MerchantPoolIds = new List<string>();

        public BuqiSupplyDefinition Clone()
        {
            return new BuqiSupplyDefinition
            {
                DefinitionId = DefinitionId,
                ArchetypeId = ArchetypeId,
                Category = Category,
                Role = Role,
                MinimumDay = MinimumDay,
                MaximumDay = MaximumDay,
                Size = Size,
                Quality = Quality,
                Sources = Sources,
                BaseWeight = BaseWeight,
                RefinementId = RefinementId,
                AnchorSlot = AnchorSlot,
                Tags = new List<string>(Tags),
                MerchantPoolIds = new List<string>(MerchantPoolIds),
            };
        }
    }

    public sealed class BuqiSupplyRequest
    {
        public int Day = 1;
        public BuqiSupplySource Source = BuqiSupplySource.Merchant;
        public string MerchantPoolId = string.Empty;
        public string PreferredArchetypeId = string.Empty;
        public BuqiSupplyQuality MinimumQuality = BuqiSupplyQuality.Common;
        public BuqiSupplyQuality MaximumQuality = BuqiSupplyQuality.Finalized;
        public int CandidateCount = 4;
        public int ShelfSlotBudget;
        public List<int> AllowedSizes = new List<int>();
        public List<string> AllowedArchetypeIds = new List<string>();
        public List<BuqiSupplyProductRole> AllowedRoles = new List<BuqiSupplyProductRole>();
    }

    public sealed class BuqiSupplyTagMemory
    {
        public int PreferenceBps;
        public int MissStreak;
        public int SeenAge = int.MaxValue;
        public int AcquiredAge = int.MaxValue;

        public BuqiSupplyTagMemory Clone()
        {
            return (BuqiSupplyTagMemory)MemberwiseClone();
        }
    }

    public sealed class BuqiSupplyState
    {
        public long Seed;
        public int Cursor;
        public int Generation;
        public List<string> LastOfferDefinitionIds = new List<string>();
        public List<string> PriorOfferDefinitionIds = new List<string>();
        public Dictionary<string, BuqiSupplyTagMemory> TagMemory =
            new Dictionary<string, BuqiSupplyTagMemory>(StringComparer.Ordinal);

        public static BuqiSupplyState CreateInitial(long seed)
        {
            return new BuqiSupplyState { Seed = seed };
        }

        public BuqiSupplyState Clone()
        {
            var clone = new BuqiSupplyState
            {
                Seed = Seed,
                Cursor = Cursor,
                Generation = Generation,
                LastOfferDefinitionIds = new List<string>(LastOfferDefinitionIds),
                PriorOfferDefinitionIds = new List<string>(PriorOfferDefinitionIds),
            };
            foreach (KeyValuePair<string, BuqiSupplyTagMemory> pair in TagMemory)
                clone.TagMemory.Add(pair.Key, pair.Value.Clone());
            return clone;
        }
    }

    public sealed class BuqiSupplyShelf
    {
        public int Day;
        public BuqiSupplySource Source;
        public string MerchantPoolId = string.Empty;
        public int RefreshIndex;
        public int RefreshPricePaid;
        public int NextRefreshPrice;
        public int ShelfSlotCount;
        public List<BuqiSupplyDefinition> Offers = new List<BuqiSupplyDefinition>();
        public List<int> EmptySlots = new List<int>();
        public List<BuqiSupplySlotPurpose> SlotPurposes = new List<BuqiSupplySlotPurpose>();
        public BuqiSupplyState NextState = null!;
    }
}
