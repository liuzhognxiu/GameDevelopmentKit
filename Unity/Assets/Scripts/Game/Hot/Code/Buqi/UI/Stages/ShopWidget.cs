using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.UI.Widgets;
using UnityEngine;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class ShopWidget : BuqiStageWidgetBase
    {
        [SerializeField]
        private OfferCardWidget[] m_OfferCards = Array.Empty<OfferCardWidget>();

        [SerializeField]
        private BuqiSellZoneWidget m_SellZone = null;

        [SerializeField]
        private BuqiDraggableItemWidget[] m_BoardItems = Array.Empty<BuqiDraggableItemWidget>();

        private IBuqiBazaarSupplyViewSource m_SupplySource;
        private BuqiBazaarSupplyView m_Supply;
        private Action<BuqiDemoItemView> m_ShowItemDetails;
        private Action m_HideItemDetails;

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
            RenderOffers(view, submit);
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
            foreach (OfferCardWidget card in m_OfferCards)
                card?.Clear();
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
                    () => m_HideItemDetails?.Invoke());
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
            }
        }

        private void BeginSale(BuqiDemoItemView item, Action<BuqiUIDemoCommand> submit)
        {
            if (item == null || m_SellZone == null)
                return;

            m_SellZone.BindCommand(
                item.Id,
                Math.Max(1, item.Price / 2),
                instanceId => submit?.Invoke(new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.SellItem,
                    PrimaryId = instanceId,
                }));
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
