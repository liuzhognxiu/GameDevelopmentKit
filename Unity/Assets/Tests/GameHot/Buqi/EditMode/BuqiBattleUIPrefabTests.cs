using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiBattleUIPrefabTests
    {
        private const string BattleFormPath = "Assets/Res/UI/UIForm/Hot/Buqi/BattleForm.prefab";
        private const string ItemCardPath = "Assets/Res/UI/UIPrefab/Buqi/ItemCardWidget.prefab";
        private const string BattleLogPath = "Assets/Res/UI/UIPrefab/Buqi/BattleLogWidget.prefab";

        [TestCase(BattleFormPath, "BattleForm")]
        [TestCase(ItemCardPath, "ItemCardWidget")]
        [TestCase(BattleLogPath, "BattleLogWidget")]
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
        public void BattleForm_HasTracksEvidenceAndPlaybackControls()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleFormPath);
            Assert.That(prefab, Is.Not.Null, BattleFormPath);

            for (int slot = 1; slot <= 8; slot++)
            {
                AssertChild(prefab, $"Slot{slot:00}_Left");
                AssertChild(prefab, $"Slot{slot:00}_Right");
            }
            for (int row = 1; row <= 12; row++)
                AssertChild(prefab, $"Log{row:00}");

            string[] controls = { "Back", "PlayPause", "Speed1", "Speed2", "Speed4", "Skip", "Replay" };
            foreach (string control in controls)
                AssertChild(prefab, control);
        }

        [Test]
        public void BattleForm_HasCompleteSerializedBindingsAndGeneratedId()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleFormPath);
            MonoBehaviour form = prefab.GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().Name == "BattleForm");
            var serialized = new SerializedObject(form);

            string[] references =
            {
                "m_TitleText", "m_LeftNameText", "m_RightNameText",
                "m_LeftStatsText", "m_RightStatsText", "m_TickText",
                "m_CurrentEventText", "m_OutcomeText", "m_PageText",
                "m_PlayPauseText", "m_TimelineFill", "m_ErrorPanel", "m_ErrorText",
                "m_BackButton", "m_PlayPauseButton", "m_Speed1Button",
                "m_Speed2Button", "m_Speed4Button", "m_SkipButton",
                "m_ReplayButton", "m_PreviousPageButton", "m_NextPageButton",
            };
            foreach (string propertyName in references)
                AssertReference(serialized, propertyName);

            AssertArray(serialized, "m_LeftCards", 8);
            AssertArray(serialized, "m_RightCards", 8);
            AssertArray(serialized, "m_LogRows", 12);
            AssertArray(serialized, "m_FactTexts", 3);
            Assert.That(UIFormId.BattleForm, Is.EqualTo(103));
        }

        private static void AssertChild(GameObject prefab, string name)
        {
            bool found = prefab.GetComponentsInChildren<Transform>(true).Any(child => child.name == name);
            Assert.That(found, Is.True, name);
        }

        private static void AssertReference(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
        }

        private static void AssertArray(SerializedObject serialized, string propertyName, int expectedSize)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.arraySize, Is.EqualTo(expectedSize), propertyName);
            for (int index = 0; index < property.arraySize; index++)
            {
                Assert.That(
                    property.GetArrayElementAtIndex(index).objectReferenceValue,
                    Is.Not.Null,
                    $"{propertyName}[{index}]");
            }
        }
    }
}
