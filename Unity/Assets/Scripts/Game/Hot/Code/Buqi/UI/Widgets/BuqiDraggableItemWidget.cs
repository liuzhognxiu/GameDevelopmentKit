using System;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Widgets
{
    [DisallowMultipleComponent]
    public sealed class BuqiDraggableItemWidget : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler
    {
        [SerializeField]
        private CanvasGroup m_CanvasGroup = null;

        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private Text m_NameText = null;

        [SerializeField]
        private Text m_SizeText = null;

        [SerializeField]
        private Text m_SourceText = null;

        private BuqiDeploymentSlotRef m_Source;
        private Action<BuqiDeploymentSlotRef> m_Click;
        private Action<BuqiDeploymentSlotRef, PointerEventData> m_BeginDrag;
        private Action<PointerEventData> m_Drag;
        private Action<BuqiDeploymentSlotRef, PointerEventData> m_EndDrag;

        public void BindVisuals(
            CanvasGroup canvasGroup,
            Image background,
            Text nameText,
            Text sizeText,
            Text sourceText)
        {
            m_CanvasGroup = canvasGroup;
            m_Background = background;
            m_NameText = nameText;
            m_SizeText = sizeText;
            m_SourceText = sourceText;
        }

        public void Render(
            BuqiUIDemoItemDefinition item,
            BuqiDeploymentSlotRef source,
            Action<BuqiDeploymentSlotRef> click,
            Action<BuqiDeploymentSlotRef, PointerEventData> beginDrag,
            Action<PointerEventData> drag,
            Action<BuqiDeploymentSlotRef, PointerEventData> endDrag)
        {
            m_Source = source;
            m_Click = click;
            m_BeginDrag = beginDrag;
            m_Drag = drag;
            m_EndDrag = endDrag;
            CanvasGroup canvasGroup = ResolveCanvasGroup();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            if (m_Background != null)
                m_Background.color = source.Area == BuqiDeploymentArea.Board
                    ? new Color32(61, 113, 105, 255)
                    : new Color32(74, 84, 94, 255);
            SetText(m_NameText, item == null ? string.Empty : item.Name);
            SetText(m_SizeText, item == null
                ? string.Empty
                : GameFramework.Utility.Text.Format("占用 {0} 格", item.Size));
            SetText(m_SourceText, GameFramework.Utility.Text.Format(
                source.Area == BuqiDeploymentArea.Board ? "棋盘 {0:00}" : "仓库 {0:00}",
                source.Index + 1));
        }

        public void Clear()
        {
            m_Click = null;
            m_BeginDrag = null;
            m_Drag = null;
            m_EndDrag = null;
            CanvasGroup canvasGroup = ResolveCanvasGroup();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            SetText(m_NameText, string.Empty);
            SetText(m_SizeText, string.Empty);
            SetText(m_SourceText, string.Empty);
        }

        public void SetRaycastEnabled(bool enabled)
        {
            ResolveCanvasGroup().blocksRaycasts = enabled;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            CanvasGroup canvasGroup = ResolveCanvasGroup();
            canvasGroup.alpha = 0.35f;
            SetRaycastEnabled(false);
            m_BeginDrag?.Invoke(m_Source, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            m_Drag?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            CanvasGroup canvasGroup = ResolveCanvasGroup();
            canvasGroup.alpha = 1f;
            SetRaycastEnabled(true);
            m_EndDrag?.Invoke(m_Source, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            m_Click?.Invoke(m_Source);
        }

        private CanvasGroup ResolveCanvasGroup()
        {
            if (m_CanvasGroup == null)
                m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null)
                m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            return m_CanvasGroup;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
