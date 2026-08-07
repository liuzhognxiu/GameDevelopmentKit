using Game.Hot.Buqi.DemoUI;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class PredictionWidget : BuqiStageWidgetBase
    {
        public override BuqiUIDemoPhase Phase => BuqiUIDemoPhase.Prediction;

        protected override void ConfigureActions(BuqiUIDemoView view)
        {
            if (view.PredictionSubmitted)
                return;
            AddAction("预测: 胜利", BuqiUIDemoCommandType.SubmitPrediction, "Win");
            AddAction("预测: 失败", BuqiUIDemoCommandType.SubmitPrediction, "Lose");
            AddAction("预测: 平局", BuqiUIDemoCommandType.SubmitPrediction, "Draw");
            AddAction("跳过预测", BuqiUIDemoCommandType.SkipPrediction);
        }
    }
}
