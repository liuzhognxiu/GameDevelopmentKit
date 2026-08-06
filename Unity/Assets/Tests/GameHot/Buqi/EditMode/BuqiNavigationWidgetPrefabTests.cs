using System;
using System.Linq;
using Game.Hot.Buqi.Battle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiNavigationWidgetPrefabTests
    {
        private const string WidgetFolder = "Assets/Res/UI/UIPrefab/Buqi/";
        private const string ResourceChipPath = WidgetFolder + "ResourceChipWidget.prefab";
        private const string PhaseStepPath = WidgetFolder + "PhaseStepWidget.prefab";

        [TestCase(ResourceChipPath, "ResourceChipWidget")]
        [TestCase(PhaseStepPath, "PhaseStepWidget")]
        public void NavigationWidget_HasExpectedRootComponent(string path, string componentName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(
                prefab.GetComponents<MonoBehaviour>().Any(component => component != null && component.GetType().Name == componentName),
                Is.True,
                componentName);
        }

        [TestCase("ResourceChipWidget", "ResourceChipView", 1)]
        [TestCase("PhaseStepWidget", "PhaseStepView", 2)]
        public void NavigationWidget_ExposesRenderAndClearContract(
            string componentName,
            string viewTypeName,
            int renderParameterCount)
        {
            Type componentType = typeof(BuqiBattleSimulator).Assembly.GetType(
                "Game.Hot.Buqi.UI.Widgets." + componentName);
            Type viewType = typeof(BuqiBattleSimulator).Assembly.GetType(
                "Game.Hot.Buqi.UI.Widgets." + viewTypeName);

            Assert.That(componentType, Is.Not.Null, componentName);
            Assert.That(viewType, Is.Not.Null, viewTypeName);
            Assert.That(componentType.GetMethod("Clear"), Is.Not.Null, "Clear");
            Assert.That(
                componentType.GetMethods()
                    .Any(method => method.Name == "Render" && method.GetParameters().Length == renderParameterCount),
                Is.True,
                "Render");
        }

        [Test]
        public void ResourceChipWidget_HasStableSizeAndCompleteBindings()
        {
            GameObject prefab = LoadPrefab(ResourceChipPath);
            AssertSize(prefab, 176f, 54f);

            MonoBehaviour widget = FindRootComponent(prefab, "ResourceChipWidget");
            Assert.That(widget, Is.Not.Null, "ResourceChipWidget");
            AssertReferences(widget,
                "m_Background",
                "m_IconText",
                "m_LabelText",
                "m_ValueText",
                "m_StateText");

            AssertChild(prefab, "Icon_Text");
            AssertChild(prefab, "Label_Text");
            AssertChild(prefab, "Value_Text");
            AssertChild(prefab, "State_Text");
        }

        [Test]
        public void PhaseStepWidget_HasStableSizeCompleteBindingsAndNonColorStateChannels()
        {
            GameObject prefab = LoadPrefab(PhaseStepPath);
            AssertSize(prefab, 208f, 48f);

            MonoBehaviour widget = FindRootComponent(prefab, "PhaseStepWidget");
            Assert.That(widget, Is.Not.Null, "PhaseStepWidget");
            AssertReferences(widget,
                "m_Background",
                "m_SelectionOutline",
                "m_Button",
                "m_IndexText",
                "m_LabelText",
                "m_StateText");

            Assert.That(prefab.GetComponent<Button>(), Is.Not.Null, "PhaseStepWidget must be clickable");
            AssertChild(prefab, "Index_Text");
            AssertChild(prefab, "Label_Text");
            AssertChild(prefab, "State_Text");
            AssertChild(prefab, "SelectionOutline");
        }

        private static GameObject LoadPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            return prefab;
        }

        private static MonoBehaviour FindRootComponent(GameObject prefab, string componentName)
        {
            return prefab.GetComponents<MonoBehaviour>()
                .SingleOrDefault(component => component != null && component.GetType().Name == componentName);
        }

        private static void AssertSize(GameObject prefab, float width, float height)
        {
            RectTransform rectTransform = prefab.GetComponent<RectTransform>();
            Assert.That(rectTransform, Is.Not.Null, "RectTransform");
            Assert.That(rectTransform.sizeDelta, Is.EqualTo(new Vector2(width, height)));
        }

        private static void AssertReferences(MonoBehaviour widget, params string[] propertyNames)
        {
            SerializedObject serialized = new SerializedObject(widget);
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
            }
        }

        private static void AssertChild(GameObject prefab, string name)
        {
            bool found = prefab.GetComponentsInChildren<Transform>(true).Any(child => child.name == name);
            Assert.That(found, Is.True, name);
        }
    }
}
