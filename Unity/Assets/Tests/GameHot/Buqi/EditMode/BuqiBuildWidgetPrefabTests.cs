using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiBuildWidgetPrefabTests
    {
        private const string BoardSlotPath = "Assets/Res/UI/UIPrefab/Buqi/BoardSlotWidget.prefab";
        private const string ChoiceCardPath = "Assets/Res/UI/UIPrefab/Buqi/ChoiceCardWidget.prefab";
        private const string OfferCardPath = "Assets/Res/UI/UIPrefab/Buqi/OfferCardWidget.prefab";

        private static readonly IReadOnlyDictionary<string, string[]> RequiredReferences =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["BoardSlotWidget"] = new[]
                {
                    "m_Background", "m_Selection", "m_LockedOverlay", "m_NameText",
                    "m_SizeText", "m_SlotText", "m_Button",
                },
                ["ChoiceCardWidget"] = new[]
                {
                    "m_Background", "m_Selection", "m_DisabledOverlay", "m_TitleText",
                    "m_DescriptionText", "m_CostText", "m_Button",
                },
                ["OfferCardWidget"] = new[]
                {
                    "m_Background", "m_LockOverlay", "m_SoldOverlay", "m_NameText",
                    "m_DescriptionText", "m_PriceText", "m_BuyButton", "m_DetailsButton",
                },
            };

        [TestCase(BoardSlotPath, "BoardSlotWidget")]
        [TestCase(ChoiceCardPath, "ChoiceCardWidget")]
        [TestCase(OfferCardPath, "OfferCardWidget")]
        public void WidgetPrefab_HasExpectedRootComponent(string path, string componentName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(
                prefab.GetComponents<MonoBehaviour>().Any(component => component.GetType().Name == componentName),
                Is.True,
                componentName);
        }

        [TestCase(BoardSlotPath, "BoardSlotWidget", 132f, 132f)]
        [TestCase(ChoiceCardPath, "ChoiceCardWidget", 320f, 168f)]
        [TestCase(OfferCardPath, "OfferCardWidget", 320f, 188f)]
        public void WidgetPrefab_HasStablePreferredSize(
            string path,
            string componentName,
            float expectedWidth,
            float expectedHeight)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            RectTransform rectTransform = prefab.GetComponent<RectTransform>();
            Assert.That(rectTransform, Is.Not.Null, componentName);
            Assert.That(rectTransform.sizeDelta.x, Is.EqualTo(expectedWidth).Within(0.01f), componentName);
            Assert.That(rectTransform.sizeDelta.y, Is.EqualTo(expectedHeight).Within(0.01f), componentName);
        }

        [TestCase(BoardSlotPath, "BoardSlotWidget")]
        [TestCase(ChoiceCardPath, "ChoiceCardWidget")]
        [TestCase(OfferCardPath, "OfferCardWidget")]
        public void WidgetPrefab_HasCompleteSerializedBindings(string path, string componentName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            MonoBehaviour widget = prefab.GetComponents<MonoBehaviour>()
                .SingleOrDefault(component => component.GetType().Name == componentName);
            Assert.That(widget, Is.Not.Null, componentName);

            var serialized = new SerializedObject(widget);
            foreach (string propertyName in RequiredReferences[componentName])
            {
                SerializedProperty property = serialized.FindProperty(propertyName);
                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
            }
        }

        [Test]
        public void WidgetTypes_ExposeRenderAndClearCallbacks()
        {
            Type assemblyType = typeof(BuqiBattleSimulator).Assembly.GetType("Game.Hot.Buqi.UI.BoardSlotWidget");
            Assert.That(assemblyType, Is.Not.Null, "BoardSlotWidget");

            Assert.That(assemblyType.GetMethod("Clear"), Is.Not.Null);
            Assert.That(assemblyType.GetMethods().Any(method => method.Name == "Render"), Is.True);
            Assert.That(typeof(BuqiBattleSimulator).Assembly.GetType("Game.Hot.Buqi.UI.ChoiceCardWidget"), Is.Not.Null);
            Assert.That(typeof(BuqiBattleSimulator).Assembly.GetType("Game.Hot.Buqi.UI.OfferCardWidget"), Is.Not.Null);
        }
    }
}
