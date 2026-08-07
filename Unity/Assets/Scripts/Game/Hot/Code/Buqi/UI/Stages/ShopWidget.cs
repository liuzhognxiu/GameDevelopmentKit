using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class ShopWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.Shop;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            foreach (BuqiDemoOfferView offer in view.ShopOffers)
            {
                string name = offer.Item == null ? offer.Id : offer.Item.Name;
                string label = offer.Sold
                    ? GameFramework.Utility.Text.Format("{0} [已售出]", name)
                    : GameFramework.Utility.Text.Format("{0}  {1} 金币", name, offer.Price);
                AddAction(label, BuqiUIDemoCommandType.BuyOffer, offer.Id);
            }
            AddAction(view.ShopLocked ? "解锁商店" : "锁定商店", BuqiUIDemoCommandType.ToggleShopLock);
            AddAction("刷新商店  1 金币", BuqiUIDemoCommandType.RefreshShop);
        }
    }
}
