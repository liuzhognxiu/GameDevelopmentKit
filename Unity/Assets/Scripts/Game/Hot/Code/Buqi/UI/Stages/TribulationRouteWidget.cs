using System;
using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class TribulationRouteWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.TribulationRoute;
        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            foreach (BuqiDemoChoiceView choice in view.Choices)
            {
                int spend = choice.Id == "question-heart" ? Math.Min(view.DaoSeals, view.TribulationOmen) : 0;
                AddAction(choice.Title, BuqiUIDemoCommandType.SelectTribulationRoute, choice.Id, spend);
            }
        }
    }
}
