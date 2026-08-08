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
            Assert.That(draggable.GetMethod("SetRaycastEnabled"), Is.Not.Null);
            Assert.That(slot.GetMethod("Render"), Is.Not.Null);
            Assert.That(slot.GetMethod("Clear"), Is.Not.Null);
            Assert.That(form.GetMethod("TryInitialize"), Is.Not.Null);
            Assert.That(form.GetMethod("SelectSource"), Is.Not.Null);
            Assert.That(form.GetMethod("MoveSelectedTo"), Is.Not.Null);
            Assert.That(form.GetMethod("ResetDeployment"), Is.Not.Null);
            Assert.That(form.GetMethod("TryConfirm"), Is.Not.Null);
            Assert.That(form.GetMethod("CancelSession"), Is.Not.Null);
            Assert.That(form.GetMethod(
                "SetItemRaycasts",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(openData.GetField("Catalog"), Is.Not.Null);
            Assert.That(openData.GetField("Board"), Is.Not.Null);
            Assert.That(openData.GetField("Storage"), Is.Not.Null);
            Assert.That(openData.GetField("Confirmed"), Is.Not.Null);
            Assert.That(openData.GetField("Round"), Is.Not.Null);
            Assert.That(openData.GetField("Coins"), Is.Not.Null);
            Assert.That(openData.GetField("Wins"), Is.Not.Null);
            Assert.That(openData.GetField("Lives"), Is.Not.Null);
            Assert.That(openData.GetField("OpponentName"), Is.Not.Null);
        }

        [Test]
        public void RunShell_ExposesDragDeployLaunchAndApplyHooks()
        {
            Type shell = RuntimeType("Game.Hot.Buqi.UI.BuqiRunShellForm");
            var flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;

            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.GetField("m_DemoCatalog", flags)?.FieldType,
                Is.EqualTo(typeof(BuqiUIDemoCatalog)));
            Assert.That(shell.GetMethod("OpenDragDeploy", flags), Is.Not.Null);
            System.Reflection.MethodInfo apply = shell.GetMethod("ApplyDeployment", flags);
            Assert.That(apply, Is.Not.Null);
            Assert.That(apply.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(BuqiDeploymentSnapshot) }));
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
        public void ClickFallback_OccupiedItemIsTreatedAsDestination()
        {
            var formObject = new GameObject("DragDeployFormTest");
            FormHandle form = FormHandle.Create(formObject);
            try
            {
                object data = CreateOpenData();
                SetBoardItem(data, 0, "item-s", "Short", 1);
                Assert.That(form.TryInitialize(data, out string error), Is.True, error);

                BuqiDeploymentSlotRef source = BuqiDeploymentSlotRef.Storage(0);
                form.SelectSource(source);
                form.ClickItem(BuqiDeploymentSlotRef.Board(0));

                Assert.That(form.SelectedSource, Is.EqualTo(source));
                Assert.That(form.View.StorageSlots[0], Is.EqualTo("item-m"));
                Assert.That(form.View.BoardSlots[0], Is.EqualTo("item-s"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(formObject);
            }
        }

        [Test]
        public void StorageHover_RendersIllegalAndLegalTargetStates()
        {
            var formObject = new GameObject("DragDeployFormTest");
            FormHandle form = FormHandle.Create(formObject);
            var states = new Text[8];
            var backgrounds = new Image[8];
            var slots = new BuqiDeploySlotWidget[8];
            try
            {
                for (int index = 0; index < slots.Length; index++)
                    slots[index] = CreateSlot(formObject.transform, index, out backgrounds[index], out states[index]);
                form.SetPrivateField("m_StorageSlots", slots);

                object data = CreateOpenData();
                SetBoardItem(data, 0, "item-s", "Short", 1);
                Assert.That(form.TryInitialize(data, out string error), Is.True, error);
                form.SelectSource(BuqiDeploymentSlotRef.Board(0));

                form.HoverSlot(BuqiDeploymentSlotRef.Storage(0), true);
                Assert.That(states[0].text, Does.StartWith("×"));
                Assert.That(backgrounds[0].color.r, Is.GreaterThan(backgrounds[0].color.g));

                form.HoverSlot(BuqiDeploymentSlotRef.Storage(1), true);
                Assert.That(states[1].text, Is.EqualTo("✓ 可放置"));
                Assert.That(backgrounds[1].color.g, Is.GreaterThan(backgrounds[1].color.r));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(formObject);
            }
        }

        [Test]
        public void Initialize_RejectsMissingConfirmationCallback()
        {
            var formObject = new GameObject("DragDeployFormTest");
            FormHandle form = FormHandle.Create(formObject);
            try
            {
                object data = CreateOpenData();
                data.GetType().GetField("Confirmed").SetValue(data, null);

                Assert.That(form.TryInitialize(data, out string error), Is.False);
                Assert.That(error, Is.EqualTo("拖拽上阵确认回调不可用"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(formObject);
            }
        }

        [Test]
        public void Header_RendersRunContext()
        {
            var formObject = new GameObject("DragDeployFormTest");
            FormHandle form = FormHandle.Create(formObject);
            Text contextText = CreateText(formObject.transform, "Context");
            try
            {
                form.SetPrivateField("m_ContextText", contextText);
                object data = CreateOpenData();
                SetOpenDataField(data, "Round", 3);
                SetOpenDataField(data, "Coins", 12);
                SetOpenDataField(data, "Wins", 4);
                SetOpenDataField(data, "Lives", 2);
                SetOpenDataField(data, "OpponentName", "清虚真人");

                Assert.That(form.TryInitialize(data, out string error), Is.True, error);
                Assert.That(contextText.text,
                    Is.EqualTo("第 3 回合  |  金币 12  |  胜场 4  |  生命 2  |  对手 清虚真人"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(formObject);
            }
        }

        [Test]
        public void StaticLabels_RestoreTextAfterBaseLocalizationMiss()
        {
            var formObject = new GameObject("DragDeployFormTest");
            FormHandle form = FormHandle.Create(formObject);
            Text missingKey = CreateText(formObject.transform, "MissingKey");
            Text dynamicText = CreateText(formObject.transform, "Dynamic");
            try
            {
                missingKey.text = "<NoKey>待上阵道具";
                dynamicText.text = "当前阵容";

                form.RestoreStaticLabels();

                Assert.That(missingKey.text, Is.EqualTo("待上阵道具"));
                Assert.That(dynamicText.text, Is.EqualTo("当前阵容"));
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

                System.Reflection.MethodInfo setRaycast = widget.GetType().GetMethod("SetRaycastEnabled");
                Assert.That(setRaycast, Is.Not.Null);
                setRaycast.Invoke(widget, new object[] { false });
                Assert.That(canvasGroup.blocksRaycasts, Is.False);
                setRaycast.Invoke(widget, new object[] { true });
                Assert.That(canvasGroup.blocksRaycasts, Is.True);

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
                Assert.That(stateText.text, Is.EqualTo("× 不可放置"));
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
                Assert.That(stateText.text, Is.EqualTo("✓ 可放置"));
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
            confirmed ??= _ => { };
            var catalog = new BuqiUIDemoCatalog();
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-s", Name = "Short", Size = 1 });
            catalog.Items.Add(new BuqiUIDemoItemDefinition { Id = "item-m", Name = "Middle", Size = 2 });
            var board = new List<BuqiDemoItemView>();
            for (int index = 0; index < 8; index++)
                board.Add(new BuqiDemoItemView { Empty = true, Slot = index });
            var storage = new List<BuqiDemoItemView>();
            for (int index = 0; index < 8; index++)
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

        private static void SetBoardItem(object openData, int slot, string id, string name, int size)
        {
            var board = (List<BuqiDemoItemView>)openData.GetType().GetField("Board").GetValue(openData);
            board[slot] = new BuqiDemoItemView
            {
                Id = id,
                Name = name,
                Size = size,
                Slot = slot,
            };
        }

        private static void SetOpenDataField(object openData, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = openData.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(openData, value);
        }

        private static BuqiDeploySlotWidget CreateSlot(
            Transform parent,
            int index,
            out Image background,
            out Text stateText)
        {
            var owner = new GameObject("StorageSlot_" + index, typeof(RectTransform), typeof(Image));
            owner.transform.SetParent(parent, false);
            var widget = owner.AddComponent<BuqiDeploySlotWidget>();
            background = owner.GetComponent<Image>();
            Text indexText = CreateText(owner.transform, "Index");
            Text itemText = CreateText(owner.transform, "Item");
            stateText = CreateText(owner.transform, "State");
            var invalidSymbol = new GameObject("InvalidSymbol");
            invalidSymbol.transform.SetParent(owner.transform, false);
            SetPrivate(widget, "m_Background", background);
            SetPrivate(widget, "m_IndexText", indexText);
            SetPrivate(widget, "m_ItemText", itemText);
            SetPrivate(widget, "m_StateText", stateText);
            SetPrivate(widget, "m_InvalidSymbol", invalidSymbol);
            return widget;
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

            public BuqiDeploymentSlotRef? SelectedSource =>
                (BuqiDeploymentSlotRef?)m_Type.GetField(
                    "m_SelectedSource",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(m_Component);

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

            public void ClickItem(BuqiDeploymentSlotRef source)
            {
                m_Type.GetMethod(
                    "OnItemClick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(m_Component, new object[] { source });
            }

            public void HoverSlot(BuqiDeploymentSlotRef target, bool isInside)
            {
                m_Type.GetMethod(
                    "OnSlotHover",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(m_Component, new object[] { target, isInside });
            }

            public void SetPrivateField(string fieldName, object value)
            {
                System.Reflection.FieldInfo field = m_Type.GetField(
                    fieldName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, fieldName);
                field.SetValue(m_Component, value);
            }

            public void RestoreStaticLabels()
            {
                System.Reflection.MethodInfo method = m_Type.GetMethod(
                    "RestoreStaticLabels",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                method.Invoke(m_Component, null);
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
