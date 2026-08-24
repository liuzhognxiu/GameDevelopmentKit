using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Widgets
{
    public sealed class BuqiRunPauseOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject m_Panel = null;
        [SerializeField] private Text m_Title = null;
        [SerializeField] private Button m_ContinueButton = null;
        [SerializeField] private Button m_ExitButton = null;

        public void Render(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit)
        {
            bool visible = view != null && view.IsPaused;
            if (m_Panel != null) m_Panel.SetActive(visible);
            if (!visible) return;
            if (m_Title != null) m_Title.text = "单局暂停";
            Bind(m_ContinueButton, () => submit?.Invoke(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.ResumeRun }));
            Bind(m_ExitButton, () => submit?.Invoke(new BuqiUIDemoCommand { Type = BuqiUIDemoCommandType.ExitRun }));
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
