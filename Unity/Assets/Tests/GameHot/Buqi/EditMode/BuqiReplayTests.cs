using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Hot.Buqi.Battle;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiReplayTests
    {
        [Test]
        public void ReplayController_IsAvailableInBattleAssembly()
        {
            Type controllerType = typeof(BuqiBattleSimulator).Assembly.GetType(
                "Game.Hot.Buqi.Battle.BattleReplayController");

            Assert.That(controllerType, Is.Not.Null);
        }

        [TestCase("BattleReplayData")]
        [TestCase("BattleReplayFrame")]
        [TestCase("BattleReplaySideFrame")]
        [TestCase("BattleReplayItemFrame")]
        public void ReplayModel_IsAvailableInBattleAssembly(string typeName)
        {
            Type modelType = typeof(BuqiBattleSimulator).Assembly.GetType(
                $"Game.Hot.Buqi.Battle.{typeName}");

            Assert.That(modelType, Is.Not.Null, typeName);
        }

        [Test]
        public void ReplayOpenData_ConfirmOnceInvokesOwnerOnlyOnce()
        {
            int confirmationCount = 0;
            var openData = new BattleReplayOpenData
            {
                Replay = CreateReplayData(out _),
                Confirmed = () => confirmationCount++,
            };

            openData.ConfirmOnce();
            openData.ConfirmOnce();

            Assert.That(confirmationCount, Is.EqualTo(1));
        }

        [Test]
        public void InitialFrame_ComesFromBuildSnapshots()
        {
            BattleReplayData data = CreateReplayData(out BattleRequest request);

            object controller = Activator.CreateInstance(typeof(BattleReplayController), data);
            object frame = GetPublicProperty(controller, "Frame");
            object left = GetPublicField(frame, "Left");

            Assert.That(GetPublicField(frame, "Tick"), Is.EqualTo(0));
            Assert.That(GetPublicField(left, "Execution"), Is.EqualTo(request.Left.InitialExecution));
            Assert.That(GetPublicField(left, "Buffer"), Is.EqualTo(request.Left.InitialBuffer));
            Assert.That(GetPublicField(left, "Noise"), Is.EqualTo(request.Left.InitialNoiseDebt));
            Assert.That(((IReadOnlyList<string>)GetPublicField(left, "Slots")).Count, Is.EqualTo(8));
            Assert.That(GetPublicField(frame, "CurrentEvent"), Is.Null);
        }

        [Test]
        public void Playback_SpeedAndSkipUseRecordedLog()
        {
            BattleReplayData data = CreateReplayData(out _);
            var controller = new BattleReplayController(data);

            InvokePublicMethod(controller, "SetSpeed", 2);
            InvokePublicMethod(controller, "Advance", 1f);
            Assert.That(controller.Frame.Tick, Is.EqualTo(Math.Min(20, data.Result.DurationTicks)));

            InvokePublicMethod(controller, "SkipToResult");
            Assert.That(controller.Frame.IsFinished, Is.True);
            Assert.That(controller.Frame.Left.Execution, Is.EqualTo(data.Result.LeftExecution));
            Assert.That(controller.Frame.Right.Execution, Is.EqualTo(data.Result.RightExecution));
            Assert.That(controller.Frame.Left.Buffer, Is.EqualTo(data.Result.LeftBuffer));
            Assert.That(controller.Frame.Right.Buffer, Is.EqualTo(data.Result.RightBuffer));
            Assert.That(controller.Frame.Left.Noise, Is.EqualTo(data.Result.LeftNoise));
            Assert.That(controller.Frame.Right.Noise, Is.EqualTo(data.Result.RightNoise));
        }

        [Test]
        public void FilteringFactsAndPaging_DoNotMutateFrame()
        {
            BattleReplayData data = CreateReplayData(out _);
            var controller = new BattleReplayController(data);
            controller.Advance(1f);
            int tick = controller.Frame.Tick;
            int execution = controller.Frame.Left.Execution;

            Type filterType = typeof(BuqiBattleSimulator).Assembly.GetType(
                "Game.Hot.Buqi.Battle.BattleReplayFilter");
            Assert.That(filterType, Is.Not.Null);
            object filter = Activator.CreateInstance(filterType);
            SetPublicField(filter, "ReasonCode", "Damage");
            InvokePublicMethod(controller, "SetFilter", filter);
            object page = InvokePublicMethod(controller, "GetLogPage", 0);
            object rows = GetPublicField(page, "Rows");

            Assert.That(controller.Frame.Tick, Is.EqualTo(tick));
            Assert.That(controller.Frame.Left.Execution, Is.EqualTo(execution));
            Assert.That(rows, Is.Not.Null);
            Assert.That(((System.Collections.ICollection)rows).Count, Is.LessThanOrEqualTo(12));

            object facts = InvokePublicMethod(controller, "GetFacts");
            Assert.That(((System.Collections.ICollection)facts).Count, Is.EqualTo(3));
            foreach (object fact in (System.Collections.IEnumerable)facts)
                Assert.That(((System.Collections.ICollection)GetPublicField(fact, "EventSequences")).Count, Is.GreaterThan(0));
        }

        [Test]
        public void CooldownProgress_UsesRecordedDeclareTicks()
        {
            BattleReplayData data = CreateReplayData(out _);
            BattleEvent firstDeclare = null;
            BattleEvent secondDeclare = null;
            foreach (BattleEvent battleEvent in data.Log)
            {
                if (battleEvent.Type != BuqiEventType.Declare || string.IsNullOrEmpty(battleEvent.ActorInstanceId))
                    continue;
                if (firstDeclare == null)
                {
                    firstDeclare = battleEvent;
                    continue;
                }
                if (battleEvent.ActorInstanceId == firstDeclare.ActorInstanceId && battleEvent.Tick > firstDeclare.Tick)
                {
                    secondDeclare = battleEvent;
                    break;
                }
            }

            Assert.That(firstDeclare, Is.Not.Null);
            Assert.That(secondDeclare, Is.Not.Null);
            int midpoint = (firstDeclare.Tick + secondDeclare.Tick) / 2;
            var controller = new BattleReplayController(data);
            controller.Advance(midpoint * 0.1f);
            BattleReplayItemFrame item = FindItem(controller.Frame, firstDeclare.ActorInstanceId);

            Assert.That(item.Cooldown01, Is.GreaterThan(0f));
            Assert.That(item.Cooldown01, Is.LessThan(1f));
        }

        private static BattleReplayData CreateReplayData(out BattleRequest request)
        {
            IItemDefinitionProvider provider = BuqiTestSuite.CreateFixtureProvider();
            request = BuqiTestSuite.CreateVectors()[0].Request;
            BattleResult result = BuqiBattleSimulator.Simulate(
                request, provider, out List<BattleEvent> log, out _, out _);
            return new BattleReplayData
            {
                Title = "Replay Test",
                LeftName = "Left",
                RightName = "Right",
                LeftBuild = request.Left,
                RightBuild = request.Right,
                Result = result,
                Log = log,
                Definitions = provider,
            };
        }

        private static void SetPublicField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Missing public field {target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }

        private static object GetPublicField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Missing public field {target.GetType().Name}.{name}");
            return field.GetValue(target);
        }

        private static object GetPublicProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing public property {target.GetType().Name}.{name}");
            return property.GetValue(target);
        }

        private static object InvokePublicMethod(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"Missing public method {target.GetType().Name}.{name}");
            return method.Invoke(target, arguments);
        }

        private static BattleReplayItemFrame FindItem(BattleReplayFrame frame, string instanceId)
        {
            foreach (BattleReplayItemFrame item in frame.Left.Items)
                if (item.InstanceId == instanceId)
                    return item;
            foreach (BattleReplayItemFrame item in frame.Right.Items)
                if (item.InstanceId == instanceId)
                    return item;
            Assert.Fail($"Item was not found: {instanceId}");
            return null;
        }
    }
}
