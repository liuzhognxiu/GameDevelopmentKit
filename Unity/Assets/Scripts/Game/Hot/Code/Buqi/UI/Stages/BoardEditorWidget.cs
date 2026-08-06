using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class BoardEditorWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.BoardEditor;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            foreach (BuqiDemoItemView item in view.BoardSlots)
            {
                if (item.Empty)
                    AddAction(GameFramework.Utility.Text.Format("\u68CB\u76D8 {0}: \u7A7A\u4F4D", item.Slot + 1), BuqiUIDemoCommandType.PlaceBoardItem, slot: item.Slot);
                else
                    AddAction(GameFramework.Utility.Text.Format("\u68CB\u76D8 {0}: {1}", item.Slot + 1, item.Name), BuqiUIDemoCommandType.SelectBoardSource, item.Id);
            }
        }
    }
}
