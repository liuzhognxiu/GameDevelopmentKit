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
                "{0}\n构筑方向: {1}\n公开装备: {2}",
                view.Opponent.Name,
                view.Opponent.Build,
                view.Opponent.Items.Count);
        }
    }
}
