using CodeBind;
using Game;
using UnityEngine.UI;

namespace SpaceShooter.UI
{
    [MonoCodeBind('_')]
    public class UIAboutForm : AUGuiForm
    {
        // private Button _maskButton;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            // _maskButton.onClick.AddListener(OnMaskButtonClick);
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);
            // _maskButton.onClick.RemoveListener(OnMaskButtonClick);
        }

        private void OnMaskButtonClick()
        {
            // GameEntry.UI.CloseUIForm(this);
        }
    }
}