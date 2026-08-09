using System.Collections.Generic;
using System.IO;
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
            "ShopWidget",
            "EventWidget",
            "BattleSummaryWidget",
            "RoundSettlementWidget",
            "RunTerminalWidget",
            "OperationChoiceWidget",
            "PveSelectionStageWidget",
            "TribulationRouteWidget",
            "TribulationStageWidget",
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
            Assert.That(Children(prefab, "PhaseStep").Count, Is.EqualTo(4));
            Assert.That(stageNames.All(name => Children(prefab, name).Count == 1), Is.True);
            Transform stageHost = prefab.transform.Find("StageHost");
            Assert.That(
                stageHost.Cast<Transform>().Select(child => child.name),
                Is.EquivalentTo(stageNames));
        }

        [Test]
        public void RunShell_HasDedicatedLocalizedDeploymentEntry()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath);
            Transform buttonTransform = prefab.transform.Find("CommandBar/ConfigureBoard");

            Assert.That(buttonTransform, Is.Not.Null);
            Button button = buttonTransform.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.GetComponentInChildren<Text>(true).text, Is.EqualTo("Buqi.Deploy.Title"));

            MonoBehaviour form = prefab.GetComponents<MonoBehaviour>().Single(component =>
                component.GetType().FullName == "Game.Hot.Buqi.UI.BuqiRunShellForm");
            SerializedProperty serializedButton = new SerializedObject(form).FindProperty("m_DeployButton");
            Assert.That(serializedButton, Is.Not.Null);
            Assert.That(serializedButton.objectReferenceValue, Is.SameAs(button));
        }

        [Test]
        public void RunShell_DeploymentEntryIsAvailableOutsideBattleAndSettlementLocks()
        {
            System.Type shellType = typeof(BuqiUIDemoController).Assembly.GetType(
                "Game.Hot.Buqi.UI.BuqiRunShellForm",
                true);
            MethodInfo canConfigure = shellType.GetMethod(
                "CanConfigureDeployment",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(canConfigure, Is.Not.Null);
            Assert.That(CanConfigure(BuqiUIDemoPhase.OperationChoice), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.Shop), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.Event), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.PveSelection), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.TribulationRoute), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.TribulationStage), Is.True);
            Assert.That(CanConfigure(BuqiUIDemoPhase.BattleReplay), Is.False);
            Assert.That(CanConfigure(BuqiUIDemoPhase.BattleSummary), Is.False);
            Assert.That(CanConfigure(BuqiUIDemoPhase.RoundSettlement), Is.False);
            Assert.That(CanConfigure(BuqiUIDemoPhase.RunTerminal), Is.False);

            bool CanConfigure(BuqiUIDemoPhase phase)
            {
                return (bool)canConfigure.Invoke(null, new object[]
                {
                    new BuqiUIDemoView { Phase = phase },
                });
            }
        }

        [Test]
        public void StageWithoutReadOnlyBoard_CanClear()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StageFolder + "ShopWidget.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                IBuqiStageWidget stage = instance.GetComponents<MonoBehaviour>().OfType<IBuqiStageWidget>().Single();
                Assert.DoesNotThrow(stage.Clear);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("OperationChoiceWidget")]
        [TestCase("PveSelectionStageWidget")]
        public void FinalFlowChoiceStages_ShowEightReadOnlyBoardSlots(string stageName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StageFolder + stageName + ".prefab");

            Assert.That(prefab, Is.Not.Null, stageName);
            List<Transform> slots = Children(prefab, "ReadOnlyBoardSlot");
            Assert.That(slots.Count, Is.EqualTo(8), stageName);
            Assert.That(slots.All(slot => slot.GetComponent<Button>() == null), Is.True, stageName);
        }

        [TestCase("OperationChoiceWidget", BuqiUIDemoPhase.OperationChoice)]
        [TestCase("PveSelectionStageWidget", BuqiUIDemoPhase.PveSelection)]
        public void FinalFlowChoiceStages_RenderCurrentBoard(string stageName, BuqiUIDemoPhase phase)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StageFolder + stageName + ".prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                IBuqiStageWidget stage = instance.GetComponents<MonoBehaviour>().OfType<IBuqiStageWidget>().Single();
                stage.Render(new BuqiUIDemoView
                {
                    Phase = phase,
                    ContextTitle = "Choice",
                    BoardSlots = Enumerable.Range(0, 8)
                        .Select(slot => new BuqiDemoItemView
                        {
                            Empty = slot != 0,
                            Name = slot == 0 ? "Test Blade" : string.Empty,
                            Slot = slot,
                        })
                        .ToList(),
                    Choices = new List<BuqiDemoChoiceView>(),
                }, _ => { });

                Text firstSlot = Children(instance, "ReadOnlyBoardSlot01").Single()
                    .GetComponentInChildren<Text>(true);
                Assert.That(firstSlot.text, Does.Contain("Test Blade"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void RunShell_StageComponentReferencesImplementStageContract()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath);
            MonoBehaviour form = prefab.GetComponents<MonoBehaviour>().Single(component =>
                component.GetType().FullName == "Game.Hot.Buqi.UI.BuqiRunShellForm");
            var serializedForm = new SerializedObject(form);
            SerializedProperty stages = serializedForm.FindProperty("m_StageComponents");

            Assert.That(stages, Is.Not.Null);
            Assert.That(stages.arraySize, Is.EqualTo(stageNames.Length));
            for (int index = 0; index < stages.arraySize; index++)
            {
                Object component = stages.GetArrayElementAtIndex(index).objectReferenceValue;
                Assert.That(component is IBuqiStageWidget, Is.True, stageNames[index]);
            }
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
        public void MainMenuStart_OpensRunShellInsteadOfStandaloneBattleSandbox()
        {
            string menuFormPath = Path.Combine(
                Application.dataPath,
                "Scripts/Game/Hot/Code/UI/MenuForm.cs");
            string source = File.ReadAllText(menuFormPath);

            Assert.That(
                source.IndexOf("OpenUIForm(UIFormId.BuqiRunShellForm)", System.StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0));
            Assert.That(
                source.IndexOf("OpenUIForm(UIFormId.BattleForm)", System.StringComparison.Ordinal),
                Is.LessThan(0));
        }

        [Test]
        public void RunShell_BattleReplayWaitsForConfirmedCloseBeforeAdvancing()
        {
            string shellFormPath = Path.Combine(
                Application.dataPath,
                "Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs");
            string source = File.ReadAllText(shellFormPath);
            int openMethodStart = source.IndexOf(
                "private void OpenBattleReplay()",
                System.StringComparison.Ordinal);
            int nextMethodStart = source.IndexOf(
                "private void CompleteBattleReplay()",
                openMethodStart,
                System.StringComparison.Ordinal);
            string openMethod = source.Substring(openMethodStart, nextMethodStart - openMethodStart);

            Assert.That(openMethodStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(nextMethodStart, Is.GreaterThan(openMethodStart));
            Assert.That(openMethod, Does.Contain("BattleReplayOpenData"));
            Assert.That(openMethod, Does.Contain("Confirmed = CompleteBattleReplay"));
            Assert.That(openMethod, Does.Not.Contain("m_Controller.Execute"));
        }

        [Test]
        public void RunShell_BackClosesInsteadOfSubmittingUnsupportedPreviousPhase()
        {
            string shellFormPath = Path.Combine(
                Application.dataPath,
                "Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs");
            string source = File.ReadAllText(shellFormPath);
            int methodStart = source.IndexOf("private void GoBack()", System.StringComparison.Ordinal);
            int methodEnd = source.IndexOf("private void Advance()", methodStart, System.StringComparison.Ordinal);
            string method = source.Substring(methodStart, methodEnd - methodStart);

            Assert.That(method, Does.Contain("Close();"));
            Assert.That(method, Does.Not.Contain("PreviousPhase"));
        }

        [Test]
        public void RunShell_ShopPurchaseRequiresConfirmationAndEmptyLabelsHidePrimaryButton()
        {
            string shellFormPath = Path.Combine(
                Application.dataPath,
                "Scripts/Game/Hot/Code/Buqi/UI/BuqiRunShellForm.cs");
            string source = File.ReadAllText(shellFormPath);

            Assert.That(source, Does.Contain("command.Type == BuqiUIDemoCommandType.BuyOffer"));
            Assert.That(source, Does.Contain("OpenPurchaseConfirmation(command)"));
            Assert.That(source, Does.Contain("OpenUIForm(UIFormId.BuqiConfirmForm"));
            Assert.That(source, Does.Contain("m_PrimaryButton.gameObject.SetActive(!string.IsNullOrEmpty(view.PrimaryCommandLabel))"));
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
