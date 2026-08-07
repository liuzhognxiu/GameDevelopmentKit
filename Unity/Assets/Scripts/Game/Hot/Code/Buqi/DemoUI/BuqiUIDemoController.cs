using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI.Deployment;

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
            if (command.Type == BuqiUIDemoCommandType.OpenDragDeploy)
                return OpenDragDeploy();
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
                        return Reject(out reason, "当前阶段不能选择起始装备");
                    if (m_Catalog.FindItem(command.PrimaryId) == null)
                        return Reject(out reason, "起始装备不存在");
                    state.SelectedId = command.PrimaryId;
                    return true;

                case BuqiUIDemoCommandType.SelectChoice:
                    List<BuqiDemoChoiceView> choices = ChoicesForPhase(state.Phase);
                    if (choices == null)
                        return Reject(out reason, "当前阶段不能选择该项");
                    if (!choices.Exists(choice => string.Equals(choice.Id, command.PrimaryId, StringComparison.Ordinal)))
                        return Reject(out reason, "选项不存在");
                    state.SelectedId = command.PrimaryId;
                    return true;

                case BuqiUIDemoCommandType.BuyOffer:
                    return TryBuy(state, command.PrimaryId, out reason);

                case BuqiUIDemoCommandType.RefreshShop:
                    if (state.Phase != BuqiUIDemoPhase.Shop)
                        return Reject(out reason, "当前阶段不能刷新商店");
                    if (state.Coins < 1)
                        return Reject(out reason, "金币不足");
                    if (state.ShopLocked)
                        return Reject(out reason, "商店已锁定");
                    state.Coins--;
                    state.ShopRefreshCount++;
                    state.SoldOffers.Clear();
                    return true;

                case BuqiUIDemoCommandType.ToggleShopLock:
                    if (state.Phase != BuqiUIDemoPhase.Shop)
                        return Reject(out reason, "当前阶段不能锁定商店");
                    state.ShopLocked = !state.ShopLocked;
                    return true;

                case BuqiUIDemoCommandType.SelectBoardSource:
                    if (state.Phase != BuqiUIDemoPhase.BoardEditor)
                        return Reject(out reason, "当前阶段不能编辑棋盘");
                    state.SelectedBoardSourceId = command.PrimaryId ?? string.Empty;
                    return true;

                case BuqiUIDemoCommandType.PlaceBoardItem:
                    return TryPlace(state, command, out reason);

                case BuqiUIDemoCommandType.ApplyDeployment:
                    return TryApplyDeployment(state, command.Deployment, out reason);

                case BuqiUIDemoCommandType.SubmitPrediction:
                    if (state.Phase != BuqiUIDemoPhase.Prediction)
                        return Reject(out reason, "当前阶段不能提交预测");
                    if (state.PredictionSubmitted)
                        return Reject(out reason, "预测已经提交");
                    state.PredictionSubmitted = true;
                    state.Prediction = string.IsNullOrEmpty(command.PrimaryId) ? "Draw" : command.PrimaryId;
                    return true;

                case BuqiUIDemoCommandType.SkipPrediction:
                    if (state.Phase != BuqiUIDemoPhase.Prediction)
                        return Reject(out reason, "当前阶段不能跳过预测");
                    if (state.PredictionSubmitted)
                        return Reject(out reason, "预测已经提交");
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
                return Reject(out reason, "当前阶段不能购买装备");
            BuqiDemoOfferView offer = m_Catalog.ShopOffers.Find(value => value.Id == offerId);
            if (offer == null)
                return Reject(out reason, "商店装备不存在");
            if (state.SoldOffers.Contains(offerId))
                return Reject(out reason, "该装备已售出");
            if (state.Coins < offer.Price)
                return Reject(out reason, "金币不足");
            int slot = state.Storage.FindIndex(string.IsNullOrEmpty);
            if (slot < 0)
                return Reject(out reason, "仓库已满");
            state.Coins -= offer.Price;
            state.Storage[slot] = offer.Item.Id;
            state.SoldOffers.Add(offerId);
            reason = string.Empty;
            return true;
        }

        private bool TryPlace(BuqiUIDemoState state, BuqiUIDemoCommand command, out string reason)
        {
            if (state.Phase != BuqiUIDemoPhase.BoardEditor)
                return Reject(out reason, "当前阶段不能编辑棋盘");
            if (command.Slot < 0 || command.Slot >= state.Board.Count)
                return Reject(out reason, "棋盘位置无效");
            string itemId = string.IsNullOrEmpty(command.PrimaryId) ? state.SelectedBoardSourceId : command.PrimaryId;
            BuqiUIDemoItemDefinition item = m_Catalog.FindItem(itemId);
            if (item == null)
                return Reject(out reason, "请先选择装备");
            if (command.Slot + item.Size > state.Board.Count)
                return Reject(out reason, "装备超出棋盘范围");
            for (int slot = command.Slot; slot < command.Slot + item.Size; slot++)
            {
                if (!string.IsNullOrEmpty(state.Board[slot]))
                    return Reject(out reason, "目标位置已占用");
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

        private BuqiUIDemoCommandResult OpenDragDeploy()
        {
            if (m_State.Phase != BuqiUIDemoPhase.BoardEditor)
                return Rejected("当前阶段不能编辑棋盘");
            return new BuqiUIDemoCommandResult { Accepted = true, View = View };
        }

        private bool TryApplyDeployment(
            BuqiUIDemoState state,
            BuqiDeploymentSnapshot deployment,
            out string reason)
        {
            if (state.Phase != BuqiUIDemoPhase.BoardEditor)
                return Reject(out reason, "当前阶段不能编辑棋盘");
            if (deployment == null)
                return Reject(out reason, "部署快照不可用");
            if (!BuqiDragDeployController.TryCreate(
                    m_Catalog,
                    state.Board,
                    state.Storage,
                    out BuqiDragDeployController current,
                    out reason))
                return false;
            if (!BuqiDragDeployController.TryCreate(
                    m_Catalog,
                    deployment.BoardSlots,
                    deployment.StorageSlots,
                    out BuqiDragDeployController proposed,
                    out reason))
                return false;
            if (!SameOwnedItems(current.View, proposed.View))
                return Reject(out reason, "部署快照与当前装备不一致");

            state.Board = new List<string>(proposed.View.BoardSlots);
            state.Storage = new List<string>(proposed.View.StorageSlots);
            state.SelectedBoardSourceId = string.Empty;
            reason = string.Empty;
            return true;
        }

        private static bool SameOwnedItems(BuqiDeploymentSnapshot left, BuqiDeploymentSnapshot right)
        {
            HashSet<string> leftItems = OwnedItems(left);
            HashSet<string> rightItems = OwnedItems(right);
            return leftItems.SetEquals(rightItems);
        }

        private static HashSet<string> OwnedItems(BuqiDeploymentSnapshot snapshot)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiDeploymentPlacement placement in snapshot.Placements)
                result.Add(placement.ItemId);
            foreach (string itemId in snapshot.StorageSlots)
            {
                if (!string.IsNullOrEmpty(itemId))
                    result.Add(itemId);
            }
            return result;
        }

        private bool TryAdvance(BuqiUIDemoState state, out string reason)
        {
            if (state.Phase == BuqiUIDemoPhase.StarterSelection)
            {
                if (string.IsNullOrEmpty(state.SelectedId))
                    return Reject(out reason, "请先选择起始装备");
                state.Board[0] = state.SelectedId;
            }
            if (state.Phase == BuqiUIDemoPhase.Prediction && !state.PredictionSubmitted)
                return Reject(out reason, "请先提交或跳过预测");
            if (state.Phase == BuqiUIDemoPhase.RunTerminal)
                return Reject(out reason, "本局已结束");

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
                return Rejected("已经是第一个阶段");
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
                PrimaryCommandLabel = state.Phase == BuqiUIDemoPhase.RunTerminal ? "重新开始" : "继续",
                SecondaryCommandLabel = "返回",
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
                new BuqiDemoFactView { Title = "输出贡献", Body = "核心装备完成了最高有效伤害", Tick = 180 },
                new BuqiDemoFactView { Title = "连锁证据", Body = "充能在同一触发链内被消耗", Tick = 260 },
                new BuqiDemoFactView { Title = "风险账单", Body = "过载伤害造成本场最大损失", Tick = 420 },
            };
        }

        private static string PhaseTitle(BuqiUIDemoPhase phase)
        {
            string[] titles =
            {
                "起始选择", "对手快照", "战前准备", "商店", "事件", "改造",
                "棋盘编辑", "胜负预测", "战斗回放", "战斗总结", "回合结算", "单局结束",
            };
            return titles[(int)phase];
        }

        private static string PhaseBody(BuqiUIDemoPhase phase)
        {
            string[] bodies =
            {
                "从三件装备中选择本局的构筑起点。",
                "检查对手的棋盘、改造和构筑方向。",
                "选择本回合的准备收益。",
                "使用金币购买装备，或锁定当前报价。",
                "在收益与风险之间选择。",
                "为一件装备添加收益与代价并存的改造。",
                "将装备在 8 格棋盘与 5 格仓库之间整理。",
                "在战斗前记录你对结果的判断。",
                "使用真实模拟日志进行可暂停、变速的回放。",
                "只展示可回溯的战斗事实。",
                "结算胜场、单局生命与金币变化。",
                "查看本局的构筑与战斗摘要。",
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
