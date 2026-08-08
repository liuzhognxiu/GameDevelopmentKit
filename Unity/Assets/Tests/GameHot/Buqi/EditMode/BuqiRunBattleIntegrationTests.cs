using System.Collections.Generic;
using System.Reflection;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.Run.Battle;
using Game.Hot.Buqi.Run.Core;
using NUnit.Framework;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiRunBattleIntegrationTests
    {
        [Test]
        public void PveAndPvpSelectOnlyFromTheirOwnLocalPools()
        {
            BuqiLocalOpponentPool pool = TestPool.Create(
                pveIds: new[] { "monster-a", "monster-b" },
                pvpIds: new[] { "player-a", "player-b" });
            var provider = new BuqiLocalOpponentProvider(pool);
            BuqiRunState pveRun = BuqiRunState.CreateInitial(1001);
            pveRun.Phase = BuqiRunPhase.PveBattle;
            BuqiRunState pvpRun = pveRun.Clone();
            pvpRun.Phase = BuqiRunPhase.PvpBattle;

            Assert.That(provider.TrySelect(
                pveRun,
                BuqiRunBattleKind.Pve,
                out BuqiRunOpponent pve,
                out int pveCursor,
                out string pveError), Is.True, pveError);
            Assert.That(pve.Source, Is.EqualTo(BuqiRunOpponentSource.PvePreset));
            Assert.That(pve.OpponentId, Does.StartWith("monster-"));
            Assert.That(pve.Build, Is.Not.Null);
            Assert.That(pveCursor, Is.EqualTo(pveRun.RngCursor + 1));

            Assert.That(provider.TrySelect(
                pvpRun,
                BuqiRunBattleKind.Pvp,
                out BuqiRunOpponent pvp,
                out int pvpCursor,
                out string pvpError), Is.True, pvpError);
            Assert.That(pvp.Source, Is.EqualTo(BuqiRunOpponentSource.LocalPlayerPreset));
            Assert.That(pvp.OpponentId, Does.StartWith("player-"));
            Assert.That(pvp.Build, Is.Not.Null);
            Assert.That(pvpCursor, Is.EqualTo(pvpRun.RngCursor + 1));
        }

        [Test]
        public void SameSeedAndCursorSelectSamePresetPlayer()
        {
            var provider = new BuqiLocalOpponentProvider(TestPool.Standard());
            BuqiRunState left = BuqiRunState.CreateInitial(2002);
            left.Phase = BuqiRunPhase.PvpBattle;
            BuqiRunState right = left.Clone();

            Assert.That(provider.TrySelect(
                left,
                BuqiRunBattleKind.Pvp,
                out BuqiRunOpponent leftOpponent,
                out int leftCursor,
                out string leftError), Is.True, leftError);
            Assert.That(provider.TrySelect(
                right,
                BuqiRunBattleKind.Pvp,
                out BuqiRunOpponent rightOpponent,
                out int rightCursor,
                out string rightError), Is.True, rightError);

            Assert.That(leftOpponent.OpponentId, Is.EqualTo(rightOpponent.OpponentId));
            Assert.That(leftOpponent.Build.SnapshotId, Is.EqualTo(rightOpponent.Build.SnapshotId));
            Assert.That(leftCursor, Is.EqualTo(rightCursor));
        }

        [Test]
        public void SelectionFailsWhenPhaseDoesNotMatchBattleKind()
        {
            var provider = new BuqiLocalOpponentProvider(TestPool.Standard());
            BuqiRunState run = BuqiRunState.CreateInitial(3003);
            run.Phase = BuqiRunPhase.Encounter;
            int originalCursor = run.RngCursor;

            Assert.That(provider.TrySelect(
                run,
                BuqiRunBattleKind.Pve,
                out BuqiRunOpponent opponent,
                out int nextCursor,
                out string error), Is.False);
            Assert.That(opponent, Is.Null);
            Assert.That(nextCursor, Is.EqualTo(originalCursor));
            Assert.That(error, Does.Contain("phase"));
        }

        [Test]
        public void SelectionFailsClosedWhenBattleKindIsUndefined()
        {
            var provider = new BuqiLocalOpponentProvider(TestPool.Standard());
            BuqiRunState run = BuqiRunState.CreateInitial(3111);
            run.Phase = BuqiRunPhase.PvpBattle;
            int originalCursor = run.RngCursor;

            Assert.That(provider.TrySelect(
                run,
                (BuqiRunBattleKind)99,
                out BuqiRunOpponent opponent,
                out int nextCursor,
                out string error), Is.False);
            Assert.That(opponent, Is.Null);
            Assert.That(nextCursor, Is.EqualTo(originalCursor));
            Assert.That(error, Does.Contain("kind"));
        }

        [Test]
        public void SelectionFailsClosedWhenRngCursorIsNegative()
        {
            var provider = new BuqiLocalOpponentProvider(TestPool.Standard());
            BuqiRunState run = BuqiRunState.CreateInitial(3222);
            run.Phase = BuqiRunPhase.PveBattle;
            run.RngCursor = -1;

            Assert.That(provider.TrySelect(
                run,
                BuqiRunBattleKind.Pve,
                out BuqiRunOpponent opponent,
                out int nextCursor,
                out string error), Is.False);
            Assert.That(opponent, Is.Null);
            Assert.That(nextCursor, Is.EqualTo(-1));
            Assert.That(error, Does.Contain("cursor"));
        }

        [Test]
        public void SelectionFailsClosedWhenPoolIsEmptyOrStructurallyInvalid()
        {
            var emptyProvider = new BuqiLocalOpponentProvider(new BuqiLocalOpponentPool());
            BuqiRunState run = BuqiRunState.CreateInitial(4004);
            run.Phase = BuqiRunPhase.PveBattle;
            int originalCursor = run.RngCursor;

            Assert.That(emptyProvider.TrySelect(
                run,
                BuqiRunBattleKind.Pve,
                out BuqiRunOpponent emptyOpponent,
                out int emptyCursor,
                out string emptyError), Is.False);
            Assert.That(emptyOpponent, Is.Null);
            Assert.That(emptyCursor, Is.EqualTo(originalCursor));
            Assert.That(emptyError, Does.Contain("empty"));

            var invalidPool = new BuqiLocalOpponentPool();
            invalidPool.Pve.Add(new BuqiRunOpponent
            {
                OpponentId = "invalid-monster",
                DisplayName = "Invalid Monster",
                Source = BuqiRunOpponentSource.LocalPlayerPreset,
                Build = null,
            });

            var invalidProvider = new BuqiLocalOpponentProvider(invalidPool);
            Assert.That(invalidProvider.TrySelect(
                run,
                BuqiRunBattleKind.Pve,
                out BuqiRunOpponent invalidOpponent,
                out int invalidCursor,
                out string invalidError), Is.False);
            Assert.That(invalidOpponent, Is.Null);
            Assert.That(invalidCursor, Is.EqualTo(originalCursor));
            Assert.That(invalidError, Does.Contain("invalid"));
        }

        [Test]
        public void Service_UsesCallerBuildOnLeftAndSelectedPveOpponentOnRight()
        {
            BuqiLocalOpponentPool pool = TestPool.Create(
                pveIds: new[] { "monster-a" },
                pvpIds: new string[0]);
            var provider = new BuqiLocalOpponentProvider(pool);
            var service = new BuqiRunBattleService(provider);
            BuqiRunState run = BuqiRunState.CreateInitial(5005);
            run.Day = 2;
            run.Phase = BuqiRunPhase.PveBattle;
            BuildSnapshot playerBuild = TestPool.CreatePlayerBuild(
                snapshotId: "player-left",
                definitionId: "damage",
                instancePrefix: "player-left");
            IItemDefinitionProvider definitions = BuqiTestSuite.CreateFixtureProvider();

            Assert.That(service.TryCreateAndSimulate(
                run,
                BuqiRunBattleKind.Pve,
                playerBuild,
                definitions,
                out BuqiRunBattleSession session,
                out string error), Is.True, error);

            Assert.That(session.Kind, Is.EqualTo(BuqiRunBattleKind.Pve));
            Assert.That(session.OpponentId, Is.EqualTo("monster-a"));
            Assert.That(session.NextRngCursor, Is.EqualTo(run.RngCursor + 1));
            Assert.That(session.Request.Left, Is.SameAs(playerBuild));
            Assert.That(session.Request.Right, Is.Not.Null);
            Assert.That(session.Request.Right, Is.Not.SameAs(pool.Pve[0].Build));
            Assert.That(session.Request.Right.SnapshotId, Is.EqualTo(pool.Pve[0].Build.SnapshotId));
            Assert.That(session.Request.RoundIndex, Is.EqualTo(3));
            Assert.That(session.Log.Count, Is.GreaterThan(0));
            Assert.That(session.Replay, Is.Not.Null);
            Assert.That(session.Replay.LeftBuild, Is.SameAs(playerBuild));
            Assert.That(session.Replay.RightBuild, Is.SameAs(session.Request.Right));
            Assert.That(session.Replay.Log, Is.SameAs(session.Log));

            var controller = new BattleReplayController(session.Replay);
            controller.SkipToEnd();
            Assert.That(controller.Frame.Error, Is.Empty);
        }

        [Test]
        public void Service_UsesCallerBuildOnLeftAndSelectedPvpOpponentOnRight()
        {
            BuqiLocalOpponentPool pool = TestPool.Create(
                pveIds: new string[0],
                pvpIds: new[] { "player-a" });
            var provider = new BuqiLocalOpponentProvider(pool);
            var service = new BuqiRunBattleService(provider);
            BuqiRunState run = BuqiRunState.CreateInitial(6006);
            run.Day = 2;
            run.Phase = BuqiRunPhase.PvpBattle;
            BuildSnapshot playerBuild = TestPool.CreatePlayerBuild(
                snapshotId: "player-pvp-left",
                definitionId: "buffer",
                instancePrefix: "player-pvp-left");
            IItemDefinitionProvider definitions = BuqiTestSuite.CreateFixtureProvider();

            Assert.That(service.TryCreateAndSimulate(
                run,
                BuqiRunBattleKind.Pvp,
                playerBuild,
                definitions,
                out BuqiRunBattleSession session,
                out string error), Is.True, error);

            Assert.That(session.Kind, Is.EqualTo(BuqiRunBattleKind.Pvp));
            Assert.That(session.OpponentId, Is.EqualTo("player-a"));
            Assert.That(session.Request.Left, Is.SameAs(playerBuild));
            Assert.That(session.Request.Right.SnapshotId, Is.EqualTo(pool.Pvp[0].Build.SnapshotId));
            Assert.That(session.Request.RoundIndex, Is.EqualTo(4));
        }

        [Test]
        public void Service_PreservesDrawRawOutcome()
        {
            var pool = new BuqiLocalOpponentPool();
            pool.Pve.Add(new BuqiRunOpponent
            {
                OpponentId = "monster-draw",
                DisplayName = "monster-draw",
                Source = BuqiRunOpponentSource.PvePreset,
                Build = TestPool.CreateDrawBuild("monster-draw"),
            });

            var provider = new BuqiLocalOpponentProvider(pool);
            var service = new BuqiRunBattleService(provider);
            BuqiRunState run = BuqiRunState.CreateInitial(7007);
            run.Phase = BuqiRunPhase.PveBattle;
            BuildSnapshot playerBuild = TestPool.CreateDrawBuild("player-draw");
            IItemDefinitionProvider definitions = BuqiTestSuite.CreateFixtureProvider();

            Assert.That(service.TryCreateAndSimulate(
                run,
                BuqiRunBattleKind.Pve,
                playerBuild,
                definitions,
                out BuqiRunBattleSession session,
                out string error), Is.True, error);

            Assert.That(session.Result.Outcome, Is.EqualTo(BattleOutcome.Draw));
            Assert.That(session.RawOutcome, Is.EqualTo(BuqiRunRawBattleOutcome.Draw));
            Assert.That(session.Log.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Service_RejectsInvalidPlayerOrOpponentBuilds()
        {
            BuqiLocalOpponentPool validPool = TestPool.Create(
                pveIds: new[] { "monster-a" },
                pvpIds: new string[0]);
            var provider = new BuqiLocalOpponentProvider(validPool);
            var service = new BuqiRunBattleService(provider);
            BuqiRunState run = BuqiRunState.CreateInitial(8008);
            run.Phase = BuqiRunPhase.PveBattle;
            IItemDefinitionProvider definitions = BuqiTestSuite.CreateFixtureProvider();

            Assert.That(service.TryCreateAndSimulate(
                run,
                BuqiRunBattleKind.Pve,
                TestPool.CreateInvalidBuild("invalid-player"),
                definitions,
                out BuqiRunBattleSession invalidPlayerSession,
                out string invalidPlayerError), Is.False);
            Assert.That(invalidPlayerSession, Is.Null);
            Assert.That(invalidPlayerError, Does.Contain("player"));

            var invalidOpponentPool = new BuqiLocalOpponentPool();
            invalidOpponentPool.Pve.Add(new BuqiRunOpponent
            {
                OpponentId = "invalid-opponent",
                DisplayName = "invalid-opponent",
                Source = BuqiRunOpponentSource.PvePreset,
                Build = TestPool.CreateInvalidBuild("invalid-opponent"),
            });
            var invalidOpponentProvider = new BuqiLocalOpponentProvider(invalidOpponentPool);
            var invalidOpponentService = new BuqiRunBattleService(invalidOpponentProvider);

            Assert.That(invalidOpponentService.TryCreateAndSimulate(
                run,
                BuqiRunBattleKind.Pve,
                TestPool.CreatePlayerBuild("valid-player", "damage", "valid-player"),
                definitions,
                out BuqiRunBattleSession invalidOpponentSession,
                out string invalidOpponentError), Is.False);
            Assert.That(invalidOpponentSession, Is.Null);
            Assert.That(invalidOpponentError, Does.Contain("opponent"));
        }

        [Test]
        public void Service_ChangesBattleIdentityWhenPlayerBuildChanges()
        {
            BuqiLocalOpponentPool pool = TestPool.Create(
                pveIds: new[] { "monster-a" },
                pvpIds: new string[0]);
            var provider = new BuqiLocalOpponentProvider(pool);
            var service = new BuqiRunBattleService(provider);
            IItemDefinitionProvider definitions = BuqiTestSuite.CreateFixtureProvider();

            BuqiRunState firstRun = BuqiRunState.CreateInitial(9009);
            firstRun.Phase = BuqiRunPhase.PveBattle;
            BuqiRunState secondRun = firstRun.Clone();

            Assert.That(service.TryCreateAndSimulate(
                firstRun,
                BuqiRunBattleKind.Pve,
                TestPool.CreatePlayerBuild("player-a", "damage", "player-a"),
                definitions,
                out BuqiRunBattleSession firstSession,
                out string firstError), Is.True, firstError);
            Assert.That(service.TryCreateAndSimulate(
                secondRun,
                BuqiRunBattleKind.Pve,
                TestPool.CreatePlayerBuild("player-b", "buffer", "player-b"),
                definitions,
                out BuqiRunBattleSession secondSession,
                out string secondError), Is.True, secondError);

            Assert.That(firstSession.BattleId, Is.Not.EqualTo(secondSession.BattleId));
            Assert.That(firstSession.Result.LeftSnapshotHash, Is.Not.EqualTo(secondSession.Result.LeftSnapshotHash));
        }

        [Test]
        public void Service_FailsClosedWhenBattleKindIsUndefined()
        {
            var provider = new BuqiLocalOpponentProvider(TestPool.Standard());
            var service = new BuqiRunBattleService(provider);
            BuqiRunState run = BuqiRunState.CreateInitial(9333);
            run.Phase = BuqiRunPhase.PvpBattle;
            int originalCursor = run.RngCursor;
            IItemDefinitionProvider definitions = BuqiTestSuite.CreateFixtureProvider();

            Assert.That(service.TryCreateAndSimulate(
                run,
                (BuqiRunBattleKind)99,
                TestPool.CreatePlayerBuild("player-invalid-kind", "damage", "player-invalid-kind"),
                definitions,
                out BuqiRunBattleSession session,
                out string error), Is.False);
            Assert.That(session, Is.Null);
            Assert.That(run.RngCursor, Is.EqualTo(originalCursor));
            Assert.That(error, Does.Contain("kind"));
        }

        [Test]
        public void Service_FailsClosedWhenDayIsLessThanOne()
        {
            var provider = new BuqiLocalOpponentProvider(TestPool.Standard());
            var service = new BuqiRunBattleService(provider);
            BuqiRunState run = BuqiRunState.CreateInitial(9444);
            run.Day = 0;
            run.Phase = BuqiRunPhase.PveBattle;
            int originalCursor = run.RngCursor;
            IItemDefinitionProvider definitions = BuqiTestSuite.CreateFixtureProvider();

            Assert.That(service.TryCreateAndSimulate(
                run,
                BuqiRunBattleKind.Pve,
                TestPool.CreatePlayerBuild("player-invalid-day", "damage", "player-invalid-day"),
                definitions,
                out BuqiRunBattleSession session,
                out string error), Is.False);
            Assert.That(session, Is.Null);
            Assert.That(run.RngCursor, Is.EqualTo(originalCursor));
            Assert.That(error, Does.Contain("day"));
        }

        [Test]
        public void Service_FailsClosedWhenRngCursorIsNegative()
        {
            var provider = new BuqiLocalOpponentProvider(TestPool.Standard());
            var service = new BuqiRunBattleService(provider);
            BuqiRunState run = BuqiRunState.CreateInitial(9555);
            run.Phase = BuqiRunPhase.PveBattle;
            run.RngCursor = -5;
            IItemDefinitionProvider definitions = BuqiTestSuite.CreateFixtureProvider();

            Assert.That(service.TryCreateAndSimulate(
                run,
                BuqiRunBattleKind.Pve,
                TestPool.CreatePlayerBuild("player-invalid-cursor", "damage", "player-invalid-cursor"),
                definitions,
                out BuqiRunBattleSession session,
                out string error), Is.False);
            Assert.That(session, Is.Null);
            Assert.That(run.RngCursor, Is.EqualTo(-5));
            Assert.That(error, Does.Contain("cursor"));
        }

        [Test]
        public void UnknownBattleOutcomeIsRejectedInsteadOfDowngradingToDraw()
        {
            MethodInfo method = typeof(BuqiRunBattleService).GetMethod(
                "MapOutcome",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            Assert.That(
                () => method.Invoke(null, new object[] { (BattleOutcome)99 }),
                Throws.TypeOf<TargetInvocationException>());
        }

        [Test]
        public void Adapter_PlacesExplicitEchoAssignmentsIntoMatchingPools()
        {
            BuqiConfigCatalog catalog = TestCatalog.CreateAdapterCatalog();
            var adapter = new BuqiLocalOpponentPoolAdapter(
                new[] { "echo-pve-a", "echo-pve-b" },
                new[] { "echo-pvp-a", "echo-pvp-b" });

            Assert.That(adapter.TryCreate(
                catalog,
                out BuqiLocalOpponentPool pool,
                out string error), Is.True, error);

            Assert.That(pool.Pve.Count, Is.EqualTo(2));
            Assert.That(pool.Pvp.Count, Is.EqualTo(2));
            Assert.That(pool.Pve[0].Source, Is.EqualTo(BuqiRunOpponentSource.PvePreset));
            Assert.That(pool.Pvp[0].Source, Is.EqualTo(BuqiRunOpponentSource.LocalPlayerPreset));
            CollectionAssert.AreEquivalent(
                new[] { "echo-pve-a", "echo-pve-b" },
                new[] { pool.Pve[0].OpponentId, pool.Pve[1].OpponentId });
            CollectionAssert.AreEquivalent(
                new[] { "echo-pvp-a", "echo-pvp-b" },
                new[] { pool.Pvp[0].OpponentId, pool.Pvp[1].OpponentId });
            Assert.That(pool.Pve[0].Build.ContentVersion, Is.EqualTo(catalog.Global.ContentVersion));
            Assert.That(pool.Pve[0].Build.SnapshotId, Does.StartWith(pool.Pve[0].OpponentId + ":"));
        }

        [Test]
        public void Adapter_ReportsDuplicateMissingOrIllegalAssignments()
        {
            BuqiConfigCatalog catalog = TestCatalog.CreateAdapterCatalog();
            var adapter = new BuqiLocalOpponentPoolAdapter(
                new[] { "echo-pve-a", "echo-illegal" },
                new[] { "echo-pve-a", "echo-missing" });

            Assert.That(adapter.TryCreate(
                catalog,
                out BuqiLocalOpponentPool pool,
                out string error), Is.False);
            Assert.That(pool, Is.Null);
            Assert.That(error, Does.Contain("duplicate"));
            Assert.That(error, Does.Contain("missing"));
            Assert.That(error, Does.Contain("illegal"));
        }

        private static class TestPool
        {
            private static readonly string[] s_PveDefinitions =
            {
                "damage",
                "buffer",
                "large",
                "medium",
            };

            private static readonly string[] s_PvpDefinitions =
            {
                "passive",
                "delay",
                "charge",
                "heal",
            };

            public static BuqiLocalOpponentPool Standard()
            {
                return Create(
                    pveIds: new[] { "monster-a", "monster-b" },
                    pvpIds: new[] { "player-a", "player-b" });
            }

            public static BuildSnapshot CreatePlayerBuild(
                string snapshotId,
                string definitionId,
                string instancePrefix)
            {
                BuildSnapshot build = BuqiTestSuite.Snapshot(
                    id: snapshotId,
                    execution: 110,
                    buffer: 2,
                    items: new[]
                    {
                        BuqiTestSuite.Item(instancePrefix + "-0", definitionId, 0),
                    });
                build.ArchetypeId = snapshotId + "-archetype";
                return build;
            }

            public static BuildSnapshot CreateDrawBuild(string snapshotId)
            {
                BuildSnapshot build = BuqiTestSuite.Snapshot(
                    id: snapshotId,
                    execution: 2,
                    buffer: 0,
                    items: new[]
                    {
                        BuqiTestSuite.Item(snapshotId + "-0", "passive", 0),
                    });
                build.ArchetypeId = snapshotId + "-draw";
                return build;
            }

            public static BuildSnapshot CreateInvalidBuild(string snapshotId)
            {
                return new BuildSnapshot
                {
                    SnapshotId = snapshotId,
                    ContentVersion = BuqiTestSuite.FixtureContentVersion,
                    ArchetypeId = snapshotId + "-invalid",
                    InitialExecution = 100,
                    InitialBuffer = 0,
                    InitialNoiseDebt = 0,
                };
            }

            public static BuqiLocalOpponentPool Create(
                IReadOnlyList<string> pveIds,
                IReadOnlyList<string> pvpIds)
            {
                var pool = new BuqiLocalOpponentPool();
                AddOpponents(pool.Pve, pveIds, BuqiRunOpponentSource.PvePreset, s_PveDefinitions);
                AddOpponents(pool.Pvp, pvpIds, BuqiRunOpponentSource.LocalPlayerPreset, s_PvpDefinitions);
                return pool;
            }

            private static void AddOpponents(
                List<BuqiRunOpponent> destination,
                IReadOnlyList<string> ids,
                BuqiRunOpponentSource source,
                IReadOnlyList<string> definitionIds)
            {
                if (ids == null)
                    return;

                for (int index = 0; index < ids.Count; index++)
                {
                    string opponentId = ids[index];
                    destination.Add(new BuqiRunOpponent
                    {
                        OpponentId = opponentId,
                        DisplayName = opponentId,
                        Source = source,
                        Build = CreateBuild(opponentId, definitionIds[index % definitionIds.Count], index),
                    });
                }
            }

            private static BuildSnapshot CreateBuild(string opponentId, string definitionId, int index)
            {
                ItemInstance secondItem = null;
                if (index % 2 == 1)
                {
                    secondItem = BuqiTestSuite.Item(
                        instanceId: opponentId + "-extra",
                        definitionId: "buffer",
                        anchorSlot: 1);
                }

                BuildSnapshot build = BuqiTestSuite.Snapshot(
                    id: opponentId + "-snapshot",
                    execution: 100 + (index * 5),
                    buffer: index,
                    items: secondItem == null
                        ? new[]
                        {
                            BuqiTestSuite.Item(opponentId + "-item", definitionId, 0),
                        }
                        : new[]
                        {
                            BuqiTestSuite.Item(opponentId + "-item", definitionId, 0),
                            secondItem,
                        });
                build.ArchetypeId = opponentId + "-build";
                return build;
            }
        }

        private static class TestCatalog
        {
            public static BuqiConfigCatalog CreateAdapterCatalog()
            {
                IItemDefinitionProvider provider = BuqiTestSuite.CreateFixtureProvider();
                var catalog = new BuqiConfigCatalog
                {
                    Global = new BuqiGlobalConfigRow
                    {
                        ContentVersion = provider.ContentVersion,
                    },
                };

                BuildSnapshot pveA = TestPool.CreatePlayerBuild("echo-pve-a-build", "damage", "echo-pve-a");
                BuildSnapshot pveB = TestPool.CreatePlayerBuild("echo-pve-b-build", "buffer", "echo-pve-b");
                BuildSnapshot pvpA = TestPool.CreatePlayerBuild("echo-pvp-a-build", "passive", "echo-pvp-a");
                BuildSnapshot pvpB = TestPool.CreatePlayerBuild("echo-pvp-b-build", "heal", "echo-pvp-b");

                AddDefinitions(catalog, provider, pveA);
                AddDefinitions(catalog, provider, pveB);
                AddDefinitions(catalog, provider, pvpA);
                AddDefinitions(catalog, provider, pvpB);

                catalog.Echoes.Add(CreateEcho("echo-pve-a", "echo-pve-a", pveA));
                catalog.Echoes.Add(CreateEcho("echo-pve-b", "echo-pve-b", pveB));
                catalog.Echoes.Add(CreateEcho("echo-pvp-a", "echo-pvp-a", pvpA));
                catalog.Echoes.Add(CreateEcho("echo-pvp-b", "echo-pvp-b", pvpB));
                catalog.Echoes.Add(new BuqiEchoConfigRow
                {
                    EchoId = "echo-illegal",
                    DisplayName = "echo-illegal",
                    Tier = "1",
                    Build = "echo-illegal",
                    Snapshot = new BuqiBuildSnapshotConfigRow
                    {
                        SnapshotId = "echo-illegal-build",
                        ArchetypeId = "echo-illegal-build",
                        InitialExecution = 100,
                        InitialBuffer = 0,
                        InitialNoiseDebt = 0,
                    },
                });
                return catalog;
            }

            private static void AddDefinitions(
                BuqiConfigCatalog catalog,
                IItemDefinitionProvider provider,
                BuildSnapshot build)
            {
                foreach (ItemInstance instance in build.Items)
                {
                    if (catalog.Items.Exists(row => row.DefinitionId == instance.DefinitionId))
                        continue;

                    Assert.That(provider.TryGet(instance.DefinitionId, out BuqiItemDefinition definition), Is.True);
                    var row = new BuqiItemConfigRow
                    {
                        DefinitionId = definition.DefinitionId,
                        DisplayName = definition.DefinitionId,
                        Size = (Game.Hot.Buqi.Battle.BuqiSize)definition.Size,
                        BaseCooldownTicks = definition.BaseCooldownTicks,
                        ArchetypeId = build.ArchetypeId,
                    };
                    foreach (BuqiEffectSpec effect in definition.Effects)
                    {
                        row.Effects.Add(new BuqiEffectConfigRow
                        {
                            Trigger = effect.Trigger,
                            Effect = effect.Effect,
                            Target = effect.Target,
                            Amount = effect.Amount,
                            DurationTicks = effect.DurationTicks,
                            ReasonCode = effect.ReasonCode,
                            ConditionKind = effect.ConditionKind,
                            ConditionThreshold = effect.ConditionThreshold,
                            UseCountThreshold = effect.UseCountThreshold,
                            ChargeReadLimit = effect.ChargeReadLimit,
                            AmountPerCharge = effect.AmountPerCharge,
                            ChargeConsume = effect.ChargeConsume,
                            ResetCountOnReached = effect.ResetCountOnReached,
                        });
                    }

                    catalog.Items.Add(row);
                }
            }

            private static BuqiEchoConfigRow CreateEcho(string id, string name, BuildSnapshot build)
            {
                var snapshot = new BuqiBuildSnapshotConfigRow
                {
                    SnapshotId = build.SnapshotId,
                    ArchetypeId = build.ArchetypeId,
                    InitialExecution = build.InitialExecution,
                    InitialBuffer = build.InitialBuffer,
                    InitialNoiseDebt = build.InitialNoiseDebt,
                };

                foreach (ItemInstance item in build.Items)
                {
                    snapshot.Items.Add(new BuqiItemInstanceConfigRow
                    {
                        InstanceId = item.InstanceId,
                        DefinitionId = item.DefinitionId,
                        Quality = (Game.Hot.Buqi.Battle.BuqiQuality)item.Quality,
                        AnchorSlot = item.AnchorSlot,
                        RefinementId = item.AnnotationId,
                    });
                }

                return new BuqiEchoConfigRow
                {
                    EchoId = id,
                    DisplayName = name,
                    Tier = "1",
                    Build = build.ArchetypeId,
                    Snapshot = snapshot,
                };
            }
        }
    }
}
