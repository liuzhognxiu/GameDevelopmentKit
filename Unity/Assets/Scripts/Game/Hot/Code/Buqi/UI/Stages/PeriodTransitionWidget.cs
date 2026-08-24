using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class PeriodTransitionWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.PeriodTransition;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            AddAction("继续", BuqiUIDemoCommandType.NextPhase);
        }
    }
}
