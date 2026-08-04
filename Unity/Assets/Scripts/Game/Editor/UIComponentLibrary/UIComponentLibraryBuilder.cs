using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class UIComponentLibraryBuilder
    {
        private const string OutputFolder = "Assets/Res/UI/UIPrefab/ComponentLibrary";
        private const string ButtonSpritePath = "Assets/Res/UI/UISprite/Common/button-filled.png";
        private const string OutlineSpritePath = "Assets/Res/UI/UISprite/Common/button-outline.png";
        private const string BoxSpritePath = "Assets/Res/UI/UISprite/Common/box.png";
        private const string CircleSpritePath = "Assets/Res/UI/UISprite/Common/circle-filled.png";
        private const string CircleOutlineSpritePath = "Assets/Res/UI/UISprite/Common/circle-outline.png";
        private const string CheckedSpritePath = "Assets/Res/UI/UISprite/Common/checked.png";
        private const string ProgressBackgroundSpritePath = "Assets/Res/UI/UISprite/Common/progressbar-background.png";
        private const string ProgressFillSpritePath = "Assets/Res/UI/UISprite/Common/progressbar.png";

        private static readonly Color surfaceColor = new Color32(36, 43, 51, 255);
        private static readonly Color surfaceRaisedColor = new Color32(57, 67, 78, 255);
        private static readonly Color primaryColor = new Color32(27, 155, 142, 255);
        private static readonly Color successColor = new Color32(65, 181, 117, 255);
        private static readonly Color accentColor = new Color32(245, 184, 72, 255);
        private static readonly Color dangerColor = new Color32(220, 65, 74, 255);
        private static readonly Color textColor = new Color32(245, 247, 250, 255);
        private static readonly Color mutedTextColor = new Color32(190, 199, 208, 255);

        [MenuItem("Game/UI/Rebuild Common Component Library")]
        public static void BuildAll()
        {
            EnsureFolder(OutputFolder);
            BuildBadge();
            BuildButton();
            BuildToggle();
            BuildProgressBar();
            BuildItemSlot();
            BuildLoading();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"UI component library rebuilt at '{OutputFolder}'.");
        }

        private static void BuildBadge()
        {
            GameObject root = CreateRoot("CommonBadgeWidget", new Vector2(28f, 28f));
            Image background = root.AddComponent<Image>();
            ConfigureImage(background, LoadSprite(CircleSpritePath), dangerColor, Image.Type.Simple);

            CommonBadgeWidget widget = root.AddComponent<CommonBadgeWidget>();
            TMP_Text countText = CreateText(root.transform, "Count", "0", 16f, TextAlignmentOptions.Center, textColor);
            Stretch(countText.rectTransform, Vector2.zero, Vector2.zero);
            Assign(widget, "m_CountText", countText);
            root.SetActive(false);
            SavePrefab(root, "CommonBadgeWidget.prefab");
        }

        private static void BuildButton()
        {
            GameObject root = CreateRoot("CommonButtonWidget", new Vector2(220f, 56f));
            Image background = root.AddComponent<Image>();
            ConfigureImage(background, LoadSprite(ButtonSpritePath), primaryColor, Image.Type.Sliced);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = CreateColorBlock(primaryColor);
            AddLayoutElement(root, 220f, 56f);

            Image icon = CreateImage(root.transform, "Icon", null, textColor);
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(28f, 28f));
            icon.preserveAspect = true;
            icon.gameObject.SetActive(false);

            TMP_Text label = CreateText(root.transform, "Label", "Button", 22f, TextAlignmentOptions.Center, textColor);
            Stretch(label.rectTransform, new Vector2(52f, 6f), new Vector2(-18f, -6f));

            CommonBadgeWidget badge = AddBadgeInstance(root.transform, new Vector2(-8f, -8f));
            CommonButtonWidget widget = root.AddComponent<CommonButtonWidget>();
            Assign(widget, "m_Button", button);
            Assign(widget, "m_Label", label);
            Assign(widget, "m_Icon", icon);
            Assign(widget, "m_Badge", badge);
            SavePrefab(root, "CommonButtonWidget.prefab");
        }

        private static void BuildToggle()
        {
            GameObject root = CreateRoot("CommonToggleWidget", new Vector2(220f, 44f));
            Toggle toggle = root.AddComponent<Toggle>();
            AddLayoutElement(root, 220f, 44f);

            Image background = CreateImage(root.transform, "Background", LoadSprite(OutlineSpritePath), surfaceRaisedColor);
            SetRect(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(30f, 30f));
            background.type = Image.Type.Sliced;

            Image checkmark = CreateImage(background.transform, "Checkmark", LoadSprite(CheckedSpritePath), successColor);
            Stretch(checkmark.rectTransform, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            checkmark.preserveAspect = true;

            TMP_Text label = CreateText(root.transform, "Label", "Toggle", 20f, TextAlignmentOptions.MidlineLeft, textColor);
            Stretch(label.rectTransform, new Vector2(48f, 2f), new Vector2(-8f, -2f));

            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = false;
            CommonToggleWidget widget = root.AddComponent<CommonToggleWidget>();
            Assign(widget, "m_Toggle", toggle);
            Assign(widget, "m_Label", label);
            SavePrefab(root, "CommonToggleWidget.prefab");
        }

        private static void BuildProgressBar()
        {
            GameObject root = CreateRoot("CommonProgressBarWidget", new Vector2(260f, 30f));
            AddLayoutElement(root, 260f, 30f);

            Image background = CreateImage(root.transform, "Background", LoadSprite(ProgressBackgroundSpritePath), surfaceRaisedColor);
            Stretch(background.rectTransform, Vector2.zero, Vector2.zero);
            background.type = Image.Type.Sliced;

            Image fill = CreateImage(background.transform, "Fill", LoadSprite(ProgressFillSpritePath), successColor);
            Stretch(fill.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            TMP_Text valueText = CreateText(root.transform, "Value", "0%", 17f, TextAlignmentOptions.Center, textColor);
            Stretch(valueText.rectTransform, Vector2.zero, Vector2.zero);

            CommonProgressBarWidget widget = root.AddComponent<CommonProgressBarWidget>();
            Assign(widget, "m_Fill", fill);
            Assign(widget, "m_ValueText", valueText);
            SavePrefab(root, "CommonProgressBarWidget.prefab");
        }

        private static void BuildItemSlot()
        {
            GameObject root = CreateRoot("CommonItemSlotWidget", new Vector2(96f, 96f));
            Image background = root.AddComponent<Image>();
            ConfigureImage(background, LoadSprite(BoxSpritePath), surfaceColor, Image.Type.Sliced);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = CreateColorBlock(surfaceColor);
            AddLayoutElement(root, 96f, 96f);

            Image frame = CreateImage(root.transform, "Frame", LoadSprite(OutlineSpritePath), mutedTextColor);
            Stretch(frame.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;

            Image icon = CreateImage(root.transform, "Icon", null, Color.white);
            Stretch(icon.rectTransform, new Vector2(14f, 14f), new Vector2(-14f, -14f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.gameObject.SetActive(false);

            Image selection = CreateImage(root.transform, "Selection", LoadSprite(OutlineSpritePath), accentColor);
            Stretch(selection.rectTransform, Vector2.zero, Vector2.zero);
            selection.type = Image.Type.Sliced;
            selection.raycastTarget = false;
            selection.gameObject.SetActive(false);

            Image lockOverlay = CreateImage(root.transform, "LockOverlay", LoadSprite(BoxSpritePath), new Color(0f, 0f, 0f, 0.68f));
            Stretch(lockOverlay.rectTransform, Vector2.zero, Vector2.zero);
            lockOverlay.type = Image.Type.Sliced;
            lockOverlay.raycastTarget = false;
            lockOverlay.gameObject.SetActive(false);

            TMP_Text quantity = CreateText(root.transform, "Quantity", "1", 18f, TextAlignmentOptions.BottomRight, textColor);
            Stretch(quantity.rectTransform, new Vector2(6f, 4f), new Vector2(-7f, -4f));
            quantity.gameObject.SetActive(false);

            CommonBadgeWidget badge = AddBadgeInstance(root.transform, new Vector2(-4f, -4f));
            CommonItemSlotWidget widget = root.AddComponent<CommonItemSlotWidget>();
            Assign(widget, "m_Button", button);
            Assign(widget, "m_Frame", frame);
            Assign(widget, "m_Icon", icon);
            Assign(widget, "m_QuantityText", quantity);
            Assign(widget, "m_Selection", selection.gameObject);
            Assign(widget, "m_LockOverlay", lockOverlay.gameObject);
            Assign(widget, "m_Badge", badge);
            SavePrefab(root, "CommonItemSlotWidget.prefab");
        }

        private static void BuildLoading()
        {
            GameObject root = CreateRoot("CommonLoadingWidget", new Vector2(160f, 88f));
            AddLayoutElement(root, 160f, 88f);

            Image spinner = CreateImage(root.transform, "Spinner", LoadSprite(CircleOutlineSpritePath), primaryColor);
            SetRect(spinner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(48f, 48f));
            spinner.type = Image.Type.Filled;
            spinner.fillMethod = Image.FillMethod.Radial360;
            spinner.fillAmount = 0.76f;
            spinner.raycastTarget = false;

            TMP_Text label = CreateText(root.transform, "Label", string.Empty, 17f, TextAlignmentOptions.Center, mutedTextColor);
            SetRect(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(160f, 24f));
            label.gameObject.SetActive(false);

            CommonLoadingWidget widget = root.AddComponent<CommonLoadingWidget>();
            Assign(widget, "m_Spinner", spinner.rectTransform);
            Assign(widget, "m_Label", label);
            SavePrefab(root, "CommonLoadingWidget.prefab");
        }

        private static CommonBadgeWidget AddBadgeInstance(Transform parent, Vector2 anchoredPosition)
        {
            GameObject badgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{OutputFolder}/CommonBadgeWidget.prefab");
            GameObject badgeObject = (GameObject)PrefabUtility.InstantiatePrefab(badgePrefab);
            badgeObject.name = "Badge";
            badgeObject.transform.SetParent(parent, false);
            RectTransform rectTransform = badgeObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.one;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            return badgeObject.GetComponent<CommonBadgeWidget>();
        }

        private static GameObject CreateRoot(string name, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            root.layer = GetUILayer();
            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.sizeDelta = size;
            return root;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = GetUILayer();
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            ConfigureImage(image, sprite, color, Image.Type.Simple);
            return image;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            gameObject.layer = GetUILayer();
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI label = gameObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static void ConfigureImage(Image image, Sprite sprite, Color color, Image.Type type)
        {
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null ? type : Image.Type.Simple;
        }

        private static void AddLayoutElement(GameObject gameObject, float preferredWidth, float preferredHeight)
        {
            LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.preferredHeight = preferredHeight;
        }

        private static ColorBlock CreateColorBlock(Color baseColor)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, baseColor, 0.12f);
            colors.pressedColor = Color.Lerp(Color.white, Color.black, 0.18f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.58f, 0.62f, 0.65f);
            colors.colorMultiplier = 1f;
            return colors;
        }

        private static void Stretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static void Assign(Object target, string propertyName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingReferenceException($"Serialized property '{propertyName}' was not found on '{target.GetType().Name}'.");
            }
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"UI component library sprite is missing or not imported as Sprite: '{path}'.");
            }
            return sprite;
        }

        private static void SavePrefab(GameObject root, string fileName)
        {
            string assetPath = $"{OutputFolder}/{fileName}";
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Object.DestroyImmediate(root);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            AssetDatabase.SetLabels(prefab, new[] { "All", "Pack" });
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string currentPath = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = $"{currentPath}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                currentPath = nextPath;
            }
        }

        private static int GetUILayer()
        {
            int layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : 5;
        }
    }
}
