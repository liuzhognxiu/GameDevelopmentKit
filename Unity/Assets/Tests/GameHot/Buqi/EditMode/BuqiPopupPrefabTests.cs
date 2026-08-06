using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiPopupPrefabTests
    {
        private const string ItemDetailPath = "Assets/Res/UI/UIForm/Hot/Buqi/BuqiItemDetailForm.prefab";
        private const string ConfirmPath = "Assets/Res/UI/UIForm/Hot/Buqi/BuqiConfirmForm.prefab";
        private const string MessagePath = "Assets/Res/UI/UIForm/Hot/Buqi/BuqiMessageForm.prefab";

        [TestCase(ItemDetailPath, "BuqiItemDetailForm")]
        [TestCase(ConfirmPath, "BuqiConfirmForm")]
        [TestCase(MessagePath, "BuqiMessageForm")]
        public void Prefab_HasExpectedRootComponent(string path, string componentName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(
                prefab.GetComponents<MonoBehaviour>().Any(component => component.GetType().Name == componentName),
                Is.True,
                componentName);
        }

        [Test]
        public void ItemDetailForm_HasCompleteSerializedBindings()
        {
            GameObject prefab = Load(ItemDetailPath);
            Assert.That(prefab.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(720f, 420f)));

            AssertChildren(prefab, "Panel", "ItemCard", "Title_Text", "Meta_Text", "Body_Text", "Modification_Text", "Close");
            AssertReferences(prefab, "BuqiItemDetailForm", "m_TitleText", "m_MetaText", "m_BodyText", "m_ModificationText", "m_CloseButton");
        }

        [Test]
        public void ConfirmForm_HasCompleteSerializedBindings()
        {
            GameObject prefab = Load(ConfirmPath);
            Assert.That(prefab.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(680f, 320f)));

            AssertChildren(prefab, "Panel", "Title_Text", "Message_Text", "Confirm", "Cancel");
            AssertReferences(prefab, "BuqiConfirmForm", "m_TitleText", "m_MessageText", "m_ConfirmButton", "m_CancelButton", "m_ConfirmText", "m_CancelText");
        }

        [Test]
        public void MessageForm_HasCompleteSerializedBindingsAndNoConfirmControls()
        {
            GameObject prefab = Load(MessagePath);
            Assert.That(prefab.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(640f, 140f)));

            AssertChildren(prefab, "Panel", "Kind_Text", "Message_Text", "ProgressTrack", "ProgressFill_Image");
            AssertReferences(prefab, "BuqiMessageForm", "m_Background", "m_KindText", "m_MessageText", "m_ProgressFill");
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            Assert.That(components.Any(component => component != null && component.GetType().Name == "Button"), Is.False);
            foreach (Component component in components.Where(component => component != null &&
                (component.GetType().Name == "Image" || component.GetType().Name == "Text")))
            {
                var serialized = new SerializedObject(component);
                SerializedProperty raycastTarget = serialized.FindProperty("m_RaycastTarget");
                Assert.That(raycastTarget, Is.Not.Null, component.name);
                Assert.That(raycastTarget.boolValue, Is.False, component.name);
            }
        }

        private static GameObject Load(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            return prefab;
        }

        private static void AssertChildren(GameObject prefab, params string[] names)
        {
            Transform[] children = prefab.GetComponentsInChildren<Transform>(true);
            foreach (string name in names)
                Assert.That(children.Any(child => child.name == name), Is.True, name);
        }

        private static void AssertReferences(GameObject prefab, string componentName, params string[] propertyNames)
        {
            MonoBehaviour component = prefab.GetComponents<MonoBehaviour>()
                .Single(candidate => candidate.GetType().Name == componentName);
            var serialized = new SerializedObject(component);

            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
            }
        }
    }
}
