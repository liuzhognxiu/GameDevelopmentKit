using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Hot.Buqi.UI.Widgets;
using UnityGameFramework.Runtime;

namespace Game.Hot.Buqi.UI
{
    public sealed class BuqiDragDeployOpenData
    {
        public BuqiUIDemoCatalog Catalog;
        public IReadOnlyList<BuqiDemoItemView> Board;
        public IReadOnlyList<BuqiDemoItemView> Storage;
        public int Round;
        public int Coins;
        public int Wins;
        public int Lives;
        public string OpponentName = string.Empty;
        public Action<BuqiDeploymentSnapshot> Confirmed;
    }

    [DisallowMultipleComponent]
    public sealed class BuqiDragDeployForm : StarForceUIForm
    {
        private const float BoardSlotWidth = 108f;
        private const float SlotGap = 8f;
        private const float StorageSlotHeight = 92f;
        private const float StorageGap = 12f;
        [SerializeField]
        private Text m_TitleText = null;

        [SerializeField]
        private Text m_ContextText = null;

        [SerializeField]
        private Text m_DetailText = null;

        [SerializeField]
        private Text m_FeedbackText = null;

        [SerializeField]
        private BuqiDeploySlotWidget[] m_BoardSlots = Array.Empty<BuqiDeploySlotWidget>();

        [SerializeField]
        private BuqiDeploySlotWidget[] m_StorageSlots = Array.Empty<BuqiDeploySlotWidget>();

        [SerializeField]
        private BuqiDraggableItemWidget m_ItemTemplate = null;

        [SerializeField]
        private Transform m_BoardItemLayer = null;

        [SerializeField]
        private Transform m_StorageItemLayer = null;

        [SerializeField]
        private Transform m_DragLayer = null;

        [SerializeField]
        private Button m_ResetButton = null;

        [SerializeField]
        private Button m_CancelButton = null;

        [SerializeField]
        private Button m_ConfirmButton = null;

        private readonly List<BuqiDraggableItemWidget> m_ItemWidgets = new List<BuqiDraggableItemWidget>();
        private BuqiUIDemoCatalog m_Catalog;
        private BuqiDragDeployController m_Controller;
        private Action<BuqiDeploymentSnapshot> m_Confirmed;
        private BuqiDeploymentSlotRef? m_SelectedSource;
        private BuqiDeploymentTargetPreview m_Preview;
        private BuqiDraggableItemWidget m_DragVisual;
        private bool m_IsDragging;
        private bool m_DropHandled;

        public BuqiDeploymentSnapshot View => m_Controller?.View;

#if UNITY_2017_3_OR_NEWER
        protected override void OnInit(object userData)
#else
        protected internal override void OnInit(object userData)
#endif
        {
            base.OnInit(userData);
            m_ResetButton?.onClick.AddListener(ResetDeployment);
            m_CancelButton?.onClick.AddListener(OnCancelButtonClick);
            m_ConfirmButton?.onClick.AddListener(OnConfirmButtonClick);
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnOpen(object userData)
#else
        protected internal override void OnOpen(object userData)
#endif
        {
            base.OnOpen(userData);
            if (!TryInitialize(userData as BuqiDragDeployOpenData, out string error))
            {
                Log.Warning(error);
                Close();
            }
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnClose(bool isShutdown, object userData)
#else
        protected internal override void OnClose(bool isShutdown, object userData)
#endif
        {
            CancelSession();
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDestroy()
        {
            m_ResetButton?.onClick.RemoveListener(ResetDeployment);
            m_CancelButton?.onClick.RemoveListener(OnCancelButtonClick);
            m_ConfirmButton?.onClick.RemoveListener(OnConfirmButtonClick);
            CancelSession();
            base.OnDestroy();
        }

        public bool TryInitialize(BuqiDragDeployOpenData data, out string error)
        {
            CancelSession();
            if (data == null || data.Catalog == null || data.Board == null || data.Storage == null)
                return RejectInitialization(out error, "拖拽上阵数据不可用");
            if (data.Confirmed == null)
                return RejectInitialization(out error, "拖拽上阵确认回调不可用");
            if (data.Board.Count != BuqiDragDeployController.BoardSlotCount)
                return RejectInitialization(out error, "棋盘位置数量无效");
            if (data.Storage.Count != BuqiDragDeployController.StorageSlotCount)
                return RejectInitialization(out error, "仓库位置数量无效");

            var board = ToIds(data.Board);
            var storage = ToIds(data.Storage);
            if (!BuqiDragDeployController.TryCreate(
                    data.Catalog,
                    board,
                    storage,
                    out BuqiDragDeployController controller,
                    out error))
                return false;

            m_Catalog = data.Catalog;
            m_Controller = controller;
            m_Confirmed = data.Confirmed;
            m_SelectedSource = null;
            m_Preview = null;
            SetText(m_ContextText, GameFramework.Utility.Text.Format(
                "第 {0} 回合  |  金币 {1}  |  胜场 {2}  |  生命 {3}  |  对手 {4}",
                data.Round,
                data.Coins,
                data.Wins,
                data.Lives,
                string.IsNullOrEmpty(data.OpponentName) ? "待侦察" : data.OpponentName));
            SetText(m_FeedbackText, string.Empty);
            Render();
            return true;
        }

        public void SelectSource(BuqiDeploymentSlotRef source)
        {
            if (m_Controller == null)
                return;
            if (!TryResolveItem(source, out BuqiDeploymentSlotRef resolvedSource, out string error))
            {
                SetText(m_FeedbackText, error);
                return;
            }
            m_SelectedSource = resolvedSource;
            m_Preview = null;
            SetText(m_FeedbackText, string.Empty);
            RenderSlots();
            RenderDetails();
        }

        public BuqiDeploymentCommandResult MoveSelectedTo(BuqiDeploymentSlotRef target)
        {
            if (m_Controller == null)
                return Rejected("拖拽上阵数据不可用");
            if (!m_SelectedSource.HasValue)
                return Rejected("请先选择装备");

            BuqiDeploymentCommandResult result = m_Controller.TryMove(m_SelectedSource.Value, target);
            if (result.Accepted)
            {
                m_SelectedSource = null;
                m_Preview = null;
                SetText(m_FeedbackText, string.Empty);
                Render();
            }
            else
            {
                SetText(m_FeedbackText, result.Reason);
                RenderSlots();
            }
            return result;
        }

        public void ResetDeployment()
        {
            if (m_Controller == null)
                return;
            m_Controller.Reset();
            m_SelectedSource = null;
            m_Preview = null;
            SetText(m_FeedbackText, string.Empty);
            Render();
        }

        public bool TryConfirm(out string error)
        {
            if (m_Controller == null || m_Confirmed == null)
            {
                error = "部署已确认或不可用";
                return false;
            }
            Action<BuqiDeploymentSnapshot> callback = m_Confirmed;
            m_Confirmed = null;
            callback(m_Controller.View);
            error = string.Empty;
            return true;
        }

        public void CancelSession()
        {
            ClearDragVisual();
            foreach (BuqiDraggableItemWidget itemWidget in m_ItemWidgets)
                itemWidget?.Clear();
            for (int index = 0; index < m_BoardSlots.Length; index++)
                m_BoardSlots[index]?.Clear();
            for (int index = 0; index < m_StorageSlots.Length; index++)
                m_StorageSlots[index]?.Clear();
            DestroyItemWidgets();
            m_Catalog = null;
            m_Controller = null;
            m_Confirmed = null;
            m_SelectedSource = null;
            m_Preview = null;
            m_IsDragging = false;
            m_DropHandled = false;
            SetText(m_ContextText, string.Empty);
            SetText(m_FeedbackText, string.Empty);
        }

        public void OnConfirmButtonClick()
        {
            if (TryConfirm(out _))
                Close();
        }

        public void OnCancelButtonClick()
        {
            CancelSession();
            Close();
        }

        private void OnItemClick(BuqiDeploymentSlotRef source)
        {
            if (m_SelectedSource.HasValue && m_SelectedSource.Value != source)
            {
                MoveSelectedTo(source);
                return;
            }
            SelectSource(source);
        }

        private void OnBeginDrag(BuqiDeploymentSlotRef source, PointerEventData eventData)
        {
            SelectSource(source);
            if (!m_SelectedSource.HasValue)
                return;
            SetItemRaycasts(false);
            m_IsDragging = true;
            m_DropHandled = false;
            CreateDragVisual(m_SelectedSource.Value);
            UpdateDragVisual(eventData);
        }

        private void OnDrag(PointerEventData eventData)
        {
            if (m_IsDragging)
                UpdateDragVisual(eventData);
        }

        private void OnEndDrag(BuqiDeploymentSlotRef source, PointerEventData eventData)
        {
            if (m_IsDragging && !m_DropHandled)
                SetText(m_FeedbackText, "请将装备放在有效位置");
            SetItemRaycasts(true);
            ClearDragVisual();
            m_IsDragging = false;
            m_Preview = null;
            Render();
        }

        private void OnSlotClick(BuqiDeploymentSlotRef target)
        {
            if (m_SelectedSource.HasValue)
                MoveSelectedTo(target);
        }

        private void OnSlotHover(BuqiDeploymentSlotRef target, bool isInside)
        {
            if (!isInside || !m_SelectedSource.HasValue || m_Controller == null)
            {
                m_Preview = null;
                RenderSlots();
                return;
            }
            m_Preview = m_Controller.Preview(m_SelectedSource.Value, target);
            SetText(m_FeedbackText, m_Preview.Accepted ? string.Empty : m_Preview.Reason);
            RenderSlots();
        }

        private void OnSlotDrop(BuqiDeploymentSlotRef target)
        {
            if (!m_IsDragging)
                return;
            m_DropHandled = true;
            MoveSelectedTo(target);
            ClearDragVisual();
            m_IsDragging = false;
        }

        private void Render()
        {
            SetText(m_TitleText, "拖拽上阵");
            RenderSlots();
            RenderItems();
            RenderDetails();
        }

        private void RenderSlots()
        {
            if (m_Controller == null)
                return;
            for (int index = 0; index < m_BoardSlots.Length; index++)
            {
                BuqiDeploymentSlotRef slot = BuqiDeploymentSlotRef.Board(index);
                BuqiDeploymentPlacement placement = FindPlacement(index);
                BuqiDeploySlotVisualState state = placement != null && placement.AnchorSlot != index
                    ? BuqiDeploySlotVisualState.Continuation
                    : BuqiDeploySlotVisualState.Normal;
                if (m_SelectedSource.HasValue && m_SelectedSource.Value == slot)
                    state = BuqiDeploySlotVisualState.Selected;
                if (m_Preview != null && Contains(m_Preview.BoardSlots, index))
                    state = m_Preview.Accepted ? BuqiDeploySlotVisualState.Legal : BuqiDeploySlotVisualState.Illegal;
                m_BoardSlots[index]?.Render(
                    slot,
                    placement == null ? string.Empty : ItemName(placement.ItemId),
                    state,
                    m_Preview != null && Contains(m_Preview.BoardSlots, index) ? m_Preview.Reason : string.Empty,
                    OnSlotClick,
                    OnSlotHover,
                    OnSlotDrop);
            }
            for (int index = 0; index < m_StorageSlots.Length; index++)
            {
                BuqiDeploymentSlotRef slot = BuqiDeploymentSlotRef.Storage(index);
                string itemId = m_Controller.View.StorageSlots[index];
                BuqiDeploySlotVisualState state = m_SelectedSource.HasValue && m_SelectedSource.Value == slot
                    ? BuqiDeploySlotVisualState.Selected
                    : BuqiDeploySlotVisualState.Normal;
                bool isPreviewTarget = m_Preview != null && m_Preview.Target == slot;
                if (isPreviewTarget)
                    state = m_Preview.Accepted ? BuqiDeploySlotVisualState.Legal : BuqiDeploySlotVisualState.Illegal;
                m_StorageSlots[index]?.Render(
                    slot,
                    string.IsNullOrEmpty(itemId) ? string.Empty : ItemName(itemId),
                    state,
                    isPreviewTarget ? m_Preview.Reason : string.Empty,
                    OnSlotClick,
                    OnSlotHover,
                    OnSlotDrop);
            }
        }

        private void RenderItems()
        {
            DestroyItemWidgets();
            if (m_ItemTemplate == null || m_Controller == null)
                return;
            foreach (BuqiDeploymentPlacement placement in m_Controller.View.Placements)
            {
                BuqiDraggableItemWidget widget = CreateItemWidget(
                    m_BoardItemLayer,
                    placement.ItemId,
                    BuqiDeploymentSlotRef.Board(placement.AnchorSlot));
                PositionBoardItem(widget, placement.AnchorSlot, placement.Span);
            }
            for (int index = 0; index < m_Controller.View.StorageSlots.Count; index++)
            {
                string itemId = m_Controller.View.StorageSlots[index];
                if (string.IsNullOrEmpty(itemId))
                    continue;
                BuqiDraggableItemWidget widget = CreateItemWidget(
                    m_StorageItemLayer,
                    itemId,
                    BuqiDeploymentSlotRef.Storage(index));
                PositionStorageItem(widget, index);
            }
        }

        private BuqiDraggableItemWidget CreateItemWidget(
            Transform parent,
            string itemId,
            BuqiDeploymentSlotRef source)
        {
            if (parent == null)
                parent = transform;
            BuqiDraggableItemWidget widget = Instantiate(m_ItemTemplate, parent);
            widget.gameObject.SetActive(true);
            widget.Render(
                m_Catalog.FindItem(itemId),
                source,
                OnItemClick,
                OnBeginDrag,
                OnDrag,
                OnEndDrag);
            m_ItemWidgets.Add(widget);
            return widget;
        }

        private void PositionBoardItem(BuqiDraggableItemWidget widget, int anchor, int span)
        {
            RectTransform rect = widget.transform as RectTransform;
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(
                -((BoardSlotWidth + SlotGap) * 8f - SlotGap) * 0.5f +
                    (BoardSlotWidth + SlotGap) * anchor + BoardSlotWidth * 0.5f,
                0f);
            rect.sizeDelta = new Vector2(BoardSlotWidth * span + SlotGap * (span - 1f), 104f);
        }

        private void PositionStorageItem(BuqiDraggableItemWidget widget, int index)
        {
            RectTransform rect = widget.transform as RectTransform;
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 190f - (StorageSlotHeight + StorageGap) * index);
            rect.sizeDelta = new Vector2(300f, StorageSlotHeight);
        }

        private void RenderDetails()
        {
            if (m_Controller == null || !m_SelectedSource.HasValue)
            {
                SetText(m_DetailText, "选择一件装备查看详情");
                return;
            }
            if (TryResolveItem(m_SelectedSource.Value, out BuqiDeploymentSlotRef source, out string error))
            {
                string itemId = source.Area == BuqiDeploymentArea.Storage
                    ? m_Controller.View.StorageSlots[source.Index]
                    : FindPlacement(source.Index).ItemId;
                BuqiUIDemoItemDefinition item = m_Catalog.FindItem(itemId);
                SetText(m_DetailText, GameFramework.Utility.Text.Format(
                    "{0}\n占用格数 {1}\n{2}", item.Name, item.Size, item.Description));
            }
            else
            {
                SetText(m_DetailText, error);
            }
        }

        private bool TryResolveItem(
            BuqiDeploymentSlotRef source,
            out BuqiDeploymentSlotRef resolved,
            out string error)
        {
            resolved = source;
            error = string.Empty;
            if (m_Controller == null)
            {
                error = "拖拽上阵数据不可用";
                return false;
            }
            if (source.Area == BuqiDeploymentArea.Storage)
            {
                if (source.Index < 0 || source.Index >= m_Controller.View.StorageSlots.Count ||
                    string.IsNullOrEmpty(m_Controller.View.StorageSlots[source.Index]))
                {
                    error = "来源位置没有装备";
                    return false;
                }
                return true;
            }
            BuqiDeploymentPlacement placement = FindPlacement(source.Index);
            if (placement == null)
            {
                error = "来源位置没有装备";
                return false;
            }
            resolved = BuqiDeploymentSlotRef.Board(placement.AnchorSlot);
            return true;
        }

        private BuqiDeploymentPlacement FindPlacement(int boardSlot)
        {
            foreach (BuqiDeploymentPlacement placement in m_Controller.View.Placements)
            {
                if (boardSlot >= placement.AnchorSlot && boardSlot < placement.AnchorSlot + placement.Span)
                    return placement;
            }
            return null;
        }

        private string ItemName(string itemId)
        {
            BuqiUIDemoItemDefinition item = m_Catalog.FindItem(itemId);
            return item == null ? string.Empty : item.Name;
        }

        private void CreateDragVisual(BuqiDeploymentSlotRef source)
        {
            ClearDragVisual();
            if (m_ItemTemplate == null || m_DragLayer == null || !TryResolveItem(source, out BuqiDeploymentSlotRef resolved, out _))
                return;
            string itemId = resolved.Area == BuqiDeploymentArea.Storage
                ? m_Controller.View.StorageSlots[resolved.Index]
                : FindPlacement(resolved.Index).ItemId;
            m_DragVisual = Instantiate(m_ItemTemplate, m_DragLayer);
            m_DragVisual.gameObject.SetActive(true);
            m_DragVisual.Render(m_Catalog.FindItem(itemId), resolved, null, null, null, null);
            CanvasGroup canvasGroup = m_DragVisual.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = m_DragVisual.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.85f;
        }

        private void UpdateDragVisual(PointerEventData eventData)
        {
            if (m_DragVisual == null || m_DragLayer == null)
                return;
            RectTransform layer = m_DragLayer as RectTransform;
            RectTransform rect = m_DragVisual.transform as RectTransform;
            if (layer == null || rect == null)
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    layer,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
                rect.anchoredPosition = localPoint;
        }

        private void ClearDragVisual()
        {
            if (m_DragVisual == null)
                return;
            Destroy(m_DragVisual.gameObject);
            m_DragVisual = null;
        }

        private void DestroyItemWidgets()
        {
            foreach (BuqiDraggableItemWidget widget in m_ItemWidgets)
            {
                if (widget != null)
                    Destroy(widget.gameObject);
            }
            m_ItemWidgets.Clear();
        }

        private void SetItemRaycasts(bool enabled)
        {
            foreach (BuqiDraggableItemWidget widget in m_ItemWidgets)
                widget?.SetRaycastEnabled(enabled);
        }

        private static List<string> ToIds(IReadOnlyList<BuqiDemoItemView> views)
        {
            var ids = new List<string>(views.Count);
            foreach (BuqiDemoItemView view in views)
                ids.Add(view == null || view.Empty ? string.Empty : view.Id ?? string.Empty);
            return ids;
        }

        private bool RejectInitialization(out string error, string message)
        {
            error = message;
            return false;
        }

        private BuqiDeploymentCommandResult Rejected(string reason)
        {
            return new BuqiDeploymentCommandResult(false, reason, View);
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] == value)
                    return true;
            }
            return false;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
