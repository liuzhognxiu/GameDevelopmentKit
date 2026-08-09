using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Interaction;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class ShopWidget : BuqiStageWidgetBase
    {
        private IBuqiBazaarSupplyViewSource m_SupplySource;
        private BuqiBazaarSupplyView m_Supply;

        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.Shop;

        public void BindSupplySource(IBuqiBazaarSupplyViewSource supplySource)
        {
            m_SupplySource = supplySource;
            m_Supply = null;
        }

        protected override void Prepare(BuqiUIDemoView view)
        {
            m_Supply = null;
            m_SupplySource?.TryGetCurrentSupply(out m_Supply);
        }

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            foreach (BuqiDemoOfferView offer in view.ShopOffers)
            {
                if (offer.Sold)
                    continue;
                string name = offer.Item == null ? offer.Id : offer.Item.Name;
                string role = m_Supply?.FindOfferRole(offer.Id) ?? string.Empty;
                string label = string.IsNullOrEmpty(role)
                    ? $"{name}  {offer.Price} coins"
                    : $"{role} · {name}  {offer.Price} coins";
                AddAction(label, BuqiUIDemoCommandType.BuyOffer, offer.Id);
            }
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
            return GameFramework.Utility.Text.Format("{0}   余额 {1}", refresh, view?.Coins ?? 0);
        }

        protected override void OnCleared()
        {
            m_Supply = null;
        }
    }
}
