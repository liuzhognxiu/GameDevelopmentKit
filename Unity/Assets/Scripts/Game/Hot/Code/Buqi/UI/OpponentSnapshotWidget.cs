using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class OpponentSnapshotWidget : MonoBehaviour
    {
        [SerializeField]
        private Text m_NameText = null;

        [SerializeField]
        private Text m_BuildText = null;

        [SerializeField]
        private Text m_SlotsText = null;

        [SerializeField]
        private Text m_ThreatText = null;

        [SerializeField]
        private Text m_RiskText = null;

        [SerializeField]
        private Image m_StatusMarker = null;

        [SerializeField]
        private Text[] m_ItemLabels = Array.Empty<Text>();

        [SerializeField]
        private Button[] m_ItemButtons = Array.Empty<Button>();

        private Action<string> m_ItemDetailsHandler;
        private string[] m_ItemIds = Array.Empty<string>();

        public void Render(BuqiDemoOpponentView view, Action<string> onItemDetails)
        {
            Clear();
            if (view == null)
                return;

            gameObject.SetActive(true);
            m_ItemDetailsHandler = onItemDetails;
            m_ItemIds = new string[m_ItemButtons.Length];

            SetText(m_NameText, string.IsNullOrEmpty(view.Name) ? view.Id : view.Name);
            SetText(m_BuildText, string.Format("\u65B9\u5411  {0}", EmptyFallback(view.Build, "\u672A\u516C\u5F00")));
            SetText(m_SlotsText, "\u8FDE\u7EED 8 \u683C\u6784\u7B51  \u00B7  \u516C\u5F00\u60C5\u62A5");
            SetText(m_ThreatText, "\u4E3B\u8981\u5A01\u80C1\uFF1A\u516C\u5F00\u88C5\u5907\u89E6\u53D1\u5173\u7CFB");
            SetText(m_RiskText, "\u5DF2\u77E5\u98CE\u9669\uFF1A\u672A\u516C\u5F00\u6539\u9020");
            if (m_StatusMarker != null)
                m_StatusMarker.color = new Color32(229, 176, 71, 255);

            for (int index = 0; index < m_ItemButtons.Length; index++)
            {
                Button button = m_ItemButtons[index];
                if (button == null)
                    continue;

                button.onClick.RemoveAllListeners();
                button.interactable = false;
                SetText(GetItemLabel(index), "\u7A7A\u7F6E");

                BuqiDemoItemView item = GetItem(view.Items, index);
                if (item == null || item.Empty)
                    continue;

                m_ItemIds[index] = item.Id ?? string.Empty;
                string itemText = string.IsNullOrEmpty(item.Name) ? item.Id : item.Name;
                if (!string.IsNullOrEmpty(item.Description))
                    itemText = string.Format("{0}\n{1}", itemText, item.Description);
                SetText(GetItemLabel(index), itemText);
                button.interactable = m_ItemDetailsHandler != null;
                int slotIndex = index;
                button.onClick.AddListener(() => HandleItemDetails(slotIndex));
            }
        }

        public void Clear()
        {
            m_ItemDetailsHandler = null;
            m_ItemIds = Array.Empty<string>();
            for (int index = 0; index < m_ItemButtons.Length; index++)
            {
                Button button = m_ItemButtons[index];
                if (button == null)
                    continue;
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }

            SetText(m_NameText, string.Empty);
            SetText(m_BuildText, string.Empty);
            SetText(m_SlotsText, string.Empty);
            SetText(m_ThreatText, string.Empty);
            SetText(m_RiskText, string.Empty);
            for (int index = 0; index < m_ItemLabels.Length; index++)
                SetText(m_ItemLabels[index], string.Empty);
            if (m_StatusMarker != null)
                m_StatusMarker.color = new Color32(92, 102, 104, 255);
            gameObject.SetActive(false);
        }

        private void HandleItemDetails(int index)
        {
            if (index < 0 || index >= m_ItemIds.Length || string.IsNullOrEmpty(m_ItemIds[index]))
                return;
            m_ItemDetailsHandler?.Invoke(m_ItemIds[index]);
        }

        private Text GetItemLabel(int index)
        {
            return index >= 0 && index < m_ItemLabels.Length ? m_ItemLabels[index] : null;
        }

        private static BuqiDemoItemView GetItem(System.Collections.Generic.IReadOnlyList<BuqiDemoItemView> items, int index)
        {
            return items != null && index >= 0 && index < items.Count ? items[index] : null;
        }

        private static string EmptyFallback(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value ?? string.Empty;
        }
    }
}
