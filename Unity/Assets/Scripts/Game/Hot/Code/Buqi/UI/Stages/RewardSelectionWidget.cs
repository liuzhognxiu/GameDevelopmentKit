using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class RewardSelectionWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.RewardSelection;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            foreach (BuqiDemoRewardView reward in view.Rewards)
            {
                if (!reward.Claimed)
                    AddAction(reward.Selected ? "领取" : "预览", reward.Selected
                        ? BuqiUIDemoCommandType.ClaimReward
                        : BuqiUIDemoCommandType.PreviewReward, reward.Id, reward.TargetId);
            }
        }
    }
}
