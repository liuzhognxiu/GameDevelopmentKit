using System;
using System.Collections.Generic;
using Game.Hot.Buqi.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Hot.Editor
{
    public static class BuqiBattleUIBuilder
    {
        private const string WidgetFolder = "Assets/Res/UI/UIPrefab/Buqi";
        private const string FormFolder = "Assets/Res/UI/UIForm/Hot/Buqi";
        private const string ItemCardPath = WidgetFolder + "/ItemCardWidget.prefab";
        private const string BattleLogPath = WidgetFolder + "/BattleLogWidget.prefab";
        private const string BattleFormPath = FormFolder + "/BattleForm.prefab";

        private static readonly Color canvasColor = new Color32(19, 24, 29, 255);
        private static readonly Color surfaceColor = new Color32(35, 43, 50, 255);
        private static readonly Color raisedColor = new Color32(51, 62, 70, 255);
        private static readonly Color inkColor = new Color32(239, 242, 238, 255);
        private static readonly Color mutedColor = new Color32(165, 176, 178, 255);
        private static readonly Color accentColor = new Color32(229, 176, 71, 255);
        private static readonly Color jadeColor = new Color32(51, 150, 128, 255);
        private static readonly Color dangerColor = new Color32(207, 76, 71, 255);

        [MenuItem("游戏/不器/重建战斗界面演示")]
        public static void BuildAll()
        {
            EnsureFolder(WidgetFolder);
            EnsureFolder(FormFolder);
            BuildItemCard();
            BuildBattleLog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildBattleForm();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("不器战斗界面演示已重建。");
        }

        private static void BuildItemCard()
        {
            GameObject root = CreateRoot("ItemCardWidget", new Vector2(150f, 164f));
            Image background = AddImage(root, surfaceColor);
            ItemCardWidget widget = root.AddComponent<ItemCardWidget>();

            Text name = CreateText(root.transform, "Name_Text", "W8-000", 19, TextAnchor.UpperLeft, inkColor);
            SetRect(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(126f, 28f));
            name.fontStyle = FontStyle.Bold;

            Text effect = CreateText(root.transform, "Effect_Text", "伤害", 16, TextAnchor.MiddleLeft, accentColor);
            SetRect(effect.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -59f), new Vector2(126f, 28f));

            Text status = CreateText(root.transform, "Status_Text", "1格  充能 0  冻结 0", 14, TextAnchor.MiddleLeft, mutedColor);
            SetRect(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(126f, 25f));

            Image cooldownTrack = CreateImage(root.transform, "CooldownTrack", raisedColor);
            SetRect(cooldownTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(126f, 8f));
            Image cooldownFill = CreateImage(cooldownTrack.transform, "CooldownFill_Image", jadeColor);
            Stretch(cooldownFill.rectTransform, Vector2.zero, Vector2.zero);
            cooldownFill.type = Image.Type.Filled;
            cooldownFill.fillMethod = Image.FillMethod.Horizontal;
            cooldownFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            cooldownFill.fillAmount = 0f;

            GameObject frozenMarker = CreatePanel(
                root.transform,
                "FrozenMarker",
                new Vector2(57f, 64f),
                new Vector2(32f, 32f),
                dangerColor);
            Text frozen = CreateText(frozenMarker.transform, "Label", "冻", 18, TextAnchor.MiddleCenter, inkColor);
            Stretch(frozen.rectTransform, Vector2.zero, Vector2.zero);
            frozenMarker.transform.SetAsLastSibling();
            frozenMarker.SetActive(false);

            Assign(widget, "m_Background", background);
            Assign(widget, "m_CooldownFill", cooldownFill);
            Assign(widget, "m_NameText", name);
            Assign(widget, "m_EffectText", effect);
            Assign(widget, "m_StatusText", status);
            Assign(widget, "m_FrozenMarker", frozenMarker);
            SavePrefab(root, ItemCardPath);
        }

        private static void BuildBattleLog()
        {
            GameObject root = CreateRoot("BattleLogWidget", new Vector2(382f, 31f));
            AddImage(root, new Color32(28, 35, 40, 235));
            BattleLogWidget widget = root.AddComponent<BattleLogWidget>();

            Image marker = CreateImage(root.transform, "Marker_Image", accentColor);
            SetRect(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, 23f));
            Text content = CreateText(root.transform, "Content_Text", "Buqi.Log.Placeholder", 14, TextAnchor.MiddleLeft, inkColor);
            Stretch(content.rectTransform, new Vector2(18f, 1f), new Vector2(-8f, -1f));

            Assign(widget, "m_Marker", marker);
            Assign(widget, "m_ContentText", content);
            SavePrefab(root, BattleLogPath);
        }

        private static void BuildBattleForm()
        {
            GameObject itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemCardPath);
            GameObject logPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleLogPath);
            if (itemPrefab == null || logPrefab == null)
                throw new InvalidOperationException("Buqi widget prefabs are missing.");

            GameObject root = CreateRoot("BattleForm", new Vector2(1920f, 1080f));
            Image canvas = AddImage(root, canvasColor);
            canvas.raycastTarget = true;
            BattleForm form = root.AddComponent<BattleForm>();

            GameObject header = CreatePanel(root.transform, "Header", new Vector2(0f, 488f), new Vector2(1920f, 104f), surfaceColor);
            Text title = CreateText(header.transform, "Title_Text", "不器 · 战斗回放", 30, TextAnchor.MiddleCenter, inkColor);
            Stretch(title.rectTransform, new Vector2(180f, 12f), new Vector2(-180f, -12f));
            title.fontStyle = FontStyle.Bold;

            Text tick = CreateText(header.transform, "Tick_Text", "Buqi.Battle.TickPlaceholder", 18, TextAnchor.MiddleRight, mutedColor);
            SetRect(tick.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(260f, 42f));
            Text outcome = CreateText(header.transform, "Outcome_Text", "战斗推演中", 18, TextAnchor.MiddleLeft, accentColor);
            SetRect(outcome.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(260f, 42f));

            GameObject arena = CreatePanel(root.transform, "Arena", new Vector2(-220f, 8f), new Vector2(1430f, 820f), new Color32(24, 31, 36, 255));
            Text leftName = CreateText(arena.transform, "LeftName_Text", "左侧构筑", 23, TextAnchor.MiddleLeft, inkColor);
            SetRect(leftName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(180f, -42f), new Vector2(330f, 38f));
            Text leftStats = CreateText(arena.transform, "LeftStats_Text", "生命值 --  护盾 --  过载 --", 17, TextAnchor.MiddleRight, mutedColor);
            SetRect(leftStats.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-330f, -42f), new Vector2(620f, 38f));

            Text rightName = CreateText(arena.transform, "RightName_Text", "右侧构筑", 23, TextAnchor.MiddleLeft, inkColor);
            SetRect(rightName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(180f, -55f), new Vector2(330f, 38f));
            Text rightStats = CreateText(arena.transform, "RightStats_Text", "生命值 --  护盾 --  过载 --", 17, TextAnchor.MiddleRight, mutedColor);
            SetRect(rightStats.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-330f, -55f), new Vector2(620f, 38f));

            var leftCards = new List<ItemCardWidget>(8);
            var rightCards = new List<ItemCardWidget>(8);
            for (int slot = 0; slot < 8; slot++)
            {
                float x = -567f + slot * 162f;
                leftCards.Add(AddItemCard(itemPrefab, arena.transform, string.Format("Slot{0:00}_Left", slot + 1), new Vector2(x, 235f)));
                rightCards.Add(AddItemCard(itemPrefab, arena.transform, string.Format("Slot{0:00}_Right", slot + 1), new Vector2(x, -180f)));
            }

            Text currentEvent = CreateText(arena.transform, "CurrentEvent_Text", "尚无事件", 20, TextAnchor.MiddleCenter, accentColor);
            SetRect(currentEvent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(980f, 42f));

            Image timelineTrack = CreateImage(arena.transform, "TimelineTrack", raisedColor);
            SetRect(timelineTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1288f, 14f));
            Image timelineFill = CreateImage(timelineTrack.transform, "TimelineFill_Image", jadeColor);
            Stretch(timelineFill.rectTransform, Vector2.zero, Vector2.zero);
            timelineFill.type = Image.Type.Filled;
            timelineFill.fillMethod = Image.FillMethod.Horizontal;
            timelineFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            timelineFill.fillAmount = 0f;

            GameObject evidence = CreatePanel(root.transform, "Evidence", new Vector2(745f, 8f), new Vector2(430f, 820f), surfaceColor);
            Text evidenceTitle = CreateText(evidence.transform, "EvidenceTitle", "战斗证据", 23, TextAnchor.MiddleLeft, inkColor);
            SetRect(evidenceTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(122f, -31f), new Vector2(220f, 38f));

            var logRows = new List<BattleLogWidget>(12);
            for (int row = 0; row < 12; row++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(logPrefab);
                instance.name = string.Format("Log{0:00}", row + 1);
                instance.transform.SetParent(evidence.transform, false);
                SetRect(instance.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f - row * 33f), new Vector2(394f, 31f));
                logRows.Add(instance.GetComponent<BattleLogWidget>());
            }

            Text page = CreateText(evidence.transform, "Page_Text", "实时", 15, TextAnchor.MiddleCenter, mutedColor);
            SetRect(page.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 286f), new Vector2(100f, 32f));
            Button previousPage = CreateButton(evidence.transform, "PreviousPage", "<", new Vector2(-82f, 286f), new Vector2(48f, 34f), raisedColor, out _);
            Button nextPage = CreateButton(evidence.transform, "NextPage", ">", new Vector2(82f, 286f), new Vector2(48f, 34f), raisedColor, out _);

            Text factsTitle = CreateText(evidence.transform, "FactsTitle", "终局事实", 19, TextAnchor.MiddleLeft, accentColor);
            SetRect(factsTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 238f), new Vector2(160f, 32f));
            var factTexts = new List<Text>(3);
            for (int fact = 0; fact < 3; fact++)
            {
                Text factText = CreateText(evidence.transform, string.Format("Fact{0}_Text", fact + 1), "--", 14, TextAnchor.UpperLeft, inkColor);
                SetRect(factText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f - fact * 60f), new Vector2(394f, 52f));
                factText.horizontalOverflow = HorizontalWrapMode.Wrap;
                factText.verticalOverflow = VerticalWrapMode.Truncate;
                factTexts.Add(factText);
            }

            GameObject controls = CreatePanel(root.transform, "Controls", new Vector2(-220f, -476f), new Vector2(1430f, 84f), surfaceColor);
            Button back = CreateButton(controls.transform, "Back", "<", new Vector2(-650f, 0f), new Vector2(62f, 52f), raisedColor, out _);
            Button playPause = CreateButton(controls.transform, "PlayPause", "暂停", new Vector2(-490f, 0f), new Vector2(124f, 52f), jadeColor, out Text playPauseText);
            Button speed1 = CreateButton(controls.transform, "Speed1", "1x", new Vector2(-335f, 0f), new Vector2(82f, 52f), raisedColor, out _);
            Button speed2 = CreateButton(controls.transform, "Speed2", "2x", new Vector2(-239f, 0f), new Vector2(82f, 52f), raisedColor, out _);
            Button speed4 = CreateButton(controls.transform, "Speed4", "4x", new Vector2(-143f, 0f), new Vector2(82f, 52f), raisedColor, out _);
            Button skip = CreateButton(controls.transform, "Skip", ">>", new Vector2(14f, 0f), new Vector2(104f, 52f), accentColor, out _);
            Button replay = CreateButton(controls.transform, "Replay", "重播", new Vector2(138f, 0f), new Vector2(82f, 52f), raisedColor, out _);

            GameObject errorPanel = CreatePanel(root.transform, "ErrorPanel", Vector2.zero, new Vector2(760f, 250f), new Color32(66, 31, 31, 248));
            Text errorText = CreateText(errorPanel.transform, "Error_Text", "战斗回放错误", 22, TextAnchor.MiddleCenter, inkColor);
            Stretch(errorText.rectTransform, new Vector2(36f, 28f), new Vector2(-36f, -28f));
            errorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            errorPanel.SetActive(false);

            Assign(form, "m_TitleText", title);
            Assign(form, "m_LeftNameText", leftName);
            Assign(form, "m_RightNameText", rightName);
            Assign(form, "m_LeftStatsText", leftStats);
            Assign(form, "m_RightStatsText", rightStats);
            Assign(form, "m_TickText", tick);
            Assign(form, "m_CurrentEventText", currentEvent);
            Assign(form, "m_OutcomeText", outcome);
            Assign(form, "m_PageText", page);
            Assign(form, "m_PlayPauseText", playPauseText);
            AssignArray(form, "m_FactTexts", factTexts);
            AssignArray(form, "m_LeftCards", leftCards);
            AssignArray(form, "m_RightCards", rightCards);
            AssignArray(form, "m_LogRows", logRows);
            Assign(form, "m_TimelineFill", timelineFill);
            Assign(form, "m_ErrorPanel", errorPanel);
            Assign(form, "m_ErrorText", errorText);
            Assign(form, "m_BackButton", back);
            Assign(form, "m_PlayPauseButton", playPause);
            Assign(form, "m_Speed1Button", speed1);
            Assign(form, "m_Speed2Button", speed2);
            Assign(form, "m_Speed4Button", speed4);
            Assign(form, "m_SkipButton", skip);
            Assign(form, "m_ReplayButton", replay);
            Assign(form, "m_PreviousPageButton", previousPage);
            Assign(form, "m_NextPageButton", nextPage);
            SavePrefab(root, BattleFormPath);
        }

        private static ItemCardWidget AddItemCard(GameObject prefab, Transform parent, string name, Vector2 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            SetRect(instance.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(150f, 164f));
            return instance.GetComponent<ItemCardWidget>();
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
            labelText = CreateText(buttonObject.transform, "Label", label, 19, TextAnchor.MiddleCenter, inkColor);
            Stretch(labelText.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            return button;
        }

        private static GameObject CreateRoot(string name, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
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
            Image image = gameObject.GetComponent<Image>();
            if (image == null)
                image = gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = image.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = CreateRoot(name, Vector2.zero);
            imageObject.transform.SetParent(parent, false);
            return AddImage(imageObject, color);
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
                throw new MissingReferenceException(string.Format("{1} 缺少序列化属性 {0}。", propertyName, target.GetType().Name));
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray<T>(Object target, string propertyName, IReadOnlyList<T> values)
            where T : Object
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new MissingReferenceException(string.Format("{1} 缺少序列化数组 {0}。", propertyName, target.GetType().Name));
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
                string next = string.Format("{0}/{1}", current, parts[index]);
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
