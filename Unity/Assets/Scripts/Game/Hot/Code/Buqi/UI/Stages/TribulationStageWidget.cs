using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class TribulationStageWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.TribulationStage;
        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            AddAction("开始挑战", BuqiUIDemoCommandType.ResolveTribulationStage, "resolve");
        }
    }
}
