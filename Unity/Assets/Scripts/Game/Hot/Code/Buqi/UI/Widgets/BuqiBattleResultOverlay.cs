using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Widgets
{
    public sealed class BuqiBattleResultOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject m_Panel = null;
        [SerializeField] private Text m_ResultText = null;
        [SerializeField] private Button m_ContinueButton = null;

        public void Render(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit)
        {
            bool visible = view != null && view.BattleResultVisible && !view.IsPaused;
            if (m_Panel != null) m_Panel.SetActive(visible);
            if (!visible) return;
            if (m_ResultText != null) m_ResultText.text = view.BattleResultLabel;
            if (m_ContinueButton != null)
            {
                m_ContinueButton.onClick.RemoveAllListeners();
                m_ContinueButton.onClick.AddListener(() => submit?.Invoke(new BuqiUIDemoCommand
                {
                    Type = BuqiUIDemoCommandType.ContinueBattleResult,
                }));
            }
        }
    }
}
