using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI.Deployment;

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
    }

    public enum BuqiUIDemoCommandType
    {
        SelectStarter,
        SelectChoice,
        BuyOffer,
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
        public bool Empty;
        public bool Selected;
        public bool Locked;
        public int Slot;
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
        public bool Sold;
        public bool Locked;
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
        public int Coins;
        public int Wins;
        public int Lives;
        public int Round;
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
