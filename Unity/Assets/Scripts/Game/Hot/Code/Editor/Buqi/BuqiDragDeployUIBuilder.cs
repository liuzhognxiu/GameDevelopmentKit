using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.UI;
using Game.Hot.Buqi.UI.Widgets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Hot.Editor
{
    public static class BuqiDragDeployUIBuilder
    {
        private const string WidgetFolder = "Assets/Res/UI/UIPrefab/Buqi";
        private const string FormFolder = "Assets/Res/UI/UIForm/Hot/Buqi";
        private const string ItemPath = WidgetFolder + "/BuqiDraggableItemWidget.prefab";
        private const string SlotPath = WidgetFolder + "/BuqiDeploySlotWidget.prefab";
        private const string FormPath = FormFolder + "/BuqiDragDeployForm.prefab";

        private static readonly Color canvasColor = new Color32(18, 23, 28, 255);
        private static readonly Color surfaceColor = new Color32(34, 42, 49, 255);
        private static readonly Color raisedColor = new Color32(53, 63, 71, 255);
        private static readonly Color inkColor = new Color32(239, 242, 238, 255);
        private static readonly Color mutedColor = new Color32(165, 176, 178, 255);
        private static readonly Color accentColor = new Color32(229, 176, 71, 255);
        private static readonly Color jadeColor = new Color32(51, 150, 128, 255);
        private static readonly Color dangerColor = new Color32(174, 67, 67, 255);

        [MenuItem("Game/Buqi/Rebuild Drag Deploy UI")]
        public static void BuildAll()
        {
            EnsureFolder(WidgetFolder);
            EnsureFolder(FormFolder);
            BuildItemWidget();
            BuildSlotWidget();
            BuildForm();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Buqi drag deploy UI rebuilt.");
        }

        [MenuItem("Game/Buqi/Open Drag Deploy UI Prefab")]
        public static void OpenFormPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FormPath);
            if (prefab == null)
                throw new MissingReferenceException("Drag deploy form prefab has not been generated.");
            Selection.activeObject = prefab;
            if (!AssetDatabase.OpenAsset(prefab))
                throw new UnityException("Could not open drag deploy form prefab.");
            EditorApplication.delayCall += FocusOpenedPrefab;
        }

        [MenuItem("Game/Buqi/Open Drag Deploy UI Demo")]
        public static void OpenRuntimeDemo()
        {
            if (!EditorApplication.isPlaying || GameEntry.UI == null)
            {
                Debug.LogWarning("Start the Buqi demo in Play Mode before opening the drag deploy UI demo.");
                return;
            }

            var catalog = new BuqiUIDemoCatalog();
            catalog.Items.Add(new BuqiUIDemoItemDefinition
            {
                Id = "demo-short",
                Name = "引气刃",
                Description = "单格试作道具",
                Size = 1,
            });
            catalog.Items.Add(new BuqiUIDemoItemDefinition
            {
                Id = "demo-long",
                Name = "两仪炉",
                Description = "双格试作道具",
                Size = 2,
            });
            List<BuqiDemoItemView> board = EmptySlots(BuqiDragDeployController.BoardSlotCount);
            List<BuqiDemoItemView> storage = EmptySlots(BuqiDragDeployController.StorageSlotCount);
            storage[0] = ItemView(catalog.Items[0], 0);
            storage[1] = ItemView(catalog.Items[1], 1);

            GameEntry.UI.OpenUIForm(UIFormId.BuqiDragDeployForm, new BuqiDragDeployOpenData
            {
                Catalog = catalog,
                Board = board,
                Storage = storage,
                Round = 3,
                Coins = 12,
                Wins = 4,
                Lives = 2,
                OpponentName = "清虚真人",
                Confirmed = snapshot => Debug.Log(
                    $"Drag deploy demo confirmed: {snapshot.Placements.Count} deployed item(s)."),
            });
        }

        [MenuItem("Game/Buqi/Open Drag Deploy UI Demo", true)]
        private static bool CanOpenRuntimeDemo()
        {
            return EditorApplication.isPlaying && GameEntry.UI != null;
        }

        private static void FocusOpenedPrefab()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (stage == null || sceneView == null)
                return;
            Selection.activeGameObject = stage.prefabContentsRoot;
            sceneView.in2DMode = true;
            SceneView.FrameLastActiveSceneView();
            sceneView.Repaint();
        }

        private static void BuildItemWidget()
        {
            GameObject root = CreateRoot("BuqiDraggableItemWidget", new Vector2(300f, 92f));
            Image background = AddImage(root, raisedColor);
            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            BuqiDraggableItemWidget widget = root.AddComponent<BuqiDraggableItemWidget>();

            Text name = CreateText(root.transform, "Name_Text", "W8-000", 18, TextAnchor.MiddleLeft, inkColor);
            SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -24f), new Vector2(-96f, 28f));
            name.fontStyle = FontStyle.Bold;

            Text size = CreateText(root.transform, "Size_Text", "占用 1 格", 14, TextAnchor.MiddleLeft, accentColor);
            SetRect(size.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(16f, 19f), new Vector2(-22f, 24f));

            Text source = CreateText(root.transform, "Source_Text", "仓库 01", 14, TextAnchor.MiddleRight, mutedColor);
            SetRect(source.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 19f), new Vector2(-22f, 24f));

            Assign(widget, "m_CanvasGroup", canvasGroup);
            Assign(widget, "m_Background", background);
            Assign(widget, "m_NameText", name);
            Assign(widget, "m_SizeText", size);
            Assign(widget, "m_SourceText", source);
            SavePrefab(root, ItemPath);
        }

        private static void BuildSlotWidget()
        {
            GameObject root = CreateRoot("BuqiDeploySlotWidget", new Vector2(108f, 104f));
            Image background = AddImage(root, raisedColor);
            BuqiDeploySlotWidget widget = root.AddComponent<BuqiDeploySlotWidget>();

            Text index = CreateText(root.transform, "Index_Text", "01", 14, TextAnchor.UpperLeft, mutedColor);
            Stretch(index.rectTransform, new Vector2(8f, 6f), new Vector2(-8f, -6f));

            Text item = CreateText(root.transform, "Item_Text", "空位", 15, TextAnchor.MiddleCenter, inkColor);
            SetRect(item.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-12f, -36f));

            Text state = CreateText(root.transform, "State_Text", "空位", 12, TextAnchor.LowerCenter, mutedColor);
            Stretch(state.rectTransform, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            Text invalid = CreateText(root.transform, "InvalidSymbol", "×", 32, TextAnchor.UpperRight, dangerColor);
            Stretch(invalid.rectTransform, new Vector2(6f, 4f), new Vector2(-7f, -4f));
            invalid.gameObject.SetActive(false);

            Assign(widget, "m_Background", background);
            Assign(widget, "m_IndexText", index);
            Assign(widget, "m_ItemText", item);
            Assign(widget, "m_StateText", state);
            Assign(widget, "m_InvalidSymbol", invalid.gameObject);
            SavePrefab(root, SlotPath);
        }

        private static void BuildForm()
        {
            GameObject itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath);
            GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);
            if (itemPrefab == null || slotPrefab == null)
                throw new MissingReferenceException("Drag deploy widget prefabs were not generated.");

            GameObject root = CreateRoot("BuqiDragDeployForm", new Vector2(1920f, 1080f));
            AddImage(root, canvasColor);
            BuqiDragDeployForm form = root.AddComponent<BuqiDragDeployForm>();

            GameObject header = CreatePanel(root.transform, "Header", new Vector2(0f, 472f), new Vector2(1856f, 72f), surfaceColor);
            Text title = CreateText(header.transform, "Title_Text", "拖拽上阵", 26, TextAnchor.MiddleLeft, inkColor);
            Stretch(title.rectTransform, new Vector2(24f, 8f), new Vector2(-1510f, -8f));
            title.fontStyle = FontStyle.Bold;
            Text context = CreateText(
                header.transform,
                "Context_Text",
                "第 3 回合  |  金币 12  |  胜场 4  |  生命 2  |  对手 清虚真人",
                16,
                TextAnchor.MiddleCenter,
                mutedColor);
            Stretch(context.rectTransform, new Vector2(350f, 8f), new Vector2(-260f, -8f));
            Text headerState = CreateText(header.transform, "State_Text", "阵容编辑", 16, TextAnchor.MiddleRight, accentColor);
            Stretch(headerState.rectTransform, new Vector2(1620f, 8f), new Vector2(-24f, -8f));

            GameObject storagePanel = CreatePanel(root.transform, "StoragePanel", new Vector2(-758f, 0f), new Vector2(320f, 824f), surfaceColor);
            Text storageTitle = CreateText(storagePanel.transform, "StorageTitle_Text", "待上阵道具", 20, TextAnchor.MiddleLeft, inkColor);
            SetRect(storageTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -32f), new Vector2(-32f, 40f));
            Transform storageItemLayer = CreateLayer(storagePanel.transform, "StorageItemLayer", Vector2.zero, new Vector2(300f, 508f));
            var storageSlots = new List<BuqiDeploySlotWidget>();
            for (int index = 0; index < 5; index++)
            {
                GameObject slot = InstantiatePrefab(slotPrefab, storagePanel.transform, "StorageSlot_" + (index + 1).ToString("00"));
                SetRect(slot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 190f - 104f * index), new Vector2(300f, 92f));
                storageSlots.Add(slot.GetComponent<BuqiDeploySlotWidget>());
            }
            storageItemLayer.SetAsLastSibling();

            GameObject boardPanel = CreatePanel(root.transform, "BoardPanel", new Vector2(-72f, 0f), new Vector2(1020f, 824f), new Color32(25, 31, 36, 255));
            Text boardTitle = CreateText(boardPanel.transform, "BoardTitle_Text", "不器阵列", 20, TextAnchor.MiddleLeft, inkColor);
            SetRect(boardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -32f), new Vector2(-48f, 40f));
            Text boardHint = CreateText(boardPanel.transform, "BoardHint_Text", "01  02  03  04  05  06  07  08", 14, TextAnchor.MiddleCenter, mutedColor);
            SetRect(boardHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(920f, 28f));
            Transform boardItemLayer = CreateLayer(boardPanel.transform, "BoardItemLayer", new Vector2(0f, 80f), new Vector2(920f, 104f));
            var boardSlots = new List<BuqiDeploySlotWidget>();
            for (int index = 0; index < 8; index++)
            {
                GameObject slot = InstantiatePrefab(slotPrefab, boardPanel.transform, "BoardSlot_" + (index + 1).ToString("00"));
                float x = -460f + 58f + 116f * index;
                SetRect(slot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 80f), new Vector2(108f, 104f));
                boardSlots.Add(slot.GetComponent<BuqiDeploySlotWidget>());
            }
            boardItemLayer.SetAsLastSibling();

            Text flow = CreateText(boardPanel.transform, "Flow_Text",
                "仓库  >  阵列  |  拖动调整位置  |  拖回仓库撤下",
                16, TextAnchor.MiddleCenter, accentColor);
            SetRect(flow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(920f, 44f));

            GameObject detailPanel = CreatePanel(root.transform, "DetailPanel", new Vector2(698f, 0f), new Vector2(480f, 824f), surfaceColor);
            Text detailTitle = CreateText(detailPanel.transform, "DetailTitle_Text", "道具详情", 20, TextAnchor.MiddleLeft, inkColor);
            SetRect(detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -32f), new Vector2(-40f, 40f));
            Text detail = CreateText(detailPanel.transform, "Detail_Text", "选择一件装备查看详情", 17, TextAnchor.UpperLeft, mutedColor);
            SetRect(detail.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -202f), new Vector2(-40f, 260f));
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text validationTitle = CreateText(detailPanel.transform, "ValidationTitle_Text", "放置校验", 18, TextAnchor.MiddleLeft, inkColor);
            SetRect(validationTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20f, 56f), new Vector2(-40f, 36f));
            Text feedback = CreateText(detailPanel.transform, "Feedback_Text", string.Empty, 16, TextAnchor.UpperLeft, dangerColor);
            SetRect(feedback.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20f, -22f), new Vector2(-40f, 112f));
            feedback.horizontalOverflow = HorizontalWrapMode.Wrap;

            GameObject commandBar = CreatePanel(root.transform, "CommandBar", new Vector2(0f, -472f), new Vector2(1856f, 88f), surfaceColor);
            Button reset = CreateButton(commandBar.transform, "Reset", "重置", new Vector2(590f, 0f), new Vector2(120f, 52f), raisedColor, out _);
            Button cancel = CreateButton(commandBar.transform, "Cancel", "取消", new Vector2(730f, 0f), new Vector2(120f, 52f), raisedColor, out _);
            Button confirm = CreateButton(commandBar.transform, "Confirm", "确认上阵", new Vector2(864f, 0f), new Vector2(132f, 52f), jadeColor, out _);
            Text commandStatus = CreateText(commandBar.transform, "CommandStatus_Text", "阵列变更仅在确认后生效", 15, TextAnchor.MiddleLeft, mutedColor);
            Stretch(commandStatus.rectTransform, new Vector2(24f, 8f), new Vector2(-460f, -8f));

            Transform dragLayer = CreateLayer(root.transform, "DragLayer", Vector2.zero, new Vector2(1920f, 1080f));
            GameObject itemTemplateObject = InstantiatePrefab(itemPrefab, root.transform, "ItemTemplate");
            itemTemplateObject.SetActive(false);
            RectTransform itemTemplateRect = itemTemplateObject.GetComponent<RectTransform>();
            SetRect(itemTemplateRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 92f));
            itemTemplateObject.transform.SetAsLastSibling();
            dragLayer.SetAsLastSibling();

            Assign(form, "m_TitleText", title);
            Assign(form, "m_ContextText", context);
            Assign(form, "m_DetailText", detail);
            Assign(form, "m_FeedbackText", feedback);
            AssignArray(form, "m_BoardSlots", boardSlots);
            AssignArray(form, "m_StorageSlots", storageSlots);
            Assign(form, "m_ItemTemplate", itemTemplateObject.GetComponent<BuqiDraggableItemWidget>());
            Assign(form, "m_BoardItemLayer", boardItemLayer);
            Assign(form, "m_StorageItemLayer", storageItemLayer);
            Assign(form, "m_DragLayer", dragLayer);
            Assign(form, "m_ResetButton", reset);
            Assign(form, "m_CancelButton", cancel);
            Assign(form, "m_ConfirmButton", confirm);
            SavePrefab(root, FormPath);
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, string name)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static List<BuqiDemoItemView> EmptySlots(int count)
        {
            var result = new List<BuqiDemoItemView>(count);
            for (int index = 0; index < count; index++)
                result.Add(new BuqiDemoItemView { Empty = true, Slot = index });
            return result;
        }

        private static BuqiDemoItemView ItemView(BuqiUIDemoItemDefinition item, int slot)
        {
            return new BuqiDemoItemView
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Size = item.Size,
                Slot = slot,
            };
        }

        private static Transform CreateLayer(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject layer = CreateRoot(name, size);
            layer.transform.SetParent(parent, false);
            SetRect(layer.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            return layer.transform;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color color,
            out Text labelText)
        {
            GameObject buttonObject = CreatePanel(parent, name, position, size, color);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            labelText = CreateText(buttonObject.transform, "Label", label, 18, TextAnchor.MiddleCenter, inkColor);
            Stretch(labelText.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            return button;
        }

        private static GameObject CreateRoot(string name, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            root.layer = LayerMask.NameToLayer("UI");
            root.GetComponent<RectTransform>().sizeDelta = size;
            return root;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject panel = CreateRoot(name, size);
            panel.transform.SetParent(parent, false);
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            AddImage(panel, color);
            return panel;
        }

        private static Image AddImage(GameObject gameObject, Color color)
        {
            Image image = gameObject.GetComponent<Image>();
            if (image == null)
                image = gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = image.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject textObject = CreateRoot(name, Vector2.zero);
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            Vector2 size)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }

        private static void Stretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static void Assign(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new MissingReferenceException(string.Format("{0} is missing serialized property {1}.", target.GetType().Name, propertyName));
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray<T>(Object target, string propertyName, IReadOnlyList<T> values) where T : Object
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
                throw new MissingReferenceException(string.Format("{0} is missing serialized array {1}.", target.GetType().Name, propertyName));
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SavePrefab(GameObject root, string assetPath)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SetLabels(prefab, new[] { "All", "Pack" });
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
