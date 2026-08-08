using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Encounter;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunEncounterTests
    {
        [Test]
        public void SameSeedAndCursorProduceSameSequence()
        {
            int leftCursor = 0;
            int rightCursor = 0;

            for (int index = 0; index < 20; index++)
            {
                Assert.That(
                    BuqiRunRandom.Next(12345, ref leftCursor, 17),
                    Is.EqualTo(BuqiRunRandom.Next(12345, ref rightCursor, 17)),
                    $"Mismatch at draw {index}.");
            }

            Assert.That(leftCursor, Is.EqualTo(20));
            Assert.That(rightCursor, Is.EqualTo(20));
        }

        [Test]
        public void InvalidRangeDoesNotAdvanceCursor()
        {
            int cursor = 4;

            Assert.Throws<ArgumentOutOfRangeException>(() => BuqiRunRandom.Next(10, ref cursor, 0));
            Assert.That(cursor, Is.EqualTo(4));
        }

        [Test]
        public void EncounterContractsExistInGameHotAssembly()
        {
            Type gameHotAssemblyAnchor = typeof(BuqiRunState);

            // Assert.Multiple(() =>
            // {
            //     Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEncounterKind"), Is.Not.Null);
            //     Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEncounterState"), Is.Not.Null);
            //     Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEncounterDelta"), Is.Not.Null);
            //     Assert.That(FindEncounterType(gameHotAssemblyAnchor, "IBuqiRunEncounterCatalog"), Is.Not.Null);
            //     Assert.That(FindEncounterType(gameHotAssemblyAnchor, "IBuqiRunEventCatalog"), Is.Not.Null);
            //     Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEncounterService"), Is.Not.Null);
            //     Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEventResolver"), Is.Not.Null);
            // });
            Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEncounterKind"), Is.Not.Null);
            Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEncounterState"), Is.Not.Null);
            Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEncounterDelta"), Is.Not.Null);
            Assert.That(FindEncounterType(gameHotAssemblyAnchor, "IBuqiRunEncounterCatalog"), Is.Not.Null);
            Assert.That(FindEncounterType(gameHotAssemblyAnchor, "IBuqiRunEventCatalog"), Is.Not.Null);
            Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEncounterService"), Is.Not.Null);
            Assert.That(FindEncounterType(gameHotAssemblyAnchor, "BuqiRunEventResolver"), Is.Not.Null);
        }

        [Test]
        public void TryGetOrCreate_UsesSharedDeterministicSelectionAndSurfacesBothEncounterKinds()
        {
            var service = new BuqiRunEncounterService(CreateEncounterCatalog());
            var kinds = new List<BuqiRunEncounterKind>();

            foreach (long seed in new long[] { 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L })
            {
                BuqiRunState run = BuqiRunState.CreateInitial(seed);

                Assert.That(service.TryGetOrCreate(run, null, out BuqiRunEncounterState encounter, out string error), Is.True, error);

                int expectedCursor = run.RngCursor;
                BuqiRunEncounterKind expectedKind =
                    BuqiRunRandom.Next(run.RunSeed, ref expectedCursor, 2) == 0
                        ? BuqiRunEncounterKind.Shop
                        : BuqiRunEncounterKind.Event;

                Assert.That(encounter.Kind, Is.EqualTo(expectedKind), $"Unexpected kind for seed {seed}.");
                kinds.Add(encounter.Kind);
            }

            Assert.That(kinds, Does.Contain(BuqiRunEncounterKind.Shop));
            Assert.That(kinds, Does.Contain(BuqiRunEncounterKind.Event));
        }

        [Test]
        public void TryGetOrCreate_FreezesUnresolvedEncounterAndRepeatedReadsDoNotAdvanceCursor()
        {
            var service = new BuqiRunEncounterService(CreateEncounterCatalog());
            BuqiRunState run = BuqiRunState.CreateInitial(812345L);

            Assert.That(service.TryGetOrCreate(run, null, out BuqiRunEncounterState created, out string error), Is.True, error);

            BuqiRunEncounterState snapshot = created.Clone();

            Assert.That(service.TryGetOrCreate(run, created, out BuqiRunEncounterState replayed, out string replayError), Is.True, replayError);

            AssertEncounterState(replayed, snapshot);
            AssertEncounterState(created, snapshot);
            Assert.That(run.RngCursor, Is.EqualTo(0));
        }

        [Test]
        public void TryGetOrCreate_RejectsResolvedOrStaleCurrentWithoutAdvancingCursor()
        {
            var service = new BuqiRunEncounterService(CreateEncounterCatalog());
            BuqiRunState run = BuqiRunState.CreateInitial(812345L);
            run.RngCursor = 6;
            BuqiRunEncounterState current = CreateFrozenEventEncounter();
            current.Day = run.Day;
            current.EncounterIndex = run.EncounterIndex;
            current.Resolved = true;

            Assert.That(service.TryGetOrCreate(run, current, out _, out _), Is.False);
            Assert.That(run.RngCursor, Is.EqualTo(6));

            current.Resolved = false;
            current.Day++;
            Assert.That(service.TryGetOrCreate(run, current, out _, out _), Is.False);
            Assert.That(run.RngCursor, Is.EqualTo(6));
        }

        [Test]
        public void TryGetOrCreate_AdvancesCursorAcrossThreeDailyEncountersAndEmbedsDayAndIndexInIds()
        {
            var service = new BuqiRunEncounterService(CreateEncounterCatalog());
            BuqiRunState run = BuqiRunState.CreateInitial(1337L);
            run.Day = 4;

            var encounterIds = new HashSet<string>();
            int previousCursor = run.RngCursor;

            for (int index = 0; index < BuqiRunRules.EncountersPerDay; index++)
            {
                run.EncounterIndex = index;

                Assert.That(service.TryGetOrCreate(run, null, out BuqiRunEncounterState encounter, out string error), Is.True, error);

                Assert.That(encounter.EncounterId, Does.Contain("day-4"));
                Assert.That(encounter.EncounterId, Does.Contain($"enc-{index}"));
                Assert.That(encounterIds.Add(encounter.EncounterId), Is.True, $"Encounter id {encounter.EncounterId} should be unique.");
                Assert.That(encounter.NextRngCursor, Is.GreaterThan(previousCursor));
                Assert.That(encounter.CandidateIds, Is.Not.Empty);
                Assert.That(encounter.CandidateIds.Distinct().Count(), Is.EqualTo(encounter.CandidateIds.Count));

                if (encounter.Kind == BuqiRunEncounterKind.Shop)
                {
                    Assert.That(encounter.CandidateIds, Has.Count.EqualTo(4));
                }
                else
                {
                    Assert.That(encounter.CandidateIds, Has.Count.EqualTo(3));
                }

                previousCursor = encounter.NextRngCursor;
                run.RngCursor = encounter.NextRngCursor;
            }
        }

        [Test]
        public void TryGetOrCreate_ReturnsAllAvailableShopOffersWhenPoolIsSmallerThanDefaultLimit()
        {
            long seed = FindSeedForFirstEncounter(BuqiRunEncounterKind.Shop);
            var service = new BuqiRunEncounterService(new TestEncounterCatalog(
                new[] { "shop-a", "shop-b", "shop-c" },
                new[] { "event-a", "event-b", "event-c" }));

            Assert.That(
                service.TryGetOrCreate(BuqiRunState.CreateInitial(seed), null, out BuqiRunEncounterState encounter, out string error),
                Is.True,
                error);

            Assert.That(encounter.Kind, Is.EqualTo(BuqiRunEncounterKind.Shop));
            Assert.That(encounter.CandidateIds, Is.EqualTo(new[] { "shop-a", "shop-b", "shop-c" }).AsCollection);
        }

        [Test]
        public void TryGetOrCreate_ReturnsAllAvailableEventsWhenPoolIsSmallerThanDefaultLimit()
        {
            long seed = FindSeedForFirstEncounter(BuqiRunEncounterKind.Event);
            var service = new BuqiRunEncounterService(new TestEncounterCatalog(
                new[] { "shop-a", "shop-b", "shop-c", "shop-d" },
                new[] { "event-a", "event-b" }));

            Assert.That(
                service.TryGetOrCreate(BuqiRunState.CreateInitial(seed), null, out BuqiRunEncounterState encounter, out string error),
                Is.True,
                error);

            Assert.That(encounter.Kind, Is.EqualTo(BuqiRunEncounterKind.Event));
            Assert.That(encounter.CandidateIds, Is.EqualTo(new[] { "event-a", "event-b" }).AsCollection);
        }

        [TestCase(0, 3)]
        [TestCase(4, 0)]
        public void TryGetOrCreate_FailsClosedWhenRequiredPoolsAreEmptyAndDoesNotAdvanceCursor(
            int shopCount,
            int eventCount)
        {
            var service = new BuqiRunEncounterService(new TestEncounterCatalog(
                CreateSequentialIds("shop", shopCount),
                CreateSequentialIds("event", eventCount)));

            BuqiRunState run = BuqiRunState.CreateInitial(77L);
            run.RngCursor = 5;

            Assert.That(service.TryGetOrCreate(run, null, out BuqiRunEncounterState encounter, out string error), Is.False);
            Assert.That(encounter, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(run.RngCursor, Is.EqualTo(5));
        }

        [Test]
        public void TryGetOrCreate_RejectsInvalidPhaseAndEncounterIndexWithoutAdvancingCursor()
        {
            var service = new BuqiRunEncounterService(CreateEncounterCatalog());
            BuqiRunState run = BuqiRunState.CreateInitial(99L);
            run.RngCursor = 8;
            run.Phase = BuqiRunPhase.PveBattle;

            Assert.That(service.TryGetOrCreate(run, null, out _, out _), Is.False);
            Assert.That(run.RngCursor, Is.EqualTo(8));

            run.Phase = BuqiRunPhase.Encounter;
            run.EncounterIndex = BuqiRunRules.EncountersPerDay;
            Assert.That(service.TryGetOrCreate(run, null, out _, out _), Is.False);
            Assert.That(run.RngCursor, Is.EqualTo(8));
        }

        [Test]
        public void TryGetOrCreate_DeduplicatesAndDropsBlankCandidateIds()
        {
            long seed = FindSeedForFirstEncounter(BuqiRunEncounterKind.Shop);
            var service = new BuqiRunEncounterService(new TestEncounterCatalog(
                new[] { "shop-a", "", " ", "shop-a", "shop-b", "shop-c", "shop-b", "shop-d" },
                new[] { "event-a", "event-b", "event-c" }));

            Assert.That(service.TryGetOrCreate(BuqiRunState.CreateInitial(seed), null,
                out BuqiRunEncounterState encounter, out string error), Is.True, error);
            Assert.That(encounter.CandidateIds, Is.EqualTo(new[] { "shop-a", "shop-b", "shop-c", "shop-d" }).AsCollection);
            Assert.That(encounter.CandidateIds.Any(string.IsNullOrWhiteSpace), Is.False);
        }

        [TestCase("event-coins", 5, 0, "", "")]
        [TestCase("event-life", 0, 2, "", "")]
        [TestCase("event-item", 0, 0, "blade.alpha", "")]
        [TestCase("event-refine", 0, 0, "", "refine.steel")]
        public void TryResolve_ReturnsExplicitDeltaAndMarksEncounterResolved(
            string eventId,
            int expectedCoins,
            int expectedLives,
            string expectedItemDefinitionId,
            string expectedRefinementId)
        {
            var resolver = new BuqiRunEventResolver(CreateEventCatalog());
            BuqiRunEncounterState encounter = CreateFrozenEventEncounter();
            BuqiRunEncounterState sourceSnapshot = encounter.Clone();

            Assert.That(
                resolver.TryResolve(encounter, eventId, out BuqiRunEncounterState resolved, out BuqiRunEncounterDelta delta, out string error),
                Is.True,
                error);

            Assert.That(delta.Coins, Is.EqualTo(expectedCoins));
            Assert.That(delta.Lives, Is.EqualTo(expectedLives));
            Assert.That(delta.GrantedItemDefinitionId, Is.EqualTo(expectedItemDefinitionId));
            Assert.That(delta.GrantedRefinementId, Is.EqualTo(expectedRefinementId));
            Assert.That(resolved.Resolved, Is.True);
            Assert.That(resolved.SelectedChoiceId, Is.EqualTo(eventId));
            Assert.That(resolved.ResolutionId, Is.EqualTo($"{encounter.EncounterId}:{eventId}"));
            AssertEncounterState(encounter, sourceSnapshot);
        }

        [Test]
        public void TryResolve_RejectsChoiceOutsideFrozenCandidateListWithoutMutatingSource()
        {
            var resolver = new BuqiRunEventResolver(CreateEventCatalog(includeHiddenChoice: true));
            BuqiRunEncounterState encounter = CreateFrozenEventEncounter();
            BuqiRunEncounterState sourceSnapshot = encounter.Clone();

            Assert.That(
                resolver.TryResolve(encounter, "event-hidden", out BuqiRunEncounterState resolved, out BuqiRunEncounterDelta delta, out string error),
                Is.False);

            Assert.That(resolved, Is.Null);
            Assert.That(delta, Is.Null);
            Assert.That(error, Is.Not.Empty);
            AssertEncounterState(encounter, sourceSnapshot);
        }

        [Test]
        public void TryResolve_RejectsEmptyChoiceIdWithoutMutatingSource()
        {
            var resolver = new BuqiRunEventResolver(CreateEventCatalog());
            BuqiRunEncounterState encounter = CreateFrozenEventEncounter();
            BuqiRunEncounterState sourceSnapshot = encounter.Clone();

            Assert.That(
                resolver.TryResolve(encounter, string.Empty, out BuqiRunEncounterState resolved, out BuqiRunEncounterDelta delta, out string error),
                Is.False);

            Assert.That(resolved, Is.Null);
            Assert.That(delta, Is.Null);
            Assert.That(error, Is.Not.Empty);
            AssertEncounterState(encounter, sourceSnapshot);
        }

        [Test]
        public void TryResolve_RejectsAlreadyResolvedEncounterWithoutMutatingSource()
        {
            var resolver = new BuqiRunEventResolver(CreateEventCatalog());
            BuqiRunEncounterState encounter = CreateFrozenEventEncounter();
            encounter.Resolved = true;
            encounter.SelectedChoiceId = "event-coins";
            encounter.ResolutionId = $"{encounter.EncounterId}:event-coins";
            BuqiRunEncounterState sourceSnapshot = encounter.Clone();

            Assert.That(
                resolver.TryResolve(encounter, "event-life", out BuqiRunEncounterState resolved, out BuqiRunEncounterDelta delta, out string error),
                Is.False);

            Assert.That(resolved, Is.Null);
            Assert.That(delta, Is.Null);
            Assert.That(error, Is.Not.Empty);
            AssertEncounterState(encounter, sourceSnapshot);
        }

        [Test]
        public void TryResolve_RejectsDuplicateResolutionWithoutMutatingResolvedSource()
        {
            var resolver = new BuqiRunEventResolver(CreateEventCatalog());
            BuqiRunEncounterState encounter = CreateFrozenEventEncounter();

            Assert.That(
                resolver.TryResolve(encounter, "event-item", out BuqiRunEncounterState resolved, out BuqiRunEncounterDelta delta, out string error),
                Is.True,
                error);
            Assert.That(delta.GrantedItemDefinitionId, Is.EqualTo("blade.alpha"));

            BuqiRunEncounterState resolvedSnapshot = resolved.Clone();

            Assert.That(
                resolver.TryResolve(resolved, "event-item", out BuqiRunEncounterState duplicate, out BuqiRunEncounterDelta duplicateDelta, out string duplicateError),
                Is.False);

            Assert.That(duplicate, Is.Null);
            Assert.That(duplicateDelta, Is.Null);
            Assert.That(duplicateError, Is.Not.Empty);
            AssertEncounterState(resolved, resolvedSnapshot);
        }

        [Test]
        public void TryResolve_RejectsNullDeltaWithoutMutatingSource()
        {
            var resolver = new BuqiRunEventResolver(new NullDeltaEventCatalog("event-item"));
            BuqiRunEncounterState encounter = CreateFrozenEventEncounter();
            BuqiRunEncounterState snapshot = encounter.Clone();

            Assert.That(resolver.TryResolve(encounter, "event-item", out BuqiRunEncounterState resolved,
                out BuqiRunEncounterDelta delta, out string error), Is.False);
            Assert.That(resolved, Is.Null);
            Assert.That(delta, Is.Null);
            Assert.That(error, Is.Not.Empty);
            AssertEncounterState(encounter, snapshot);
        }

        private static Type FindEncounterType(Type assemblyAnchor, string shortName)
        {
            return assemblyAnchor.Assembly.GetType($"Game.Hot.Buqi.Run.Encounter.{shortName}");
        }

        private static TestEncounterCatalog CreateEncounterCatalog()
        {
            return new TestEncounterCatalog(
                new[] { "shop-a", "shop-b", "shop-c", "shop-d", "shop-e" },
                new[] { "event-coins", "event-life", "event-item", "event-refine", "event-bonus" });
        }

        private static TestEventCatalog CreateEventCatalog(bool includeHiddenChoice = false)
        {
            Dictionary<string, BuqiRunEncounterDelta> deltas = new Dictionary<string, BuqiRunEncounterDelta>
            {
                ["event-coins"] = new BuqiRunEncounterDelta { Coins = 5 },
                ["event-life"] = new BuqiRunEncounterDelta { Lives = 2 },
                ["event-item"] = new BuqiRunEncounterDelta { GrantedItemDefinitionId = "blade.alpha" },
                ["event-refine"] = new BuqiRunEncounterDelta { GrantedRefinementId = "refine.steel" },
            };
            if (includeHiddenChoice)
            {
                deltas["event-hidden"] = new BuqiRunEncounterDelta { Coins = 99 };
            }

            return new TestEventCatalog(deltas);
        }

        private static BuqiRunEncounterState CreateFrozenEventEncounter()
        {
            return new BuqiRunEncounterState
            {
                EncounterId = "day-2-enc-1-event",
                Kind = BuqiRunEncounterKind.Event,
                Day = 2,
                EncounterIndex = 1,
                NextRngCursor = 4,
                CandidateIds = new List<string>
                {
                    "event-coins",
                    "event-life",
                    "event-item",
                    "event-refine",
                },
            };
        }

        private static IReadOnlyList<string> CreateSequentialIds(string prefix, int count)
        {
            var values = new List<string>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add($"{prefix}-{index}");
            }

            return values;
        }

        private static long FindSeedForFirstEncounter(BuqiRunEncounterKind expectedKind)
        {
            for (long seed = 1; seed < 1024; seed++)
            {
                int cursor = 0;
                BuqiRunEncounterKind actualKind =
                    BuqiRunRandom.Next(seed, ref cursor, 2) == 0
                        ? BuqiRunEncounterKind.Shop
                        : BuqiRunEncounterKind.Event;
                if (actualKind == expectedKind)
                {
                    return seed;
                }
            }

            Assert.Fail($"Unable to find a deterministic seed for {expectedKind}.");
            return 0;
        }

        private static void AssertEncounterState(BuqiRunEncounterState actual, BuqiRunEncounterState expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(expected, Is.Not.Null);
            Assert.That(actual.EncounterId, Is.EqualTo(expected.EncounterId));
            Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
            Assert.That(actual.Day, Is.EqualTo(expected.Day));
            Assert.That(actual.EncounterIndex, Is.EqualTo(expected.EncounterIndex));
            Assert.That(actual.NextRngCursor, Is.EqualTo(expected.NextRngCursor));
            Assert.That(actual.Resolved, Is.EqualTo(expected.Resolved));
            Assert.That(actual.ResolutionId, Is.EqualTo(expected.ResolutionId));
            Assert.That(actual.SelectedChoiceId, Is.EqualTo(expected.SelectedChoiceId));
            Assert.That(actual.CandidateIds, Is.EqualTo(expected.CandidateIds).AsCollection);
        }

        private sealed class TestEncounterCatalog : IBuqiRunEncounterCatalog
        {
            public TestEncounterCatalog(IReadOnlyList<string> shopOfferIds, IReadOnlyList<string> eventIds)
            {
                ShopOfferIds = shopOfferIds;
                EventIds = eventIds;
            }

            public IReadOnlyList<string> ShopOfferIds { get; }

            public IReadOnlyList<string> EventIds { get; }
        }

        private sealed class TestEventCatalog : IBuqiRunEventCatalog
        {
            private readonly IReadOnlyDictionary<string, BuqiRunEncounterDelta> m_Deltas;

            public TestEventCatalog(IReadOnlyDictionary<string, BuqiRunEncounterDelta> deltas)
            {
                m_Deltas = deltas;
            }

            public bool TryGet(string eventId, out BuqiRunEncounterDelta delta)
            {
                if (m_Deltas.TryGetValue(eventId, out BuqiRunEncounterDelta value))
                {
                    delta = value.Clone();
                    return true;
                }

                delta = null;
                return false;
            }
        }

        private sealed class NullDeltaEventCatalog : IBuqiRunEventCatalog
        {
            private readonly string m_EventId;

            public NullDeltaEventCatalog(string eventId)
            {
                m_EventId = eventId;
            }

            public bool TryGet(string eventId, out BuqiRunEncounterDelta delta)
            {
                delta = null;
                return eventId == m_EventId;
            }
        }
    }
}
