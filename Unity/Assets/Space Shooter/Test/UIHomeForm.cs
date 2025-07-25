
using CodeBind;
using Game;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceShooter.Test
{
    [MonoCodeBind('_')]
    public partial class UIHomeForm : AUGuiForm
    {
        // private TextMeshProUGUI _versionText;
        // private GameObject _aboutView;
        // private Button _playGameButton;
        // private Button _aboutButton;
        // private Button _aboutMaskButton;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // _playGameButton.onClick.AddListener(OnPlayGameButtonClick);
            // _aboutButton.onClick.AddListener(OnAboutButtonClick);
            // _aboutMaskButton.onClick.AddListener(OnAboutMaskButtonClick);

            // _versionText.text = Application.version;
            // _aboutView.SetActive(false);
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);

            // _playGameButton.onClick.RemoveListener(OnPlayGameButtonClick);
            // _aboutButton.onClick.RemoveListener(OnAboutButtonClick);
            // _aboutMaskButton.onClick.RemoveListener(OnAboutMaskButtonClick);
        }

        private void OnPlayGameButtonClick()
        {
            // Handle play game logic
        }

        private void OnAboutButtonClick()
        {
            // _aboutView.SetActive(true);
        }

        private void OnAboutMaskButtonClick()
        {
            // _aboutView.SetActive(false);
        }
    }
}
