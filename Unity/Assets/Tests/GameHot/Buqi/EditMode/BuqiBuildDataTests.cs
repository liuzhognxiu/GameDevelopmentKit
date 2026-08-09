using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI;
using Luban;
using NUnit.Framework;
using BattleEffect = Game.Hot.Buqi.Battle.BuqiEffect;
using BattleQuality = Game.Hot.Buqi.Battle.BuqiQuality;
using BattleSize = Game.Hot.Buqi.Battle.BuqiSize;
using BattleTarget = Game.Hot.Buqi.Battle.BuqiTarget;
using BattleTrigger = Game.Hot.Buqi.Battle.BuqiTrigger;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiBuildDataTests
    {
        private static readonly string[] s_CoreBuildIds = { "fast", "buffer", "heal" };

        [Test]
        public void CoreBuildEchoes_FillAllEightSlotsAndUseOwnArchetypeItems()
        {
            BuqiConfigCatalog catalog = LoadGeneratedCatalog();
            var definitions = catalog.Items.ToDictionary(item => item.DefinitionId, StringComparer.Ordinal);

            foreach (string buildId in s_CoreBuildIds)
            {
                List<BuqiEchoConfigRow> echoes = catalog.Echoes
                    .Where(echo => echo.Build == buildId)
                    .OrderBy(echo => echo.Tier, StringComparer.Ordinal)
                    .ToList();

                Assert.That(echoes, Has.Count.EqualTo(2), buildId);
                foreach (BuqiEchoConfigRow echo in echoes)
                {
                    int occupiedSlots = echo.Snapshot.Items.Sum(item => (int)definitions[item.DefinitionId].Size);
                    Assert.That(occupiedSlots, Is.EqualTo(BuqiBoardValidator.BoardSlotCount), echo.EchoId);
                    Assert.That(
                        echo.Snapshot.Items.All(item => definitions[item.DefinitionId].ArchetypeId == buildId),
                        Is.True,
                        echo.EchoId);
                }
            }
        }

        [Test]
        public void GeneratedBytes_AdaptBuildEnumsToSymbolicNamesAndCreateCoreStarters()
        {
            BuqiConfigCatalog source = LoadGeneratedCatalog();
            string[] expectedBuildIds =
            {
                "fast", "buffer", "chain", "heal", "poison", "burn", "freeze", "overload",
            };

            Assert.That(
                source.Items.Select(item => item.ArchetypeId).Distinct(StringComparer.Ordinal),
                Is.EquivalentTo(expectedBuildIds));
            Assert.That(
                source.Echoes.Select(echo => echo.Build).Distinct(StringComparer.Ordinal),
                Is.EquivalentTo(expectedBuildIds));
            foreach (BuqiEchoConfigRow echo in source.Echoes)
            {
                Assert.That(expectedBuildIds, Does.Contain(echo.Snapshot.ArchetypeId), echo.EchoId);
                Assert.That(echo.Snapshot.ArchetypeId, Is.EqualTo(echo.Build), echo.EchoId);
            }

            Assert.That(BuqiUIDemoCatalog.TryCreate(source, out BuqiUIDemoCatalog catalog, out string error),
                Is.True, error);
            var buildByItemId = source.Items.ToDictionary(
                item => item.DefinitionId,
                item => item.ArchetypeId,
                StringComparer.Ordinal);
            Assert.That(
                catalog.StarterChoices.Select(choice => buildByItemId[choice.Id]),
                Is.EquivalentTo(s_CoreBuildIds));
        }

        [Test]
        public void GenericCatalog_WithoutPreferredBuilds_StillCreatesThreeDistinctStarters()
        {
            var source = new BuqiConfigCatalog();
            for (int index = 1; index <= 7; index++)
            {
                var item = new BuqiItemConfigRow
                {
                    DefinitionId = "generic-" + index.ToString("00"),
                    DisplayName = "通用器物" + index,
                    Size = BattleSize.S,
                    BasePrice = 2,
                    BaseCooldownTicks = 30,
                    ArchetypeId = "generic",
                };
                item.Effects.Add(new BuqiEffectConfigRow
                {
                    Trigger = BattleTrigger.OnUse,
                    Effect = BattleEffect.Damage,
                    Target = BattleTarget.EnemyExecution,
                    Amount = index,
                    ReasonCode = item.DefinitionId + "-damage",
                });
                source.Items.Add(item);
            }
            for (int index = 1; index <= 3; index++)
            {
                source.Refinements.Add(new BuqiRefinementConfigRow
                {
                    RefinementId = "generic-refinement-" + index,
                    DisplayName = "通用改造" + index,
                    Summary = "通用改造说明",
                });
            }
            var echo = new BuqiEchoConfigRow
            {
                EchoId = "generic-echo",
                DisplayName = "通用对手",
                Build = "generic",
            };
            echo.Snapshot.Items.Add(new BuqiItemInstanceConfigRow
            {
                InstanceId = "generic-echo-item",
                DefinitionId = "generic-01",
                Quality = BattleQuality.Normal,
                AnchorSlot = 0,
            });
            source.Echoes.Add(echo);

            Assert.That(BuqiUIDemoCatalog.TryCreate(source, out BuqiUIDemoCatalog catalog, out string error),
                Is.True, error);
            Assert.That(
                catalog.StarterChoices.Select(choice => choice.Id),
                Is.EqualTo(new[] { "generic-01", "generic-02", "generic-03" }));
            Assert.That(
                catalog.StarterChoices.Select(choice => choice.Id).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(3));
        }

        [Test]
        public void CoreBuildItems_ArePurchasableAndStarterChoicesExposeAllThreeArchetypes()
        {
            BuqiConfigCatalog source = LoadGeneratedCatalog();
            Assert.That(BuqiUIDemoCatalog.TryCreate(source, out BuqiUIDemoCatalog catalog, out string error),
                Is.True, error);

            var archetypeByItemId = source.Items.ToDictionary(
                item => item.DefinitionId,
                item => item.ArchetypeId,
                StringComparer.Ordinal);
            string[] starterBuilds = catalog.StarterChoices
                .Select(choice => archetypeByItemId[choice.Id])
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(starterBuilds, Is.EqualTo(new[] { "buffer", "fast", "heal" }));
            foreach (string buildId in s_CoreBuildIds)
            {
                string[] itemIds = source.Items
                    .Where(item => item.ArchetypeId == buildId)
                    .Select(item => item.DefinitionId)
                    .ToArray();
                Assert.That(itemIds, Is.Not.Empty, buildId);
                Assert.That(itemIds.All(id => catalog.ShopOfferIds.Contains(id)), Is.True, buildId);
            }
        }

        [Test]
        public void PlayerVisibleBuildText_IsChinese()
        {
            BuqiConfigCatalog source = LoadGeneratedCatalog();
            Assert.That(BuqiUIDemoCatalog.TryCreate(source, out BuqiUIDemoCatalog catalog, out string error),
                Is.True, error);

            foreach (BuqiRefinementConfigRow refinement in source.Refinements)
                Assert.That(ContainsHan(refinement.Summary), Is.True, refinement.RefinementId);

            foreach (BuqiUIDemoItemDefinition item in catalog.Items)
            {
                Assert.That(ContainsHan(item.Name), Is.True, item.Id);
                Assert.That(ContainsHan(item.Description), Is.True, item.Id);
                Assert.That(
                    Regex.IsMatch(item.Description, "Damage|Buffer|Haste|Delay|Charge|Noise|Heal|Regen|Poison|Burn|Freeze"),
                    Is.False,
                    item.Id);
            }
        }

        [Test]
        public void CoreLessonBuilds_SimulateLegallyAndProduceDistinctOutputProfiles()
        {
            BuqiConfigCatalog catalog = LoadGeneratedCatalog();
            var provider = new BuqiDefinitionProvider(catalog);
            BuildSnapshot fast = ToBattleSnapshot(catalog, FindEcho(catalog, "echo-fast-lesson"));
            BuildSnapshot buffer = ToBattleSnapshot(catalog, FindEcho(catalog, "echo-buffer-lesson"));
            BuildSnapshot heal = ToBattleSnapshot(catalog, FindEcho(catalog, "echo-heal-lesson"));

            BattleRun fastRun = Simulate(fast, heal, provider, 101);
            BattleRun bufferRun = Simulate(buffer, fast, provider, 102);
            BattleRun healRun = Simulate(heal, fast, provider, 103);

            Assert.That(fastRun.Result.Outcome, Is.Not.EqualTo(BattleOutcome.InvalidBuild));
            Assert.That(bufferRun.Result.Outcome, Is.Not.EqualTo(BattleOutcome.InvalidBuild));
            Assert.That(healRun.Result.Outcome, Is.Not.EqualTo(BattleOutcome.InvalidBuild));
            Assert.That(SumEffect(fastRun.Log, fast, BuqiEffect.Damage), Is.GreaterThan(0));
            Assert.That(SumEffect(bufferRun.Log, buffer, BuqiEffect.Buffer), Is.GreaterThan(0));
            Assert.That(SumEffect(healRun.Log, heal, BuqiEffect.Heal), Is.GreaterThan(0));
            Assert.That(
                new[] { fastRun.Result.BattleLogHash, bufferRun.Result.BattleLogHash, healRun.Result.BattleLogHash }
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                Is.EqualTo(3));
        }

        [Test]
        public void CoreBuildCounterplay_MatchesDeclaredWeaknesses()
        {
            BuqiConfigCatalog catalog = LoadGeneratedCatalog();
            var provider = new BuqiDefinitionProvider(catalog);
            BuildSnapshot fast = ToBattleSnapshot(catalog, FindEcho(catalog, "echo-fast-lesson"));
            BuildSnapshot buffer = ToBattleSnapshot(catalog, FindEcho(catalog, "echo-buffer-lesson"));
            BuildSnapshot heal = ToBattleSnapshot(catalog, FindEcho(catalog, "echo-heal-lesson"));
            BuildSnapshot poison = ToBattleSnapshot(catalog, FindEcho(catalog, "echo-poison-lesson"));

            Assert.That(Simulate(fast, buffer, provider, 201).Result.Outcome, Is.EqualTo(BattleOutcome.RightWin));
            Assert.That(Simulate(buffer, poison, provider, 202).Result.Outcome, Is.EqualTo(BattleOutcome.RightWin));
            Assert.That(Simulate(heal, fast, provider, 203).Result.Outcome, Is.EqualTo(BattleOutcome.RightWin));
        }

        [Test]
        public void HealLessonBuild_RecoversFromLowExecutionAgainstLowPressure()
        {
            BuqiConfigCatalog catalog = LoadGeneratedCatalog();
            var provider = new BuqiDefinitionProvider(catalog);
            BuildSnapshot heal = ToBattleSnapshot(catalog, FindEcho(catalog, "echo-heal-lesson"));
            heal.SnapshotId = "heal-low-line";
            heal.InitialExecution = 35;

            var weakAttack = new BuildSnapshot
            {
                SnapshotId = "weak-attack",
                ContentVersion = catalog.Global.ContentVersion,
                ArchetypeId = "fast",
                InitialExecution = 100,
            };
            weakAttack.Items.Add(new ItemInstance
            {
                InstanceId = "weak-urgent",
                DefinitionId = "W8-003",
                Quality = (int)BuqiQuality.Normal,
                AnchorSlot = 0,
            });

            BattleRun run = Simulate(heal, weakAttack, provider, 204);

            Assert.That(run.Result.Outcome, Is.EqualTo(BattleOutcome.LeftWin));
            Assert.That(run.Result.LeftExecution, Is.GreaterThan(heal.InitialExecution));
            Assert.That(SumEffect(run.Log, heal, BuqiEffect.Heal), Is.GreaterThan(0));
        }

        private static BuqiConfigCatalog LoadGeneratedCatalog()
        {
            GeneratedBuqiTables tables = GeneratedBuqiTables.LoadFromProject();
            Assert.That(BuqiGeneratedConfigAdapter.TryReadFromTables(
                tables, out BuqiConfigCatalog catalog, out List<string> errors), Is.True, string.Join("\n", errors));
            Assert.That(BuqiConfigValidator.Validate(catalog), Is.Empty);
            return catalog;
        }

        private static BuqiEchoConfigRow FindEcho(BuqiConfigCatalog catalog, string echoId)
        {
            return catalog.Echoes.Single(echo => echo.EchoId == echoId);
        }

        private static BuildSnapshot ToBattleSnapshot(BuqiConfigCatalog catalog, BuqiEchoConfigRow echo)
        {
            var snapshot = new BuildSnapshot
            {
                SnapshotId = echo.Snapshot.SnapshotId,
                ContentVersion = catalog.Global.ContentVersion,
                ArchetypeId = echo.Snapshot.ArchetypeId,
                InitialExecution = echo.Snapshot.InitialExecution,
                InitialBuffer = echo.Snapshot.InitialBuffer,
                InitialNoiseDebt = echo.Snapshot.InitialNoiseDebt,
            };
            foreach (BuqiItemInstanceConfigRow item in echo.Snapshot.Items)
            {
                snapshot.Items.Add(new ItemInstance
                {
                    InstanceId = item.InstanceId,
                    DefinitionId = item.DefinitionId,
                    Quality = (int)item.Quality,
                    AnchorSlot = item.AnchorSlot,
                    AnnotationId = item.RefinementId,
                });
            }

            return snapshot;
        }

        private static BattleRun Simulate(
            BuildSnapshot left,
            BuildSnapshot right,
            IItemDefinitionProvider provider,
            int roundIndex)
        {
            BattleResult result = BuqiBattleSimulator.Simulate(
                new BattleRequest
                {
                    RuleVersion = BuqiBattleSimulator.RuleVersion,
                    BattleSeed = (ulong)roundIndex,
                    RoundIndex = roundIndex,
                    Left = left,
                    Right = right,
                },
                provider,
                out List<BattleEvent> log,
                out _,
                out _);
            return new BattleRun(result, log);
        }

        private static int SumEffect(
            IEnumerable<BattleEvent> log,
            BuildSnapshot source,
            BuqiEffect effect)
        {
            var instanceIds = new HashSet<string>(
                source.Items.Select(item => item.InstanceId),
                StringComparer.Ordinal);
            string marker = ":" + effect + ":";
            return log
                .Where(entry => entry.Type == BuqiEventType.Effect &&
                                instanceIds.Contains(entry.SourceInstanceId) &&
                                entry.EffectId.Contains(marker))
                .Sum(entry => entry.Amount);
        }

        private static bool ContainsHan(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, "[\u4e00-\u9fff]");
        }

        private sealed class BattleRun
        {
            public BattleRun(BattleResult result, List<BattleEvent> log)
            {
                Result = result;
                Log = log;
            }

            public BattleResult Result { get; }
            public List<BattleEvent> Log { get; }
        }

        private sealed class GeneratedBuqiTables
        {
            public DTBuqiGlobal DTBuqiGlobal { get; private set; }
            public DTBuqiItem DTBuqiItem { get; private set; }
            public DTBuqiRefinement DTBuqiRefinement { get; private set; }
            public DTBuqiEcho DTBuqiEcho { get; private set; }
            public DTBuqiMerchant DTBuqiMerchant { get; private set; }
            public DTBuqiTrainer DTBuqiTrainer { get; private set; }
            public DTBuqiTrainingProject DTBuqiTrainingProject { get; private set; }
            public DTBuqiEvent DTBuqiEvent { get; private set; }
            public DTBuqiEventOption DTBuqiEventOption { get; private set; }

            public static GeneratedBuqiTables LoadFromProject()
            {
                var tables = new GeneratedBuqiTables
                {
                    DTBuqiGlobal = new DTBuqiGlobal(() => LoadBytes("dtbuqiglobal")),
                    DTBuqiItem = new DTBuqiItem(() => LoadBytes("dtbuqiitem")),
                    DTBuqiRefinement = new DTBuqiRefinement(() => LoadBytes("dtbuqirefinement")),
                    DTBuqiEcho = new DTBuqiEcho(() => LoadBytes("dtbuqiecho")),
                    DTBuqiMerchant = new DTBuqiMerchant(() => LoadBytes("dtbuqimerchant")),
                    DTBuqiTrainer = new DTBuqiTrainer(() => LoadBytes("dtbuqitrainer")),
                    DTBuqiTrainingProject = new DTBuqiTrainingProject(() => LoadBytes("dtbuqitrainingproject")),
                    DTBuqiEvent = new DTBuqiEvent(() => LoadBytes("dtbuqievent")),
                    DTBuqiEventOption = new DTBuqiEventOption(() => LoadBytes("dtbuqieventoption")),
                };
                tables.DTBuqiGlobal.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiItem.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiRefinement.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiEcho.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiMerchant.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiTrainer.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiTrainingProject.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiEvent.LoadAsync().GetAwaiter().GetResult();
                tables.DTBuqiEventOption.LoadAsync().GetAwaiter().GetResult();
                return tables;
            }

            private static UniTask<ByteBuf> LoadBytes(string tableName)
            {
                string path = Path.Combine(
                    Directory.GetCurrentDirectory(), "Assets", "Res", "Hot", "Luban", tableName + ".bytes");
                Assert.That(File.Exists(path), Is.True, "generated table bytes missing: " + path);
                return UniTask.FromResult(new ByteBuf(File.ReadAllBytes(path)));
            }
        }
    }
}
