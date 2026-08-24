using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class TrainingWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.Training;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            foreach (BuqiDemoChoiceView choice in view.Choices)
            {
                if (!choice.Disabled)
                    AddAction(choice.Title, BuqiUIDemoCommandType.ExecuteTraining, choice.Id, choice.TargetId);
            }
        }
    }
}
