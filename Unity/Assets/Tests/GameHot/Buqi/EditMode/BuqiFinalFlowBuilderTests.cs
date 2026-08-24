using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Hot.Buqi.Battle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiFinalFlowBuilderTests
    {
        private GameObject m_Root;

        [SetUp]
        public void SetUp()
        {
            Type builder = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Game.Hot.Editor.BuqiFullUIBuilder", false))
                .FirstOrDefault(type => type != null);
            Assert.That(builder, Is.Not.Null, "BuqiFullUIBuilder");

            MethodInfo create = builder.GetMethod(
                "CreateFinalFlowStructure",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(create, Is.Not.Null, "CreateFinalFlowStructure");
            m_Root = create.Invoke(null, null) as GameObject;
            Assert.That(m_Root, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Root != null)
                UnityEngine.Object.DestroyImmediate(m_Root);
        }

        [Test]
        public void DailyCycle_IsFixedAtNineDaysWithFourRequiredOperations()
        {
            Transform dailyCycle = Required("DailyCycle");

            Assert.That(dailyCycle.GetComponentsInChildren<Transform>(true)
                .Count(child => child.name.StartsWith("DaySlot_", StringComparison.Ordinal)), Is.EqualTo(9));
            Assert.That(DirectChildNames(dailyCycle), Is.EquivalentTo(new[]
            {
                "DaySlot_01", "DaySlot_02", "DaySlot_03", "DaySlot_04", "DaySlot_05",
                "DaySlot_06", "DaySlot_07", "DaySlot_08", "DaySlot_09",
                "MorningOperation", "NoonOperation", "DuskPVE", "NightPVP",
            }));
        }

        [Test]
        public void OperationScreen_KeepsBoardVisibleAndHasExactlyThreeChoices()
        {
            Transform screen = Required("OperationScreen");

            Assert.That(DirectChildNames(screen), Does.Contain("Board"));
            Assert.That(DirectChildren(screen, "OperationChoice_").Count(), Is.EqualTo(3));
            Assert.That(screen.Find("Board").gameObject.activeSelf, Is.True);
            Assert.That(screen.Find("ConfigureBoardButton"), Is.Not.Null);
        }

        [Test]
        public void Bazaar_HasHoverProductsAndTopSellDropZoneWithoutLockOrSellButtons()
        {
            Transform screen = Required("BazaarScreen");
            Transform dropZone = screen.Find("SellDropZone");
            Transform[] products = DirectChildren(screen, "Product_").ToArray();

            Assert.That(dropZone, Is.Not.Null);
            Assert.That(products, Is.Not.Empty);
            Assert.That(products.All(product => product.GetComponent<EventTrigger>() != null), Is.True);
            Assert.That(products.All(product =>
                dropZone.GetComponent<RectTransform>().anchoredPosition.y >
                product.GetComponent<RectTransform>().anchoredPosition.y), Is.True);
            Assert.That(screen.GetComponentsInChildren<Transform>(true).Any(child =>
                child.name.IndexOf("LockButton", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("SellButton", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        [Test]
        public void PveSelection_HidesPhaseRailAndStorageAndHasThreeDifficultyCards()
        {
            Transform screen = Required("PVESelectionScreen");

            Assert.That(screen.Find("PhaseRail").gameObject.activeSelf, Is.False);
            Assert.That(screen.Find("Storage").gameObject.activeSelf, Is.False);
            Assert.That(DirectChildren(screen, "DifficultyCard_").Count(), Is.EqualTo(3));
        }

        [Test]
        public void BattleToolbar_ContainsOnlyOneXTwoXAndSkip()
        {
            Transform toolbar = Required("BattleToolbar");

            Assert.That(DirectChildNames(toolbar), Is.EquivalentTo(new[] { "Speed1x", "Speed2x", "Skip" }));
            Assert.That(Label(toolbar.Find("Speed1x")), Is.EqualTo("Buqi.Battle.Speed1x"));
            Assert.That(Label(toolbar.Find("Speed2x")), Is.EqualTo("Buqi.Battle.Speed2x"));
            Assert.That(Label(toolbar.Find("Skip")), Is.EqualTo("Buqi.Battle.SkipEnd"));
        }

        [Test]
        public void DayRecord_IsOptionalModalRatherThanMandatoryDailyStage()
        {
            Transform dailyCycle = Required("DailyCycle");

            Assert.That(Required("DayRecordButton"), Is.Not.Null);
            Assert.That(Label(Required("DayRecordButton")), Is.EqualTo("Buqi.RunShell.DayRecord"));
            Assert.That(Required("DayRecordModal").gameObject.activeSelf, Is.False);
            Assert.That(DirectChildNames(dailyCycle).Any(name =>
                name.IndexOf("DayRecord", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }

        [Test]
        public void Tribulation_FollowsDayNineWithThreeRoutesAndThreeStagesWithoutEchoHistory()
        {
            Transform routeScreen = Required("TribulationRouteScreen");
            Transform sequence = Required("TribulationSequence");

            Assert.That(DirectChildren(routeScreen, "RouteCard_").Count(), Is.EqualTo(3));
            Assert.That(routeScreen.GetComponentsInChildren<Transform>(true).Any(child =>
                child.name.IndexOf("Echo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("History", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(DirectChildNames(sequence), Is.EquivalentTo(new[]
            {
                "TribulationStage_01", "TribulationStage_02", "TribulationStage_03", "RunEnding",
            }));
        }

        private Transform Required(string name)
        {
            Transform result = m_Root.GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(child => child.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private static string[] DirectChildNames(Transform parent)
        {
            return parent.Cast<Transform>().Select(child => child.name).ToArray();
        }

        private static string Label(Transform parent)
        {
            return parent.GetComponentInChildren<Text>(true).text;
        }

        private static System.Collections.Generic.IEnumerable<Transform> DirectChildren(
            Transform parent,
            string prefix)
        {
            return parent.Cast<Transform>()
                .Where(child => child.name.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    public sealed class BuqiLocalizationPreservationBuilderTests
    {
        private const string TemporaryPrefabPath = "Assets/__BuqiTaskELocalization.prefab";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TemporaryPrefabPath);
        }

        [Test]
        public void BuilderRegeneration_PreservesExistingLocalizationComponentAndKey()
        {
            var existing = new GameObject("LocalizedPrefab", typeof(RectTransform));
            var existingTitle = new GameObject("Title_Text", typeof(RectTransform));
            existingTitle.transform.SetParent(existing.transform, false);
            var localization = existingTitle.AddComponent<Text>();
            localization.text = "Buqi.FinalFlow.Title";
            PrefabUtility.SaveAsPrefabAsset(existing, TemporaryPrefabPath);
            UnityEngine.Object.DestroyImmediate(existing);

            var generated = new GameObject("LocalizedPrefab", typeof(RectTransform));
            var generatedTitle = new GameObject("Title_Text", typeof(RectTransform));
            generatedTitle.transform.SetParent(generated.transform, false);
            generatedTitle.AddComponent<Text>();

            Type builder = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Game.Hot.Editor.BuqiFullUIBuilder", false))
                .FirstOrDefault(type => type != null);
            MethodInfo save = builder?.GetMethod("SavePrefab", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(save, Is.Not.Null, "SavePrefab");
            save.Invoke(null, new object[] { generated, TemporaryPrefabPath });

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemporaryPrefabPath);
            Text preserved = prefab.GetComponentInChildren<Text>(true);
            Assert.That(preserved, Is.Not.Null);
            Assert.That(preserved.text, Is.EqualTo("Buqi.FinalFlow.Title"));
        }
    }

    public sealed class BuqiBattleIntegrationBuilderTests
    {
        private GameObject m_Root;

        [Test]
        public void BattleFormIntegration_UsesSerializedCardArraysInsteadOfSyntheticSlotNames()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs");
            Assert.That(source, Does.Contain("m_LeftCards"));
            Assert.That(source, Does.Contain("m_RightCards"));
            Assert.That(source, Does.Not.Contain("\"Slot\" + slot.ToString(\"00\") + \"_Left\""));
        }

        [SetUp]
        public void SetUp()
        {
            Type builder = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Game.Hot.Editor.BuqiFullUIBuilder", false))
                .FirstOrDefault(type => type != null);
            MethodInfo create = builder?.GetMethod(
                "CreateBattleIntegrationStructure",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(create, Is.Not.Null, "CreateBattleIntegrationStructure");
            Assert.That(builder.GetMethod(
                "BuildBattleFormIntegration",
                BindingFlags.NonPublic | BindingFlags.Static), Is.Not.Null, "BuildBattleFormIntegration");
            m_Root = create.Invoke(null, null) as GameObject;
            Assert.That(m_Root, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Root != null)
                UnityEngine.Object.DestroyImmediate(m_Root);
        }

        [Test]
        public void BattleIntegration_HasEightFloatAnchorsPerSideAndOnlyApprovedToolbarControls()
        {
            Transform arena = m_Root.transform.Find("BattleArena");
            Assert.That(arena, Is.Not.Null);
            Assert.That(arena.Cast<Transform>().Count(child => child.name.EndsWith("_Left", StringComparison.Ordinal)), Is.EqualTo(BuqiBoardValidator.BoardSlotCount));
            Assert.That(arena.Cast<Transform>().Count(child => child.name.EndsWith("_Right", StringComparison.Ordinal)), Is.EqualTo(BuqiBoardValidator.BoardSlotCount));

            foreach (Transform card in arena)
            {
                Transform floatAnchor = card.Find("BattleFloatAnchor");
                Assert.That(floatAnchor, Is.Not.Null, card.name);
                CanvasGroup canvasGroup = floatAnchor.GetComponent<CanvasGroup>();
                Assert.That(canvasGroup, Is.Not.Null, card.name);
                Assert.That(canvasGroup.interactable, Is.False, card.name);
                Assert.That(canvasGroup.blocksRaycasts, Is.False, card.name);
            }

            Transform toolbar = m_Root.transform.Find("BattleToolbar");
            Assert.That(toolbar, Is.Not.Null);
            Assert.That(toolbar.Cast<Transform>().Select(child => child.name), Is.EquivalentTo(new[]
            {
                "Back", "Speed1", "Speed2", "Skip",
            }));
        }
    }
}
