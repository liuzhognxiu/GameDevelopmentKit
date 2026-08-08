using System;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class OfferCardWidget : MonoBehaviour
    {
        private static readonly Color baseColor = new Color32(36, 43, 51, 255);
        private static readonly Color soldColor = new Color32(77, 73, 61, 255);

        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private GameObject m_LockOverlay = null;

        [SerializeField]
        private GameObject m_SoldOverlay = null;

        [SerializeField]
        private Text m_NameText = null;

        [SerializeField]
        private Text m_DescriptionText = null;

        [SerializeField]
        private Text m_PriceText = null;

        [SerializeField]
        private Button m_BuyButton = null;

        [SerializeField]
        private Button m_DetailsButton = null;

        private BuqiHoverDetailTrigger m_DetailTrigger;

        public void Render(BuqiDemoOfferView view, Action<string> onBuy, Action<string> onDetails)
        {
            Render(view, onBuy, onDetails, null);
        }

        public void Render(
            BuqiDemoOfferView view,
            Action<string> onBuy,
            Action<string> onDetails,
            Action onDetailsHidden)
        {
            if (view == null)
            {
                Clear();
                return;
            }

            Clear();
            gameObject.SetActive(true);

            BuqiDemoItemView item = view.Item;
            string itemName = item == null ? view.Id : string.IsNullOrEmpty(item.Name) ? item.Id : item.Name;
            string itemDescription = item == null ? string.Empty : item.Description;
            bool unavailable = view.Sold;

            SetText(m_NameText, itemName);
            SetText(m_DescriptionText, view.Sold ? "已售出" : itemDescription);
            SetText(m_PriceText, view.Sold ? "已购买" : GameFramework.Utility.Text.Format("价格 {0}", view.Price));
            SetBackground(view.Sold ? soldColor : baseColor);

            if (m_LockOverlay != null)
                m_LockOverlay.SetActive(false);
            if (m_SoldOverlay != null)
                m_SoldOverlay.SetActive(view.Sold);
            if (m_BuyButton != null)
            {
                m_BuyButton.interactable = !unavailable;
                if (onBuy != null && !unavailable)
                    m_BuyButton.onClick.AddListener(() => onBuy(view.Id));
            }
            if (m_DetailsButton != null)
            {
                m_DetailsButton.enabled = false;
                m_DetailsButton.interactable = false;
            }
            ResolveDetailTrigger().Bind(view.Id, onDetails, onDetailsHidden);
        }

        public void Clear()
        {
            if (m_BuyButton != null)
            {
                m_BuyButton.onClick.RemoveAllListeners();
                m_BuyButton.interactable = true;
            }
            if (m_DetailsButton != null)
            {
                m_DetailsButton.onClick.RemoveAllListeners();
                m_DetailsButton.enabled = false;
                m_DetailsButton.interactable = false;
            }
            if (m_DetailTrigger != null)
                m_DetailTrigger.Clear();
            if (m_Background != null)
                m_Background.color = baseColor;
            if (m_LockOverlay != null)
                m_LockOverlay.SetActive(false);
            if (m_SoldOverlay != null)
                m_SoldOverlay.SetActive(false);
            SetText(m_NameText, string.Empty);
            SetText(m_DescriptionText, string.Empty);
            SetText(m_PriceText, string.Empty);
            gameObject.SetActive(false);
        }

        private void SetBackground(Color color)
        {
            if (m_Background != null)
                m_Background.color = color;
        }

        private BuqiHoverDetailTrigger ResolveDetailTrigger()
        {
            if (m_DetailTrigger == null)
                m_DetailTrigger = GetComponent<BuqiHoverDetailTrigger>();
            if (m_DetailTrigger == null)
                m_DetailTrigger = gameObject.AddComponent<BuqiHoverDetailTrigger>();
            return m_DetailTrigger;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
