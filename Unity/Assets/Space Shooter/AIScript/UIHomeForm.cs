
using CodeBind;
using Game;
using UnityEngine;

namespace SpaceShooter.UI
{
    [MonoCodeBind('_')]
    public partial class UIHomeForm : AUGuiForm
    {
        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // m_PlayGameButton.onClick.AddListener(OnPlayGameButtonClick);
            // m_AboutButton.onClick.AddListener(OnAboutButtonClick);
            // m_AboutmaskButton.onClick.AddListener(OnAboutMaskButtonClick);
            //
            // m_versionText.text = Application.version;
            // m_AboutTransform.gameObject.SetActive(false);
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);

            // m_PlayGameButton.onClick.RemoveListener(OnPlayGameButtonClick);
            // m_AboutButton.onClick.RemoveListener(OnAboutButtonClick);
            // m_AboutmaskButton.onClick.RemoveListener(OnAboutMaskButtonClick);
        }

        private void OnPlayGameButtonClick()
        {
            // Handle play game logic
        }

        private void OnAboutButtonClick()
        {
            // m_AboutTransform.gameObject.SetActive(true);
        }

        private void OnAboutMaskButtonClick()
        {
            // m_AboutTransform.gameObject.SetActive(false);
        }
    }
}
