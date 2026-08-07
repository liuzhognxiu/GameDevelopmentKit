using Game.Hot.Buqi.UI.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Hot.Editor
{
    public static class BuqiNavigationWidgetBuilder
    {
        private const string WidgetFolder = "Assets/Res/UI/UIPrefab/Buqi";
        private const string ResourceChipPath = WidgetFolder + "/ResourceChipWidget.prefab";
        private const string PhaseStepPath = WidgetFolder + "/PhaseStepWidget.prefab";

        private static readonly Color surfaceColor = new Color32(35, 43, 50, 255);
        private static readonly Color raisedColor = new Color32(51, 62, 70, 255);
        private static readonly Color inkColor = new Color32(239, 242, 238, 255);
        private static readonly Color mutedColor = new Color32(165, 176, 178, 255);
        private static readonly Color accentColor = new Color32(229, 176, 71, 255);
        private static readonly Color jadeColor = new Color32(51, 150, 128, 255);

        [MenuItem("游戏/不器/重建导航控件")]
        public static void BuildAll()
        {
            EnsureFolder(WidgetFolder);
            BuildResourceChip();
            BuildPhaseStep();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("不器导航控件已重建。");
        }

        private static void BuildResourceChip()
        {
            GameObject root = CreateRoot("ResourceChipWidget", new Vector2(176f, 54f));
            Image background = AddImage(root, surfaceColor);
            ResourceChipWidget widget = root.AddComponent<ResourceChipWidget>();

            Text icon = CreateText(root.transform, "Icon_Text", "+", 20, TextAnchor.MiddleCenter, accentColor);
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(30f, 32f));

            Text label = CreateText(root.transform, "Label_Text", "金币", 15, TextAnchor.MiddleLeft, mutedColor);
            SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(47f, 8f), new Vector2(82f, 22f));

            Text value = CreateText(root.transform, "Value_Text", "06", 21, TextAnchor.MiddleLeft, inkColor);
            SetRect(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(47f, -12f), new Vector2(82f, 24f));
            value.fontStyle = FontStyle.Bold;

            Text state = CreateText(root.transform, "State_Text", "正常", 12, TextAnchor.MiddleRight, jadeColor);
            SetRect(state.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(38f, 22f));

            Assign(widget, "m_Background", background);
            Assign(widget, "m_IconText", icon);
            Assign(widget, "m_LabelText", label);
            Assign(widget, "m_ValueText", value);
            Assign(widget, "m_StateText", state);
            SavePrefab(root, ResourceChipPath);
        }

        private static void BuildPhaseStep()
        {
            GameObject root = CreateRoot("PhaseStepWidget", new Vector2(208f, 48f));
            Image background = AddImage(root, raisedColor);
            PhaseStepWidget widget = root.AddComponent<PhaseStepWidget>();
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;

            Image selectionOutline = CreateImage(root.transform, "SelectionOutline", new Color32(229, 176, 71, 0));
            Stretch(selectionOutline.rectTransform, new Vector2(1f, 1f), new Vector2(-1f, -1f));
            selectionOutline.raycastTarget = false;

            Text index = CreateText(root.transform, "Index_Text", "01", 14, TextAnchor.MiddleCenter, accentColor);
            SetRect(index.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(23f, 0f), new Vector2(34f, 32f));
            index.fontStyle = FontStyle.Bold;

            Text label = CreateText(root.transform, "Label_Text", "起始选择", 16, TextAnchor.MiddleLeft, inkColor);
            SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(49f, 0f), new Vector2(112f, 32f));

            Text state = CreateText(root.transform, "State_Text", ">", 14, TextAnchor.MiddleCenter, mutedColor);
            SetRect(state.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(32f, 28f));

            Assign(widget, "m_Background", background);
            Assign(widget, "m_SelectionOutline", selectionOutline);
            Assign(widget, "m_Button", button);
            Assign(widget, "m_IndexText", index);
            Assign(widget, "m_LabelText", label);
            Assign(widget, "m_StateText", state);
            SavePrefab(root, PhaseStepPath);
        }

        private static GameObject CreateRoot(string name, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            root.layer = GetUILayer();
            root.GetComponent<RectTransform>().sizeDelta = size;
            return root;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = CreateRoot(name, Vector2.zero);
            imageObject.transform.SetParent(parent, false);
            return AddImage(imageObject, color);
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
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
            SerializedObject serializedObject = new SerializedObject(target);
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
