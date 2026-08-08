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
                AddAction($"{name}  {offer.Price} coins", BuqiUIDemoCommandType.BuyOffer, offer.Id);
            }

            AddAction("Deploy", BuqiUIDemoCommandType.OpenDragDeploy);
        }
    }
}
