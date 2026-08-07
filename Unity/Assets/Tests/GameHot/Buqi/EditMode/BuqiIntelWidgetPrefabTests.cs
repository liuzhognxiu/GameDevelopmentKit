using System.Linq;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiIntelWidgetPrefabTests
    {
        private const string OpponentSnapshotPath = "Assets/Res/UI/UIPrefab/Buqi/OpponentSnapshotWidget.prefab";
        private const string FactRowPath = "Assets/Res/UI/UIPrefab/Buqi/FactRowWidget.prefab";

        [TestCase(OpponentSnapshotPath, "OpponentSnapshotWidget")]
        [TestCase(FactRowPath, "FactRowWidget")]
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
        public void OpponentSnapshotPrefab_HasFixedSizeAndCompleteBindings()
        {
            GameObject prefab = LoadPrefab(OpponentSnapshotPath);
            Assert.That(prefab.GetComponent<RectTransform>().rect.size, Is.EqualTo(new Vector2(456f, 292f)));

            OpponentSnapshotWidget widget = prefab.GetComponent<OpponentSnapshotWidget>();
            SerializedObject serialized = new SerializedObject(widget);
            AssertReference(serialized, "m_NameText");
            AssertReference(serialized, "m_BuildText");
            AssertReference(serialized, "m_SlotsText");
            AssertReference(serialized, "m_ThreatText");
            AssertReference(serialized, "m_RiskText");
            AssertReference(serialized, "m_StatusMarker");
            AssertArray(serialized, "m_ItemLabels", 3);
            AssertArray(serialized, "m_ItemButtons", 3);

            AssertChild(prefab, "OpponentName_Text");
            AssertChild(prefab, "Build_Text");
            AssertChild(prefab, "BoardSummary_Text");
            AssertChild(prefab, "Threat_Text");
            AssertChild(prefab, "Risk_Text");
            AssertChild(prefab, "KeyItem01");
            AssertChild(prefab, "KeyItem02");
            AssertChild(prefab, "KeyItem03");
        }

        [Test]
        public void FactRowPrefab_HasFixedSizeAndCompleteBindings()
        {
            GameObject prefab = LoadPrefab(FactRowPath);
            Assert.That(prefab.GetComponent<RectTransform>().rect.size, Is.EqualTo(new Vector2(456f, 68f)));

            FactRowWidget widget = prefab.GetComponent<FactRowWidget>();
            SerializedObject serialized = new SerializedObject(widget);
            AssertReference(serialized, "m_TitleText");
            AssertReference(serialized, "m_BodyText");
            AssertReference(serialized, "m_TickText");
            AssertReference(serialized, "m_Marker");
            AssertReference(serialized, "m_JumpButton");

            AssertChild(prefab, "Title_Text");
            AssertChild(prefab, "Body_Text");
            AssertChild(prefab, "Tick_Text");
            AssertChild(prefab, "Marker_Image");
            AssertChild(prefab, "JumpButton");
        }

        [Test]
        public void OpponentSnapshotClear_RemovesItemDetailsCallback()
        {
            GameObject instance = InstantiatePrefab(OpponentSnapshotPath);
            OpponentSnapshotWidget widget = instance.GetComponent<OpponentSnapshotWidget>();
            var opponent = new BuqiDemoOpponentView
            {
                Id = "echo-demo",
                Name = "教学对手快照",
                Build = "高速构筑",
                Items = new[]
                {
                    new BuqiDemoItemView
                    {
                        Id = "item-1",
                        Name = "测试装备",
                        Description = "攻击",
                        Size = 1,
                    },
                },
            };
            int callbackCount = 0;

            widget.Render(opponent, _ => callbackCount++);
            instance.GetComponentsInChildren<Button>(true).Single(button => button.name == "KeyItem01").onClick.Invoke();
            Assert.That(callbackCount, Is.EqualTo(1));

            widget.Clear();
            instance.GetComponentsInChildren<Button>(true).Single(button => button.name == "KeyItem01").onClick.Invoke();
            Assert.That(callbackCount, Is.EqualTo(1));

            Object.DestroyImmediate(instance);
        }

        [Test]
        public void FactRowClear_RemovesJumpCallback()
        {
            GameObject instance = InstantiatePrefab(FactRowPath);
            FactRowWidget widget = instance.GetComponent<FactRowWidget>();
            int callbackTick = -1;

            widget.Render(
                new BuqiDemoFactView
                {
                    Title = "输出贡献",
                    Body = "关键装备完成有效伤害",
                    Tick = 180,
                },
                tick => callbackTick = tick);
            instance.GetComponentInChildren<Button>(true).onClick.Invoke();
            Assert.That(callbackTick, Is.EqualTo(180));

            widget.Clear();
            callbackTick = -1;
            instance.GetComponentInChildren<Button>(true).onClick.Invoke();
            Assert.That(callbackTick, Is.EqualTo(-1));

            Object.DestroyImmediate(instance);
        }

        private static GameObject LoadPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            return prefab;
        }

        private static GameObject InstantiatePrefab(string path)
        {
            GameObject prefab = LoadPrefab(path);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
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
                    string.Format("{0}[{1}]", propertyName, index));
            }
        }
    }
}
