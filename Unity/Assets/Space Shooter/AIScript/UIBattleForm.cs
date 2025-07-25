using CodeBind;
using Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceShooter.UI
{
    [MonoCodeBind('_')]
    public partial class UIBattleForm : AUGuiForm
    {

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // _replayButton.onClick.AddListener(OnReplayButtonClick);
            // _homeButton.onClick.AddListener(OnHomeButtonClick);

            // Add event listeners here if needed
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);

            // _replayButton.onClick.RemoveListener(OnReplayButtonClick);
            // _homeButton.onClick.RemoveListener(OnHomeButtonClick);

            // Remove event listeners here
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