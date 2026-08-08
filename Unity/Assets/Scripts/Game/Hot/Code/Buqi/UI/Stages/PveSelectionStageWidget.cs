using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class PveSelectionStageWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.PveSelection;
        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            foreach (BuqiDemoChoiceView choice in view.Choices)
                AddAction(
                    GameFramework.Utility.Text.Format("{0}\n{1}", choice.Title, choice.Description),
                    BuqiUIDemoCommandType.SelectPveDifficulty,
                    choice.Id);
        }
    }
}
