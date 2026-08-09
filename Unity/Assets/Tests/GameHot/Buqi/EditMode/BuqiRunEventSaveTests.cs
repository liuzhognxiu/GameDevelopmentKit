using System.Collections.Generic;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Encounter;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunEventSaveTests
    {
        [Test]
        public void CaptureAndRestore_SortsUnorderedDataAndPreservesFrozenEvent()
        {
            BuqiRunEventRuntimeState source = BuqiRunEventRuntimeState.CreateInitial(991L);
            source.Economy.Run.Day = 4;
            source.Economy.Run.Period = BuqiRunPeriod.NoonOperation;
            source.Flags.AddRange(new[] { "z.flag", "a.flag" });
            source.Counters.Add(new BuqiRunEventCounter { CounterId = "z.counter", Value = 2 });
            source.Counters.Add(new BuqiRunEventCounter { CounterId = "a.counter", Value = 1 });
            source.History.Add(new BuqiRunEventHistoryEntry
            {
                EventId = "z.event",
                Day = 3,
                ResolutionId = "z-resolution",
            });
            source.History.Add(new BuqiRunEventHistoryEntry
            {
                EventId = "a.event",
                Day = 2,
                ResolutionId = "a-resolution",
            });
            source.ScheduledReturns.Add(new BuqiRunScheduledReturn
            {
                ScheduleId = "z.return",
                EventId = "z.event",
                EarliestDay = 6,
                LatestDay = 7,
            });
            source.ScheduledReturns.Add(new BuqiRunScheduledReturn
            {
                ScheduleId = "a.return",
                EventId = "a.event",
                EarliestDay = 5,
                LatestDay = 6,
            });
            source.TemporaryModifiers.Add(new BuqiRunTemporaryModifier { ModifierId = "z.mod", RemainingBattles = 2 });
            source.TemporaryModifiers.Add(new BuqiRunTemporaryModifier { ModifierId = "a.mod", RemainingBattles = 1 });
            source.AppliedResolutions.Add(new BuqiRunResolutionRecord
            {
                ResolutionId = "z-resolution",
                SourceKind = "event",
                ContentId = "z.event",
                ChoiceId = "claim",
                RequestFingerprint = "z-fingerprint",
            });
            source.AppliedResolutions.Add(new BuqiRunResolutionRecord
            {
                ResolutionId = "a-resolution",
                SourceKind = "event",
                ContentId = "a.event",
                ChoiceId = "claim",
                RequestFingerprint = "a-fingerprint",
            });
            source.PendingEvent = new BuqiRunPendingEvent
            {
                EventId = "event.frozen",
                Day = 4,
                Period = BuqiRunPeriod.NoonOperation,
                OptionIds = new List<string> { "first", "second", "third" },
                RandomResults = new List<BuqiRunEventFrozenValue>
                {
                    new BuqiRunEventFrozenValue { ActionId = "z.action", Value = "z.item" },
                    new BuqiRunEventFrozenValue { ActionId = "a.action", Value = "a.item" },
                },
            };
            var codec = new BuqiRunEventSaveCodec();

            BuqiRunEventSaveData save = codec.Capture(source);
            bool restored = codec.TryRestore(source.Economy, save, out BuqiRunEventRuntimeState result, out string error);

            Assert.That(save.SchemaVersion, Is.EqualTo(BuqiRunEventSaveCodec.CurrentVersion));
            Assert.That(save.RunSeed, Is.EqualTo(source.Economy.Run.RunSeed));
            Assert.That(save.RngCursor, Is.EqualTo(source.Economy.Run.RngCursor));
            Assert.That(save.Revision, Is.EqualTo(source.Economy.Run.Revision));
            Assert.That(save.Flags, Is.EqualTo(new[] { "a.flag", "z.flag" }).AsCollection);
            Assert.That(save.Counters[0].CounterId, Is.EqualTo("a.counter"));
            Assert.That(save.History[0].ResolutionId, Is.EqualTo("a-resolution"));
            Assert.That(save.ScheduledReturns[0].ScheduleId, Is.EqualTo("a.return"));
            Assert.That(save.TemporaryModifiers[0].ModifierId, Is.EqualTo("a.mod"));
            Assert.That(save.AppliedResolutions[0].ResolutionId, Is.EqualTo("a-resolution"));
            Assert.That(save.PendingEvent.OptionIds, Is.EqualTo(new[] { "first", "second", "third" }).AsCollection);
            Assert.That(save.PendingEvent.RandomResults[0].ActionId, Is.EqualTo("a.action"));

            Assert.That(restored, Is.True, error);
            Assert.That(result.PendingEvent.EventId, Is.EqualTo("event.frozen"));
            Assert.That(result.PendingEvent.RandomResults[0].Value, Is.EqualTo("a.item"));
            Assert.That(result.Economy, Is.Not.SameAs(source.Economy));
            result.Flags.Add("mutated");
            Assert.That(save.Flags, Does.Not.Contain("mutated"));
        }

        [Test]
        public void SaveGraph_IsSerializableForUnityPersistence()
        {
            Assert.That(typeof(BuqiRunEventSaveData).IsSerializable, Is.True);
            Assert.That(typeof(BuqiRunPendingEvent).IsSerializable, Is.True);
            Assert.That(typeof(BuqiRunEventFrozenValue).IsSerializable, Is.True);
            Assert.That(typeof(BuqiRunEventCounter).IsSerializable, Is.True);
            Assert.That(typeof(BuqiRunEventHistoryEntry).IsSerializable, Is.True);
            Assert.That(typeof(BuqiRunScheduledReturn).IsSerializable, Is.True);
            Assert.That(typeof(BuqiRunTemporaryModifier).IsSerializable, Is.True);
            Assert.That(typeof(BuqiRunResolutionRecord).IsSerializable, Is.True);
        }

        [Test]
        public void TryRestore_MigratesLegacyMissingCollections()
        {
            var save = new BuqiRunEventSaveData
            {
                SchemaVersion = BuqiRunEventSaveCodec.LegacyVersion,
                Experience = 7,
                Flags = new List<string> { "legacy.flag" },
                TemporaryModifiers = null,
                AppliedResolutions = null,
                PendingEvent = null,
            };

            bool success = new BuqiRunEventSaveCodec().TryRestore(
                BuqiRunEventRuntimeState.CreateInitial(17L).Economy,
                save,
                out BuqiRunEventRuntimeState result,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(result.Experience, Is.EqualTo(7));
            Assert.That(result.Flags, Does.Contain("legacy.flag"));
            Assert.That(result.TemporaryModifiers, Is.Empty);
            Assert.That(result.AppliedResolutions, Is.Empty);
            Assert.That(result.PendingEvent.IsActive, Is.False);
        }

        [Test]
        public void TryRestore_RejectsDuplicateResolutionIds()
        {
            BuqiRunEventRuntimeState source = BuqiRunEventRuntimeState.CreateInitial(33L);
            BuqiRunEventSaveData save = new BuqiRunEventSaveCodec().Capture(source);
            save.AppliedResolutions.Add(new BuqiRunResolutionRecord
            {
                ResolutionId = "duplicate",
                SourceKind = "event",
                ContentId = "event.a",
                ChoiceId = "claim",
                RequestFingerprint = "duplicate-a",
            });
            save.AppliedResolutions.Add(new BuqiRunResolutionRecord
            {
                ResolutionId = "duplicate",
                SourceKind = "event",
                ContentId = "event.b",
                ChoiceId = "claim",
                RequestFingerprint = "duplicate-b",
            });

            bool success = new BuqiRunEventSaveCodec().TryRestore(
                source.Economy,
                save,
                out BuqiRunEventRuntimeState result,
                out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(result, Is.Null);
            Assert.That(source.Economy.Run.Revision, Is.EqualTo(0));
        }

        [Test]
        public void TryRestore_RejectsDifferentRunAndNullCurrentPendingCollections()
        {
            BuqiRunEventRuntimeState source = BuqiRunEventRuntimeState.CreateInitial(33L, "content-a");
            var codec = new BuqiRunEventSaveCodec();
            BuqiRunEventSaveData save = codec.Capture(source);

            bool wrongRun = codec.TryRestore(
                BuqiRunEventRuntimeState.CreateInitial(34L, "content-a").Economy,
                save,
                out _,
                out string wrongRunError);

            save.PendingEvent.OptionIds = null;
            bool malformed = codec.TryRestore(
                source.Economy,
                save,
                out _,
                out string malformedError);

            Assert.That(wrongRun, Is.False);
            Assert.That(wrongRunError, Is.Not.Empty);
            Assert.That(malformed, Is.False);
            Assert.That(malformedError, Is.Not.Empty);
        }
    }
}
