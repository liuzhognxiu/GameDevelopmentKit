using System;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.UI.Widgets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI
{
    [DisallowMultipleComponent]
    public sealed class OfferCardWidget : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler
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
        private string m_OfferId = string.Empty;
        private Action<string, PointerEventData> m_BeginDrag;
        private Action<PointerEventData> m_Drag;
        private Action<string, PointerEventData> m_EndDrag;
        private Action<string> m_FixedDetails;
        private bool m_Draggable;

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
            Render(view, onBuy, onDetails, onDetailsHidden, null, null, null);
        }

        public void Render(
            BuqiDemoOfferView view,
            Action<string> onBuy,
            Action<string> onDetails,
            Action onDetailsHidden,
            Action<string, PointerEventData> onBeginDrag,
            Action<PointerEventData> onDrag,
            Action<string, PointerEventData> onEndDrag,
            Action<string> onFixedDetails = null)
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
            string itemDescription = item == null
                ? string.Empty
                : GameFramework.Utility.Text.Format("{0}\n{1} 格 · {2}", item.Description, Math.Max(1, item.Size),
                    string.IsNullOrEmpty(item.Quality) ? "普通" : item.Quality);
            bool unavailable = view.Sold;
            m_OfferId = view.Id ?? string.Empty;
            m_BeginDrag = onBeginDrag;
            m_Drag = onDrag;
            m_EndDrag = onEndDrag;
            m_FixedDetails = onFixedDetails ?? onDetails;
            m_Draggable = !unavailable && onBeginDrag != null;

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
                // A shop card is a preview surface. Purchase is submitted only by drag/drop.
                m_BuyButton.interactable = false;
                m_BuyButton.gameObject.SetActive(false);
            }
            if (m_DetailsButton != null)
            {
                m_DetailsButton.enabled = false;
                m_DetailsButton.interactable = false;
                m_DetailsButton.gameObject.SetActive(false);
            }
            ResolveDetailTrigger().Bind(view.Id, onDetails, onDetailsHidden);
        }

        public void BindVisuals(
            Image background,
            Text nameText,
            Text descriptionText,
            Text priceText,
            Button buyButton)
        {
            m_Background = background;
            m_NameText = nameText;
            m_DescriptionText = descriptionText;
            m_PriceText = priceText;
            m_BuyButton = buyButton;
        }

        public void SetShelfLayout(
            int anchorSlot,
            int span,
            float slotWidth,
            float gap,
            int shelfSlotCount)
        {
            if (!(transform is RectTransform rect) || anchorSlot < 0 || shelfSlotCount <= 0)
                return;

            span = Math.Max(1, span);
            float shelfWidth = shelfSlotCount * slotWidth + (shelfSlotCount - 1) * gap;
            float itemWidth = span * slotWidth + (span - 1) * gap;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(
                -shelfWidth * 0.5f + anchorSlot * (slotWidth + gap) + itemWidth * 0.5f,
                rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(itemWidth, rect.sizeDelta.y > 0f ? rect.sizeDelta.y : 112f);
        }

        public void Clear()
        {
            if (m_BuyButton != null)
            {
                m_BuyButton.onClick.RemoveAllListeners();
                m_BuyButton.interactable = true;
                m_BuyButton.gameObject.SetActive(false);
            }
            if (m_DetailsButton != null)
            {
                m_DetailsButton.onClick.RemoveAllListeners();
                m_DetailsButton.enabled = false;
                m_DetailsButton.interactable = false;
                m_DetailsButton.gameObject.SetActive(false);
            }
            if (m_DetailTrigger != null)
                m_DetailTrigger.Clear();
            m_OfferId = string.Empty;
            m_BeginDrag = null;
            m_Drag = null;
            m_EndDrag = null;
            m_FixedDetails = null;
            m_Draggable = false;
            CanvasGroup canvasGroup = ResolveCanvasGroup();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
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

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!m_Draggable)
                return;

            CanvasGroup canvasGroup = ResolveCanvasGroup();
            canvasGroup.alpha = 0.55f;
            canvasGroup.blocksRaycasts = false;
            m_BeginDrag?.Invoke(m_OfferId, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_Draggable)
                m_Drag?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            CanvasGroup canvasGroup = ResolveCanvasGroup();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            if (m_Draggable)
                m_EndDrag?.Invoke(m_OfferId, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(m_OfferId))
                m_FixedDetails?.Invoke(m_OfferId);
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

        private CanvasGroup ResolveCanvasGroup()
        {
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            return canvasGroup;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
