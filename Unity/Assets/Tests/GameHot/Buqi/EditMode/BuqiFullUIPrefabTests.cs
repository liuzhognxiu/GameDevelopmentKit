using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.UI.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiFullUIPrefabTests
    {
        private const string StageFolder = "Assets/Res/UI/UIPrefab/Buqi/Stages/";
        private const string ShellPath = "Assets/Res/UI/UIForm/Hot/Buqi/BuqiRunShellForm.prefab";

        private static readonly string[] stageNames =
        {
            "StarterSelectionWidget",
            "OpponentIntelWidget",
            "PreparationChoiceWidget",
            "ShopWidget",
            "EventWidget",
            "ModificationWidget",
            "BoardEditorWidget",
            "PredictionWidget",
            "BattleSummaryWidget",
            "RoundSettlementWidget",
            "RunTerminalWidget",
        };

        [Test]
        public void EveryNonBattleStage_HasStablePrefab()
        {
            foreach (string stageName in stageNames)
            {
                string path = StageFolder + stageName + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.name, Is.EqualTo(stageName));
                Assert.That(prefab.GetComponent<RectTransform>().rect.size,
                    Is.EqualTo(new Vector2(1112f, 824f)), stageName);
                Assert.That(prefab.GetComponents<MonoBehaviour>().Any(component => component.GetType().Name == stageName),
                    Is.True, stageName);
            }
        }

        [Test]
        public void RunShell_HasHeaderRailStageHostContextAndCommands()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath);

            Assert.That(prefab, Is.Not.Null, ShellPath);
            Assert.That(prefab.GetComponent<RectTransform>().rect.size, Is.EqualTo(new Vector2(1920f, 1080f)));
            AssertChild(prefab, "Header");
            AssertChild(prefab, "PhaseRail");
            AssertChild(prefab, "StageHost");
            AssertChild(prefab, "ContextRail");
            AssertChild(prefab, "CommandBar");
            Assert.That(Children(prefab, "PhaseStep").Count, Is.EqualTo(12));
            Assert.That(stageNames.All(name => Children(prefab, name).Count == 1), Is.True);
        }

        [TestCase("BuqiRunShellForm", 104)]
        [TestCase("BuqiItemDetailForm", 105)]
        [TestCase("BuqiConfirmForm", 106)]
        [TestCase("BuqiMessageForm", 107)]
        public void UIFormId_ContainsFullDemoForms(string fieldName, int expectedValue)
        {
            FieldInfo field = typeof(UIFormId).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);

            Assert.That(field, Is.Not.Null, fieldName);
            Assert.That(field.GetValue(null), Is.EqualTo(expectedValue));
        }

        [Test]
        public void BoardEditor_ExposesDragDeployCommand()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StageFolder + "BoardEditorWidget.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                IBuqiStageWidget stage = instance.GetComponents<MonoBehaviour>().OfType<IBuqiStageWidget>().Single();
                BuqiUIDemoCommand submitted = null;
                stage.Render(new BuqiUIDemoView
                {
                    Phase = BuqiUIDemoPhase.BoardEditor,
                    ContextTitle = "Board",
                    ContextBody = "Deploy",
                    BoardSlots = Enumerable.Range(0, 8)
                        .Select(slot => new BuqiDemoItemView { Empty = true, Slot = slot })
                        .ToList(),
                }, command => submitted = command);

                Button openButton = instance.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.gameObject.activeSelf &&
                        button.GetComponentInChildren<Text>(true)?.text == "拖拽上阵");
                Assert.That(openButton, Is.Not.Null);

                openButton.onClick.Invoke();

                Assert.That(submitted, Is.Not.Null);
                Assert.That(submitted.Type, Is.EqualTo(BuqiUIDemoCommandType.OpenDragDeploy));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void AssertChild(GameObject prefab, string name)
        {
            Assert.That(Children(prefab, name), Is.Not.Empty, name);
        }

        private static List<Transform> Children(GameObject prefab, string name)
        {
            return prefab.GetComponentsInChildren<Transform>(true)
                .Where(child => child.name.StartsWith(name))
                .ToList();
        }
    }
}
