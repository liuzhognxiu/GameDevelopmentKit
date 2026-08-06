using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.DemoUI;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.UI.Widgets;
using NUnit.Framework;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiDragDeployRuntimeTests
    {
        [Test]
        public void RuntimeTypes_ExposeRequiredPointerContracts()
        {
            Type draggable = RuntimeType("Game.Hot.Buqi.UI.Widgets.BuqiDraggableItemWidget");
            Type slot = RuntimeType("Game.Hot.Buqi.UI.Widgets.BuqiDeploySlotWidget");
            Type form = RuntimeType("Game.Hot.Buqi.UI.BuqiDragDeployForm");
            Type openData = RuntimeType("Game.Hot.Buqi.UI.BuqiDragDeployOpenData");

            Assert.That(draggable, Is.Not.Null);
            Assert.That(slot, Is.Not.Null);
            Assert.That(form, Is.Not.Null);
            Assert.That(openData, Is.Not.Null);
            Assert.That(new[]
            {
                typeof(IBeginDragHandler),
                typeof(IDragHandler),
                typeof(IEndDragHandler),
                typeof(IPointerClickHandler),
            }.All(contract => contract.IsAssignableFrom(draggable)), Is.True);
            Assert.That(new[]
            {
                typeof(IPointerEnterHandler),
                typeof(IPointerExitHandler),
                typeof(IPointerClickHandler),
                typeof(IDropHandler),
            }.All(contract => contract.IsAssignableFrom(slot)), Is.True);
            Assert.That(draggable.GetMethod("Render"), Is.Not.Null);
            Assert.That(draggable.GetMethod("Clear"), Is.Not.Null);
            Assert.That(slot.GetMethod("Render"), Is.Not.Null);
            Assert.That(slot.GetMethod("Clear"), Is.Not.Null);
            Assert.That(form.GetMethod("TryInitialize"), Is.Not.Null);
            Assert.That(form.GetMethod("SelectSource"), Is.Not.Null);
            Assert.That(form.GetMethod("MoveSelectedTo"), Is.Not.Null);
            Assert.That(form.GetMethod("ResetDeployment"), Is.Not.Null);
            Assert.That(form.GetMethod("TryConfirm"), Is.Not.Null);
            Assert.That(form.GetMethod("CancelSession"), Is.Not.Null);
            Assert.That(openData.GetField("Catalog"), Is.Not.Null);
            Assert.That(openData.GetField("Board"), Is.Not.Null);
            Assert.That(openData.GetField("Storage"), Is.Not.Null);
            Assert.That(openData.GetField("Confirmed"), Is.Not.Null);
        }

        [Test]
        public void ClickFallback_MovesItemAndResetRestoresOpeningView()
        {
            var formObject = new GameObject("DragDeployFormTest");
            FormHandle form = FormHandle.Create(formObject);
            try
            {
                Assert.That(form.TryInitialize(CreateOpenData(), out string error), Is.True, error);
                BuqiDeploymentSnapshot opening = form.View;

                form.SelectSource(BuqiDeploymentSlotRef.Storage(0));
                BuqiDeploymentCommandResult moved = form.MoveSelectedTo(BuqiDeploymentSlotRef.Board(2));

                Assert.That(moved.Accepted, Is.True, moved.Reason);
                Assert.That(form.View.BoardSlots[2], Is.EqualTo("item-m"));
                Assert.That(form.View.BoardSlots[3], Is.EqualTo("item-m"));
                Assert.That(form.View.StorageSlots[0], Is.Empty);

                form.ResetDeployment();

                Assert.That(form.View, Is.SameAs(opening));
                Assert.That(form.View.StorageSlots[0], Is.EqualTo("item-m"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(formObject);
            }
        }

        [Test]
        public void Confirm_InvokesCallbackOnceWithCurrentSnapshot()
        {
            var formObject = new GameObject("DragDeployFormTest");
            FormHandle form = FormHandle.Create(formObject);
            int callbackCount = 0;
            BuqiDeploymentSnapshot confirmed = null;
            try
            {
                Assert.That(form.TryInitialize(CreateOpenData(snapshot =>
                {
                    callbackCount++;
                    confirmed = snapshot;
                }), out string error), Is.True, error);
                form.SelectSource(BuqiDeploymentSlotRef.Storage(0));
                Assert.That(form.MoveSelectedTo(BuqiDeploymentSlotRef.Board(2)).Accepted, Is.True);

                Assert.That(form.TryConfirm(out error), Is.True, error);
                Assert.That(form.TryConfirm(out error), Is.False);

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(confirmed, Is.SameAs(form.View));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(formObject);
            }
        }

        [Test]
        public void Reinitialize_ReplacesOldCallbackAndClearsSelection()
        {
            var formObject = new GameObject("DragDeployFormTest");
            FormHandle form = FormHandle.Create(formObject);
            int oldCallbackCount = 0;
            int newCallbackCount = 0;
            try
            {
                Assert.That(form.TryInitialize(CreateOpenData(_ => oldCallbackCount++), out string error), Is.True, error);
                form.SelectSource(BuqiDeploymentSlotRef.Storage(0));
                Assert.That(form.TryInitialize(CreateOpenData(_ => newCallbackCount++), out error), Is.True, error);

                Assert.That(form.TryConfirm(out error), Is.True, error);

                Assert.That(oldCallbackCount, Is.EqualTo(0));
                Assert.That(newCallbackCount, Is.EqualTo(1));
                Assert.That(form.View.StorageSlots[0], Is.EqualTo("item-m"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(formObject);
            }
        }

        [Test]
        public void Cancel_ClearsCallbackAndRejectsLaterConfirmation()
        {
            var formObject = new GameObject("DragDeployFormTest");
            FormHandle form = FormHandle.Create(formObject);
            int callbackCount = 0;
            try
            {
                Assert.That(form.TryInitialize(CreateOpenData(_ => callbackCount++), out string error), Is.True, error);
                form.CancelSession();

                Assert.That(form.TryConfirm(out error), Is.False);
                Assert.That(callbackCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(formObject);
            }
        }

        [Test]
        public void DraggableItem_DisablesRaycastsDuringDragAndClearsCallbacks()
        {
            var owner = new GameObject("Draggable", typeof(RectTransform), typeof(CanvasGroup));
            var widget = owner.AddComponent<BuqiDraggableItemWidget>();
            CanvasGroup canvasGroup = owner.GetComponent<CanvasGroup>();
            int beginCount = 0;
            int endCount = 0;
            int clickCount = 0;
            try
            {
                widget.Render(
                    new BuqiUIDemoItemDefinition { Id = "item-s", Name = "Short", Size = 1 },
                    BuqiDeploymentSlotRef.Storage(0),
                    _ => clickCount++,
                    (_, __) => beginCount++,
                    _ => { },
                    (_, __) => endCount++);

                widget.OnBeginDrag(null);
                Assert.That(canvasGroup.blocksRaycasts, Is.False);
                widget.OnEndDrag(null);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);
                Assert.That(beginCount, Is.EqualTo(1));
                Assert.That(endCount, Is.EqualTo(1));

                widget.Clear();
                widget.OnPointerClick(null);
                Assert.That(clickCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DeploySlot_UsesTextSymbolAndColorForLegality()
        {
            var owner = new GameObject("Slot", typeof(RectTransform), typeof(Image));
            var widget = owner.AddComponent<BuqiDeploySlotWidget>();
            Image background = owner.GetComponent<Image>();
            Text indexText = CreateText(owner.transform, "Index");
            Text itemText = CreateText(owner.transform, "Item");
            Text stateText = CreateText(owner.transform, "State");
            var invalidSymbol = new GameObject("InvalidSymbol");
            invalidSymbol.transform.SetParent(owner.transform, false);
            try
            {
                SetPrivate(widget, "m_Background", background);
                SetPrivate(widget, "m_IndexText", indexText);
                SetPrivate(widget, "m_ItemText", itemText);
                SetPrivate(widget, "m_StateText", stateText);
                SetPrivate(widget, "m_InvalidSymbol", invalidSymbol);

                widget.Render(
                    BuqiDeploymentSlotRef.Board(2),
                    "Middle",
                    BuqiDeploySlotVisualState.Illegal,
                    string.Empty,
                    null,
                    null,
                    null);

                Assert.That(invalidSymbol.activeSelf, Is.True);
                Assert.That(stateText.text, Is.EqualTo("\u00D7 \u4E0D\u53EF\u653E\u7F6E"));
                Assert.That(background.color.r, Is.GreaterThan(background.color.g));

                widget.Render(
                    BuqiDeploymentSlotRef.Board(2),
                    "Middle",
                    BuqiDeploySlotVisualState.Legal,
                    string.Empty,
                    null,
                    null,
                    null);

                Assert.That(invalidSymbol.activeSelf, Is.False);
                Assert.That(stateText.text, Is.EqualTo("\u2713 \u53EF\u653E\u7F6E"));
                Assert.That(background.color.g, Is.GreaterThan(background.color.r));
                Assert.That(indexText.text, Is.EqualTo("03"));
                Assert.That(itemText.text, Is.EqualTo("Middle"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static object CreateOpenData(Action<BuqiDeploymentSnapshot> confirmed = null)
        {
            var catalog = new BuqiUIDemoCatalog();
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-s", Name = "Short", Size = 1 });
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-m", Name = "Middle", Size = 2 });
            var board = new List<BuqiDemoItemView>();
            for (int index = 0; index < 8; index++)
                board.Add(new BuqiDemoItemView { Empty = true, Slot = index });
            var storage = new List<BuqiDemoItemView>();
            for (int index = 0; index < 5; index++)
                storage.Add(new BuqiDemoItemView { Empty = true, Slot = index });
            storage[0] = new BuqiDemoItemView
            {
                Id = "item-m",
                Name = "Middle",
                Size = 2,
                Slot = 0,
            };
            Type openDataType = typeof(BuqiUIDemoController).Assembly
                .GetType("Game.Hot.Buqi.UI.BuqiDragDeployOpenData", true);
            object openData = Activator.CreateInstance(openDataType);
            openDataType.GetField("Catalog").SetValue(openData, catalog);
            openDataType.GetField("Board").SetValue(openData, board);
            openDataType.GetField("Storage").SetValue(openData, storage);
            openDataType.GetField("Confirmed").SetValue(openData, confirmed);
            return openData;
        }

        private sealed class FormHandle
        {
            private readonly Component m_Component;
            private readonly Type m_Type;

            private FormHandle(Component component)
            {
                m_Component = component;
                m_Type = component.GetType();
            }

            public BuqiDeploymentSnapshot View =>
                (BuqiDeploymentSnapshot)m_Type.GetProperty("View").GetValue(m_Component, null);

            public static FormHandle Create(GameObject owner)
            {
                Type formType = typeof(BuqiUIDemoController).Assembly
                    .GetType("Game.Hot.Buqi.UI.BuqiDragDeployForm", true);
                return new FormHandle(owner.AddComponent(formType));
            }

            public bool TryInitialize(object data, out string error)
            {
                object[] args = { data, null };
                bool accepted = (bool)m_Type.GetMethod("TryInitialize").Invoke(m_Component, args);
                error = args[1] as string;
                return accepted;
            }

            public void SelectSource(BuqiDeploymentSlotRef source)
            {
                m_Type.GetMethod("SelectSource").Invoke(m_Component, new object[] { source });
            }

            public BuqiDeploymentCommandResult MoveSelectedTo(BuqiDeploymentSlotRef target)
            {
                return (BuqiDeploymentCommandResult)m_Type.GetMethod("MoveSelectedTo")
                    .Invoke(m_Component, new object[] { target });
            }

            public void ResetDeployment()
            {
                m_Type.GetMethod("ResetDeployment").Invoke(m_Component, null);
            }

            public bool TryConfirm(out string error)
            {
                object[] args = { null };
                bool accepted = (bool)m_Type.GetMethod("TryConfirm").Invoke(m_Component, args);
                error = args[0] as string;
                return accepted;
            }

            public void CancelSession()
            {
                m_Type.GetMethod("CancelSession").Invoke(m_Component, null);
            }
        }

        private static Type RuntimeType(string fullName)
        {
            return typeof(BuqiUIDemoController).Assembly.GetType(fullName, false);
        }

        private static Text CreateText(Transform parent, string name)
        {
            var owner = new GameObject(name, typeof(RectTransform));
            owner.transform.SetParent(parent, false);
            return owner.AddComponent<Text>();
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
