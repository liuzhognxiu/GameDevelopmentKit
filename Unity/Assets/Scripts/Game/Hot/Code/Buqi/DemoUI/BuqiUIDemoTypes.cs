using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.DemoUI
{
    public enum BuqiUIDemoPhase
    {
        StarterSelection,
        OpponentIntel,
        PreparationChoice,
        Shop,
        Event,
        Modification,
        BoardEditor,
        Prediction,
        BattleReplay,
        BattleSummary,
        RoundSettlement,
        RunTerminal,
        OperationChoice,
        PveSelection,
        TribulationRoute,
        TribulationStage,
    }

    public enum BuqiUIDemoCommandType
    {
        SelectStarter,
        SelectChoice,
        BuyOffer,
        SellItem,
        RefreshShop,
        ToggleShopLock,
        SelectBoardSource,
        PlaceBoardItem,
        OpenDragDeploy,
        ApplyDeployment,
        SubmitPrediction,
        SkipPrediction,
        NextPhase,
        PreviousPhase,
        Restart,
        SelectOperation,
        SelectPveDifficulty,
        SelectTribulationRoute,
        ResolveTribulationStage,
    }

    public sealed class BuqiUIDemoCommand
    {
        public BuqiUIDemoCommandType Type;
        public string PrimaryId = string.Empty;
        public string SecondaryId = string.Empty;
        public int Slot = -1;
        public BuqiDeploymentSnapshot Deployment;
    }

    public sealed class BuqiUIDemoCommandResult
    {
        public bool Accepted;
        public string Reason = string.Empty;
        public BuqiUIDemoView View;
    }

    public sealed class BuqiDemoItemView
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public int Size;
        public int Price;
        public int SellPrice;
        public int CooldownTicks;
        public string EffectDescription = string.Empty;
        public string Quality = string.Empty;
        public string ArchetypeId = string.Empty;
        public string Role = string.Empty;
        public string PositionHint = string.Empty;
        public string UpgradeSummary = string.Empty;
        public List<string> Tags = new List<string>();
        public bool Empty;
        public bool Selected;
        public bool Locked;
        public int Slot;
        public int AnchorSlot = -1;
    }

    public sealed class BuqiDemoChoiceView
    {
        public string Id = string.Empty;
        public string Title = string.Empty;
        public string Description = string.Empty;
        public int Cost;
        public bool Selected;
        public bool Disabled;
    }

    public sealed class BuqiDemoOfferView
    {
        public string Id = string.Empty;
        public BuqiDemoItemView Item;
        public int Price;
        public int AnchorSlot = -1;
        public int Span;
        public bool Sold;
        public bool Locked;
    }

    public interface IBuqiBazaarSupplyViewSource
    {
        bool TryGetCurrentSupply(out BuqiBazaarSupplyView supply);
    }

    public sealed class BuqiBazaarSupplyView
    {
        public const int DefaultShelfSlotCount = 10;
        public int ShelfSlotCount = DefaultShelfSlotCount;
        public string MerchantId = string.Empty;
        public string MerchantName = string.Empty;
        public string MerchantSpecialty = string.Empty;
        public string PreferredArchetypeId = string.Empty;
        public int Balance;
        public int RefreshCount;
        public bool CanRefresh;
        public int RefreshPrice;
        public string RefreshPriceLabel = string.Empty;
        public IReadOnlyList<string> OfferIds = Array.Empty<string>();
        public IReadOnlyList<string> PurchasedOfferIds = Array.Empty<string>();
        public IReadOnlyDictionary<string, int> OfferAnchorSlots =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, string> OfferRoles =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public string FindOfferRole(string offerId)
        {
            if (string.IsNullOrEmpty(offerId) || OfferRoles == null)
                return string.Empty;
            return OfferRoles.TryGetValue(offerId, out string role)
                ? role ?? string.Empty
                : string.Empty;
        }
    }

    public sealed class BuqiDemoOpponentView
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Build = string.Empty;
        public IReadOnlyList<BuqiDemoItemView> Items = Array.Empty<BuqiDemoItemView>();
    }

    public sealed class BuqiDemoFactView
    {
        public string Title = string.Empty;
        public string Body = string.Empty;
        public int Tick;
    }

    public sealed class BuqiUIDemoView
    {
        public BuqiUIDemoPhase Phase;
        public BuqiRunPeriod Period;
        public int Coins;
        public int Wins;
        public int Lives;
        public int Round;
        public int DaoSeals;
        public int TribulationOmen;
        public int TribulationStage;
        public bool ShopLocked;
        public bool PredictionSubmitted;
        public string SelectedId = string.Empty;
        public string Prediction = string.Empty;
        public string ContextTitle = string.Empty;
        public string ContextBody = string.Empty;
        public string PrimaryCommandLabel = string.Empty;
        public string SecondaryCommandLabel = string.Empty;
        public IReadOnlyList<BuqiUIDemoPhase> VisitedPhases = Array.Empty<BuqiUIDemoPhase>();
        public IReadOnlyList<BuqiDemoItemView> BoardSlots = Array.Empty<BuqiDemoItemView>();
        public IReadOnlyList<BuqiDemoItemView> StorageSlots = Array.Empty<BuqiDemoItemView>();
        public IReadOnlyList<BuqiDemoChoiceView> Choices = Array.Empty<BuqiDemoChoiceView>();
        public IReadOnlyList<BuqiDemoOfferView> ShopOffers = Array.Empty<BuqiDemoOfferView>();
        public BuqiDemoOpponentView Opponent;
        public IReadOnlyList<BuqiDemoFactView> Facts = Array.Empty<BuqiDemoFactView>();
    }
}
