
using CodeBind;
using Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceShooter.Test
{
    [MonoCodeBind('_')]
    public partial class UIBattleForm : AUGuiForm
    {
        // private GameObject _overView;
        // private TextMeshProUGUI _scoreLabel;
        // private Button _replayButton;
        // private Button _homeButton;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // _replayButton.onClick.AddListener(OnReplayButtonClick);
            // _homeButton.onClick.AddListener(OnHomeButtonClick);
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);

            // _replayButton.onClick.RemoveListener(OnReplayButtonClick);
            // _homeButton.onClick.RemoveListener(OnHomeButtonClick);
        }

        public void SetScore(int score)
        {
            // _scoreLabel.text = $"Score : {score}";
        }

        public void ShowOverView()
        {
            // _overView.SetActive(true);
        }

        private void OnReplayButtonClick()
        {
            // Handle replay logic
        }

        private void OnHomeButtonClick()
        {
            // Handle home logic
        }
    }
}
