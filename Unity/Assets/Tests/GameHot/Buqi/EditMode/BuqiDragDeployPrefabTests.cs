using System.Linq;
using System.Reflection;
using Game.Hot;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiDragDeployPrefabTests
    {
        private const string ItemPath = "Assets/Res/UI/UIPrefab/Buqi/BuqiDraggableItemWidget.prefab";
        private const string SlotPath = "Assets/Res/UI/UIPrefab/Buqi/BuqiDeploySlotWidget.prefab";
        private const string FormPath = "Assets/Res/UI/UIForm/Hot/Buqi/BuqiDragDeployForm.prefab";

        [Test]
        public void ComponentPrefabs_HaveStableSizesAndCompleteBindings()
        {
            GameObject item = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath);
            GameObject slot = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);

            Assert.That(item, Is.Not.Null, ItemPath);
            Assert.That(slot, Is.Not.Null, SlotPath);
            Assert.That(item.GetComponent<RectTransform>().rect.size, Is.EqualTo(new Vector2(300f, 92f)));
            Assert.That(slot.GetComponent<RectTransform>().rect.size, Is.EqualTo(new Vector2(108f, 104f)));
            AssertReferences(FindComponent(item, "BuqiDraggableItemWidget"),
                "m_CanvasGroup", "m_Background", "m_NameText", "m_SizeText", "m_SourceText");
            AssertReferences(FindComponent(slot, "BuqiDeploySlotWidget"),
                "m_Background", "m_IndexText", "m_ItemText", "m_StateText", "m_InvalidSymbol");
        }

        [Test]
        public void FormPrefab_HasFullScreenLayoutAndCompleteBindings()
        {
            GameObject form = AssetDatabase.LoadAssetAtPath<GameObject>(FormPath);

            Assert.That(form, Is.Not.Null, FormPath);
            Assert.That(form.GetComponent<RectTransform>().rect.size, Is.EqualTo(new Vector2(1920f, 1080f)));
            AssertChild(form, "Header");
            AssertChild(form, "Context_Text");
            AssertChild(form, "StoragePanel");
            AssertChild(form, "BoardPanel");
            AssertChild(form, "DetailPanel");
            AssertChild(form, "CommandBar");
            AssertChild(form, "DragLayer");
            AssertChild(form, "ItemTemplate");
            Assert.That(Children(form, "BoardSlot_").Count(), Is.EqualTo(8));
            Assert.That(Children(form, "StorageSlot_").Count(), Is.EqualTo(8));

            MonoBehaviour component = FindComponent(form, "BuqiDragDeployForm");
            AssertReferences(component,
                "m_TitleText", "m_ContextText", "m_DetailText", "m_FeedbackText", "m_ItemTemplate",
                "m_BoardItemLayer", "m_StorageItemLayer", "m_DragLayer",
                "m_ResetButton", "m_CancelButton", "m_ConfirmButton");
            AssertArray(component, "m_BoardSlots", 8);
            AssertArray(component, "m_StorageSlots", 8);
        }

        [Test]
        public void UIFormId_RegistersDragDeployForm()
        {
            FieldInfo field = typeof(UIFormId).GetField(
                "BuqiDragDeployForm",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.GetValue(null), Is.EqualTo(108));
        }

        [Test]
        public void Builder_ExposesPrefabOpenCommand()
        {
            System.Type builder = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Game.Hot.Editor.BuqiDragDeployUIBuilder", false))
                .FirstOrDefault(type => type != null);
            Assert.That(builder, Is.Not.Null);
            Assert.That(builder.GetMethod(
                "OpenFormPrefab",
                BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(builder.GetMethod(
                "OpenRuntimeDemo",
                BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
        }

        [Test]
        public void DetailText_StaysInsideDetailPanel()
        {
            GameObject form = AssetDatabase.LoadAssetAtPath<GameObject>(FormPath);
            RectTransform panel = Children(form, "DetailPanel").Single().GetComponent<RectTransform>();
            RectTransform title = Children(form, "DetailTitle_Text").Single().GetComponent<RectTransform>();
            MonoBehaviour component = FindComponent(form, "BuqiDragDeployForm");
            var serialized = new SerializedObject(component);
            RectTransform detail = ((Text)serialized.FindProperty("m_DetailText").objectReferenceValue).rectTransform;
            var corners = new Vector3[4];
            detail.GetWorldCorners(corners);

            foreach (Vector3 corner in corners)
            {
                Vector3 local = panel.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(panel.rect.xMin, panel.rect.xMax));
                Assert.That(local.y, Is.InRange(panel.rect.yMin, panel.rect.yMax));
            }

            var titleCorners = new Vector3[4];
            title.GetWorldCorners(titleCorners);
            float detailTop = panel.InverseTransformPoint(corners[1]).y;
            float titleBottom = panel.InverseTransformPoint(titleCorners[0]).y;
            Assert.That(detailTop, Is.LessThanOrEqualTo(titleBottom - 8f));
        }

        private static MonoBehaviour FindComponent(GameObject prefab, string typeName)
        {
            MonoBehaviour component = prefab == null
                ? null
                : prefab.GetComponents<MonoBehaviour>().FirstOrDefault(value => value.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null, typeName);
            return component;
        }

        private static void AssertReferences(MonoBehaviour component, params string[] fieldNames)
        {
            var serialized = new SerializedObject(component);
            foreach (string fieldName in fieldNames)
            {
                SerializedProperty property = serialized.FindProperty(fieldName);
                Assert.That(property, Is.Not.Null, fieldName);
                Assert.That(property.objectReferenceValue, Is.Not.Null, fieldName);
            }
        }

        private static void AssertArray(MonoBehaviour component, string fieldName, int expectedCount)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            Assert.That(property.isArray, Is.True, fieldName);
            Assert.That(property.arraySize, Is.EqualTo(expectedCount), fieldName);
            for (int index = 0; index < property.arraySize; index++)
                Assert.That(property.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null, fieldName);
        }

        private static void AssertChild(GameObject prefab, string name)
        {
            Assert.That(Children(prefab, name), Is.Not.Empty, name);
        }

        private static Transform[] Children(GameObject prefab, string prefix)
        {
            return prefab.GetComponentsInChildren<Transform>(true)
                .Where(child => child.name.StartsWith(prefix))
                .ToArray();
        }
    }
}
