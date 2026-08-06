using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class OpponentIntelWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.OpponentIntel;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
        }

        protected override string ResolveBody(BuqiUIDemoView view)
        {
            if (view?.Opponent == null)
                return base.ResolveBody(view);
            return GameFramework.Utility.Text.Format(
                "{0}\n\u6784\u7B51\u65B9\u5411: {1}\n\u516C\u5F00\u88C5\u5907: {2}",
                view.Opponent.Name,
                view.Opponent.Build,
                view.Opponent.Items.Count);
        }
    }
}
