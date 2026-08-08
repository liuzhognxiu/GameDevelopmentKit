using System;
using System.Collections.Generic;
using Game.Hot.Buqi.UI;
using Game.Hot.Buqi.UI.Stages;
using Game.Hot.Buqi.UI.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Hot.Editor
{
    public static class BuqiFullUIBuilder
    {
        private const string StageFolder = "Assets/Res/UI/UIPrefab/Buqi/Stages";
        private const string WidgetFolder = "Assets/Res/UI/UIPrefab/Buqi";
        private const string FormFolder = "Assets/Res/UI/UIForm/Hot/Buqi";
        private const string ShellPath = FormFolder + "/BuqiRunShellForm.prefab";

        private static readonly Color canvasColor = new Color32(18, 23, 28, 255);
        private static readonly Color surfaceColor = new Color32(35, 43, 50, 255);
        private static readonly Color raisedColor = new Color32(51, 62, 70, 255);
        private static readonly Color inkColor = new Color32(239, 242, 238, 255);
        private static readonly Color mutedColor = new Color32(165, 176, 178, 255);
        private static readonly Color accentColor = new Color32(229, 176, 71, 255);
        private static readonly Color jadeColor = new Color32(51, 150, 128, 255);

        [MenuItem("游戏/不器/重建完整界面演示")]
        public static void BuildAll()
        {
            EnsureFolder(StageFolder);
            EnsureFolder(FormFolder);
            BuildStage<StarterSelectionWidget>("StarterSelectionWidget", "起始选择", "选择本局的第一件装备。");
            BuildStage<OpponentIntelWidget>("OpponentIntelWidget", "对手快照", "只展示公开的棋盘和构筑信息。");
            BuildStage<PreparationChoiceWidget>("PreparationChoiceWidget", "战前准备", "选择本回合的准备收益。");
            BuildStage<ShopWidget>("ShopWidget", "商店", "购买装备、刷新或锁定当前报价。");
            BuildStage<EventWidget>("EventWidget", "事件", "在收益与风险之间做出选择。");
            BuildStage<ModificationWidget>("ModificationWidget", "改造", "为装备添加收益与代价并存的改造。");
            BuildStage<BoardEditorWidget>("BoardEditorWidget", "棋盘编辑", "点选装备，再选择 8 格棋盘中的目标位。");
            BuildStage<PredictionWidget>("PredictionWidget", "胜负预测", "战斗前记录你对结果的判断。");
            BuildStage<BattleSummaryWidget>("BattleSummaryWidget", "战斗总结", "从真实战斗日志中提取可回溯事实。");
            BuildStage<RoundSettlementWidget>("RoundSettlementWidget", "回合结算", "结算胜场、单局生命与金币变化。");
            BuildStage<RunTerminalWidget>("RunTerminalWidget", "单局结束", "查看本局构筑摘要并重新开始。");
            BuildShell();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("不器完整界面演示已重建。");
        }

        private static void BuildStage<T>(string name, string titleValue, string bodyValue)
            where T : BuqiStageWidgetBase
        {
            GameObject root = CreateRoot(name, new Vector2(1112f, 824f));
            AddImage(root, new Color32(25, 31, 36, 255));
            T widget = root.AddComponent<T>();

            Text title = CreateText(root.transform, "Title_Text", titleValue, 32, TextAnchor.MiddleLeft, inkColor);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -46f), new Vector2(-80f, 52f));
            title.fontStyle = FontStyle.Bold;

            Text body = CreateText(root.transform, "Body_Text", bodyValue, 19, TextAnchor.UpperLeft, mutedColor);
            SetRect(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -118f), new Vector2(-80f, 76f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text meta = CreateText(root.transform, "Meta_Text", "演示", 15, TextAnchor.MiddleLeft, accentColor);
            SetRect(meta.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -176f), new Vector2(-80f, 32f));

            var buttons = new List<Button>(8);
            var labels = new List<Text>(8);
            for (int index = 0; index < 8; index++)
            {
                int row = index / 2;
                int column = index % 2;
                float x = column == 0 ? -266f : 266f;
                float y = 210f - row * 126f;
                Button button = CreateButton(root.transform, "Action" + (index + 1).ToString("00"), "--", new Vector2(x, y), new Vector2(500f, 104f), raisedColor, out Text label);
                label.fontSize = 18;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                button.gameObject.SetActive(false);
                buttons.Add(button);
                labels.Add(label);
            }

            Assign(widget, "m_TitleText", title);
            Assign(widget, "m_BodyText", body);
            Assign(widget, "m_MetaText", meta);
            AssignArray(widget, "m_ActionButtons", buttons);
            AssignArray(widget, "m_ActionLabels", labels);
            SavePrefab(root, StageFolder + "/" + name + ".prefab");
        }

        private static void BuildShell()
        {
            GameObject resourcePrefab = LoadPrefab(WidgetFolder + "/ResourceChipWidget.prefab");
            GameObject phasePrefab = LoadPrefab(WidgetFolder + "/PhaseStepWidget.prefab");
            string[] stageNames =
            {
                "StarterSelectionWidget", "OpponentIntelWidget", "PreparationChoiceWidget", "ShopWidget", "EventWidget", "ModificationWidget",
                "BoardEditorWidget", "PredictionWidget", "BattleSummaryWidget", "RoundSettlementWidget", "RunTerminalWidget",
            };

            GameObject root = CreateRoot("BuqiRunShellForm", new Vector2(1920f, 1080f));
            AddImage(root, canvasColor);
            BuqiRunShellForm form = root.AddComponent<BuqiRunShellForm>();

            GameObject header = CreatePanel(root.transform, "Header", new Vector2(0f, 472f), new Vector2(1856f, 72f), surfaceColor);
            Text title = CreateText(header.transform, "Title_Text", "不器  |  演示界面总览", 24, TextAnchor.MiddleLeft, inkColor);
            SetRect(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(210f, 0f), new Vector2(380f, 44f));
            title.fontStyle = FontStyle.Bold;

            var chips = new List<ResourceChipWidget>(4);
            for (int index = 0; index < 4; index++)
            {
                GameObject chip = Instantiate(resourcePrefab, header.transform, "ResourceChip" + (index + 1).ToString("00"));
                SetRect(chip.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f + index * 190f, 0f), new Vector2(176f, 54f));
                chips.Add(chip.GetComponent<ResourceChipWidget>());
            }

            GameObject phaseRail = CreatePanel(root.transform, "PhaseRail", new Vector2(-824f, 0f), new Vector2(208f, 824f), surfaceColor);
            var phaseSteps = new List<PhaseStepWidget>(12);
            for (int index = 0; index < 12; index++)
            {
                GameObject step = Instantiate(phasePrefab, phaseRail.transform, "PhaseStep" + (index + 1).ToString("00"));
                SetRect(step.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f - index * 66f), new Vector2(208f, 48f));
                phaseSteps.Add(step.GetComponent<PhaseStepWidget>());
            }

            GameObject stageHost = CreatePanel(root.transform, "StageHost", new Vector2(-140f, 0f), new Vector2(1112f, 824f), new Color32(25, 31, 36, 255));
            var stages = new List<MonoBehaviour>(stageNames.Length);
            foreach (string stageName in stageNames)
            {
                GameObject prefab = LoadPrefab(StageFolder + "/" + stageName + ".prefab");
                GameObject stage = Instantiate(prefab, stageHost.transform, stageName);
                SetRect(stage.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1112f, 824f));
                MonoBehaviour stageComponent = Array.Find(
                    stage.GetComponents<MonoBehaviour>(),
                    component => component is IBuqiStageWidget);
                if (stageComponent == null)
                    throw new InvalidOperationException("Stage prefab has no IBuqiStageWidget: " + stageName);
                stages.Add(stageComponent);
            }

            GameObject context = CreatePanel(root.transform, "ContextRail", new Vector2(720f, 0f), new Vector2(488f, 824f), surfaceColor);
            Text contextTitle = CreateText(context.transform, "ContextTitle_Text", "当前阶段", 24, TextAnchor.MiddleLeft, inkColor);
            SetRect(contextTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -46f), new Vector2(-56f, 44f));
            contextTitle.fontStyle = FontStyle.Bold;
            Text contextBody = CreateText(context.transform, "ContextBody_Text", "--", 18, TextAnchor.UpperLeft, mutedColor);
            SetRect(contextBody.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -140f), new Vector2(-56f, 128f));
            contextBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text status = CreateText(context.transform, "Status_Text", string.Empty, 17, TextAnchor.UpperLeft, new Color32(242, 165, 150, 255));
            SetRect(status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 120f), new Vector2(-56f, 120f));
            status.horizontalOverflow = HorizontalWrapMode.Wrap;

            GameObject commands = CreatePanel(root.transform, "CommandBar", new Vector2(0f, -472f), new Vector2(1856f, 88f), surfaceColor);
            Button back = CreateButton(commands.transform, "Back", "<", new Vector2(-850f, 0f), new Vector2(64f, 52f), raisedColor, out _);
            Button restart = CreateButton(commands.transform, "Restart", "重启", new Vector2(730f, 0f), new Vector2(64f, 52f), raisedColor, out _);
            Button primary = CreateButton(commands.transform, "Primary", "继续", new Vector2(840f, 0f), new Vector2(132f, 52f), jadeColor, out Text primaryLabel);

            GameObject errorPanel = CreatePanel(root.transform, "ErrorPanel", Vector2.zero, new Vector2(760f, 240f), new Color32(88, 39, 39, 250));
            Text errorText = CreateText(errorPanel.transform, "Error_Text", "--", 20, TextAnchor.MiddleCenter, inkColor);
            Stretch(errorText.rectTransform, new Vector2(30f, 24f), new Vector2(-30f, -24f));
            errorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            errorPanel.SetActive(false);

            Assign(form, "m_TitleText", title);
            Assign(form, "m_ContextTitleText", contextTitle);
            Assign(form, "m_ContextBodyText", contextBody);
            Assign(form, "m_StatusText", status);
            Assign(form, "m_PrimaryLabel", primaryLabel);
            AssignArray(form, "m_ResourceChips", chips);
            AssignArray(form, "m_PhaseSteps", phaseSteps);
            AssignArray(form, "m_StageComponents", stages);
            Assign(form, "m_BackButton", back);
            Assign(form, "m_PrimaryButton", primary);
            Assign(form, "m_RestartButton", restart);
            Assign(form, "m_ErrorPanel", errorPanel);
            Assign(form, "m_ErrorText", errorText);
            SavePrefab(root, ShellPath);
        }

        private static GameObject LoadPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("缺少不器界面预制体：" + path);
            return prefab;
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent, string name)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color, out Text labelText)
        {
            GameObject buttonObject = CreatePanel(parent, name, position, size, color);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            labelText = CreateText(buttonObject.transform, "Label", label, 18, TextAnchor.MiddleCenter, inkColor);
            Stretch(labelText.rectTransform, new Vector2(6f, 3f), new Vector2(-6f, -3f));
            return button;
        }

        private static GameObject CreateRoot(string name, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            root.layer = GetUILayer();
            root.GetComponent<RectTransform>().sizeDelta = size;
            return root;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject panel = CreateRoot(name, size);
            panel.transform.SetParent(parent, false);
            panel.GetComponent<RectTransform>().anchoredPosition = position;
            AddImage(panel, color);
            return panel;
        }

        private static Image AddImage(GameObject gameObject, Color color)
        {
            Image image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = image.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Color color)
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

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Assign(Object target, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new MissingReferenceException(target.GetType().Name + " 缺少序列化属性 " + propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray<T>(Object target, string propertyName, IReadOnlyList<T> values) where T : Object
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new MissingReferenceException(target.GetType().Name + " 缺少序列化数组 " + propertyName);
            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
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

        private static int GetUILayer()
        {
            int layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : 5;
        }
    }
}
