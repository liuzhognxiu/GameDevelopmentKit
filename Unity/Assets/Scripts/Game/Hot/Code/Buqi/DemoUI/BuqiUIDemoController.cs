using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.DemoUI
{
    public sealed class BuqiUIDemoController
    {
        private readonly BuqiUIDemoCatalog m_Catalog;
        private readonly Stack<BuqiUIDemoState> m_History = new Stack<BuqiUIDemoState>();
        private BuqiUIDemoState m_State;

        private BuqiUIDemoController(BuqiUIDemoCatalog catalog)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            m_State = new BuqiUIDemoState();
            View = CreateView(m_State);
        }

        public BuqiUIDemoView View { get; private set; }

        public static BuqiUIDemoController Create(BuqiUIDemoCatalog catalog)
        {
            return new BuqiUIDemoController(catalog);
        }

        public BuqiUIDemoCommandResult Execute(BuqiUIDemoCommand command)
        {
            if (command == null)
                return Rejected("Command is null.");
            if (command.Type == BuqiUIDemoCommandType.PreviousPhase)
                return GoBack();
            if (command.Type == BuqiUIDemoCommandType.Restart)
                return Restart();

            BuqiUIDemoState next = m_State.Clone();
            if (!TryApply(next, command, out string reason))
                return Rejected(reason);

            m_History.Push(m_State);
            m_State = next;
            View = CreateView(m_State);
            return new BuqiUIDemoCommandResult { Accepted = true, View = View };
        }

        private bool TryApply(BuqiUIDemoState state, BuqiUIDemoCommand command, out string reason)
        {
            reason = string.Empty;
            switch (command.Type)
            {
                case BuqiUIDemoCommandType.SelectStarter:
                    if (state.Phase != BuqiUIDemoPhase.StarterSelection)
                        return Reject(out reason, "\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u9009\u62E9\u8D77\u59CB\u88C5\u5907");
                    if (m_Catalog.FindItem(command.PrimaryId) == null)
                        return Reject(out reason, "\u8D77\u59CB\u88C5\u5907\u4E0D\u5B58\u5728");
                    state.SelectedId = command.PrimaryId;
                    return true;

                case BuqiUIDemoCommandType.SelectChoice:
                    List<BuqiDemoChoiceView> choices = ChoicesForPhase(state.Phase);
                    if (choices == null)
                        return Reject(out reason, "\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u9009\u62E9\u8BE5\u9879");
                    if (!choices.Exists(choice => string.Equals(choice.Id, command.PrimaryId, StringComparison.Ordinal)))
                        return Reject(out reason, "\u9009\u9879\u4E0D\u5B58\u5728");
                    state.SelectedId = command.PrimaryId;
                    return true;

                case BuqiUIDemoCommandType.BuyOffer:
                    return TryBuy(state, command.PrimaryId, out reason);

                case BuqiUIDemoCommandType.RefreshShop:
                    if (state.Phase != BuqiUIDemoPhase.Shop)
                        return Reject(out reason, "\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u5237\u65B0\u5546\u5E97");
                    if (state.Coins < 1)
                        return Reject(out reason, "\u91D1\u5E01\u4E0D\u8DB3");
                    if (state.ShopLocked)
                        return Reject(out reason, "\u5546\u5E97\u5DF2\u9501\u5B9A");
                    state.Coins--;
                    state.ShopRefreshCount++;
                    state.SoldOffers.Clear();
                    return true;

                case BuqiUIDemoCommandType.ToggleShopLock:
                    if (state.Phase != BuqiUIDemoPhase.Shop)
                        return Reject(out reason, "\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u9501\u5B9A\u5546\u5E97");
                    state.ShopLocked = !state.ShopLocked;
                    return true;

                case BuqiUIDemoCommandType.SelectBoardSource:
                    if (state.Phase != BuqiUIDemoPhase.BoardEditor)
                        return Reject(out reason, "\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u7F16\u8F91\u68CB\u76D8");
                    state.SelectedBoardSourceId = command.PrimaryId ?? string.Empty;
                    return true;

                case BuqiUIDemoCommandType.PlaceBoardItem:
                    return TryPlace(state, command, out reason);

                case BuqiUIDemoCommandType.SubmitPrediction:
                    if (state.Phase != BuqiUIDemoPhase.Prediction)
                        return Reject(out reason, "\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u63D0\u4EA4\u9884\u6D4B");
                    if (state.PredictionSubmitted)
                        return Reject(out reason, "\u9884\u6D4B\u5DF2\u7ECF\u63D0\u4EA4");
                    state.PredictionSubmitted = true;
                    state.Prediction = string.IsNullOrEmpty(command.PrimaryId) ? "Draw" : command.PrimaryId;
                    return true;

                case BuqiUIDemoCommandType.SkipPrediction:
                    if (state.Phase != BuqiUIDemoPhase.Prediction)
                        return Reject(out reason, "\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u8DF3\u8FC7\u9884\u6D4B");
                    if (state.PredictionSubmitted)
                        return Reject(out reason, "\u9884\u6D4B\u5DF2\u7ECF\u63D0\u4EA4");
                    state.PredictionSubmitted = true;
                    state.Prediction = "Skipped";
                    return true;

                case BuqiUIDemoCommandType.NextPhase:
                    return TryAdvance(state, out reason);

                default:
                    return Reject(out reason, "不支持的演示指令。");
            }
        }

        private bool TryBuy(BuqiUIDemoState state, string offerId, out string reason)
        {
            if (state.Phase != BuqiUIDemoPhase.Shop)
                return Reject(out reason, "\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u8D2D\u4E70\u88C5\u5907");
            BuqiDemoOfferView offer = m_Catalog.ShopOffers.Find(value => value.Id == offerId);
            if (offer == null)
                return Reject(out reason, "\u5546\u5E97\u88C5\u5907\u4E0D\u5B58\u5728");
            if (state.SoldOffers.Contains(offerId))
                return Reject(out reason, "\u8BE5\u88C5\u5907\u5DF2\u552E\u51FA");
            if (state.Coins < offer.Price)
                return Reject(out reason, "\u91D1\u5E01\u4E0D\u8DB3");
            int slot = state.Storage.FindIndex(string.IsNullOrEmpty);
            if (slot < 0)
                return Reject(out reason, "\u4ED3\u5E93\u5DF2\u6EE1");
            state.Coins -= offer.Price;
            state.Storage[slot] = offer.Item.Id;
            state.SoldOffers.Add(offerId);
            reason = string.Empty;
            return true;
        }

        private bool TryPlace(BuqiUIDemoState state, BuqiUIDemoCommand command, out string reason)
        {
            if (state.Phase != BuqiUIDemoPhase.BoardEditor)
                return Reject(out reason, "\u5F53\u524D\u9636\u6BB5\u4E0D\u80FD\u7F16\u8F91\u68CB\u76D8");
            if (command.Slot < 0 || command.Slot >= state.Board.Count)
                return Reject(out reason, "\u68CB\u76D8\u4F4D\u7F6E\u65E0\u6548");
            string itemId = string.IsNullOrEmpty(command.PrimaryId) ? state.SelectedBoardSourceId : command.PrimaryId;
            BuqiUIDemoItemDefinition item = m_Catalog.FindItem(itemId);
            if (item == null)
                return Reject(out reason, "\u8BF7\u5148\u9009\u62E9\u88C5\u5907");
            if (command.Slot + item.Size > state.Board.Count)
                return Reject(out reason, "\u88C5\u5907\u8D85\u51FA\u68CB\u76D8\u8303\u56F4");
            for (int slot = command.Slot; slot < command.Slot + item.Size; slot++)
            {
                if (!string.IsNullOrEmpty(state.Board[slot]))
                    return Reject(out reason, "\u76EE\u6807\u4F4D\u7F6E\u5DF2\u5360\u7528");
            }
            for (int slot = command.Slot; slot < command.Slot + item.Size; slot++)
                state.Board[slot] = itemId;
            int storageSlot = state.Storage.FindIndex(value => value == itemId);
            if (storageSlot >= 0)
                state.Storage[storageSlot] = string.Empty;
            state.SelectedBoardSourceId = string.Empty;
            reason = string.Empty;
            return true;
        }

        private bool TryAdvance(BuqiUIDemoState state, out string reason)
        {
            if (state.Phase == BuqiUIDemoPhase.StarterSelection)
            {
                if (string.IsNullOrEmpty(state.SelectedId))
                    return Reject(out reason, "\u8BF7\u5148\u9009\u62E9\u8D77\u59CB\u88C5\u5907");
                state.Board[0] = state.SelectedId;
            }
            if (state.Phase == BuqiUIDemoPhase.Prediction && !state.PredictionSubmitted)
                return Reject(out reason, "\u8BF7\u5148\u63D0\u4EA4\u6216\u8DF3\u8FC7\u9884\u6D4B");
            if (state.Phase == BuqiUIDemoPhase.RunTerminal)
                return Reject(out reason, "\u672C\u5C40\u5DF2\u7ED3\u675F");

            state.Phase++;
            state.SelectedId = string.Empty;
            if (!state.Visited.Contains(state.Phase))
                state.Visited.Add(state.Phase);
            reason = string.Empty;
            return true;
        }

        private BuqiUIDemoCommandResult GoBack()
        {
            if (m_History.Count == 0)
                return Rejected("\u5DF2\u7ECF\u662F\u7B2C\u4E00\u4E2A\u9636\u6BB5");
            m_State = m_History.Pop();
            View = CreateView(m_State);
            return new BuqiUIDemoCommandResult { Accepted = true, View = View };
        }

        private BuqiUIDemoCommandResult Restart()
        {
            m_History.Clear();
            m_State = new BuqiUIDemoState();
            View = CreateView(m_State);
            return new BuqiUIDemoCommandResult { Accepted = true, View = View };
        }

        private BuqiUIDemoView CreateView(BuqiUIDemoState state)
        {
            return new BuqiUIDemoView
            {
                Phase = state.Phase,
                Coins = state.Coins,
                Wins = state.Wins,
                Lives = state.Lives,
                Round = state.Round,
                ShopLocked = state.ShopLocked,
                PredictionSubmitted = state.PredictionSubmitted,
                SelectedId = state.SelectedId,
                Prediction = state.Prediction,
                ContextTitle = PhaseTitle(state.Phase),
                ContextBody = PhaseBody(state.Phase),
                PrimaryCommandLabel = state.Phase == BuqiUIDemoPhase.RunTerminal ? "\u91CD\u65B0\u5F00\u59CB" : "\u7EE7\u7EED",
                SecondaryCommandLabel = "\u8FD4\u56DE",
                VisitedPhases = new List<BuqiUIDemoPhase>(state.Visited),
                BoardSlots = CreateSlots(state.Board, state.SelectedBoardSourceId),
                StorageSlots = CreateSlots(state.Storage, state.SelectedBoardSourceId),
                Choices = CreateChoices(state),
                ShopOffers = CreateOffers(state),
                Opponent = m_Catalog.Opponent,
                Facts = CreateFacts(),
            };
        }

        private IReadOnlyList<BuqiDemoItemView> CreateSlots(List<string> ids, string selectedId)
        {
            var result = new List<BuqiDemoItemView>(ids.Count);
            for (int slot = 0; slot < ids.Count; slot++)
            {
                BuqiUIDemoItemDefinition item = m_Catalog.FindItem(ids[slot]);
                BuqiDemoItemView view = item == null
                    ? new BuqiDemoItemView { Empty = true }
                    : BuqiUIDemoCatalog.ItemView(item);
                view.Slot = slot;
                view.Selected = !view.Empty && view.Id == selectedId;
                result.Add(view);
            }
            return result;
        }

        private IReadOnlyList<BuqiDemoChoiceView> CreateChoices(BuqiUIDemoState state)
        {
            List<BuqiDemoChoiceView> source = state.Phase == BuqiUIDemoPhase.StarterSelection
                ? m_Catalog.StarterChoices
                : ChoicesForPhase(state.Phase) ?? new List<BuqiDemoChoiceView>();
            var result = new List<BuqiDemoChoiceView>(source.Count);
            foreach (BuqiDemoChoiceView choice in source)
            {
                result.Add(new BuqiDemoChoiceView
                {
                    Id = choice.Id,
                    Title = choice.Title,
                    Description = choice.Description,
                    Cost = choice.Cost,
                    Selected = choice.Id == state.SelectedId,
                    Disabled = choice.Disabled,
                });
            }
            return result;
        }

        private List<BuqiDemoChoiceView> ChoicesForPhase(BuqiUIDemoPhase phase)
        {
            switch (phase)
            {
                case BuqiUIDemoPhase.PreparationChoice:
                    return m_Catalog.PreparationChoices;
                case BuqiUIDemoPhase.Event:
                    return m_Catalog.EventChoices;
                case BuqiUIDemoPhase.Modification:
                    return m_Catalog.Modifications;
                default:
                    return null;
            }
        }

        private IReadOnlyList<BuqiDemoOfferView> CreateOffers(BuqiUIDemoState state)
        {
            var result = new List<BuqiDemoOfferView>(m_Catalog.ShopOffers.Count);
            int count = m_Catalog.ShopOffers.Count;
            for (int index = 0; index < count; index++)
            {
                BuqiDemoOfferView source = m_Catalog.ShopOffers[(index + state.ShopRefreshCount) % count];
                result.Add(new BuqiDemoOfferView
                {
                    Id = source.Id,
                    Item = source.Item,
                    Price = source.Price,
                    Sold = state.SoldOffers.Contains(source.Id),
                    Locked = state.ShopLocked,
                });
            }
            return result;
        }

        private static IReadOnlyList<BuqiDemoFactView> CreateFacts()
        {
            return new List<BuqiDemoFactView>
            {
                new BuqiDemoFactView { Title = "\u8F93\u51FA\u8D21\u732E", Body = "\u6838\u5FC3\u88C5\u5907\u5B8C\u6210\u4E86\u6700\u9AD8\u6709\u6548\u4F24\u5BB3", Tick = 180 },
                new BuqiDemoFactView { Title = "\u8FDE\u9501\u8BC1\u636E", Body = "\u5145\u80FD\u5728\u540C\u4E00\u89E6\u53D1\u94FE\u5185\u88AB\u6D88\u8017", Tick = 260 },
                new BuqiDemoFactView { Title = "\u98CE\u9669\u8D26\u5355", Body = "\u8FC7\u8F7D\u4F24\u5BB3\u9020\u6210\u672C\u573A\u6700\u5927\u635F\u5931", Tick = 420 },
            };
        }

        private static string PhaseTitle(BuqiUIDemoPhase phase)
        {
            string[] titles =
            {
                "\u8D77\u59CB\u9009\u62E9", "\u5BF9\u624B\u5FEB\u7167", "\u6218\u524D\u51C6\u5907", "\u5546\u5E97", "\u4E8B\u4EF6", "\u6539\u9020",
                "\u68CB\u76D8\u7F16\u8F91", "\u80DC\u8D1F\u9884\u6D4B", "\u6218\u6597\u56DE\u653E", "\u6218\u6597\u603B\u7ED3", "\u56DE\u5408\u7ED3\u7B97", "\u5355\u5C40\u7ED3\u675F",
            };
            return titles[(int)phase];
        }

        private static string PhaseBody(BuqiUIDemoPhase phase)
        {
            string[] bodies =
            {
                "\u4ECE\u4E09\u4EF6\u88C5\u5907\u4E2D\u9009\u62E9\u672C\u5C40\u7684\u6784\u7B51\u8D77\u70B9\u3002",
                "\u68C0\u67E5\u5BF9\u624B\u7684\u68CB\u76D8\u3001\u6539\u9020\u548C\u6784\u7B51\u65B9\u5411\u3002",
                "\u9009\u62E9\u672C\u56DE\u5408\u7684\u51C6\u5907\u6536\u76CA\u3002",
                "\u4F7F\u7528\u91D1\u5E01\u8D2D\u4E70\u88C5\u5907\uFF0C\u6216\u9501\u5B9A\u5F53\u524D\u62A5\u4EF7\u3002",
                "\u5728\u6536\u76CA\u4E0E\u98CE\u9669\u4E4B\u95F4\u9009\u62E9\u3002",
                "\u4E3A\u4E00\u4EF6\u88C5\u5907\u6DFB\u52A0\u6536\u76CA\u4E0E\u4EE3\u4EF7\u5E76\u5B58\u7684\u6539\u9020\u3002",
                "\u5C06\u88C5\u5907\u5728 8 \u683C\u68CB\u76D8\u4E0E 5 \u683C\u4ED3\u5E93\u4E4B\u95F4\u6574\u7406\u3002",
                "\u5728\u6218\u6597\u524D\u8BB0\u5F55\u4F60\u5BF9\u7ED3\u679C\u7684\u5224\u65AD\u3002",
                "\u4F7F\u7528\u771F\u5B9E\u6A21\u62DF\u65E5\u5FD7\u8FDB\u884C\u53EF\u6682\u505C\u3001\u53D8\u901F\u7684\u56DE\u653E\u3002",
                "\u53EA\u5C55\u793A\u53EF\u56DE\u6EAF\u7684\u6218\u6597\u4E8B\u5B9E\u3002",
                "\u7ED3\u7B97\u80DC\u573A\u3001\u5355\u5C40\u751F\u547D\u4E0E\u91D1\u5E01\u53D8\u5316\u3002",
                "\u67E5\u770B\u672C\u5C40\u7684\u6784\u7B51\u4E0E\u6218\u6597\u6458\u8981\u3002",
            };
            return bodies[(int)phase];
        }

        private BuqiUIDemoCommandResult Rejected(string reason)
        {
            return new BuqiUIDemoCommandResult { Accepted = false, Reason = reason, View = View };
        }

        private static bool Reject(out string reason, string value)
        {
            reason = value;
            return false;
        }
    }
}
