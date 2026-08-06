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
            AddAction("\u9884\u6D4B: \u80DC\u5229", BuqiUIDemoCommandType.SubmitPrediction, "Win");
            AddAction("\u9884\u6D4B: \u5931\u8D25", BuqiUIDemoCommandType.SubmitPrediction, "Lose");
            AddAction("\u9884\u6D4B: \u5E73\u5C40", BuqiUIDemoCommandType.SubmitPrediction, "Draw");
            AddAction("\u8DF3\u8FC7\u9884\u6D4B", BuqiUIDemoCommandType.SkipPrediction);
        }
    }
}
