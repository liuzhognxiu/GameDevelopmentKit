using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class RunTerminalWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.RunTerminal;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            AddAction("重新开始", BuqiUIDemoCommandType.Restart);
        }
    }
}
