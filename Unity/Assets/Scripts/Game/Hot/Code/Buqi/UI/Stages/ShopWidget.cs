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
                    ? GameFramework.Utility.Text.Format("{0} [\u5DF2\u552E\u51FA]", name)
                    : GameFramework.Utility.Text.Format("{0}  {1} \u91D1\u5E01", name, offer.Price);
                AddAction(label, BuqiUIDemoCommandType.BuyOffer, offer.Id);
            }
            AddAction(view.ShopLocked ? "\u89E3\u9501\u5546\u5E97" : "\u9501\u5B9A\u5546\u5E97", BuqiUIDemoCommandType.ToggleShopLock);
            AddAction("\u5237\u65B0\u5546\u5E97  1 \u91D1\u5E01", BuqiUIDemoCommandType.RefreshShop);
        }
    }
}
