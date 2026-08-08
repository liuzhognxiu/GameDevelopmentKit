using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiBazaarInteractionTests
    {
        private const string InteractionNamespace = "Game.Hot.Buqi.DemoUI.Interaction.";

        [Test]
        public void BazaarHasNeitherLockNorSellButtonAndLockedOfferRemainsBuyable()
        {
            Type interactionType = RuntimeType("BuqiBazaarInteraction");
            var interaction = Activator.CreateInstance(
                interactionType,
                new object[] { new BuqiRunEconomyService(TestCatalog.Create()) });
            var owner = new GameObject("Offer", typeof(RectTransform));
            var widget = owner.AddComponent<OfferCardWidget>();
            Button buyButton = CreateButton(owner.transform, "Buy");
            Button detailsButton = CreateButton(owner.transform, "Details");
            int buyCount = 0;
            int detailsCount = 0;
            try
            {
                SetPrivate(widget, "m_BuyButton", buyButton);
                SetPrivate(widget, "m_DetailsButton", detailsButton);
                widget.Render(
                    new BuqiDemoOfferView
                    {
                        Id = "offer-a",
                        Item = new BuqiDemoItemView { Id = "blade", Name = "Blade" },
                        Price = 4,
                        Locked = true,
                    },
                    _ => buyCount++,
                    _ => detailsCount++);

                buyButton.onClick.Invoke();
                detailsButton.onClick.Invoke();

                Assert.That(GetProperty<bool>(interaction, "HasLock"), Is.False);
                Assert.That(GetProperty<bool>(interaction, "HasSellButton"), Is.False);
                Assert.That(buyButton.interactable, Is.True);
                Assert.That(buyCount, Is.EqualTo(1));
                Assert.That(detailsCount, Is.EqualTo(0), "Details must not open from a click.");
                Assert.That(detailsButton.enabled, Is.False, "The obsolete button must not intercept long press input.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DetailTriggerUsesHoverAndLongPressButExposesNoClickContract()
        {
            Type triggerType = WidgetType("BuqiHoverDetailTrigger");
            Type sellZoneType = WidgetType("BuqiSellZoneWidget");

            Assert.That(typeof(IPointerEnterHandler).IsAssignableFrom(triggerType), Is.True);
            Assert.That(typeof(IPointerExitHandler).IsAssignableFrom(triggerType), Is.True);
            Assert.That(typeof(IPointerDownHandler).IsAssignableFrom(triggerType), Is.True);
            Assert.That(typeof(IPointerUpHandler).IsAssignableFrom(triggerType), Is.True);
            Assert.That(typeof(IPointerClickHandler).IsAssignableFrom(triggerType), Is.False);
            Assert.That(typeof(IPointerEnterHandler).IsAssignableFrom(sellZoneType), Is.True);
            Assert.That(typeof(IPointerExitHandler).IsAssignableFrom(sellZoneType), Is.True);
            Assert.That(typeof(IDropHandler).IsAssignableFrom(sellZoneType), Is.True);

            var owner = new GameObject("DetailTrigger", typeof(RectTransform));
            Component trigger = owner.AddComponent(triggerType);
            int showCount = 0;
            int hideCount = 0;
            try
            {
                triggerType.GetMethod("Bind").Invoke(
                    trigger,
                    new object[] { "blade", new Action<string>(_ => showCount++), new Action(() => hideCount++) });

                triggerType.GetMethod("OnPointerEnter").Invoke(trigger, new object[] { null });
                triggerType.GetMethod("OnPointerExit").Invoke(trigger, new object[] { null });
                Assert.That(showCount, Is.EqualTo(1));
                Assert.That(hideCount, Is.EqualTo(1));

                var touch = new PointerEventData(null) { pointerId = 0 };
                var mouse = new PointerEventData(null) { pointerId = -1 };
                triggerType.GetMethod("OnPointerDown").Invoke(trigger, new object[] { touch });
                triggerType.GetMethod("AdvancePress").Invoke(trigger, new object[] { 0.49f });
                Assert.That(showCount, Is.EqualTo(1));
                triggerType.GetMethod("AdvancePress").Invoke(trigger, new object[] { 0.01f });
                Assert.That(showCount, Is.EqualTo(2));
                triggerType.GetMethod("OnPointerUp").Invoke(trigger, new object[] { touch });
                Assert.That(hideCount, Is.EqualTo(2));

                triggerType.GetMethod("OnPointerDown").Invoke(trigger, new object[] { mouse });
                triggerType.GetMethod("AdvancePress").Invoke(trigger, new object[] { 0.5f });
                Assert.That(showCount, Is.EqualTo(2), "Mouse hold must not act as a mobile long press.");
                triggerType.GetMethod("OnPointerUp").Invoke(trigger, new object[] { mouse });

                triggerType.GetMethod("OnPointerDown").Invoke(trigger, new object[] { touch });
                triggerType.GetMethod("OnPointerExit").Invoke(trigger, new object[] { touch });
                triggerType.GetMethod("AdvancePress").Invoke(trigger, new object[] { 0.5f });
                Assert.That(showCount, Is.EqualTo(2), "A press that left the card must stay cancelled.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DetailTriggerCancelsTouchLongPressOnPointerExit()
        {
            Type triggerType = WidgetType("BuqiHoverDetailTrigger");
            var owner = new GameObject("DetailTriggerExit", typeof(RectTransform));
            Component trigger = owner.AddComponent(triggerType);
            int showCount = 0;
            try
            {
                triggerType.GetMethod("Bind").Invoke(
                    trigger,
                    new object[] { "blade", new Action<string>(_ => showCount++), new Action(() => { }) });
                var touch = new PointerEventData(null) { pointerId = 0 };

                triggerType.GetMethod("OnPointerDown").Invoke(trigger, new object[] { touch });
                triggerType.GetMethod("OnPointerExit").Invoke(trigger, new object[] { touch });
                triggerType.GetMethod("AdvancePress").Invoke(trigger, new object[] { 0.5f });

                Assert.That(showCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SellDragPreviewsRefundThenDropsAtomically()
        {
            var service = new BuqiRunEconomyService(TestCatalog.Create());
            object interaction = Activator.CreateInstance(RuntimeType("BuqiBazaarInteraction"), new object[] { service });
            BuqiRunEconomySnapshot source = CreateBoardState();

            object session = BeginSellDrag(interaction, source, "board-blade");
            session.GetType().GetMethod("SetOverSellZone").Invoke(session, new object[] { true });

            Assert.That(GetProperty<bool>(session, "Accepted"), Is.True);
            Assert.That(GetProperty<int>(session, "ExpectedRefund"), Is.EqualTo(3));
            Assert.That(GetProperty<bool>(session, "PreviewVisible"), Is.True);

            var result = (BuqiRunEconomyResult)session.GetType().GetMethod("Drop").Invoke(
                session,
                new object[] { source });

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.Snapshot.Run.Coins, Is.EqualTo(8));
            Assert.That(result.Snapshot.Items.ContainsKey("board-blade"), Is.False);
            Assert.That(result.Snapshot.Run.BoardInstanceIds.All(id => id != "board-blade"), Is.True);
            Assert.That(source.Run.Coins, Is.EqualTo(5));
            Assert.That(source.Items.ContainsKey("board-blade"), Is.True);
        }

        [Test]
        public void CancelAndStaleOrUnknownDropsNeverMutateEconomy()
        {
            var service = new BuqiRunEconomyService(TestCatalog.Create());
            object interaction = Activator.CreateInstance(RuntimeType("BuqiBazaarInteraction"), new object[] { service });
            BuqiRunEconomySnapshot source = CreateBoardState();

            object cancelled = BeginSellDrag(interaction, source, "board-blade");
            cancelled.GetType().GetMethod("SetOverSellZone").Invoke(cancelled, new object[] { true });
            cancelled.GetType().GetMethod("Cancel").Invoke(cancelled, Array.Empty<object>());
            var cancelledDrop = (BuqiRunEconomyResult)cancelled.GetType().GetMethod("Drop").Invoke(
                cancelled,
                new object[] { source });

            Assert.That(cancelledDrop.Success, Is.False);
            AssertEconomyEqual(cancelledDrop.Snapshot, source);
            AssertEconomyEqual(source, CreateBoardState());

            object stale = BeginSellDrag(interaction, source, "board-blade");
            stale.GetType().GetMethod("SetOverSellZone").Invoke(stale, new object[] { true });
            BuqiRunEconomySnapshot alreadySold = service.Sell(source, "board-blade").Snapshot;
            var staleDrop = (BuqiRunEconomyResult)stale.GetType().GetMethod("Drop").Invoke(
                stale,
                new object[] { alreadySold });
            Assert.That(staleDrop.Success, Is.False);
            AssertEconomyEqual(staleDrop.Snapshot, alreadySold);

            object unknown = BeginSellDrag(interaction, source, "missing");
            Assert.That(GetProperty<bool>(unknown, "Accepted"), Is.False);
            AssertEconomyEqual(source, CreateBoardState());
        }

        [Test]
        public void SellZoneReportsOnlyTheFirstCompletedDrop()
        {
            var service = new BuqiRunEconomyService(TestCatalog.Create());
            object interaction = Activator.CreateInstance(RuntimeType("BuqiBazaarInteraction"), new object[] { service });
            object session = BeginSellDrag(interaction, CreateBoardState(), "board-blade");
            Type sellZoneType = WidgetType("BuqiSellZoneWidget");
            var owner = new GameObject("SellZone", typeof(RectTransform));
            Component sellZone = owner.AddComponent(sellZoneType);
            int settledCount = 0;
            BuqiRunEconomyResult firstResult = null;
            try
            {
                sellZoneType.GetMethod("Bind").Invoke(
                    sellZone,
                    new object[]
                    {
                        session,
                        new Func<BuqiRunEconomySnapshot>(CreateBoardState),
                        new Action<BuqiRunEconomyResult>(result =>
                        {
                            settledCount++;
                            firstResult ??= result;
                        }),
                    });
                sellZoneType.GetMethod("OnPointerEnter").Invoke(sellZone, new object[] { null });
                sellZoneType.GetMethod("OnDrop").Invoke(sellZone, new object[] { null });
                sellZoneType.GetMethod("OnDrop").Invoke(sellZone, new object[] { null });

                Assert.That(firstResult.Success, Is.True, firstResult.FailureReason);
                Assert.That(settledCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static object BeginSellDrag(object interaction, BuqiRunEconomySnapshot source, string instanceId)
        {
            return interaction.GetType().GetMethod("BeginSellDrag").Invoke(
                interaction,
                new object[] { source, instanceId });
        }

        private static BuqiRunEconomySnapshot CreateBoardState()
        {
            BuqiRunEconomySnapshot state = BuqiRunEconomySnapshot.CreateInitial(1200);
            state.Run.Coins = 5;
            state.Run.BoardInstanceIds[0] = "board-blade";
            state.Items["board-blade"] = new BuqiRunItemInstance
            {
                InstanceId = "board-blade",
                DefinitionId = "blade",
                Quality = BuqiRunItemQuality.Common,
            };
            return state;
        }

        private static void AssertEconomyEqual(BuqiRunEconomySnapshot actual, BuqiRunEconomySnapshot expected)
        {
            Assert.That(actual.Run.Coins, Is.EqualTo(expected.Run.Coins));
            Assert.That(actual.Run.BoardInstanceIds, Is.EqualTo(expected.Run.BoardInstanceIds));
            Assert.That(actual.Run.StorageInstanceIds, Is.EqualTo(expected.Run.StorageInstanceIds));
            Assert.That(actual.Items.Keys, Is.EquivalentTo(expected.Items.Keys));
        }

        private static Type RuntimeType(string typeName)
        {
            Type type = typeof(BuqiRunEconomyService).Assembly.GetType(InteractionNamespace + typeName);
            Assert.That(type, Is.Not.Null, InteractionNamespace + typeName);
            return type;
        }

        private static Type WidgetType(string typeName)
        {
            Type type = typeof(OfferCardWidget).Assembly.GetType("Game.Hot.Buqi.UI.Widgets." + typeName);
            Assert.That(type, Is.Not.Null, typeName);
            return type;
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName).GetValue(target);
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var owner = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            owner.transform.SetParent(parent, false);
            return owner.GetComponent<Button>();
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class TestCatalog : IBuqiRunItemCatalog
        {
            public static TestCatalog Create()
            {
                return new TestCatalog();
            }

            public bool TryGet(string definitionId, out BuqiRunItemDefinition definition)
            {
                if (definitionId == "blade")
                {
                    definition = new BuqiRunItemDefinition
                    {
                        DefinitionId = "blade",
                        Size = 1,
                        BuyPrice = 6,
                        SellPrice = 3,
                        UpgradePrice = 6,
                        RefinementPrice = 6,
                    };
                    return true;
                }

                definition = null;
                return false;
            }
        }
    }
}
