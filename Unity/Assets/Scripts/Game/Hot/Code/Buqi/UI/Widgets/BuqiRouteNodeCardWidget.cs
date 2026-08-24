using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Widgets
{
    public sealed class BuqiRouteNodeCardWidget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Text m_TitleText = null;
        [SerializeField] private Text m_DetailText = null;
        [SerializeField] private GameObject m_HoverDetails = null;
        [SerializeField] private Button m_Button = null;

        public void Render(BuqiDemoRouteNodeView node, Action<string> selected)
        {
            if (m_TitleText != null) m_TitleText.text = node?.Title ?? string.Empty;
            if (m_DetailText != null)
            {
                m_DetailText.text = node == null
                    ? string.Empty
                    : string.Format("{0}\n{1}\n{2}", node.Benefit, node.Cost, node.Condition);
            }
            m_HoverDetails?.SetActive(false);
            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.interactable = node != null && node.Available;
                string nodeId = node?.Id ?? string.Empty;
                m_Button.onClick.AddListener(() => selected?.Invoke(nodeId));
            }
        }

        public void Clear()
        {
            m_Button?.onClick.RemoveAllListeners();
            m_HoverDetails?.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_HoverDetails?.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_HoverDetails?.SetActive(false);
        }
    }
}
