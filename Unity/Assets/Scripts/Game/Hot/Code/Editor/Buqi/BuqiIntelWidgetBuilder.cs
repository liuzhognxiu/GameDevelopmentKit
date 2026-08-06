using System;
using System.Collections.Generic;
using Game.Hot.Buqi.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Hot.Editor
{
    public static class BuqiIntelWidgetBuilder
    {
        private const string WidgetFolder = "Assets/Res/UI/UIPrefab/Buqi";
        private const string OpponentSnapshotPath = WidgetFolder + "/OpponentSnapshotWidget.prefab";
        private const string FactRowPath = WidgetFolder + "/FactRowWidget.prefab";

        private static readonly Color surfaceColor = new Color32(35, 43, 50, 255);
        private static readonly Color raisedColor = new Color32(51, 62, 70, 255);
        private static readonly Color inkColor = new Color32(239, 242, 238, 255);
        private static readonly Color mutedColor = new Color32(165, 176, 178, 255);
        private static readonly Color accentColor = new Color32(229, 176, 71, 255);
        private static readonly Color jadeColor = new Color32(51, 150, 128, 255);

        [MenuItem("Game/Buqi/Rebuild Intel Widgets")]
        public static void BuildAll()
        {
            EnsureFolder(WidgetFolder);
            BuildOpponentSnapshot();
            BuildFactRow();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Buqi intel widgets rebuilt.");
        }

        private static void BuildOpponentSnapshot()
        {
            GameObject root = CreateRoot("OpponentSnapshotWidget", new Vector2(456f, 292f));
            AddImage(root, surfaceColor);
            OpponentSnapshotWidget widget = root.AddComponent<OpponentSnapshotWidget>();

            Image marker = CreateImage(root.transform, "StatusMarker", accentColor);
            SetRect(marker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -20f), new Vector2(8f, 36f));

            Text name = CreateText(root.transform, "OpponentName_Text", "\u5BF9\u624B\u5FEB\u7167", 22, TextAnchor.MiddleLeft, inkColor);
            SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -24f), new Vector2(300f, 32f));
            name.fontStyle = FontStyle.Bold;

            Text build = CreateText(root.transform, "Build_Text", "\u65B9\u5411  \u9AD8\u901F\u6784\u7B51", 15, TextAnchor.MiddleLeft, accentColor);
            SetRect(build.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -53f), new Vector2(340f, 24f));

            Text slots = CreateText(root.transform, "BoardSummary_Text", "\u8FDE\u7EED 8 \u683C\u6784\u7B51  \u00B7  \u516C\u5F00\u60C5\u62A5", 14, TextAnchor.MiddleLeft, mutedColor);
            SetRect(slots.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -40f), new Vector2(180f, 48f));

            Text[] itemLabels = new Text[3];
            Button[] itemButtons = new Button[3];
            for (int index = 0; index < 3; index++)
            {
                float x = -150f + index * 150f;
                Button button = CreateButton(
                    root.transform,
                    string.Format("KeyItem{0:00}", index + 1),
                    "\u7A7A\u7F6E",
                    new Vector2(x, 53f),
                    new Vector2(136f, 72f),
                    raisedColor,
                    out Text label);
                label.fontSize = 13;
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                itemButtons[index] = button;
                itemLabels[index] = label;
            }

            Text threat = CreateText(root.transform, "Threat_Text", "\u4E3B\u8981\u5A01\u80C1\uFF1A\u516C\u5F00\u88C5\u5907\u89E6\u53D1\u5173\u7CFB", 14, TextAnchor.MiddleLeft, inkColor);
            SetRect(threat.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 72f), new Vector2(400f, 24f));

            Text risk = CreateText(root.transform, "Risk_Text", "\u5DF2\u77E5\u98CE\u9669\uFF1A\u672A\u516C\u5F00\u6539\u9020", 14, TextAnchor.MiddleLeft, mutedColor);
            SetRect(risk.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 42f), new Vector2(400f, 24f));

            Assign(widget, "m_NameText", name);
            Assign(widget, "m_BuildText", build);
            Assign(widget, "m_SlotsText", slots);
            Assign(widget, "m_ThreatText", threat);
            Assign(widget, "m_RiskText", risk);
            Assign(widget, "m_StatusMarker", marker);
            AssignArray(widget, "m_ItemLabels", itemLabels);
            AssignArray(widget, "m_ItemButtons", itemButtons);
            SavePrefab(root, OpponentSnapshotPath);
        }

        private static void BuildFactRow()
        {
            GameObject root = CreateRoot("FactRowWidget", new Vector2(456f, 68f));
            AddImage(root, new Color32(28, 35, 40, 235));
            FactRowWidget widget = root.AddComponent<FactRowWidget>();

            Image marker = CreateImage(root.transform, "Marker_Image", accentColor);
            SetRect(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(6f, 50f));

            Text title = CreateText(root.transform, "Title_Text", "\u7EC8\u5C40\u4E8B\u5B9E", 15, TextAnchor.MiddleLeft, inkColor);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -19f), new Vector2(230f, 24f));
            title.fontStyle = FontStyle.Bold;

            Text body = CreateText(root.transform, "Body_Text", "\u5173\u952E\u88C5\u5907\u5B8C\u6210\u6709\u6548\u4F24\u5BB3", 13, TextAnchor.MiddleLeft, mutedColor);
            SetRect(body.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 19f), new Vector2(310f, 25f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button jump = CreateButton(root.transform, "JumpButton", "\u8DF3\u5230 T000", new Vector2(370f, 0f), new Vector2(126f, 42f), raisedColor, out Text tick);
            tick.fontSize = 13;

            Assign(widget, "m_TitleText", title);
            Assign(widget, "m_BodyText", body);
            Assign(widget, "m_TickText", tick);
            Assign(widget, "m_Marker", marker);
            Assign(widget, "m_JumpButton", jump);
            SavePrefab(root, FactRowPath);
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
            labelText = CreateText(buttonObject.transform, "Label", label, 15, TextAnchor.MiddleCenter, inkColor);
            Stretch(labelText.rectTransform, new Vector2(5f, 2f), new Vector2(-5f, -2f));
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
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new MissingReferenceException(string.Format("Missing serialized property {0} on {1}.", propertyName, target.GetType().Name));
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray<T>(Object target, string propertyName, IReadOnlyList<T> values)
            where T : Object
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new MissingReferenceException(string.Format("Missing serialized array {0} on {1}.", propertyName, target.GetType().Name));
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
