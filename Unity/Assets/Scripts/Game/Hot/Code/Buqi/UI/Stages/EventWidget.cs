using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class EventWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.Event;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            foreach (BuqiDemoChoiceView choice in view.Choices)
                AddAction(choice.Title, BuqiUIDemoCommandType.SelectChoice, choice.Id);

            AddAction("调整装备栏", BuqiUIDemoCommandType.OpenDragDeploy);
        }
    }
}
