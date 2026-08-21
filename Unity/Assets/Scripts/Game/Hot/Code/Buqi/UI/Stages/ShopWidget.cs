using System;
using System.Collections;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.UI.Widgets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class ShopWidget : BuqiStageWidgetBase,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IDropHandler
    {
        private const int RuntimeOfferCount = BuqiBazaarShelfProjection.ShelfSlotCount;
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

        [SerializeField]
        private GameObject m_DetailPanel = null;

        [SerializeField]
        private Text m_DetailText = null;

        private IBuqiBazaarSupplyViewSource m_SupplySource;
        private BuqiBazaarSupplyView m_Supply;
        private Action<BuqiDemoItemView> m_ShowItemDetails;
        private Action m_HideItemDetails;
        private BuqiUIDemoView m_CurrentView;
        private Action<BuqiUIDemoCommand> m_Submit;
        private BuqiDemoOfferView m_DraggedOffer;
        private BuqiDemoItemView m_DraggedBoardItem;
        private int m_HoveredBoardSlot = -1;
        private int m_BoardSlotCount = BuqiRunRules.BoardSlotCount;
        private BuqiDemoItemView m_FixedPreview;
        private BuqiDemoItemView m_HoverPreview;
        private bool m_ShelfPointerOver;
        private Coroutine m_SuccessPulse;

        public bool IsDragging => m_DraggedOffer != null || m_DraggedBoardItem != null ||
                                   (m_SellZone != null && m_SellZone.HasActiveDrag);

        public void NotifyTransactionSuccess()
        {
            if (!isActiveAndEnabled)
                return;

            if (m_SuccessPulse != null)
                StopCoroutine(m_SuccessPulse);
            m_SuccessPulse = StartCoroutine(PlaySuccessPulse());
        }

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
            m_BoardSlotCount = view?.BoardSlots == null || view.BoardSlots.Count == 0
                ? BuqiRunRules.BoardSlotCount
                : view.BoardSlots.Count;
            EnsureRuntimeSurface();
            EnsureDetailsSurface();
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
                AddAction(GameFramework.Utility.Text.Format("↻ {0}", refreshLabel), BuqiUIDemoCommandType.RefreshShop);
            }

            if (m_OfferCards.Length > 0)
                return;

            // Product cards are drag sources only. BuyOffer is never exposed as a button action.
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
            string refresh;
            if (m_Supply.CanRefresh && m_Supply.Balance < m_Supply.RefreshPrice)
            {
                refresh = GameFramework.Utility.Text.Format(
                    "刷新不可用：金币不足（需 {0}）",
                    m_Supply.RefreshPrice);
            }
            else
            {
                refresh = string.IsNullOrEmpty(m_Supply.RefreshPriceLabel)
                    ? GameFramework.Utility.Text.Format("刷新 {0}", m_Supply.RefreshPrice)
                    : m_Supply.RefreshPriceLabel;
            }
            return GameFramework.Utility.Text.Format("{0}   余额 {1}", refresh, m_Supply.Balance);
        }

        protected override void OnCleared()
        {
            if (m_SuccessPulse != null)
                StopCoroutine(m_SuccessPulse);
            m_SuccessPulse = null;
            m_Supply = null;
            m_CurrentView = null;
            m_Submit = null;
            m_DraggedOffer = null;
            m_DraggedBoardItem = null;
            m_HoveredBoardSlot = -1;
            m_FixedPreview = null;
            m_HoverPreview = null;
            m_ShelfPointerOver = false;
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
            RenderDetails(null);
        }

        private void RenderOffers(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit)
        {
            IReadOnlyDictionary<string, int> anchors = m_Supply?.OfferAnchorSlots;
            IReadOnlyList<BuqiDemoOfferView> offers = BuqiBazaarShelfProjection.Project(
                view?.ShopOffers,
                m_Supply?.ShelfSlotCount ?? BuqiBazaarShelfProjection.ShelfSlotCount,
                anchors);
            for (int index = 0; index < m_OfferCards.Length; index++)
            {
                OfferCardWidget card = m_OfferCards[index];
                if (card == null)
                    continue;
                if (index >= offers.Count || offers[index] == null || offers[index].Sold ||
                    offers[index].AnchorSlot < 0)
                {
                    card.Clear();
                    continue;
                }

                BuqiDemoOfferView offer = CreateOfferPresentation(offers[index]);
                card.Render(
                    offer,
                    null,
                    _ => ShowTemporaryPreview(offer.Item),
                    HideTemporaryPreview,
                    BeginOfferDrag,
                    null,
                    EndOfferDrag,
                    _ => SetFixedPreview(offer.Item));
                card.SetShelfLayout(
                    offer.AnchorSlot,
                    Math.Max(1, offer.Span),
                    92f,
                    6f,
                    Math.Max(1, m_Supply?.ShelfSlotCount ?? BuqiBazaarShelfProjection.ShelfSlotCount));
            }
        }

        private void RenderBoardDropSlots(BuqiUIDemoView view)
        {
            IReadOnlyList<BuqiDemoItemView> board = view?.BoardSlots ?? Array.Empty<BuqiDemoItemView>();
            bool hasPreview = (m_DraggedOffer != null || m_DraggedBoardItem != null) &&
                              m_HoveredBoardSlot >= 0;
            bool previewAccepted = m_DraggedOffer != null
                ? CanPurchaseOfferAt(board, m_DraggedOffer, m_HoveredBoardSlot)
                : CanMoveBoardItem(board, m_DraggedBoardItem, m_HoveredBoardSlot);
            int previewSpan = m_DraggedOffer?.Item?.Size ?? m_DraggedBoardItem?.Size ?? 0;

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
                string reason = string.Empty;
                if (inPreview && !previewAccepted)
                {
                    reason = m_DraggedOffer != null &&
                             CanDropOfferAt(board, m_DraggedOffer, m_HoveredBoardSlot) &&
                             !HasEnoughCoins(m_DraggedOffer)
                        ? "金币不足"
                        : "位置被占用、越界或空间不足";
                }
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
                        SellPrice = item.SellPrice,
                        CooldownTicks = item.CooldownTicks,
                        EffectDescription = item.EffectDescription,
                        ArchetypeId = item.ArchetypeId,
                        Role = item.Role,
                        PositionHint = item.PositionHint,
                        UpgradeSummary = item.UpgradeSummary,
                        Tags = item.Tags == null ? new List<string>() : new List<string>(item.Tags),
                    },
                    BuqiDeploymentSlotRef.Board(item.AnchorSlot >= 0 ? item.AnchorSlot : item.Slot),
                    null,
                    (_, __) => BeginBoardItemDrag(capturedItem),
                    null,
                    (_, __) => EndBoardItemDrag());
                PositionBoardItem(
                    widget,
                    item.AnchorSlot >= 0 ? item.AnchorSlot : item.Slot,
                    Math.Max(1, item.Size));
            }
        }

        private void BeginBoardItemDrag(BuqiDemoItemView item)
        {
            if (item == null || m_SellZone == null)
                return;

            ClearDrag();
            m_DraggedBoardItem = item;
            m_HoveredBoardSlot = -1;
            m_HideItemDetails?.Invoke();
            SetBoardItemRaycasts(false);

            m_SellZone.BindCommand(
                item.Id,
                Math.Max(0, item.SellPrice),
                instanceId =>
                {
                    Action<BuqiUIDemoCommand> submit = m_Submit;
                    ClearDrag();
                    submit?.Invoke(new BuqiUIDemoCommand
                    {
                        Type = BuqiUIDemoCommandType.SellItem,
                        PrimaryId = instanceId,
                    });
                });
            RenderBoardDropSlots(m_CurrentView);
        }

        private void BeginOfferDrag(string offerId, PointerEventData eventData)
        {
            m_DraggedOffer = FindOffer(m_CurrentView, offerId);
            m_DraggedBoardItem = null;
            m_HoveredBoardSlot = -1;
            if (m_DraggedOffer == null || m_DraggedOffer.Sold)
            {
                m_DraggedOffer = null;
                return;
            }

            HideTemporaryPreview();
            SetBoardItemRaycasts(false);
            RenderBoardDropSlots(m_CurrentView);
        }

        private void EndOfferDrag(string offerId, PointerEventData eventData)
        {
            ClearDrag();
        }

        private void EndBoardItemDrag()
        {
            if (m_DraggedBoardItem != null)
                ClearDrag();
        }

        private void OnBoardSlotHover(BuqiDeploymentSlotRef slot, bool over)
        {
            if ((m_DraggedOffer == null && m_DraggedBoardItem == null) ||
                slot.Area != BuqiDeploymentArea.Board)
                return;

            if (m_DraggedBoardItem != null)
            {
                if (over)
                    m_SellZone?.OnPointerExit(null);
                else if (m_ShelfPointerOver)
                    m_SellZone?.OnPointerEnter(null);
            }

            m_HoveredBoardSlot = over ? slot.Index : -1;
            RenderBoardDropSlots(m_CurrentView);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsShelfWorkspace(eventData))
                return;

            m_ShelfPointerOver = true;
            if (m_DraggedBoardItem != null)
                m_SellZone?.OnPointerEnter(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!IsShelfWorkspace(eventData))
                return;

            m_ShelfPointerOver = false;
            m_SellZone?.OnPointerExit(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_DraggedOffer != null || m_DraggedBoardItem != null)
                return;

            m_FixedPreview = null;
            m_HoverPreview = null;
            RenderDetails(null);
            m_HideItemDetails?.Invoke();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (m_DraggedBoardItem == null || !IsShelfWorkspace(eventData))
                return;

            m_SellZone?.OnPointerEnter(eventData);
            m_SellZone?.OnDrop(eventData);
        }

        private void OnBoardSlotDrop(BuqiDeploymentSlotRef slot)
        {
            if (slot.Area != BuqiDeploymentArea.Board)
            {
                return;
            }

            if (m_DraggedOffer != null)
            {
                if (!CanPurchaseOfferAt(m_CurrentView?.BoardSlots, m_DraggedOffer, slot.Index))
                    return;

                string offerId = m_DraggedOffer.Id;
                Action<BuqiUIDemoCommand> submit = m_Submit;
                ClearDrag();
                submit?.Invoke(new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.BuyOffer,
                    PrimaryId = offerId,
                    Slot = slot.Index,
                });
                return;
            }

            if (m_DraggedBoardItem == null ||
                !CanMoveBoardItem(m_CurrentView?.BoardSlots, m_DraggedBoardItem, slot.Index))
                return;

            BuqiDeploymentSnapshot deployment = BuildMoveSnapshot(
                m_CurrentView.BoardSlots,
                m_CurrentView.StorageSlots,
                m_DraggedBoardItem,
                slot.Index);
            Action<BuqiUIDemoCommand> moveSubmit = m_Submit;
            ClearDrag();
            moveSubmit?.Invoke(new BuqiUIDemoCommand
            {
                Type = BuqiUIDemoCommandType.ApplyDeployment,
                Deployment = deployment,
            });
        }

        private void ClearDrag()
        {
            m_DraggedOffer = null;
            m_DraggedBoardItem = null;
            m_HoveredBoardSlot = -1;
            m_ShelfPointerOver = false;
            SetBoardItemRaycasts(true);
            m_SellZone?.Cancel();
            if (m_CurrentView != null)
                RenderBoardDropSlots(m_CurrentView);
        }

        private bool IsShelfWorkspace(PointerEventData eventData)
        {
            if (eventData == null || !(transform is RectTransform rect))
                return true;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 local))
            {
                return false;
            }

            // The shelf occupies the upper work area; the lower board and right detail
            // panel remain independent drop targets and do not sell an item accidentally.
            return local.x >= -510f && local.x <= 510f && local.y >= -150f && local.y <= 180f;
        }

        private void ClearOfferDrag()
        {
            ClearDrag();
        }

        private void SetBoardItemRaycasts(bool enabled)
        {
            foreach (BuqiDraggableItemWidget item in m_BoardItems)
                item?.SetRaycastEnabled(enabled);
        }

        private IEnumerator PlaySuccessPulse()
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0.86f;
            yield return new WaitForSecondsRealtime(0.08f);
            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.1f);
            m_SuccessPulse = null;
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

        private bool CanPurchaseOfferAt(
            IReadOnlyList<BuqiDemoItemView> board,
            BuqiDemoOfferView offer,
            int anchorSlot)
        {
            return CanDropOfferAt(board, offer, anchorSlot) && HasEnoughCoins(offer);
        }

        private bool HasEnoughCoins(BuqiDemoOfferView offer)
        {
            return offer != null && (m_CurrentView?.Coins ?? 0) >= Math.Max(0, offer.Price);
        }

        private void SetFixedPreview(BuqiDemoItemView item)
        {
            m_FixedPreview = item;
            m_HoverPreview = null;
            RenderDetails(item);
            m_ShowItemDetails?.Invoke(item);
        }

        private void ShowTemporaryPreview(BuqiDemoItemView item)
        {
            m_HoverPreview = item;
            RenderDetails(item);
            m_ShowItemDetails?.Invoke(item);
        }

        private void HideTemporaryPreview()
        {
            m_HoverPreview = null;
            BuqiDemoItemView fallback = m_FixedPreview;
            RenderDetails(fallback);
            if (fallback == null)
                m_HideItemDetails?.Invoke();
            else
                m_ShowItemDetails?.Invoke(fallback);
        }

        private void RenderDetails(BuqiDemoItemView item)
        {
            if (m_DetailText == null)
                return;
            if (item == null)
            {
                string merchantName = m_Supply?.MerchantName ?? string.Empty;
                string specialty = m_Supply?.MerchantSpecialty ?? string.Empty;
                string context = m_CurrentView?.ContextBody ?? string.Empty;
                bool hasMerchantDetails = !string.IsNullOrEmpty(merchantName) ||
                                           !string.IsNullOrEmpty(specialty) ||
                                           !string.IsNullOrEmpty(context);
                if (m_DetailPanel != null)
                    m_DetailPanel.SetActive(hasMerchantDetails);
                m_DetailText.text = hasMerchantDetails
                    ? string.Join("\n", new[] { merchantName, specialty, context })
                    : string.Empty;
                return;
            }

            if (m_DetailPanel != null)
                m_DetailPanel.SetActive(true);

            string effect = string.IsNullOrEmpty(item.EffectDescription)
                ? item.Description
                : item.EffectDescription;
            string tags = item.Tags == null || item.Tags.Count == 0
                ? "无"
                : string.Join(" / ", item.Tags);
            m_DetailText.text = string.Format(
                "{0}\n品质：{1}\n购买价：{2} 金币\n出售价：{3} 金币\n尺寸：{4} 格\n冷却：{5} 时间单位\n作用：{6}\n类型：{7}  ·  流派：{8}\n标签：{9}\n位置：{10}\n改造：{11}",
                item.Name,
                string.IsNullOrEmpty(item.Quality) ? "普通" : item.Quality,
                item.Price,
                item.SellPrice,
                item.Size,
                item.CooldownTicks,
                effect,
                item.Role,
                item.ArchetypeId,
                tags,
                item.PositionHint,
                string.IsNullOrEmpty(item.UpgradeSummary) ? "无" : item.UpgradeSummary);
        }

        private static bool CanMoveBoardItem(
            IReadOnlyList<BuqiDemoItemView> board,
            BuqiDemoItemView item,
            int anchorSlot)
        {
            int span = item?.Size ?? 0;
            if (board == null || item == null || string.IsNullOrEmpty(item.Id) || span <= 0 ||
                anchorSlot < 0 || anchorSlot + span > board.Count)
            {
                return false;
            }

            int sourceAnchor = item.AnchorSlot >= 0 ? item.AnchorSlot : item.Slot;
            for (int slot = anchorSlot; slot < anchorSlot + span; slot++)
            {
                BuqiDemoItemView occupant = board[slot];
                if (occupant == null || occupant.Empty || string.IsNullOrEmpty(occupant.Id) ||
                    string.Equals(occupant.Id, item.Id, StringComparison.Ordinal))
                    continue;
                return false;
            }

            return sourceAnchor != anchorSlot || CanMoveWithinSource(board, item, sourceAnchor);
        }

        private static bool CanMoveWithinSource(
            IReadOnlyList<BuqiDemoItemView> board,
            BuqiDemoItemView item,
            int sourceAnchor)
        {
            return sourceAnchor >= 0 && sourceAnchor + item.Size <= board.Count;
        }

        private static BuqiDeploymentSnapshot BuildMoveSnapshot(
            IReadOnlyList<BuqiDemoItemView> board,
            IReadOnlyList<BuqiDemoItemView> storage,
            BuqiDemoItemView item,
            int targetAnchor)
        {
            var boardSlots = new string[board?.Count ?? 0];
            for (int index = 0; index < boardSlots.Length; index++)
            {
                BuqiDemoItemView slot = board[index];
                boardSlots[index] = slot == null || slot.Empty ? string.Empty : slot.Id ?? string.Empty;
                if (string.Equals(boardSlots[index], item.Id, StringComparison.Ordinal))
                    boardSlots[index] = string.Empty;
            }

            for (int offset = 0; offset < item.Size && targetAnchor + offset < boardSlots.Length; offset++)
                boardSlots[targetAnchor + offset] = item.Id;

            var storageSlots = new string[storage?.Count ?? 0];
            for (int index = 0; index < storageSlots.Length; index++)
            {
                BuqiDemoItemView slot = storage[index];
                storageSlots[index] = slot == null || slot.Empty ? string.Empty : slot.Id ?? string.Empty;
            }

            return new BuqiDeploymentSnapshot(boardSlots, storageSlots);
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

            float boardWidth = BoardSlotWidth * m_BoardSlotCount + BoardSlotGap * (m_BoardSlotCount - 1);
            float itemWidth = BoardSlotWidth * span + BoardSlotGap * (span - 1);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(
                -boardWidth * 0.5f + anchorSlot * (BoardSlotWidth + BoardSlotGap) + itemWidth * 0.5f,
                -22f);
            rect.sizeDelta = new Vector2(itemWidth, 92f);
        }

        private void EnsureDetailsSurface()
        {
            if (m_DetailPanel != null)
                return;

            Font font = null;
            Text existingText = GetComponentInChildren<Text>(true);
            if (existingText != null)
                font = existingText.font;
            m_DetailPanel = CreateRuntimePanel(
                transform,
                "RuntimeShopDetailsPanel",
                new Vector2(558f, -52f),
                new Vector2(300f, 430f),
                new Color32(24, 31, 37, 255));
            m_DetailText = CreateRuntimeText(
                m_DetailPanel.transform,
                "Details_Text",
                string.Empty,
                font,
                14,
                new Vector2(0f, 0f),
                new Vector2(276f, 408f),
                TextAnchor.UpperLeft);
            m_DetailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            m_DetailText.verticalOverflow = VerticalWrapMode.Overflow;
            m_DetailPanel.SetActive(false);
        }

        private void EnsureRuntimeSurface()
        {
            if (m_OfferCards.Length > 0 || m_BoardDropSlots.Length > 0 ||
                m_BoardItems.Length > 0 || m_SellZone != null)
            {
                return;
            }

            Button[] legacyActions = GetComponentsInChildren<Button>(true);

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
                GameObject offerObject = CreateRuntimePanel(
                    transform,
                    GameFramework.Utility.Text.Format("运行时商品卡{0:00}", index + 1),
                    new Vector2(-441f + index * 98f, 90f),
                    new Vector2(92f, 112f),
                    new Color32(36, 43, 51, 255));
                Text nameText = CreateRuntimeText(
                    offerObject.transform,
                    "Name_Text",
                    string.Empty,
                    font,
                    17,
                    new Vector2(0f, 30f),
                    new Vector2(84f, 30f),
                    TextAnchor.MiddleLeft);
                Text descriptionText = CreateRuntimeText(
                    offerObject.transform,
                    "Description_Text",
                    string.Empty,
                    font,
                    13,
                    new Vector2(0f, 0f),
                    new Vector2(84f, 30f),
                    TextAnchor.MiddleLeft);
                Text priceText = CreateRuntimeText(
                    offerObject.transform,
                    "Price_Text",
                    string.Empty,
                    font,
                    15,
                    new Vector2(0f, -34f),
                    new Vector2(84f, 28f),
                    TextAnchor.MiddleRight);
                OfferCardWidget card = offerObject.AddComponent<OfferCardWidget>();
                card.BindVisuals(
                    offerObject.GetComponent<Image>(),
                    nameText,
                    descriptionText,
                    priceText,
                    null);
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

            var dropSlots = new List<BuqiDeploySlotWidget>(m_BoardSlotCount);
            var boardItems = new List<BuqiDraggableItemWidget>(m_BoardSlotCount);
            float boardWidth = BoardSlotWidth * m_BoardSlotCount + BoardSlotGap * (m_BoardSlotCount - 1);
            for (int index = 0; index < m_BoardSlotCount; index++)
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
                    SellPrice = source.SellPrice,
                    CooldownTicks = source.CooldownTicks,
                    EffectDescription = source.EffectDescription,
                    Quality = source.Quality,
                    ArchetypeId = source.ArchetypeId,
                    Role = source.Role,
                    PositionHint = source.PositionHint,
                    UpgradeSummary = source.UpgradeSummary,
                    Tags = source.Tags == null ? new List<string>() : new List<string>(source.Tags),
                    Empty = source.Empty,
                    Selected = source.Selected,
                    Locked = source.Locked,
                    Slot = source.Slot,
                    AnchorSlot = source.AnchorSlot,
                },
                Price = offer.Price,
                AnchorSlot = offer.AnchorSlot,
                Span = offer.Span,
                Sold = offer.Sold,
                Locked = offer.Locked,
            };
        }
    }
}
