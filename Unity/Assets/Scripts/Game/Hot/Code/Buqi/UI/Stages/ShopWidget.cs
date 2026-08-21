using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.UI.Widgets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class ShopWidget : BuqiStageWidgetBase
    {
        private const int BoardSlotCount = 8;
        private const int RuntimeOfferCount = 8;
        private const float BoardSlotWidth = 118f;
        private const float BoardSlotGap = 6f;

        [SerializeField]
        private OfferCardWidget[] m_OfferCards = Array.Empty<OfferCardWidget>();

        [SerializeField]
        private BuqiSellZoneWidget m_SellZone = null;

        [SerializeField]
        private BuqiDraggableItemWidget[] m_BoardItems = Array.Empty<BuqiDraggableItemWidget>();

        [SerializeField]
        private BuqiDeploySlotWidget[] m_BoardDropSlots = Array.Empty<BuqiDeploySlotWidget>();

        private IBuqiBazaarSupplyViewSource m_SupplySource;
        private BuqiBazaarSupplyView m_Supply;
        private Action<BuqiDemoItemView> m_ShowItemDetails;
        private Action m_HideItemDetails;
        private BuqiUIDemoView m_CurrentView;
        private Action<BuqiUIDemoCommand> m_Submit;
        private BuqiDemoOfferView m_DraggedOffer;
        private int m_HoveredBoardSlot = -1;

        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.Shop;

        public void BindSupplySource(IBuqiBazaarSupplyViewSource supplySource)
        {
            m_SupplySource = supplySource;
            m_Supply = null;
        }

        public void BindItemDetails(Action<BuqiDemoItemView> show, Action hide)
        {
            m_ShowItemDetails = show;
            m_HideItemDetails = hide;
        }

        protected override void Prepare(BuqiUIDemoView view)
        {
            EnsureRuntimeSurface();
            m_Supply = null;
            m_SupplySource?.TryGetCurrentSupply(out m_Supply);
        }

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            if (m_Supply != null && m_Supply.CanRefresh &&
                m_Supply.Balance >= m_Supply.RefreshPrice)
            {
                string refreshLabel = string.IsNullOrEmpty(m_Supply.RefreshPriceLabel)
                    ? GameFramework.Utility.Text.Format("刷新 {0} 金币", m_Supply.RefreshPrice)
                    : m_Supply.RefreshPriceLabel;
                AddAction(refreshLabel, BuqiUIDemoCommandType.RefreshShop);
            }

            if (m_OfferCards.Length > 0)
                return;

            foreach (BuqiDemoOfferView offer in view.ShopOffers)
            {
                if (offer.Sold)
                    continue;
                string name = offer.Item == null ? "未命名装备" : offer.Item.Name;
                string role = m_Supply?.FindOfferRole(offer.Id) ?? string.Empty;
                string label = string.IsNullOrEmpty(role)
                    ? $"{name}  {offer.Price} 金币"
                    : $"{role} · {name}  {offer.Price} 金币";
                AddAction(label, BuqiUIDemoCommandType.BuyOffer, offer.Id);
            }
        }

        protected override void CompleteRender(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit)
        {
            m_CurrentView = view;
            m_Submit = submit;
            RenderOffers(view, submit);
            RenderBoardDropSlots(view);
            RenderBoardItems(view, submit);
        }

        protected override string ResolveTitle(BuqiUIDemoView view)
        {
            return string.IsNullOrEmpty(m_Supply?.MerchantName)
                ? base.ResolveTitle(view)
                : m_Supply.MerchantName;
        }

        protected override string ResolveBody(BuqiUIDemoView view)
        {
            string specialty = m_Supply?.MerchantSpecialty ?? string.Empty;
            string body = base.ResolveBody(view);
            if (string.IsNullOrEmpty(specialty))
                return body;
            return string.IsNullOrEmpty(body)
                ? specialty
                : GameFramework.Utility.Text.Format("{0}\n{1}", specialty, body);
        }

        protected override string ResolveMeta(BuqiUIDemoView view)
        {
            if (m_Supply == null)
                return base.ResolveMeta(view);
            string refresh = string.IsNullOrEmpty(m_Supply.RefreshPriceLabel)
                ? GameFramework.Utility.Text.Format("刷新 {0}", m_Supply.RefreshPrice)
                : m_Supply.RefreshPriceLabel;
            return GameFramework.Utility.Text.Format("{0}   余额 {1}", refresh, m_Supply.Balance);
        }

        protected override void OnCleared()
        {
            m_Supply = null;
            m_CurrentView = null;
            m_Submit = null;
            m_DraggedOffer = null;
            m_HoveredBoardSlot = -1;
            foreach (OfferCardWidget card in m_OfferCards)
                card?.Clear();
            foreach (BuqiDeploySlotWidget slot in m_BoardDropSlots)
                slot?.Clear();
            foreach (BuqiDraggableItemWidget item in m_BoardItems)
            {
                item?.Clear();
                item?.gameObject.SetActive(false);
            }
            m_SellZone?.Clear();
        }

        private void RenderOffers(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit)
        {
            IReadOnlyList<BuqiDemoOfferView> offers = view?.ShopOffers ?? Array.Empty<BuqiDemoOfferView>();
            for (int index = 0; index < m_OfferCards.Length; index++)
            {
                OfferCardWidget card = m_OfferCards[index];
                if (card == null)
                    continue;
                if (index >= offers.Count)
                {
                    card.Clear();
                    continue;
                }

                BuqiDemoOfferView offer = CreateOfferPresentation(offers[index]);
                card.Render(
                    offer,
                    offerId => submit?.Invoke(new BuqiUIDemoCommand
                    {
                        Type = BuqiUIDemoCommandType.BuyOffer,
                        PrimaryId = offerId,
                    }),
                    _ => m_ShowItemDetails?.Invoke(offer.Item),
                    () => m_HideItemDetails?.Invoke(),
                    BeginOfferDrag,
                    null,
                    EndOfferDrag);
            }
        }

        private void RenderBoardDropSlots(BuqiUIDemoView view)
        {
            IReadOnlyList<BuqiDemoItemView> board = view?.BoardSlots ?? Array.Empty<BuqiDemoItemView>();
            bool hasPreview = m_DraggedOffer != null && m_HoveredBoardSlot >= 0;
            bool previewAccepted = hasPreview && CanDropOfferAt(board, m_DraggedOffer, m_HoveredBoardSlot);
            int previewSpan = m_DraggedOffer?.Item == null ? 0 : m_DraggedOffer.Item.Size;

            for (int index = 0; index < m_BoardDropSlots.Length; index++)
            {
                BuqiDeploySlotWidget slot = m_BoardDropSlots[index];
                if (slot == null)
                    continue;

                BuqiDemoItemView item = index < board.Count ? board[index] : null;
                bool occupied = item != null && !item.Empty && !string.IsNullOrEmpty(item.Id);
                bool inPreview = hasPreview &&
                                 index >= m_HoveredBoardSlot &&
                                 index < m_HoveredBoardSlot + Math.Max(1, previewSpan);
                BuqiDeploySlotVisualState state = inPreview
                    ? previewAccepted ? BuqiDeploySlotVisualState.Legal : BuqiDeploySlotVisualState.Illegal
                    : BuqiDeploySlotVisualState.Normal;
                string reason = inPreview && !previewAccepted ? "位置被占用或空间不足" : string.Empty;
                slot.Render(
                    BuqiDeploymentSlotRef.Board(index),
                    occupied ? item.Name : string.Empty,
                    state,
                    reason,
                    null,
                    OnBoardSlotHover,
                    OnBoardSlotDrop);
            }
        }

        private void RenderBoardItems(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit)
        {
            foreach (BuqiDraggableItemWidget widget in m_BoardItems)
            {
                widget?.Clear();
                widget?.gameObject.SetActive(false);
            }

            IReadOnlyList<BuqiDemoItemView> slots = view?.BoardSlots ?? Array.Empty<BuqiDemoItemView>();
            var rendered = new HashSet<string>(StringComparer.Ordinal);
            int widgetIndex = 0;
            foreach (BuqiDemoItemView item in slots)
            {
                if (item == null || item.Empty || string.IsNullOrEmpty(item.Id) || !rendered.Add(item.Id))
                    continue;
                if (widgetIndex >= m_BoardItems.Length)
                    break;

                BuqiDraggableItemWidget widget = m_BoardItems[widgetIndex++];
                if (widget == null)
                    continue;

                BuqiDemoItemView capturedItem = item;
                widget.gameObject.SetActive(true);
                widget.Render(
                    new BuqiUIDemoItemDefinition
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        Size = item.Size,
                        Price = item.Price,
                    },
                    BuqiDeploymentSlotRef.Board(item.Slot),
                    null,
                    (_, __) => BeginSale(capturedItem, submit),
                    null,
                    (_, __) => m_SellZone?.Cancel());
                PositionBoardItem(widget, item.Slot, Math.Max(1, item.Size));
            }
        }

        private void BeginSale(BuqiDemoItemView item, Action<BuqiUIDemoCommand> submit)
        {
            if (item == null || m_SellZone == null)
                return;

            ClearOfferDrag();

            m_SellZone.BindCommand(
                item.Id,
                Math.Max(1, item.Price / 2),
                instanceId => submit?.Invoke(new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.SellItem,
                    PrimaryId = instanceId,
                }));
        }

        private void BeginOfferDrag(string offerId, PointerEventData eventData)
        {
            m_DraggedOffer = FindOffer(m_CurrentView, offerId);
            m_HoveredBoardSlot = -1;
            if (m_DraggedOffer == null || m_DraggedOffer.Sold)
            {
                m_DraggedOffer = null;
                return;
            }

            m_HideItemDetails?.Invoke();
            SetBoardItemRaycasts(false);
            RenderBoardDropSlots(m_CurrentView);
        }

        private void EndOfferDrag(string offerId, PointerEventData eventData)
        {
            ClearOfferDrag();
        }

        private void OnBoardSlotHover(BuqiDeploymentSlotRef slot, bool over)
        {
            if (m_DraggedOffer == null || slot.Area != BuqiDeploymentArea.Board)
                return;

            m_HoveredBoardSlot = over ? slot.Index : -1;
            RenderBoardDropSlots(m_CurrentView);
        }

        private void OnBoardSlotDrop(BuqiDeploymentSlotRef slot)
        {
            if (m_DraggedOffer == null || slot.Area != BuqiDeploymentArea.Board ||
                !CanDropOfferAt(m_CurrentView?.BoardSlots, m_DraggedOffer, slot.Index))
            {
                return;
            }

            string offerId = m_DraggedOffer.Id;
            Action<BuqiUIDemoCommand> submit = m_Submit;
            ClearOfferDrag();
            submit?.Invoke(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.BuyOffer,
                PrimaryId = offerId,
                Slot = slot.Index,
            });
        }

        private void ClearOfferDrag()
        {
            m_DraggedOffer = null;
            m_HoveredBoardSlot = -1;
            SetBoardItemRaycasts(true);
            if (m_CurrentView != null)
                RenderBoardDropSlots(m_CurrentView);
        }

        private void SetBoardItemRaycasts(bool enabled)
        {
            foreach (BuqiDraggableItemWidget item in m_BoardItems)
                item?.SetRaycastEnabled(enabled);
        }

        private static bool CanDropOfferAt(
            IReadOnlyList<BuqiDemoItemView> board,
            BuqiDemoOfferView offer,
            int anchorSlot)
        {
            int size = offer?.Item == null ? 0 : offer.Item.Size;
            if (board == null || offer == null || offer.Sold || string.IsNullOrEmpty(offer.Id) ||
                size <= 0 || anchorSlot < 0 || anchorSlot + size > board.Count)
            {
                return false;
            }

            for (int slot = anchorSlot; slot < anchorSlot + size; slot++)
            {
                BuqiDemoItemView item = board[slot];
                if (item != null && !item.Empty && !string.IsNullOrEmpty(item.Id))
                    return false;
            }

            return true;
        }

        private static BuqiDemoOfferView FindOffer(BuqiUIDemoView view, string offerId)
        {
            if (view?.ShopOffers == null || string.IsNullOrEmpty(offerId))
                return null;
            foreach (BuqiDemoOfferView offer in view.ShopOffers)
            {
                if (offer != null && string.Equals(offer.Id, offerId, StringComparison.Ordinal))
                    return offer;
            }
            return null;
        }

        private static void PositionBoardItem(BuqiDraggableItemWidget widget, int anchorSlot, int span)
        {
            if (widget == null || !(widget.transform is RectTransform rect) || anchorSlot < 0)
                return;

            float boardWidth = BoardSlotWidth * BoardSlotCount + BoardSlotGap * (BoardSlotCount - 1);
            float itemWidth = BoardSlotWidth * span + BoardSlotGap * (span - 1);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(
                -boardWidth * 0.5f + anchorSlot * (BoardSlotWidth + BoardSlotGap) + itemWidth * 0.5f,
                -22f);
            rect.sizeDelta = new Vector2(itemWidth, 92f);
        }

        private void EnsureRuntimeSurface()
        {
            if (m_OfferCards.Length > 0 || m_BoardDropSlots.Length > 0 ||
                m_BoardItems.Length > 0 || m_SellZone != null)
            {
                return;
            }

            Button[] legacyActions = GetComponentsInChildren<Button>(true);
            if (legacyActions.Length < RuntimeOfferCount)
                return;

            Font font = null;
            Text existingText = GetComponentInChildren<Text>(true);
            if (existingText != null)
                font = existingText.font;

            foreach (Button action in legacyActions)
            {
                if (action == null || !action.gameObject.name.StartsWith("Action", StringComparison.Ordinal))
                    continue;
                SetRuntimeRect(action.transform as RectTransform, new Vector2(420f, 190f), new Vector2(160f, 44f));
            }

            GameObject sellObject = CreateRuntimePanel(
                transform,
                "RuntimeSellDropZone",
                new Vector2(0f, 190f),
                new Vector2(1024f, 56f),
                new Color32(62, 67, 72, 255));
            Text sellLabel = CreateRuntimeText(
                sellObject.transform,
                "SellLabel_Text",
                "拖动棋盘道具至此出售",
                font,
                16,
                new Vector2(-330f, 0f),
                new Vector2(330f, 42f),
                TextAnchor.MiddleLeft);
            Text refundText = CreateRuntimeText(
                sellObject.transform,
                "RefundPreview_Text",
                string.Empty,
                font,
                16,
                new Vector2(330f, 0f),
                new Vector2(280f, 42f),
                TextAnchor.MiddleRight);
            m_SellZone = sellObject.AddComponent<BuqiSellZoneWidget>();
            m_SellZone.BindVisuals(
                sellObject.GetComponent<Image>(),
                sellLabel,
                refundText.gameObject,
                refundText);

            var offerCards = new List<OfferCardWidget>(RuntimeOfferCount);
            for (int index = 0; index < RuntimeOfferCount; index++)
            {
                int row = index / 4;
                int column = index % 4;
                GameObject offerObject = CreateRuntimePanel(
                    transform,
                    GameFramework.Utility.Text.Format("运行时商品卡{0:00}", index + 1),
                    new Vector2(-384f + column * 256f, 90f - row * 122f),
                    new Vector2(244f, 112f),
                    new Color32(36, 43, 51, 255));
                Button buyButton = offerObject.AddComponent<Button>();
                buyButton.targetGraphic = offerObject.GetComponent<Image>();
                Text nameText = CreateRuntimeText(
                    offerObject.transform,
                    "Name_Text",
                    string.Empty,
                    font,
                    17,
                    new Vector2(0f, 30f),
                    new Vector2(220f, 30f),
                    TextAnchor.MiddleLeft);
                Text descriptionText = CreateRuntimeText(
                    offerObject.transform,
                    "Description_Text",
                    string.Empty,
                    font,
                    13,
                    new Vector2(0f, 0f),
                    new Vector2(220f, 30f),
                    TextAnchor.MiddleLeft);
                Text priceText = CreateRuntimeText(
                    offerObject.transform,
                    "Price_Text",
                    string.Empty,
                    font,
                    15,
                    new Vector2(0f, -34f),
                    new Vector2(220f, 28f),
                    TextAnchor.MiddleRight);
                OfferCardWidget card = offerObject.AddComponent<OfferCardWidget>();
                card.BindVisuals(
                    offerObject.GetComponent<Image>(),
                    nameText,
                    descriptionText,
                    priceText,
                    buyButton);
                offerCards.Add(card);
            }

            GameObject boardPanel = CreateRuntimePanel(
                transform,
                "RuntimePlayerBoard",
                new Vector2(0f, -292f),
                new Vector2(1024f, 158f),
                new Color32(29, 36, 42, 255));
            CreateRuntimeText(
                boardPanel.transform,
                "BoardTitle_Text",
                "当前棋盘 · 将商品拖到空位购买",
                font,
                16,
                new Vector2(-340f, 58f),
                new Vector2(330f, 30f),
                TextAnchor.MiddleLeft);

            var dropSlots = new List<BuqiDeploySlotWidget>(BoardSlotCount);
            var boardItems = new List<BuqiDraggableItemWidget>(BoardSlotCount);
            float boardWidth = BoardSlotWidth * BoardSlotCount + BoardSlotGap * (BoardSlotCount - 1);
            for (int index = 0; index < BoardSlotCount; index++)
            {
                float x = -boardWidth * 0.5f + index * (BoardSlotWidth + BoardSlotGap) + BoardSlotWidth * 0.5f;
                GameObject slotObject = CreateRuntimePanel(
                    boardPanel.transform,
                    GameFramework.Utility.Text.Format("棋盘放置位{0:00}", index + 1),
                    new Vector2(x, -22f),
                    new Vector2(BoardSlotWidth, 92f),
                    new Color32(52, 62, 70, 255));
                Text indexText = CreateRuntimeText(
                    slotObject.transform,
                    "Index_Text",
                    string.Empty,
                    font,
                    12,
                    new Vector2(0f, 31f),
                    new Vector2(102f, 20f),
                    TextAnchor.MiddleLeft);
                Text itemText = CreateRuntimeText(
                    slotObject.transform,
                    "Item_Text",
                    string.Empty,
                    font,
                    14,
                    Vector2.zero,
                    new Vector2(104f, 38f),
                    TextAnchor.MiddleCenter);
                Text stateText = CreateRuntimeText(
                    slotObject.transform,
                    "State_Text",
                    string.Empty,
                    font,
                    11,
                    new Vector2(0f, -32f),
                    new Vector2(104f, 20f),
                    TextAnchor.MiddleCenter);
                BuqiDeploySlotWidget slot = slotObject.AddComponent<BuqiDeploySlotWidget>();
                slot.BindVisuals(slotObject.GetComponent<Image>(), indexText, itemText, stateText);
                dropSlots.Add(slot);

                GameObject itemObject = CreateRuntimePanel(
                    boardPanel.transform,
                    GameFramework.Utility.Text.Format("棋盘道具{0:00}", index + 1),
                    new Vector2(x, -22f),
                    new Vector2(BoardSlotWidth, 92f),
                    new Color32(61, 113, 105, 255));
                CanvasGroup canvasGroup = itemObject.AddComponent<CanvasGroup>();
                Text ownedName = CreateRuntimeText(
                    itemObject.transform,
                    "Name_Text",
                    string.Empty,
                    font,
                    14,
                    new Vector2(0f, 18f),
                    new Vector2(104f, 36f),
                    TextAnchor.MiddleCenter);
                Text ownedSize = CreateRuntimeText(
                    itemObject.transform,
                    "Size_Text",
                    string.Empty,
                    font,
                    11,
                    new Vector2(0f, -12f),
                    new Vector2(104f, 20f),
                    TextAnchor.MiddleCenter);
                Text ownedSource = CreateRuntimeText(
                    itemObject.transform,
                    "Source_Text",
                    string.Empty,
                    font,
                    10,
                    new Vector2(0f, -33f),
                    new Vector2(104f, 18f),
                    TextAnchor.MiddleCenter);
                BuqiDraggableItemWidget ownedItem = itemObject.AddComponent<BuqiDraggableItemWidget>();
                ownedItem.BindVisuals(
                    canvasGroup,
                    itemObject.GetComponent<Image>(),
                    ownedName,
                    ownedSize,
                    ownedSource);
                itemObject.SetActive(false);
                boardItems.Add(ownedItem);
            }

            m_OfferCards = offerCards.ToArray();
            m_BoardDropSlots = dropSlots.ToArray();
            m_BoardItems = boardItems.ToArray();
        }

        private GameObject CreateRuntimePanel(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var panel = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            panel.layer = gameObject.layer;
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            SetRuntimeRect(panel.transform as RectTransform, position, size);
            return panel;
        }

        private GameObject CreateRuntimePanel(
            Transform parent,
            string objectName,
            Vector2 position,
            Vector2 size,
            Color32 color)
        {
            return CreateRuntimePanel(parent, objectName, position, size, (Color)color);
        }

        private Text CreateRuntimeText(
            Transform parent,
            string objectName,
            string value,
            Font font,
            int fontSize,
            Vector2 position,
            Vector2 size,
            TextAnchor alignment)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.layer = gameObject.layer;
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color32(239, 242, 238, 255);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value ?? string.Empty;
            SetRuntimeRect(text.rectTransform, position, size);
            return text;
        }

        private static void SetRuntimeRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private BuqiDemoOfferView CreateOfferPresentation(BuqiDemoOfferView offer)
        {
            string role = m_Supply?.FindOfferRole(offer.Id) ?? string.Empty;
            if (string.IsNullOrEmpty(role) || offer.Item == null)
                return offer;

            BuqiDemoItemView source = offer.Item;
            return new BuqiDemoOfferView
            {
                Id = offer.Id,
                Item = new BuqiDemoItemView
                {
                    Id = source.Id,
                    Name = source.Name,
                    Description = string.IsNullOrEmpty(source.Description)
                        ? role
                        : GameFramework.Utility.Text.Format("{0}\n{1}", role, source.Description),
                    Size = source.Size,
                    Price = source.Price,
                    Empty = source.Empty,
                    Selected = source.Selected,
                    Locked = source.Locked,
                    Slot = source.Slot,
                },
                Price = offer.Price,
                Sold = offer.Sold,
                Locked = offer.Locked,
            };
        }
    }
}
