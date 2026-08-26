using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.UI;
using Game.Hot.Buqi.UI.Stages;
using Game.Hot.Buqi.UI.Widgets;
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
        public void BazaarOfferClickOnlyFixesPreviewAndNeverPurchases()
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

                for (int click = 0; click < 10; click++)
                {
                    buyButton.onClick.Invoke();
                    ((IPointerClickHandler)(object)widget).OnPointerClick(
                        new PointerEventData(null) { pointerId = -1 });
                }

                Assert.That(GetProperty<bool>(interaction, "HasLock"), Is.False);
                Assert.That(GetProperty<bool>(interaction, "HasSellButton"), Is.False);
                Assert.That(buyButton.interactable, Is.False);
                Assert.That(buyButton.gameObject.activeSelf, Is.False);
                Assert.That(buyCount, Is.EqualTo(0));
                Assert.That(detailsCount, Is.EqualTo(10), "Repeated clicks only fix the preview; they never purchase.");
                Assert.That(detailsButton.enabled, Is.False, "The obsolete button must not intercept long press input.");
                Assert.That(detailsButton.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ShopWidget_RendersInjectedConstrainedSupplyMetadata()
        {
            var owner = new GameObject("Shop", typeof(RectTransform));
            var widget = owner.AddComponent<ShopWidget>();
            Text title = CreateText(owner.transform, "Title");
            Text body = CreateText(owner.transform, "Body");
            Text meta = CreateText(owner.transform, "Meta");
            Button action = CreateButton(owner.transform, "Action");
            Text actionLabel = CreateText(action.transform, "Label");
            var source = new StubSupplyViewSource
            {
                Supply = new BuqiBazaarSupplyView
                {
                    MerchantName = "青篆客",
                    MerchantSpecialty = "阵器",
                    RefreshPrice = 7,
                    OfferRoles = new Dictionary<string, string>
                    {
                        ["blade"] = "破阵",
                    },
                },
            };

            try
            {
                SetPrivate(widget, "m_TitleText", title);
                SetPrivate(widget, "m_BodyText", body);
                SetPrivate(widget, "m_MetaText", meta);
                SetPrivate(widget, "m_ActionButtons", new[] { action });
                SetPrivate(widget, "m_ActionLabels", new[] { actionLabel });
                widget.BindSupplySource(source);

                widget.Render(new BuqiUIDemoView
                {
                    Phase = BuqiUIDemoPhase.Shop,
                    Coins = 19,
                    ShopOffers = new[]
                    {
                        new BuqiDemoOfferView
                        {
                            Id = "blade",
                            Item = new BuqiDemoItemView { Id = "blade", Name = "短刃" },
                            Price = 4,
                        },
                    },
                }, _ => { });

                Assert.That(source.ReadCount, Is.EqualTo(1));
                Assert.That(title.text, Does.Contain("青篆客"));
                Assert.That(body.text, Does.Contain("阵器"));
                Assert.That(meta.text, Does.Contain("刷新 7"));
                Assert.That(meta.text, Does.Contain("余额 19"));
                Assert.That(actionLabel.text, Is.Empty, "Offers are rendered as drag cards, never as BuyOffer actions.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BazaarWidgets_ExposeCommandDropBindingsForPersistentSale()
        {
            MethodInfo bindCommand = typeof(BuqiSellZoneWidget).GetMethod(
                "BindCommand",
                new[] { typeof(string), typeof(int), typeof(Action<string>) });
            FieldInfo sellZone = typeof(ShopWidget).GetField(
                "m_SellZone",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo boardItems = typeof(ShopWidget).GetField(
                "m_BoardItems",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(bindCommand, Is.Not.Null);
            Assert.That(sellZone?.FieldType, Is.EqualTo(typeof(BuqiSellZoneWidget)));
            Assert.That(boardItems?.FieldType, Is.EqualTo(typeof(BuqiDraggableItemWidget[])));
        }

        [Test]
        public void ShopOfferDropPolicyAcceptsOnlyContiguousEmptyBoardRange()
        {
            MethodInfo canDrop = typeof(ShopWidget).GetMethod(
                "CanDropOfferAt",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(canDrop, Is.Not.Null, "Shop needs one shared legality rule for preview and drop.");

            var board = Enumerable.Range(0, BuqiRunRules.BoardSlotCount)
                .Select(index => new BuqiDemoItemView
                {
                    Empty = index != 0,
                    Id = index == 0 ? "starter" : string.Empty,
                    Slot = index,
                })
                .ToArray();
            var offer = new BuqiDemoOfferView
            {
                Id = "wide-offer",
                Item = new BuqiDemoItemView { Id = "wide", Size = 2 },
            };

            Assert.That(canDrop.Invoke(null, new object[] { board, offer, 3 }), Is.True);
            Assert.That(canDrop.Invoke(null, new object[] { board, offer, 0 }), Is.False);
            Assert.That(canDrop.Invoke(null, new object[] { board, offer, 9 }), Is.False);
        }

        [Test]
        public void ShelfProjectionUsesTenSlotsAndPreservesAnchorSpans()
        {
            var offers = new[]
            {
                new BuqiDemoOfferView
                {
                    Id = "small",
                    Item = new BuqiDemoItemView { Id = "small", Size = 1 },
                    AnchorSlot = 0,
                },
                new BuqiDemoOfferView
                {
                    Id = "large",
                    Item = new BuqiDemoItemView { Id = "large", Size = 3 },
                    AnchorSlot = 1,
                },
            };

            IReadOnlyList<BuqiDemoOfferView> projected =
                BuqiBazaarShelfProjection.Project(offers, 10);

            Assert.That(projected.Count, Is.EqualTo(2));
            Assert.That(projected[0].AnchorSlot, Is.EqualTo(0));
            Assert.That(projected[0].Span, Is.EqualTo(1));
            Assert.That(projected[1].AnchorSlot, Is.EqualTo(1));
            Assert.That(projected[1].Span, Is.EqualTo(3));
            Assert.That(BuqiBazaarShelfProjection.ShelfSlotCount, Is.EqualTo(10));
        }

        [Test]
        public void ShelfProjectionClampsSuppliedCapacityToTenSlots()
        {
            var offers = new[]
            {
                new BuqiDemoOfferView
                {
                    Id = "outside",
                    Item = new BuqiDemoItemView { Id = "outside", Size = 1 },
                    AnchorSlot = 10,
                },
            };

            IReadOnlyList<BuqiDemoOfferView> projected =
                BuqiBazaarShelfProjection.Project(offers, 12);

            Assert.That(projected.Single().AnchorSlot, Is.LessThan(10));
            Assert.That(projected.Single().AnchorSlot + projected.Single().Span, Is.LessThanOrEqualTo(10));
        }

        [Test]
        public void ShopActionsNeverExposeBuyOfferCommands()
        {
            var owner = new GameObject("ShopActions", typeof(RectTransform));
            var widget = owner.AddComponent<ShopWidget>();
            try
            {
                Assert.That(widget, Is.InstanceOf<IPointerClickHandler>());
                var action = CreateButton(owner.transform, "Action");
                var label = CreateText(action.transform, "Label");
                SetPrivate(widget, "m_ActionButtons", new[] { action });
                SetPrivate(widget, "m_ActionLabels", new[] { label });
                widget.Render(new BuqiUIDemoView
                {
                    Phase = BuqiUIDemoPhase.Shop,
                    Coins = 10,
                    ShopOffers = new[]
                    {
                        new BuqiDemoOfferView
                        {
                            Id = "offer",
                            Item = new BuqiDemoItemView { Id = "item", Size = 1 },
                            Price = 4,
                        },
                    },
                }, _ => { });

                FieldInfo commands = typeof(BuqiStageWidgetBase).GetField(
                    "m_Commands", BindingFlags.Instance | BindingFlags.NonPublic);
                var values = (IEnumerable<BuqiUIDemoCommand>)commands.GetValue(widget);
                Assert.That(values.Any(command => command.Type == BuqiUIDemoCommandType.BuyOffer), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CommandSellZoneDropsOnlyOnceAndCancelDoesNotSubmit()
        {
            var owner = new GameObject("CommandSellZone", typeof(RectTransform));
            var sellZone = owner.AddComponent<BuqiSellZoneWidget>();
            var droppedIds = new List<string>();
            try
            {
                sellZone.BindCommand("board-blade", 3, droppedIds.Add);
                sellZone.OnPointerEnter(null);
                sellZone.OnDrop(null);
                sellZone.OnDrop(null);

                sellZone.BindCommand("board-shield", 2, droppedIds.Add);
                sellZone.Cancel();
                sellZone.OnPointerEnter(null);
                sellZone.OnDrop(null);

                Assert.That(droppedIds, Is.EqualTo(new[] { "board-blade" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ShopBoardDragDropsSellItemCommandWithInstanceId()
        {
            var owner = new GameObject("ShopSell", typeof(RectTransform));
            var widget = owner.AddComponent<ShopWidget>();
            var sellZoneOwner = new GameObject("SellZone", typeof(RectTransform));
            sellZoneOwner.transform.SetParent(owner.transform, false);
            var sellZone = sellZoneOwner.AddComponent<BuqiSellZoneWidget>();
            var itemOwner = new GameObject("BoardItem", typeof(RectTransform));
            itemOwner.transform.SetParent(owner.transform, false);
            var itemWidget = itemOwner.AddComponent<BuqiDraggableItemWidget>();
            BuqiUIDemoCommand submitted = null;
            try
            {
                SetPrivate(widget, "m_SellZone", sellZone);
                SetPrivate(widget, "m_BoardItems", new[] { itemWidget });
                widget.Render(new BuqiUIDemoView
                {
                    Phase = BuqiUIDemoPhase.Shop,
                    ShopOffers = Array.Empty<BuqiDemoOfferView>(),
                    BoardSlots = new[]
                    {
                        new BuqiDemoItemView
                        {
                            Id = "board-blade",
                            Name = "Blade",
                            Size = 1,
                            Price = 6,
                            Slot = 0,
                        },
                    },
                }, command => submitted = command);

                itemWidget.OnBeginDrag(null);
                sellZone.OnPointerEnter(null);
                sellZone.OnDrop(null);

                Assert.That(submitted, Is.Not.Null);
                Assert.That(submitted.Type, Is.EqualTo(BuqiUIDemoCommandType.SellItem));
                Assert.That(submitted.PrimaryId, Is.EqualTo("board-blade"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ShopOfferDragOntoLegalBoardSlotSubmitsTargetedPurchase()
        {
            var owner = new GameObject("ShopPurchase", typeof(RectTransform));
            var widget = owner.AddComponent<ShopWidget>();
            var cardOwner = new GameObject("Offer", typeof(RectTransform));
            cardOwner.transform.SetParent(owner.transform, false);
            var card = cardOwner.AddComponent<OfferCardWidget>();
            var slots = new BuqiDeploySlotWidget[BuqiRunRules.BoardSlotCount];
            BuqiUIDemoCommand submitted = null;
            try
            {
                for (int index = 0; index < slots.Length; index++)
                {
                    var slotOwner = new GameObject($"BoardSlot{index + 1}", typeof(RectTransform));
                    slotOwner.transform.SetParent(owner.transform, false);
                    slots[index] = slotOwner.AddComponent<BuqiDeploySlotWidget>();
                }

                Assert.That(card, Is.InstanceOf<IBeginDragHandler>());
                Assert.That(card, Is.InstanceOf<IEndDragHandler>());
                SetPrivate(widget, "m_OfferCards", new[] { card });
                SetPrivate(widget, "m_BoardDropSlots", slots);
                widget.Render(new BuqiUIDemoView
                {
                    Phase = BuqiUIDemoPhase.Shop,
                    Coins = 10,
                    ShopOffers = new[]
                    {
                        new BuqiDemoOfferView
                        {
                            Id = "item-02",
                            Item = new BuqiDemoItemView { Id = "item-02", Name = "Blade", Size = 1 },
                            Price = 2,
                        },
                    },
                    BoardSlots = Enumerable.Range(0, BuqiRunRules.BoardSlotCount)
                        .Select(index => new BuqiDemoItemView
                        {
                            Empty = index != 0,
                            Id = index == 0 ? "starter" : string.Empty,
                            Name = index == 0 ? "Starter" : string.Empty,
                            Size = 1,
                            Slot = index,
                        })
                        .ToArray(),
                }, command => submitted = command);

                ((IBeginDragHandler)(object)card).OnBeginDrag(null);
                slots[3].OnPointerEnter(null);
                slots[3].OnDrop(null);
                ((IEndDragHandler)(object)card).OnEndDrag(null);

                Assert.That(submitted, Is.Not.Null);
                Assert.That(submitted.Type, Is.EqualTo(BuqiUIDemoCommandType.BuyOffer));
                Assert.That(submitted.PrimaryId, Is.EqualTo("item-02"));
                Assert.That(submitted.Slot, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ShopBoardDragCanMoveOwnedItemWithinBoard()
        {
            var owner = new GameObject("ShopMove", typeof(RectTransform));
            var widget = owner.AddComponent<ShopWidget>();
            var sellZoneOwner = new GameObject("SellZone", typeof(RectTransform));
            sellZoneOwner.transform.SetParent(owner.transform, false);
            var sellZone = sellZoneOwner.AddComponent<BuqiSellZoneWidget>();
            var itemOwner = new GameObject("BoardItem", typeof(RectTransform));
            itemOwner.transform.SetParent(owner.transform, false);
            var itemWidget = itemOwner.AddComponent<BuqiDraggableItemWidget>();
            var slots = new BuqiDeploySlotWidget[BuqiRunRules.BoardSlotCount];
            BuqiUIDemoCommand submitted = null;
            try
            {
                for (int index = 0; index < slots.Length; index++)
                {
                    var slotOwner = new GameObject($"BoardSlot{index + 1}", typeof(RectTransform));
                    slotOwner.transform.SetParent(owner.transform, false);
                    slots[index] = slotOwner.AddComponent<BuqiDeploySlotWidget>();
                }

                SetPrivate(widget, "m_SellZone", sellZone);
                SetPrivate(widget, "m_BoardItems", new[] { itemWidget });
                SetPrivate(widget, "m_BoardDropSlots", slots);
                widget.Render(new BuqiUIDemoView
                {
                    Phase = BuqiUIDemoPhase.Shop,
                    BoardSlots = Enumerable.Range(0, BuqiRunRules.BoardSlotCount)
                        .Select(index => new BuqiDemoItemView
                        {
                            Id = index < 2 ? "owned-wide" : string.Empty,
                            Name = index < 2 ? "Wide" : string.Empty,
                            Empty = index >= 2,
                            Size = 2,
                            AnchorSlot = 0,
                            Slot = index,
                        })
                        .ToArray(),
                    StorageSlots = Array.Empty<BuqiDemoItemView>(),
                }, command => submitted = command);

                itemWidget.OnBeginDrag(null);
                slots[3].OnPointerEnter(null);
                slots[3].OnDrop(null);
                itemWidget.OnEndDrag(null);

                Assert.That(submitted, Is.Not.Null);
                Assert.That(submitted.Type, Is.EqualTo(BuqiUIDemoCommandType.ApplyDeployment));
                Assert.That(submitted.Deployment, Is.Not.Null);
                Assert.That(submitted.Deployment.BoardSlots, Is.EqualTo(new[]
                {
                    string.Empty, string.Empty, string.Empty, "owned-wide", "owned-wide",
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ShopOfferDropReportsInsufficientCoins()
        {
            var owner = new GameObject("ShopInsufficientCoins", typeof(RectTransform));
            var widget = owner.AddComponent<ShopWidget>();
            var cardOwner = new GameObject("Offer", typeof(RectTransform));
            cardOwner.transform.SetParent(owner.transform, false);
            var card = cardOwner.AddComponent<OfferCardWidget>();
            var slotOwner = new GameObject("BoardSlot", typeof(RectTransform));
            slotOwner.transform.SetParent(owner.transform, false);
            var slot = slotOwner.AddComponent<BuqiDeploySlotWidget>();
            Text stateText = CreateText(slotOwner.transform, "State");
            BuqiUIDemoCommand submitted = null;
            try
            {
                SetPrivate(widget, "m_OfferCards", new[] { card });
                SetPrivate(widget, "m_BoardDropSlots", new[] { slot });
                SetPrivate(slot, "m_StateText", stateText);
                widget.Render(new BuqiUIDemoView
                {
                    Phase = BuqiUIDemoPhase.Shop,
                    Coins = 1,
                    ShopOffers = new[]
                    {
                        new BuqiDemoOfferView
                        {
                            Id = "item-expensive",
                            Item = new BuqiDemoItemView { Id = "item-expensive", Name = "Expensive", Size = 1 },
                            Price = 2,
                        },
                    },
                    BoardSlots = new[] { new BuqiDemoItemView { Empty = true, Slot = 0 } },
                }, command => submitted = command);

                ((IBeginDragHandler)(object)card).OnBeginDrag(null);
                slot.OnPointerEnter(null);

                Assert.That(stateText.text, Does.Contain("金币不足"));
                slot.OnDrop(null);
                ((IEndDragHandler)(object)card).OnEndDrag(null);
                Assert.That(submitted, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ShopOfferDragOntoOccupiedBoardSlotDoesNotSubmitPurchase()
        {
            var owner = new GameObject("ShopPurchaseRejected", typeof(RectTransform));
            var widget = owner.AddComponent<ShopWidget>();
            var cardOwner = new GameObject("Offer", typeof(RectTransform));
            cardOwner.transform.SetParent(owner.transform, false);
            var card = cardOwner.AddComponent<OfferCardWidget>();
            var slotOwner = new GameObject("BoardSlot", typeof(RectTransform));
            slotOwner.transform.SetParent(owner.transform, false);
            var slot = slotOwner.AddComponent<BuqiDeploySlotWidget>();
            BuqiUIDemoCommand submitted = null;
            try
            {
                Assert.That(card, Is.InstanceOf<IBeginDragHandler>());
                Assert.That(card, Is.InstanceOf<IEndDragHandler>());
                SetPrivate(widget, "m_OfferCards", new[] { card });
                SetPrivate(widget, "m_BoardDropSlots", new[] { slot });
                widget.Render(new BuqiUIDemoView
                {
                    Phase = BuqiUIDemoPhase.Shop,
                    ShopOffers = new[]
                    {
                        new BuqiDemoOfferView
                        {
                            Id = "item-02",
                            Item = new BuqiDemoItemView { Id = "item-02", Name = "Blade", Size = 1 },
                            Price = 2,
                        },
                    },
                    BoardSlots = new[]
                    {
                        new BuqiDemoItemView { Id = "starter", Name = "Starter", Size = 1, Slot = 0 },
                    },
                }, command => submitted = command);

                ((IBeginDragHandler)(object)card).OnBeginDrag(null);
                slot.OnPointerEnter(null);
                slot.OnDrop(null);
                ((IEndDragHandler)(object)card).OnEndDrag(null);

                Assert.That(submitted, Is.Null);
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

        private static Text CreateText(Transform parent, string name)
        {
            var owner = new GameObject(name, typeof(RectTransform), typeof(Text));
            owner.transform.SetParent(parent, false);
            return owner.GetComponent<Text>();
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class StubSupplyViewSource : IBuqiBazaarSupplyViewSource
        {
            public BuqiBazaarSupplyView Supply;
            public int ReadCount;

            public bool TryGetCurrentSupply(out BuqiBazaarSupplyView supply)
            {
                ReadCount++;
                supply = Supply;
                return supply != null;
            }
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
