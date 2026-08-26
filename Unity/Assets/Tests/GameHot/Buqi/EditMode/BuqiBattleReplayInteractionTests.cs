using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.UI;
using Game.Hot.Buqi.UI.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiBattleReplayInteractionTests
    {
        [Test]
        public void PublicControls_ExposeOnlyOneTwoAndSkipToResult()
        {
            Type type = typeof(BattleReplayController);

            Assert.That(type.GetProperty("IsPaused", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(type.GetMethod("SetPaused", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(type.GetMethod("Replay", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(type.GetMethod("SkipToEnd", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(type.GetMethod("SkipToResult", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);

            var controller = new BattleReplayController(CreateReplayData());
            controller.SetSpeed(1);
            controller.SetSpeed(2);
            Assert.That(
                () => controller.SetSpeed(4),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void FeedbackEvents_AreDeterministicImmutableAndItemAnchored()
        {
            Type eventType = typeof(BuqiBattleSimulator).Assembly.GetType(
                "Game.Hot.Buqi.Battle.BattleReplayFeedbackEvent");
            Assert.That(eventType, Is.Not.Null);

            BattleReplayData replay = CreateFeedbackReplayData(out ItemInstance left, out ItemInstance right);
            IReadOnlyList<object> first = ReadFeedback(new BattleReplayController(replay));
            IReadOnlyList<object> second = ReadFeedback(new BattleReplayController(replay));

            Assert.That(first.Count, Is.EqualTo(4));
            Assert.That(first.Select(ReadSignature), Is.EqualTo(second.Select(ReadSignature)));
            Assert.That(first.Select(item => ReadField(item, "Kind").ToString()), Is.EqualTo(new[]
            {
                "Attack",
                "Damage",
                "Guard",
                "Heal",
            }));
            AssertFeedback(first[0], "Left", left.AnchorSlot, 9, 1f);
            AssertFeedback(first[1], "Right", right.AnchorSlot, 9, 1.05f);
            AssertFeedback(first[2], "Right", right.AnchorSlot, 6, 1.1f);
            AssertFeedback(first[3], "Right", right.AnchorSlot, 4, 1.15f);

            IList collection = (IList)ReadProperty(new BattleReplayController(replay), "FeedbackEvents");
            Assert.That(collection.IsReadOnly, Is.True);
            Assert.That(() => collection.Add(first[0]), Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void FeedbackEvents_ClassifyDeclarationsAndAnchorSideLevelDamageOnDefender()
        {
            BattleReplayData replay = CreateFeedbackReplayData(out ItemInstance left, out ItemInstance right);
            replay.Log = new[]
            {
                Event(0, BuqiEventType.Declare, right.InstanceId, right.InstanceId, 6, "guard", "BufferGain"),
                Event(1, BuqiEventType.Declare, right.InstanceId, right.InstanceId, 4, "heal", "Heal"),
                Event(2, BuqiEventType.Effect, left.InstanceId, string.Empty, 9, "attack", "Damage"),
            };
            replay.Result.BattleLogHash = BuqiCrypto.BattleLogHash(replay.Result, replay.Log.ToList());

            IReadOnlyList<object> feedback = ReadFeedback(new BattleReplayController(replay));

            Assert.That(feedback.Select(item => ReadField(item, "Kind").ToString()), Is.EqualTo(new[]
            {
                "Guard",
                "Heal",
                "Damage",
            }));
            AssertFeedback(feedback[0], "Right", right.AnchorSlot, 6, 1f);
            AssertFeedback(feedback[1], "Right", right.AnchorSlot, 4, 1.05f);
            AssertFeedback(feedback[2], "Right", right.AnchorSlot, 9, 1.1f);
        }

        [Test]
        public void FloatWidget_CanRenderFeedbackOverAnItemWithoutInteractionApis()
        {
            Type widgetType = typeof(BattleForm).Assembly.GetType(
                "Game.Hot.Buqi.UI.Widgets.BuqiBattleFloatWidget");

            Assert.That(widgetType, Is.Not.Null);
            Assert.That(
                widgetType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Any(method => method.Name == "Render" &&
                                   method.GetParameters().Length == 2 &&
                                   method.GetParameters()[0].ParameterType == typeof(BattleReplayFeedbackEvent)),
                Is.True);
            Assert.That(
                typeof(BattleReplayController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Any(method => method.Name.IndexOf("Move", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   method.Name.IndexOf("Replace", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void FloatWidget_StacksConcurrentFeedbackForTheSameItem()
        {
            var root = new GameObject("FloatWidgetTest");
            var textObject = new GameObject("ValueText");
            textObject.transform.SetParent(root.transform, false);
            try
            {
                var widget = root.AddComponent<BuqiBattleFloatWidget>();
                var valueText = textObject.AddComponent<Text>();
                SetField(widget, "m_ValueText", valueText);
                var feedback = new[]
                {
                    Feedback(1, BattleReplayFeedbackKind.Damage, 7, 1f),
                    Feedback(2, BattleReplayFeedbackKind.Guard, 5, 1.05f),
                    Feedback(3, BattleReplayFeedbackKind.Heal, 3, 1.1f),
                };

                widget.Render(feedback, 1.2f);

                Assert.That(valueText.text.Split('\n'), Has.Length.EqualTo(3));
                Assert.That(valueText.text, Does.Contain("-7"));
                Assert.That(valueText.text, Does.Contain("+5"));
                Assert.That(valueText.text, Does.Contain("+3"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BattleForm_ConfirmsOnlyReadyReplayAndCachesFeedbackTraversalState()
        {
            Type formType = typeof(BattleForm);
            MethodInfo gate = formType.GetMethod(
                "ShouldConfirmReplay",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(gate, Is.Not.Null);
            Assert.That(gate.Invoke(null, new object[] { false, false }), Is.False);
            Assert.That(gate.Invoke(null, new object[] { true, false }), Is.False);
            Assert.That(gate.Invoke(null, new object[] { false, true }), Is.False);
            Assert.That(gate.Invoke(null, new object[] { true, true }), Is.True);
            Assert.That(
                formType.GetField("m_LeftFeedbackBuckets", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                formType.GetField("m_RightFeedbackBuckets", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                formType.GetField("m_FeedbackCursor", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null);
        }

        private static BattleReplayData CreateReplayData()
        {
            IItemDefinitionProvider provider = BuqiTestSuite.CreateFixtureProvider();
            BattleRequest request = BuqiTestSuite.CreateVectors()[0].Request;
            BattleResult result = BuqiBattleSimulator.Simulate(
                request, provider, out List<BattleEvent> log, out _, out _);
            return new BattleReplayData
            {
                LeftBuild = request.Left,
                RightBuild = request.Right,
                Result = result,
                Log = log,
                Definitions = provider,
            };
        }

        private static BattleReplayData CreateFeedbackReplayData(
            out ItemInstance left,
            out ItemInstance right)
        {
            BattleReplayData replay = CreateReplayData();
            left = replay.LeftBuild.Items[0];
            right = replay.RightBuild.Items[0];
            BattleResult baseline = replay.Result;
            replay.Result = new BattleResult
            {
                RuleVersion = BuqiBattleSimulator.RuleVersion,
                SimulationVersion = BuqiBattleSimulator.SimulationVersion,
                ContentVersion = baseline.ContentVersion,
                LeftSnapshotHash = baseline.LeftSnapshotHash,
                RightSnapshotHash = baseline.RightSnapshotHash,
                DurationTicks = 100,
            };
            replay.Effects = new Dictionary<string, BattleReplayEffectInfo>(StringComparer.Ordinal)
            {
                ["attack"] = new BattleReplayEffectInfo
                {
                    EffectId = "attack",
                    Effect = Game.Hot.Buqi.Battle.BuqiEffect.Damage,
                    Target = Game.Hot.Buqi.Battle.BuqiTarget.EnemyExecution,
                },
                ["guard"] = new BattleReplayEffectInfo
                {
                    EffectId = "guard",
                    Effect = Game.Hot.Buqi.Battle.BuqiEffect.Buffer,
                    Target = Game.Hot.Buqi.Battle.BuqiTarget.Self,
                },
                ["heal"] = new BattleReplayEffectInfo
                {
                    EffectId = "heal",
                    Effect = Game.Hot.Buqi.Battle.BuqiEffect.Heal,
                    Target = Game.Hot.Buqi.Battle.BuqiTarget.Self,
                },
            };
            replay.Log = new[]
            {
                Event(0, BuqiEventType.Declare, left.InstanceId, right.InstanceId, 9, "attack", "Damage"),
                Event(1, BuqiEventType.Effect, left.InstanceId, right.InstanceId, 9, "attack", "Damage"),
                Event(2, BuqiEventType.Effect, right.InstanceId, right.InstanceId, 6, "guard", "BufferGain"),
                Event(3, BuqiEventType.Effect, right.InstanceId, right.InstanceId, 4, "heal", "Heal"),
            };
            replay.Result.BattleLogHash = BuqiCrypto.BattleLogHash(replay.Result, replay.Log.ToList());
            return replay;
        }

        private static BattleEvent Event(
            int sequence,
            BuqiEventType type,
            string source,
            string target,
            int amount,
            string effectId,
            string reason)
        {
            return new BattleEvent
            {
                Sequence = sequence,
                Tick = 10,
                Phase = type == BuqiEventType.Declare ? BuqiEventPhase.Declare : BuqiEventPhase.Aggregate,
                ActorInstanceId = source,
                SourceInstanceId = source,
                TargetInstanceId = target,
                Type = type,
                Amount = amount,
                EffectId = effectId,
                ReasonCode = reason,
            };
        }

        private static BattleReplayFeedbackEvent Feedback(
            int sequence,
            BattleReplayFeedbackKind kind,
            int value,
            float startSeconds)
        {
            return new BattleReplayFeedbackEvent(
                sequence,
                kind,
                BattleReplayFeedbackSide.Left,
                0,
                value,
                startSeconds,
                0.8f);
        }

        private static IReadOnlyList<object> ReadFeedback(BattleReplayController controller)
        {
            return ((IEnumerable)ReadProperty(controller, "FeedbackEvents")).Cast<object>().ToArray();
        }

        private static string ReadSignature(object feedback)
        {
            return string.Join(
                "|",
                ReadField(feedback, "Sequence"),
                ReadField(feedback, "Kind"),
                ReadField(feedback, "Side"),
                ReadField(feedback, "Slot"),
                ReadField(feedback, "Value"),
                ReadField(feedback, "StartSeconds"),
                ReadField(feedback, "DurationSeconds"));
        }

        private static void AssertFeedback(
            object feedback,
            string side,
            int slot,
            int value,
            float startSeconds)
        {
            Assert.That(ReadField(feedback, "Side").ToString(), Is.EqualTo(side));
            Assert.That(ReadField(feedback, "Slot"), Is.EqualTo(slot));
            Assert.That(ReadField(feedback, "Value"), Is.EqualTo(value));
            Assert.That(ReadField(feedback, "StartSeconds"), Is.EqualTo(startSeconds).Within(0.0001f));
            Assert.That(ReadField(feedback, "DurationSeconds"), Is.GreaterThan(0f));
        }

        private static object ReadProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing property {target.GetType().Name}.{name}");
            return property.GetValue(target);
        }

        private static object ReadField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{name}");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }
    }
}
