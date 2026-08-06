using System.Linq;
using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class BattleSummaryWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.BattleSummary;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
        }

        protected override string ResolveBody(BuqiUIDemoView view)
        {
            return view == null || view.Facts.Count == 0
                ? base.ResolveBody(view)
                : string.Join("\n", view.Facts.Select(fact =>
                    GameFramework.Utility.Text.Format("{0}: {1}", fact.Title, fact.Body)));
        }
    }
}
