using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class TribulationStageWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.TribulationStage;
        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            AddAction("应劫", BuqiUIDemoCommandType.ResolveTribulationStage, "resolve");
        }
    }
}
