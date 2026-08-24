#if !BUQI_HEADLESS_TESTS
using NUnit.Framework;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using BattleEffect = Game.Hot.Buqi.Battle.BuqiEffect;
using BattleQuality = Game.Hot.Buqi.Battle.BuqiQuality;
using BattleTarget = Game.Hot.Buqi.Battle.BuqiTarget;
using BattleTrigger = Game.Hot.Buqi.Battle.BuqiTrigger;

namespace Game.Hot.Buqi.Tests
{
    public sealed class BuqiLinkEngineTests
    {
#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void RingTopology_ConnectsSevenAndZeroAtItemBoundaries()
        {
            BuqiLinkItem atZero = Item("attack", "W8-006", 0, 3, BattleQuality.Fixed, BattleEffect.Damage);
            BuqiLinkItem atSeven = Item("tempo", "W8-003", 7, 1, BattleQuality.Improved, BattleEffect.Haste);
            var board = new BuqiLinkBoard(new[] { atZero, atSeven });

            CheckSame(atZero, BuqiLinkTopology.GetAdjacent(board, atSeven, BuqiLinkDirection.Clockwise));
            CheckSame(atSeven, BuqiLinkTopology.GetAdjacent(board, atZero, BuqiLinkDirection.CounterClockwise));
            Check(BuqiLinkTopology.GetAdjacent(board, atZero, BuqiLinkDirection.Clockwise) == null,
                "An empty boundary must block adjacency.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void Conditions_FilterDirectionTagsSizeQualityEffectsAndTriggerSource()
        {
            BuqiLinkItem attack = Item("attack", "W8-006", 0, 3, BattleQuality.Fixed, BattleEffect.Damage);
            attack.Tags.Add("attack");
            BuqiLinkItem tempo = Item("tempo", "W8-003", 7, 1, BattleQuality.Improved, BattleEffect.Haste);
            tempo.Tags.Add("tempo");
            tempo.Triggers.Add(BattleTrigger.OnAdjacentUse);
            var board = new BuqiLinkBoard(new[] { attack, tempo });
            BuqiLinkRule rule = LinkRule("tempo-to-attack", BuqiLinkDirection.Clockwise, 100, 2);
            rule.TriggerSource = BuqiLinkTriggerSource.AdjacentUse;
            rule.SourceCondition = new BuqiLinkCondition
            {
                RequiredTag = "tempo",
                RequiredEffect = BattleEffect.Haste,
                RequiredTrigger = BattleTrigger.OnAdjacentUse,
                MinimumQuality = (int)BattleQuality.Improved,
                MinimumSize = 1,
                MaximumSize = 1,
            };
            rule.TargetCondition = new BuqiLinkCondition
            {
                RequiredTag = "attack",
                RequiredEffect = BattleEffect.Damage,
                MinimumSize = 3,
                MaximumSize = 3,
            };

            BuqiLinkEvaluation evaluation = BuqiLinkEngine.Evaluate(
                board,
                new[] { rule },
                Array.Empty<BuqiFormationRule>(),
                null);
            Check(evaluation.Links.Single().IsConnected, "The clockwise boundary link should be connected.");

            var context = new BuqiLinkTriggerContext
            {
                Tick = 12,
                RootEventId = "root-1",
                ActiveUseId = "use-1",
                TriggerSource = BuqiLinkTriggerSource.AdjacentUse,
                TriggeredByInstanceId = "attack",
                StateHash = "state-a",
            };
            IReadOnlyList<BuqiLinkTriggerFact> facts = BuqiLinkEngine.ResolveTriggers(
                evaluation.Links,
                new[] { rule },
                context,
                new BuqiLinkExecutionGuard(BuqiLinkExecutionLimits.Default));
            Check(facts.Count == 1 && facts[0].IsTriggered, "Adjacent use must activate the matching listener.");
            Check(facts[0].TriggeredByInstanceId == "attack", "The trigger fact must preserve its event source.");

            context.TriggerSource = BuqiLinkTriggerSource.SelfUse;
            facts = BuqiLinkEngine.ResolveTriggers(
                evaluation.Links,
                new[] { rule },
                context,
                new BuqiLinkExecutionGuard(BuqiLinkExecutionLimits.Default));
            Check(facts.Count == 1 && !facts[0].IsTriggered && facts[0].ReasonCode == "TriggerSourceMismatch",
                "A wrong trigger source must remain queryable as an unmet fact.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void LocalFormationAndExactEcho_AreIndependentLayers()
        {
            BuqiLinkItem attack = Item("attack", "W8-006", 0, 3, BattleQuality.Fixed, BattleEffect.Damage);
            attack.Tags.Add("attack");
            BuqiLinkItem tempo = Item("tempo", "W8-003", 7, 1, BattleQuality.Improved, BattleEffect.Haste);
            tempo.Tags.Add("tempo");
            var board = new BuqiLinkBoard(new[] { attack, tempo });
            BuqiLinkRule link = LinkRule("tempo-edge", BuqiLinkDirection.Clockwise, 100, 1);
            link.SourceCondition.RequiredEffect = BattleEffect.Haste;
            link.TargetCondition.RequiredEffect = BattleEffect.Damage;

            BuqiFormationRule formation = new BuqiFormationRule
            {
                FormationId = "core.attack.tempo",
                Priority = 200,
                Requirements = new List<BuqiFormationRequirement>
                {
                    BuqiFormationRequirement.Items("damage", 1, new BuqiLinkCondition { RequiredEffect = BattleEffect.Damage }),
                    BuqiFormationRequirement.Links("tempo-link", 1,
                        new BuqiLinkCondition { RequiredEffect = BattleEffect.Haste },
                        new BuqiLinkCondition { RequiredEffect = BattleEffect.Damage }),
                },
            };
            var echo = new BuqiEchoBlueprint
            {
                EchoId = "echo.attack.exact",
                Items = new List<BuqiEchoSlot>
                {
                    new BuqiEchoSlot { DefinitionId = "W8-006", AnchorSlot = 0, Quality = (int)BattleQuality.Fixed },
                    new BuqiEchoSlot { DefinitionId = "W8-003", AnchorSlot = 7, Quality = (int)BattleQuality.Fixed },
                },
            };

            BuqiLinkEvaluation evaluation = BuqiLinkEngine.Evaluate(board, new[] { link }, new[] { formation }, echo);

            Check(evaluation.Links.Single().IsConnected, "Local link should be true.");
            Check(evaluation.Formations.Single().IsFormed, "Functional formation should be true.");
            Check(!evaluation.Echo.IsExactMatch, "A quality mismatch must keep exact Echo false.");
            Check(evaluation.Echo.Mismatches.Any(value => value == "tempo:Quality"),
                "Echo mismatch must identify the exact item field.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void DefaultFormationCatalog_ContainsSixCorePathsAndThreeBridges()
        {
            IReadOnlyList<BuqiFormationRule> rules = BuqiFormationCatalog.CreateDefault();
            string[] ids = rules.Select(rule => rule.FormationId).OrderBy(value => value, StringComparer.Ordinal).ToArray();

            Check(ids.Length == 9, "The default catalog must contain exactly six core paths and three bridges.");
            Check(ids.Count(value => value.StartsWith("core.attack.", StringComparison.Ordinal)) == 2, "Attack needs two paths.");
            Check(ids.Count(value => value.StartsWith("core.shield.", StringComparison.Ordinal)) == 2, "Shield needs two paths.");
            Check(ids.Count(value => value.StartsWith("core.recovery.", StringComparison.Ordinal)) == 2, "Recovery needs two paths.");
            Check(ids.Count(value => value.StartsWith("bridge.", StringComparison.Ordinal)) == 3, "Three bridge paths are required.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void TriggerResolution_IsDeterministicAndHonorsStackingAndExclusion()
        {
            BuqiLinkItem target = Item("target", "target", 0, 3, BattleQuality.Fixed, BattleEffect.Damage);
            BuqiLinkItem source = Item("source", "source", 7, 1, BattleQuality.Fixed, BattleEffect.Haste);
            var board = new BuqiLinkBoard(new[] { target, source });
            BuqiLinkRule addLow = LinkRule("add-low", BuqiLinkDirection.Clockwise, 100, 2);
            addLow.StackGroup = "add";
            addLow.StackMode = BuqiLinkStackMode.Add;
            addLow.StackLimit = 2;
            BuqiLinkRule addHigh = LinkRule("add-high", BuqiLinkDirection.Clockwise, 200, 3);
            addHigh.StackGroup = "add";
            addHigh.StackMode = BuqiLinkStackMode.Add;
            addHigh.StackLimit = 2;
            BuqiLinkRule exclusiveLow = LinkRule("exclusive-low", BuqiLinkDirection.Clockwise, 50, 4);
            exclusiveLow.ExclusiveGroup = "stance";
            BuqiLinkRule exclusiveHigh = LinkRule("exclusive-high", BuqiLinkDirection.Clockwise, 300, 5);
            exclusiveHigh.ExclusiveGroup = "stance";
            BuqiLinkRule maxLow = LinkRule("max-low", BuqiLinkDirection.Clockwise, 100, 2);
            maxLow.StackGroup = "max";
            maxLow.StackMode = BuqiLinkStackMode.Max;
            BuqiLinkRule maxHigh = LinkRule("max-high", BuqiLinkDirection.Clockwise, 90, 7);
            maxHigh.StackGroup = "max";
            maxHigh.StackMode = BuqiLinkStackMode.Max;
            BuqiLinkRule[] ordered = { addLow, addHigh, exclusiveLow, exclusiveHigh, maxLow, maxHigh };
            BuqiLinkRule[] reversed = ordered.Reverse().ToArray();
            var context = new BuqiLinkTriggerContext
            {
                Tick = 1,
                RootEventId = "root",
                ActiveUseId = "use",
                TriggerSource = BuqiLinkTriggerSource.SelfUse,
                TriggeredByInstanceId = "source",
                StateHash = "state",
            };

            string first = TriggerDigest(board, ordered, context);
            string second = TriggerDigest(board, reversed, context);

            Check(first == second, "Rule declaration order must not affect trigger facts.");
            Check(first.Contains("add-high:True") && first.Contains("add-low:True"), "Add mode should retain two entries.");
            Check(first.Contains("exclusive-high:True") && first.Contains("exclusive-low:False:ExclusiveSuppressed"),
                "Exclusive mode should retain the highest priority rule.");
            Check(first.Contains("max-high:True") && first.Contains("max-low:False:MaxSuppressed"),
                "Max mode should retain the greatest amount, independent of priority.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void FormationMissingFacts_AreIndependentOfRequirementDeclarationOrder()
        {
            var board = new BuqiLinkBoard(Array.Empty<BuqiLinkItem>());
            BuqiFormationRequirement alpha = BuqiFormationRequirement.Items(
                "alpha", 1, new BuqiLinkCondition { RequiredEffect = BattleEffect.Damage });
            BuqiFormationRequirement zeta = BuqiFormationRequirement.Items(
                "zeta", 1, new BuqiLinkCondition { RequiredEffect = BattleEffect.Buffer });
            BuqiFormationFact first = BuqiLinkEngine.Evaluate(
                board,
                Array.Empty<BuqiLinkRule>(),
                new[] { new BuqiFormationRule { FormationId = "formation", Requirements = new List<BuqiFormationRequirement> { zeta, alpha } } },
                null).Formations.Single();
            BuqiFormationFact second = BuqiLinkEngine.Evaluate(
                board,
                Array.Empty<BuqiLinkRule>(),
                new[] { new BuqiFormationRule { FormationId = "formation", Requirements = new List<BuqiFormationRequirement> { alpha, zeta } } },
                null).Formations.Single();

            Check(string.Join("|", first.MissingRequirements) == string.Join("|", second.MissingRequirements),
                "Equivalent requirement sets must expose identical missing facts.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void DuplicateRuleIds_AreRejectedBeforeEvaluation()
        {
            var board = new BuqiLinkBoard(new[]
            {
                Item("source", "source", 0, 1, BattleQuality.Normal, BattleEffect.Haste),
            });
            bool rejected = false;
            try
            {
                BuqiLinkEngine.Evaluate(
                    board,
                    new[]
                    {
                        LinkRule("duplicate", BuqiLinkDirection.Clockwise, 10, 1),
                        LinkRule("duplicate", BuqiLinkDirection.CounterClockwise, 20, 2),
                    },
                    Array.Empty<BuqiFormationRule>(),
                    null);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }
            Check(rejected, "Duplicate rule ids must fail closed before order can affect behavior.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void DuplicateEchoAnchors_CannotBecomeAnExactMatch()
        {
            var board = new BuqiLinkBoard(new[]
            {
                Item("only", "same", 0, 1, BattleQuality.Normal, BattleEffect.Damage),
            });
            var echo = new BuqiEchoBlueprint
            {
                EchoId = "invalid-echo",
                Items = new List<BuqiEchoSlot>
                {
                    new BuqiEchoSlot { DefinitionId = "same", AnchorSlot = 0, Quality = (int)BattleQuality.Normal },
                    new BuqiEchoSlot { DefinitionId = "same", AnchorSlot = 0, Quality = (int)BattleQuality.Normal },
                },
            };

            BuqiEchoMatchFact fact = BuqiLinkEngine.Evaluate(
                board,
                Array.Empty<BuqiLinkRule>(),
                Array.Empty<BuqiFormationRule>(),
                echo).Echo;

            Check(!fact.IsExactMatch && fact.Mismatches.Contains("slot-0:DuplicateBlueprint"),
                "Malformed Echo blueprints must fail exact matching with an actionable fact.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void ExecutionGuard_EnforcesActiveUseTickDepthAbilityAndCycleCaps()
        {
            var limits = new BuqiLinkExecutionLimits
            {
                MaxTriggersPerTick = 2,
                MaxTriggersPerActiveUse = 1,
                MaxChainDepth = 2,
                MaxAbilityFiresPerRoot = 2,
                MaxSignatureRepeats = 2,
            };
            var guard = new BuqiLinkExecutionGuard(limits);
            BuqiLinkTriggerAttempt attempt = Attempt("rule", "use-1", 0, "state-a");
            Check(guard.TryEnter(attempt, out string reason) && reason == string.Empty, "First trigger should enter.");
            Check(!guard.TryEnter(attempt, out reason) && reason == "ActiveUseCapReached", "Active-use cap should be exact.");

            attempt.ActiveUseId = "use-2";
            Check(guard.TryEnter(attempt, out reason), "A second active use may consume the remaining tick budget.");
            attempt.ActiveUseId = "use-3";
            Check(!guard.TryEnter(attempt, out reason) && reason == "TickCapReached", "Tick cap should be exact.");

            guard = new BuqiLinkExecutionGuard(new BuqiLinkExecutionLimits
            {
                MaxTriggersPerTick = 20,
                MaxTriggersPerActiveUse = 20,
                MaxChainDepth = 2,
                MaxAbilityFiresPerRoot = 20,
                MaxSignatureRepeats = 2,
            });
            attempt = Attempt("cycle", "use", 0, "same-state");
            Check(guard.TryEnter(attempt, out reason), "First signature should enter.");
            Check(guard.TryEnter(attempt, out reason), "Second signature should enter.");
            Check(!guard.TryEnter(attempt, out reason) && reason == "CycleSignatureCapReached",
                "The third identical source/rule/target/state signature must truncate.");
            attempt = Attempt("depth", "use-depth", 3, "state-depth");
            Check(!guard.TryEnter(attempt, out reason) && reason == "ChainDepthCapReached", "Depth cap should be exact.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void BuiltInRefinements_ExposeAllA01ToA06SemanticsThroughRules()
        {
            IBuqiRefinementRule a01 = Refinement("A-01");
            IBuqiRefinementRule a02 = Refinement("A-02");
            IBuqiRefinementRule a03 = Refinement("A-03");
            IBuqiRefinementRule a04 = Refinement("A-04");
            IBuqiRefinementRule a05 = Refinement("A-05");
            IBuqiRefinementRule a06 = Refinement("A-06");

            Check(a01.AdjustBaseCooldownTicks(20) == 17 && a01.OnUseNoise == 1, "A-01 contract changed.");
            Check(a02.AdjustBaseCooldownTicks(20) == 24 && a02.GetEffectMultiplierBps(BattleEffect.Damage, false) == 13000,
                "A-02 contract changed.");
            Check(a03.RewritesFirstActiveUse, "A-03 must expose its rewrite capability.");
            Check(!a04.AllowsModifier(BattleEffect.Haste, false) && !a04.AllowsModifier(BattleEffect.Delay, true),
                "A-04 must reject friendly haste and enemy delay.");
            Check(a05.GetEffectMultiplierBps(BattleEffect.Buffer, false) == 8500 && a05.AdjustNoiseAmount(3) == 2,
                "A-05 contract changed.");
            Check(a06.GetEffectMultiplierBps(BattleEffect.Damage, false) == 13500 && a06.OpeningNoise == 3,
                "A-06 contract changed.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void BattleSimulation_UsesRingForAdjacentResponseAndAdjacentTarget()
        {
            var definitions = new Dictionary<string, BuqiItemDefinition>(StringComparer.Ordinal)
            {
                ["actor"] = Definition("actor", 1, 10,
                    new BuqiEffectSpec
                    {
                        Trigger = BattleTrigger.OnUse,
                        Effect = BattleEffect.Charge,
                        Target = BattleTarget.LeftAdjacentItem,
                        Amount = 1,
                        ReasonCode = "ring-charge",
                    }),
                ["listener"] = Definition("listener", 1, 100,
                    new BuqiEffectSpec
                    {
                        Trigger = BattleTrigger.OnAdjacentUse,
                        Effect = BattleEffect.Damage,
                        Target = BattleTarget.EnemyExecution,
                        Amount = 3,
                        ReasonCode = "ring-listener",
                    }),
                ["dummy"] = Definition("dummy", 1, 100),
            };
            var provider = new DictionaryDefinitionProvider("link-test-v1", definitions);
            var request = new BattleRequest
            {
                RuleVersion = BuqiBattleSimulator.RuleVersion,
                BattleSeed = 9,
                RoundIndex = 1,
                Left = Snapshot("left", "link-test-v1",
                    Instance("actor-i", "actor", 0),
                    Instance("listener-i", "listener", 7)),
                Right = Snapshot("right", "link-test-v1", Instance("dummy-i", "dummy", 4)),
            };

            BuqiBattleSimulator.Simulate(request, provider, out List<BattleEvent> log, out _, out _);

            Check(log.Any(item => item.TargetInstanceId == "listener-i" && item.ReasonCode == "ChargeAdvanced"),
                "Slot zero's counter-clockwise Charge target must resolve to slot seven.");
            Check(log.Any(item => item.SourceInstanceId == "listener-i" && item.ReasonCode == "ring-listener"),
                "Slot seven must receive slot zero's adjacent-use event across the ring boundary.");
        }

#if !BUQI_HEADLESS_TESTS
        [Test]
#endif
        public void BattleSimulation_DeclaresRingRuleVersion()
        {
            Check(BuqiBattleSimulator.RuleVersion == "0.6.0", "Latest S01 rules require a new rule version.");
            Check(BuqiBattleSimulator.SimulationVersion == "battle-core-0.6.0",
                "Simulation version must move with the ring rules.");
        }

        private static string TriggerDigest(
            BuqiLinkBoard board,
            IReadOnlyList<BuqiLinkRule> rules,
            BuqiLinkTriggerContext context)
        {
            BuqiLinkEvaluation evaluation = BuqiLinkEngine.Evaluate(
                board,
                rules,
                Array.Empty<BuqiFormationRule>(),
                null);
            IReadOnlyList<BuqiLinkTriggerFact> facts = BuqiLinkEngine.ResolveTriggers(
                evaluation.Links,
                rules,
                context,
                new BuqiLinkExecutionGuard(BuqiLinkExecutionLimits.Default));
            return string.Join("|", facts.Select(fact =>
                fact.RuleId + ":" + fact.IsTriggered + ":" + fact.ReasonCode));
        }

        private static BuqiLinkTriggerAttempt Attempt(string ruleId, string activeUseId, int depth, string stateHash)
        {
            return new BuqiLinkTriggerAttempt
            {
                Tick = 4,
                RootEventId = "root",
                ActiveUseId = activeUseId,
                RuleId = ruleId,
                SourceInstanceId = "source",
                TargetInstanceId = "target",
                ChainDepth = depth,
                StateHash = stateHash,
            };
        }

        private static BuqiItemDefinition Definition(
            string id,
            int size,
            int cooldown,
            params BuqiEffectSpec[] effects)
        {
            return new BuqiItemDefinition
            {
                DefinitionId = id,
                Size = size,
                BaseCooldownTicks = cooldown,
                Effects = new List<BuqiEffectSpec>(effects),
            };
        }

        private static ItemInstance Instance(string instanceId, string definitionId, int anchor)
        {
            return new ItemInstance
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                AnchorSlot = anchor,
                Quality = (int)BattleQuality.Normal,
            };
        }

        private static BuildSnapshot Snapshot(string id, string contentVersion, params ItemInstance[] items)
        {
            return new BuildSnapshot
            {
                SnapshotId = id,
                ContentVersion = contentVersion,
                ArchetypeId = "link-test",
                InitialExecution = 100,
                Items = new List<ItemInstance>(items),
            };
        }

        private static IBuqiRefinementRule Refinement(string id)
        {
            Check(BuqiRefinementRuleCatalog.TryGet(id, out IBuqiRefinementRule rule), "Missing refinement " + id + ".");
            return rule;
        }

        private static BuqiLinkRule LinkRule(string id, BuqiLinkDirection direction, int priority, int amount)
        {
            return new BuqiLinkRule
            {
                RuleId = id,
                Direction = direction,
                Priority = priority,
                Amount = amount,
                TriggerSource = BuqiLinkTriggerSource.SelfUse,
                SourceCondition = new BuqiLinkCondition(),
                TargetCondition = new BuqiLinkCondition(),
            };
        }

        private static BuqiLinkItem Item(
            string instanceId,
            string definitionId,
            int anchor,
            int size,
            BattleQuality quality,
            params BattleEffect[] effects)
        {
            return new BuqiLinkItem
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                AnchorSlot = anchor,
                Size = size,
                Quality = (int)quality,
                Effects = new HashSet<BattleEffect>(effects),
            };
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void CheckSame(object expected, object actual)
        {
            Check(ReferenceEquals(expected, actual), "Expected the same board item instance.");
        }
    }
}
