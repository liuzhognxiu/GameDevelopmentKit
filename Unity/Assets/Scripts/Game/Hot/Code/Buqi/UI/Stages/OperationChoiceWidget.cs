using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class OperationChoiceWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.OperationChoice;
        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            foreach (BuqiDemoChoiceView choice in view.Choices)
                AddAction(choice.Title, BuqiUIDemoCommandType.SelectOperation, choice.Id);
        }
    }
}
