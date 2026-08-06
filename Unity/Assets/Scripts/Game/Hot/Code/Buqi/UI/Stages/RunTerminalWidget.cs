using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class RunTerminalWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.RunTerminal;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            AddAction("\u91CD\u65B0\u5F00\u59CB", BuqiUIDemoCommandType.Restart);
        }
    }
}
