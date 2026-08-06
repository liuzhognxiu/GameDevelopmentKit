using System;
using System.Collections.Generic;
using Game.Hot.Buqi.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Hot.Editor
{
    public static class BuqiBuildWidgetBuilder
    {
        private const string OutputFolder = "Assets/Res/UI/UIPrefab/Buqi";
        private const string BoardSlotPath = OutputFolder + "/BoardSlotWidget.prefab";
        private const string ChoiceCardPath = OutputFolder + "/ChoiceCardWidget.prefab";
        private const string OfferCardPath = OutputFolder + "/OfferCardWidget.prefab";

        private static readonly Color surfaceColor = new Color32(36, 43, 51, 255);
        private static readonly Color raisedColor = new Color32(57, 67, 78, 255);
        private static readonly Color primaryColor = new Color32(27, 155, 142, 255);
        private static readonly Color accentColor = new Color32(245, 184, 72, 255);
        private static readonly Color dangerColor = new Color32(220, 65, 74, 255);
        private static readonly Color textColor = new Color32(245, 247, 250, 255);
        private static readonly Color mutedTextColor = new Color32(190, 199, 208, 255);

        [MenuItem("游戏/不器/重建构筑控件")]
        public static void BuildAll()
        {
            EnsureFolder(OutputFolder);
            BuildBoardSlot();
            BuildChoiceCard();
            BuildOfferCard();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("不器构筑控件已重建。");
        }

        private static void BuildBoardSlot()
        {
            GameObject root = CreateRoot("BoardSlotWidget", new Vector2(132f, 132f));
            Image background = AddImage(root, surfaceColor);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = CreateColorBlock(surfaceColor);
            BoardSlotWidget widget = root.AddComponent<BoardSlotWidget>();

            Image selection = CreateImage(root.transform, "Selection", accentColor);
            Stretch(selection.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            selection.raycastTarget = false;
            selection.gameObject.SetActive(false);

            GameObject lockedOverlay = CreatePanel(root.transform, "LockedOverlay", Vector2.zero, new Vector2(132f, 132f), new Color(0f, 0f, 0f, 0.64f));
            Text lockedLabel = CreateText(lockedOverlay.transform, "Label", "已锁定", 17, TextAnchor.MiddleCenter, textColor);
            Stretch(lockedLabel.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            lockedOverlay.SetActive(false);

            Text name = CreateText(root.transform, "Name_Text", "装备", 18, TextAnchor.MiddleCenter, textColor);
            SetRect(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(118f, 34f));
            name.fontStyle = FontStyle.Bold;

            Text size = CreateText(root.transform, "Size_Text", "占用 1 格", 15, TextAnchor.MiddleCenter, accentColor);
            SetRect(size.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(118f, 27f));

            Text slot = CreateText(root.transform, "Slot_Text", "棋位 01", 13, TextAnchor.MiddleCenter, mutedTextColor);
            SetRect(slot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 13f), new Vector2(118f, 23f));

            Assign(widget, "m_Background", background);
            Assign(widget, "m_Selection", selection);
            Assign(widget, "m_LockedOverlay", lockedOverlay);
            Assign(widget, "m_NameText", name);
            Assign(widget, "m_SizeText", size);
            Assign(widget, "m_SlotText", slot);
            Assign(widget, "m_Button", button);
            SavePrefab(root, BoardSlotPath);
        }

        private static void BuildChoiceCard()
        {
            GameObject root = CreateRoot("ChoiceCardWidget", new Vector2(320f, 168f));
            Image background = AddImage(root, surfaceColor);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = CreateColorBlock(surfaceColor);
            ChoiceCardWidget widget = root.AddComponent<ChoiceCardWidget>();

            Image selection = CreateImage(root.transform, "Selection", primaryColor);
            Stretch(selection.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            selection.raycastTarget = false;
            selection.gameObject.SetActive(false);

            GameObject disabledOverlay = CreatePanel(root.transform, "DisabledOverlay", Vector2.zero, new Vector2(320f, 168f), new Color(0f, 0f, 0f, 0.64f));
            Text disabledLabel = CreateText(disabledOverlay.transform, "Label", "不可用", 17, TextAnchor.MiddleCenter, textColor);
            Stretch(disabledLabel.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            disabledOverlay.SetActive(false);

            Text title = CreateText(root.transform, "Title_Text", "选择", 21, TextAnchor.UpperLeft, textColor);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -27f), new Vector2(284f, 30f));
            title.fontStyle = FontStyle.Bold;

            Text description = CreateText(root.transform, "Description_Text", "选择说明", 15, TextAnchor.UpperLeft, mutedTextColor);
            SetRect(description.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -77f), new Vector2(284f, 54f));
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Truncate;

            Text cost = CreateText(root.transform, "Cost_Text", "消耗 0", 15, TextAnchor.MiddleLeft, accentColor);
            SetRect(cost.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 21f), new Vector2(284f, 25f));

            Assign(widget, "m_Background", background);
            Assign(widget, "m_Selection", selection);
            Assign(widget, "m_DisabledOverlay", disabledOverlay);
            Assign(widget, "m_TitleText", title);
            Assign(widget, "m_DescriptionText", description);
            Assign(widget, "m_CostText", cost);
            Assign(widget, "m_Button", button);
            SavePrefab(root, ChoiceCardPath);
        }

        private static void BuildOfferCard()
        {
            GameObject root = CreateRoot("OfferCardWidget", new Vector2(320f, 188f));
            Image background = AddImage(root, surfaceColor);
            OfferCardWidget widget = root.AddComponent<OfferCardWidget>();

            GameObject lockOverlay = CreatePanel(root.transform, "LockOverlay", Vector2.zero, new Vector2(320f, 188f), new Color(0f, 0f, 0f, 0.64f));
            Text lockLabel = CreateText(lockOverlay.transform, "Label", "已锁定", 17, TextAnchor.MiddleCenter, textColor);
            Stretch(lockLabel.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            lockOverlay.SetActive(false);

            GameObject soldOverlay = CreatePanel(root.transform, "SoldOverlay", Vector2.zero, new Vector2(320f, 188f), new Color(0.12f, 0.10f, 0.05f, 0.72f));
            Text soldLabel = CreateText(soldOverlay.transform, "Label", "已售出", 18, TextAnchor.MiddleCenter, accentColor);
            Stretch(soldLabel.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            soldOverlay.SetActive(false);

            Text name = CreateText(root.transform, "Name_Text", "OFFER", 19, TextAnchor.UpperLeft, textColor);
            SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -26f), new Vector2(288f, 28f));
            name.fontStyle = FontStyle.Bold;

            Text description = CreateText(root.transform, "Description_Text", "Offer description", 14, TextAnchor.UpperLeft, mutedTextColor);
            SetRect(description.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -66f), new Vector2(288f, 36f));
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Truncate;

            Text price = CreateText(root.transform, "Price_Text", "PRICE 0", 15, TextAnchor.MiddleLeft, accentColor);
            SetRect(price.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 55f), new Vector2(288f, 25f));

            Button buyButton = CreateButton(root.transform, "BuyButton", "BUY", new Vector2(-70f, 20f), new Vector2(124f, 36f), primaryColor, out _);
            Button detailsButton = CreateButton(root.transform, "DetailsButton", "DETAILS", new Vector2(72f, 20f), new Vector2(124f, 36f), raisedColor, out _);

            Assign(widget, "m_Background", background);
            Assign(widget, "m_LockOverlay", lockOverlay);
            Assign(widget, "m_SoldOverlay", soldOverlay);
            Assign(widget, "m_NameText", name);
            Assign(widget, "m_DescriptionText", description);
            Assign(widget, "m_PriceText", price);
            Assign(widget, "m_BuyButton", buyButton);
            Assign(widget, "m_DetailsButton", detailsButton);
            SavePrefab(root, OfferCardPath);
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
            button.colors = CreateColorBlock(color);
            labelText = CreateText(buttonObject.transform, "Label", label, 14, TextAnchor.MiddleCenter, textColor);
            Stretch(labelText.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            return button;
        }

        private static ColorBlock CreateColorBlock(Color color)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.14f);
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
            return colors;
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
                throw new MissingReferenceException(string.Format("Missing serialized property {0} on {1}.", propertyName, target.GetType().Name));
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SavePrefab(GameObject root, string assetPath)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Object.DestroyImmediate(root);
            if (prefab == null)
                throw new InvalidOperationException(string.Format("Could not save Buqi widget prefab at {0}.", assetPath));
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
