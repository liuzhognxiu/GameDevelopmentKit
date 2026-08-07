using Game.Hot.Buqi.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Hot.Editor
{
    public static class BuqiPopupUIBuilder
    {
        private const string FormFolder = "Assets/Res/UI/UIForm/Hot/Buqi";
        private const string ItemDetailPath = FormFolder + "/BuqiItemDetailForm.prefab";
        private const string ConfirmPath = FormFolder + "/BuqiConfirmForm.prefab";
        private const string MessagePath = FormFolder + "/BuqiMessageForm.prefab";

        private static readonly Color canvasColor = new Color32(19, 24, 29, 255);
        private static readonly Color surfaceColor = new Color32(35, 43, 50, 255);
        private static readonly Color raisedColor = new Color32(51, 62, 70, 255);
        private static readonly Color inkColor = new Color32(239, 242, 238, 255);
        private static readonly Color mutedColor = new Color32(165, 176, 178, 255);
        private static readonly Color accentColor = new Color32(229, 176, 71, 255);
        private static readonly Color jadeColor = new Color32(51, 150, 128, 255);

        [MenuItem("游戏/不器/重建弹窗界面")]
        public static void BuildAll()
        {
            EnsureFolder(FormFolder);
            BuildItemDetailForm();
            BuildConfirmForm();
            BuildMessageForm();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("不器弹窗界面已重建。");
        }

        private static void BuildItemDetailForm()
        {
            GameObject root = CreateRoot("BuqiItemDetailForm", new Vector2(720f, 420f));
            AddImage(root, canvasColor);
            BuqiItemDetailForm form = root.AddComponent<BuqiItemDetailForm>();

            GameObject panel = CreatePanel(root.transform, "Panel", Vector2.zero, new Vector2(684f, 384f), surfaceColor);
            GameObject itemCard = CreatePanel(panel.transform, "ItemCard", new Vector2(-224f, -2f), new Vector2(194f, 246f), raisedColor);
            Text itemName = CreateText(itemCard.transform, "ItemName_Text", "W8-000", 22, TextAnchor.UpperCenter, inkColor);
            SetRect(itemName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(174f, 40f));
            itemName.fontStyle = FontStyle.Bold;
            Text itemEffect = CreateText(itemCard.transform, "ItemEffect_Text", "伤害", 17, TextAnchor.MiddleCenter, accentColor);
            SetRect(itemEffect.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(174f, 40f));
            Text itemStatus = CreateText(itemCard.transform, "ItemStatus_Text", "--", 14, TextAnchor.MiddleCenter, mutedColor);
            SetRect(itemStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(174f, 34f));

            Text title = CreateText(panel.transform, "Title_Text", "装备详情", 26, TextAnchor.MiddleLeft, inkColor);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(94f, -38f), new Vector2(332f, 40f));
            title.fontStyle = FontStyle.Bold;

            Text meta = CreateText(panel.transform, "Meta_Text", "--", 16, TextAnchor.MiddleLeft, mutedColor);
            SetRect(meta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(94f, -78f), new Vector2(332f, 30f));

            Text body = CreateText(panel.transform, "Body_Text", "--", 17, TextAnchor.UpperLeft, inkColor);
            SetRect(body.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(94f, 30f), new Vector2(332f, 112f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;

            Text modification = CreateText(panel.transform, "Modification_Text", "无改造", 16, TextAnchor.UpperLeft, accentColor);
            SetRect(modification.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(94f, -74f), new Vector2(332f, 58f));
            modification.horizontalOverflow = HorizontalWrapMode.Wrap;
            modification.verticalOverflow = VerticalWrapMode.Truncate;

            Button close = CreateButton(panel.transform, "Close", "关闭", new Vector2(226f, -143f), new Vector2(118f, 42f), jadeColor, out _);

            Assign(form, "m_TitleText", title);
            Assign(form, "m_MetaText", meta);
            Assign(form, "m_BodyText", body);
            Assign(form, "m_ModificationText", modification);
            Assign(form, "m_CloseButton", close);
            SavePrefab(root, ItemDetailPath);
        }

        private static void BuildConfirmForm()
        {
            GameObject root = CreateRoot("BuqiConfirmForm", new Vector2(680f, 320f));
            AddImage(root, canvasColor);
            BuqiConfirmForm form = root.AddComponent<BuqiConfirmForm>();

            GameObject panel = CreatePanel(root.transform, "Panel", Vector2.zero, new Vector2(644f, 284f), surfaceColor);
            Text title = CreateText(panel.transform, "Title_Text", "确认操作", 26, TextAnchor.MiddleCenter, inkColor);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(580f, 42f));
            title.fontStyle = FontStyle.Bold;

            Text message = CreateText(panel.transform, "Message_Text", "--", 18, TextAnchor.MiddleCenter, mutedColor);
            SetRect(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(560f, 78f));
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.verticalOverflow = VerticalWrapMode.Truncate;

            Button confirm = CreateButton(panel.transform, "Confirm", "确认", new Vector2(116f, -96f), new Vector2(150f, 46f), jadeColor, out Text confirmText);
            Button cancel = CreateButton(panel.transform, "Cancel", "取消", new Vector2(-116f, -96f), new Vector2(150f, 46f), raisedColor, out Text cancelText);

            Assign(form, "m_TitleText", title);
            Assign(form, "m_MessageText", message);
            Assign(form, "m_ConfirmButton", confirm);
            Assign(form, "m_CancelButton", cancel);
            Assign(form, "m_ConfirmText", confirmText);
            Assign(form, "m_CancelText", cancelText);
            SavePrefab(root, ConfirmPath);
        }

        private static void BuildMessageForm()
        {
            GameObject root = CreateRoot("BuqiMessageForm", new Vector2(640f, 140f));
            Image background = AddImage(root, new Color32(42, 91, 83, 250));
            background.raycastTarget = false;
            BuqiMessageForm form = root.AddComponent<BuqiMessageForm>();

            GameObject panel = CreatePanel(root.transform, "Panel", Vector2.zero, new Vector2(604f, 104f), new Color32(25, 46, 45, 120));
            panel.GetComponent<Image>().raycastTarget = false;
            Text kind = CreateText(panel.transform, "Kind_Text", "提示", 15, TextAnchor.MiddleLeft, new Color32(193, 246, 223, 255));
            SetRect(kind.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 20f), new Vector2(86f, 30f));
            kind.fontStyle = FontStyle.Bold;

            Text message = CreateText(panel.transform, "Message_Text", "--", 20, TextAnchor.MiddleLeft, inkColor);
            SetRect(message.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(108f, 20f), new Vector2(-132f, 38f));
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.verticalOverflow = VerticalWrapMode.Truncate;

            Image progressTrack = CreateImage(panel.transform, "ProgressTrack", new Color32(25, 36, 39, 220));
            progressTrack.raycastTarget = false;
            SetRect(progressTrack.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 15f), new Vector2(-48f, 6f));
            Image progressFill = CreateImage(progressTrack.transform, "ProgressFill_Image", accentColor);
            progressFill.raycastTarget = false;
            Stretch(progressFill.rectTransform, Vector2.zero, Vector2.zero);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 1f;

            Assign(form, "m_Background", background);
            Assign(form, "m_KindText", kind);
            Assign(form, "m_MessageText", message);
            Assign(form, "m_ProgressFill", progressFill);
            SavePrefab(root, MessagePath);
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
            root.layer = GetUILayer();
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
