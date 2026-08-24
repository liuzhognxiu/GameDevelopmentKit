using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Supply;

namespace Game.Hot.Buqi.Tests
{
    public static class BuqiSupplyTestSuite
    {
        private const int ContractCount = 13;
        private const int MonteCarloSeedCount = 10000;
        private const int BenchmarkAcquisitionBps = 8000;

        public static List<string> RunAll()
        {
            var failures = new List<string>();
            Run("filter-contract", FilterContract, failures);
            Run("catalog-projection-contract", CatalogProjectionContract, failures);
            Run("formal-category-contract", FormalCategoryContract, failures);
            Run("channel-integration-contract", ChannelIntegrationContract, failures);
            Run("deterministic-four-slot-contract", DeterministicFourSlotContract, failures);
            Run("shelf-space-contract", ShelfSpaceContract, failures);
            Run("legal-empty-space-contract", LegalEmptySpaceContract, failures);
            Run("affinity-memory-contract", AffinityMemoryContract, failures);
            Run("bounded-soft-pity-contract", BoundedSoftPityContract, failures);
            Run("repeat-suppression-contract", RepeatSuppressionContract, failures);
            Run("refresh-contract", RefreshContract, failures);
            Run("formation-evaluator-contract", FormationEvaluatorContract, failures);
            Run("monte-carlo-acceptance-contract", MonteCarloAcceptanceContract, failures);
            return failures;
        }

        public static int Main()
        {
            List<string> failures = RunAll();
            foreach (string failure in failures)
                Console.Error.WriteLine(failure);
            Console.WriteLine($"supply-contracts={ContractCount - failures.Count}/{ContractCount}");
            return failures.Count == 0 ? 0 : 1;
        }

        private static void FilterContract()
        {
            List<BuqiSupplyDefinition> definitions = CreateFilterCatalog();
            var service = new BuqiSupplyService(definitions);
            BuqiSupplyRequest request = StrictMerchantRequest();

            Require(service.TryGenerate(
                request,
                BuqiSupplyState.CreateInitial(7331L),
                0,
                out BuqiSupplyShelf shelf,
                out string error), error);

            Require(shelf.Offers.Count == 4, "Strict pool should yield four offers.");
            Require(shelf.Offers.All(item => item.MinimumDay <= 3 && item.MaximumDay >= 3),
                "Day filter leaked an offer.");
            Require(shelf.Offers.All(item => item.Quality == BuqiSupplyQuality.Improved),
                "Quality filter leaked an offer.");
            Require(shelf.Offers.All(item => item.Size == 2), "Size filter leaked an offer.");
            Require(shelf.Offers.All(item => item.ArchetypeId == "fast"),
                "Archetype filter leaked an offer.");
            Require(shelf.Offers.All(item => item.Role == BuqiSupplyProductRole.Bridge),
                "Role filter leaked an offer.");
            Require(shelf.Offers.All(item => item.MerchantPoolIds.Contains("forge")),
                "Merchant pool filter leaked an offer.");
            Require(shelf.Offers.All(item => (item.Sources & BuqiSupplySource.Merchant) != 0),
                "Source filter leaked an offer.");
        }

        private static void CatalogProjectionContract()
        {
            var item = new BuqiSupplyCatalogItem
            {
                DefinitionId = "W8-003",
                ArchetypeId = "fast",
                Category = Game.Hot.BuqiItemCategory.NonWeapon,
                Size = 2,
            };
            item.Tags.AddRange(new[] { "damage", "fast", "damage", " " });
            var rule = new BuqiSupplyAvailabilityRule
            {
                Role = BuqiSupplyProductRole.Mainline,
                MinimumDay = 2,
                MaximumDay = 7,
                Quality = BuqiSupplyQuality.Improved,
                Sources = BuqiSupplySource.Merchant | BuqiSupplySource.Event,
                BaseWeight = 125,
                RefinementId = "A-02",
            };
            rule.MerchantPoolIds.Add("forge");

            Require(BuqiSupplyIntegration.TryCreateDefinition(
                item, rule, out BuqiSupplyDefinition definition, out string error), error);
            Require(definition.DefinitionId == "W8-003" && definition.ArchetypeId == "fast",
                "Formal item identity was not projected into supply metadata.");
            Require(definition.Size == 2 && definition.MinimumDay == 2 && definition.MaximumDay == 7,
                "Size or Day unlock window was not projected.");
            Require(definition.Role == BuqiSupplyProductRole.Mainline &&
                    definition.Quality == BuqiSupplyQuality.Improved &&
                    definition.Sources == (BuqiSupplySource.Merchant | BuqiSupplySource.Event) &&
                    definition.BaseWeight == 125 && definition.RefinementId == "A-02",
                "Availability rule was not projected.");
            Require(definition.Tags.SequenceEqual(new[] { "damage", "fast" }),
                "Formal tags should be trimmed and de-duplicated in stable order.");
            Require(definition.MerchantPoolIds.SequenceEqual(new[] { "forge" }),
                "Merchant pool membership was not projected.");

            item.Size = 0;
            Require(!BuqiSupplyIntegration.TryCreateDefinition(item, rule, out _, out _),
                "Invalid formal item metadata must fail closed.");
        }

        private static void ChannelIntegrationContract()
        {
            var profile = new BuqiSupplyChannelProfile
            {
                ChannelId = "merchant-forge",
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = "forge",
                UnlockDay = 2,
                RetireDay = 7,
                MinimumQuality = BuqiSupplyQuality.Common,
                MaximumQuality = BuqiSupplyQuality.Improved,
                CandidateCount = BuqiSupplyService.MerchantSlotCount,
            };
            profile.AllowedSizes.AddRange(new[] { 1, 2 });
            profile.AllowedArchetypeIds.AddRange(new[] { "fast", "buffer" });
            profile.AllowedRoles.AddRange(new[]
            {
                BuqiSupplyProductRole.Mainline,
                BuqiSupplyProductRole.Bridge,
            });

            Require(!BuqiSupplyIntegration.TryCreateRequest(
                    1, profile, "fast", out _, out _),
                "A merchant must remain locked before its unlock Day.");
            Require(!BuqiSupplyIntegration.TryCreateRequest(
                    8, profile, "fast", out _, out _),
                "A merchant must remain unavailable after its retire Day.");
            Require(BuqiSupplyIntegration.TryCreateRequest(
                4, profile, "fast", out BuqiSupplyRequest request, out string error), error);
            Require(request.Day == 4 && request.Source == BuqiSupplySource.Merchant &&
                    request.MerchantPoolId == "forge" && request.PreferredArchetypeId == "fast",
                "Formal merchant context did not map to the supply request.");
            Require(request.CandidateCount == 4 &&
                    request.AllowedSizes.SequenceEqual(new[] { 1, 2 }) &&
                    request.AllowedArchetypeIds.SequenceEqual(new[] { "fast", "buffer" }) &&
                    request.AllowedRoles.SequenceEqual(profile.AllowedRoles),
                "Merchant filters did not cross the narrow integration boundary.");
            Require(!BuqiSupplyIntegration.TryCreateRequest(
                    BuqiRunRules.RunDayCount + 1, profile, "fast", out _, out _),
                "Supply requests outside the nine-Day run must fail closed.");

            profile.Source = BuqiSupplySource.Pve;
            profile.UnlockDay = 1;
            profile.RetireDay = BuqiRunRules.RunDayCount;
            profile.CandidateCount = 2;
            Require(!BuqiSupplyIntegration.TryCreateRequest(
                    6, profile, "buffer", out _, out _),
                "A reward channel must reject merchant-only pool metadata.");
            profile.MerchantPoolId = string.Empty;
            Require(BuqiSupplyIntegration.TryCreateRequest(
                6, profile, "buffer", out BuqiSupplyRequest pveRequest, out string pveError), pveError);
            Require(pveRequest.Source == BuqiSupplySource.Pve &&
                    pveRequest.CandidateCount == 2 &&
                    string.IsNullOrEmpty(pveRequest.MerchantPoolId),
                "PVE rewards must consume the same channel interface without merchant metadata.");

            var service = new BuqiSupplyService(CreateGeneralCatalog());
            BuqiSupplyState initial = BuqiSupplyState.CreateInitial(8128L);
            BuqiSupplyState fast = BuqiSupplyIntegration.ApplyBuildPreference(
                service, initial, "fast", new[] { "damage", "fast", "damage" });
            BuqiSupplyState heal = BuqiSupplyIntegration.ApplyBuildPreference(
                service, fast, "heal", new[] { "sustain" });
            Require(fast.TagMemory["fast"].PreferenceBps == 2000 &&
                    fast.TagMemory["damage"].PreferenceBps == 2000,
                "Archetype and build tags must feed the bounded pity memory exactly once.");
            Require(heal.TagMemory["fast"].PreferenceBps == 1200 &&
                    heal.TagMemory["heal"].PreferenceBps == 2000,
                "A build pivot must decay old affinity and establish the new archetype.");
            Require(!initial.TagMemory.Any(), "Preference integration must not mutate its source state.");

            var shelf = new BuqiSupplyShelf
            {
                Offers = new List<BuqiSupplyDefinition>
                {
                    new BuqiSupplyDefinition { DefinitionId = "W8-003" },
                    new BuqiSupplyDefinition { DefinitionId = "W8-007" },
                },
            };
            Require(BuqiSupplyIntegration.GetOfferDefinitionIds(shelf)
                    .SequenceEqual(new[] { "W8-003", "W8-007" }),
                "Supply offers must bridge to formal encounter CandidateIds in order.");
        }

        private static void DeterministicFourSlotContract()
        {
            List<BuqiSupplyDefinition> definitions = CreateGeneralCatalog();
            var service = new BuqiSupplyService(definitions);
            var request = new BuqiSupplyRequest
            {
                Day = 4,
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = "general",
                PreferredArchetypeId = "fast",
            };

            Require(service.TryGenerate(request, BuqiSupplyState.CreateInitial(99173L), 0,
                out BuqiSupplyShelf left, out string leftError), leftError);
            Require(service.TryGenerate(request, BuqiSupplyState.CreateInitial(99173L), 0,
                out BuqiSupplyShelf right, out string rightError), rightError);

            Require(left.Offers.Count == 4, "Merchant shelves must contain four offers.");
            Require(left.SlotPurposes.SequenceEqual(new[]
            {
                BuqiSupplySlotPurpose.Mainline,
                BuqiSupplySlotPurpose.Bridge,
                BuqiSupplySlotPurpose.CounterOrEconomy,
                BuqiSupplySlotPurpose.Wildcard,
            }), "Four slot purposes are not stable.");
            Require(left.Offers.Select(item => item.DefinitionId).Distinct().Count() == 4,
                "Weighted draws must be without replacement by definition id.");
            Require(left.Offers.Select(item => item.DefinitionId)
                    .SequenceEqual(right.Offers.Select(item => item.DefinitionId)),
                "Same seed and state produced different shelves.");
            Require(left.NextState.Cursor == right.NextState.Cursor,
                "Same seed and state produced different RNG cursors.");
        }

        private static void AffinityMemoryContract()
        {
            var service = new BuqiSupplyService(CreateGeneralCatalog());
            BuqiSupplyState fast = service.ShiftAffinity(
                BuqiSupplyState.CreateInitial(45L), new[] { "fast" });
            BuqiSupplyState buffer = service.ShiftAffinity(fast, new[] { "buffer" });

            Require(fast.TagMemory["fast"].PreferenceBps == 2000,
                "First explicit affinity signal should add 2000 bps.");
            Require(buffer.TagMemory["fast"].PreferenceBps == 1200,
                "Old affinity should decay to 60 percent after a pivot.");
            Require(buffer.TagMemory["buffer"].PreferenceBps == 2000,
                "New affinity should receive the full explicit signal.");

            BuqiSupplyDefinition acquired = CreateGeneralCatalog().First(item => item.Tags.Contains("heal"));
            BuqiSupplyState afterAcquisition = service.RecordAcquired(buffer, acquired);
            Require(afterAcquisition.TagMemory["heal"].PreferenceBps == 2500,
                "Acquired tags should become a bounded implicit affinity signal.");
            Require(afterAcquisition.TagMemory["heal"].AcquiredAge == 0,
                "Acquired tag recency was not recorded.");
            Require(buffer.TagMemory.ContainsKey("heal") == false,
                "Affinity updates must not mutate the source state.");
        }

        private static void FormalCategoryContract()
        {
            var item = new BuqiSupplyCatalogItem
            {
                DefinitionId = "classified-item",
                ArchetypeId = "fast",
                Size = 1,
                Category = Game.Hot.BuqiItemCategory.Unknown,
            };
            var rule = new BuqiSupplyAvailabilityRule
            {
                Role = BuqiSupplyProductRole.Mainline,
                Quality = BuqiSupplyQuality.Common,
            };
            Require(!BuqiSupplyIntegration.TryCreateDefinition(item, rule, out _, out _),
                "Supply projection must reject a missing formal item category.");

            item.Category = Game.Hot.BuqiItemCategory.Weapon;
            Require(BuqiSupplyIntegration.TryCreateDefinition(
                item, rule, out BuqiSupplyDefinition definition, out string error), error);
            Require(definition.Category == Game.Hot.BuqiItemCategory.Weapon,
                "Supply projection must preserve the authored weapon category.");
        }

        private static void ShelfSpaceContract()
        {
            var service = new BuqiSupplyService(CreateGeneralCatalog());
            var request = new BuqiSupplyRequest
            {
                Day = 5,
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = "general",
                PreferredArchetypeId = "fast",
                ShelfSlotBudget = BuqiSupplyService.MerchantShelfSlotCount,
            };

            Require(service.TryGenerate(request, BuqiSupplyState.CreateInitial(21991L), 0,
                out BuqiSupplyShelf left, out string leftError), leftError);
            Require(service.TryGenerate(request, BuqiSupplyState.CreateInitial(21991L), 0,
                out BuqiSupplyShelf right, out string rightError), rightError);

            var occupied = new bool[BuqiSupplyService.MerchantShelfSlotCount];
            int expectedAnchor = 0;
            foreach (BuqiSupplyDefinition offer in left.Offers)
            {
                Require(offer.Size >= 1 && offer.Size <= 3, "Shelf item size must be 1/2/3 slots.");
                Require(offer.AnchorSlot == expectedAnchor,
                    "Generated merchant offers must be compact from the left edge.");
                Require(offer.AnchorSlot + offer.Size <= occupied.Length,
                    "A merchant offer crossed the ten-slot shelf boundary.");
                for (int slot = offer.AnchorSlot; slot < offer.AnchorSlot + offer.Size; slot++)
                {
                    Require(!occupied[slot], "Merchant offers overlapped on the shelf.");
                    occupied[slot] = true;
                }
                expectedAnchor += offer.Size;
            }

            Require(expectedAnchor <= BuqiSupplyService.MerchantShelfSlotCount,
                "Merchant shelf occupancy exceeded ten slots.");
            Require(left.Offers.Select(offer => offer.DefinitionId)
                    .SequenceEqual(right.Offers.Select(offer => offer.DefinitionId)) &&
                    left.Offers.Select(offer => offer.AnchorSlot)
                    .SequenceEqual(right.Offers.Select(offer => offer.AnchorSlot)),
                "Same seed and request must produce identical offers and anchors.");
        }

        private static void LegalEmptySpaceContract()
        {
            var definitions = new List<BuqiSupplyDefinition>
            {
                Definition("large-a", "fast", BuqiSupplyProductRole.Mainline,
                    1, 3, BuqiSupplyQuality.Common, BuqiSupplySource.Merchant, "sparse"),
                Definition("large-b", "buffer", BuqiSupplyProductRole.Bridge,
                    1, 3, BuqiSupplyQuality.Common, BuqiSupplySource.Merchant, "sparse"),
            };
            var request = new BuqiSupplyRequest
            {
                Day = 1,
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = "sparse",
                ShelfSlotBudget = BuqiSupplyService.MerchantShelfSlotCount,
            };

            Require(new BuqiSupplyService(definitions).TryGenerate(
                request, BuqiSupplyState.CreateInitial(81L), 0,
                out BuqiSupplyShelf shelf, out string error), error);
            Require(shelf.Offers.Count == 2 && shelf.Offers.Sum(offer => offer.Size) == 6,
                "A short legal pool must keep its valid offers.");
            Require(shelf.EmptySlots.SequenceEqual(new[] { 6, 7, 8, 9 }),
                "An unfillable shelf remainder must stay as explicit legal empty slots.");
        }

        private static void BoundedSoftPityContract()
        {
            var service = new BuqiSupplyService(CreateBiasCatalog());
            var request = new BuqiSupplyRequest
            {
                Day = 5,
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = "general",
                CandidateCount = 1,
            };

            int neutral = CountFirstOffer(service, request, 4000, false, false);
            int pitied = CountFirstOffer(service, request, 4000, true, false);
            double neutralRate = neutral / 4000.0;
            double pityRate = pitied / 4000.0;

            Require(pityRate > neutralRate + 0.20,
                $"Soft pity was too weak: neutral={neutralRate:F3}, pity={pityRate:F3}.");
            Require(pityRate < 0.90,
                $"Soft pity became a hard answer: pity={pityRate:F3}.");
        }

        private static void RepeatSuppressionContract()
        {
            var service = new BuqiSupplyService(CreateBiasCatalog());
            var request = new BuqiSupplyRequest
            {
                Day = 5,
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = "general",
                CandidateCount = 1,
            };

            int neutral = CountFirstOffer(service, request, 4000, false, false);
            int repeated = CountFirstOffer(service, request, 4000, false, true);
            double neutralRate = neutral / 4000.0;
            double repeatedRate = repeated / 4000.0;

            Require(repeatedRate < neutralRate * 0.60,
                $"Immediate repeats were not sufficiently suppressed: neutral={neutralRate:F3}, repeat={repeatedRate:F3}.");
            Require(repeatedRate > 0.01,
                "Repeat suppression must not become a hard ban.");
        }

        private static void RefreshContract()
        {
            var service = new BuqiSupplyService(CreateGeneralCatalog());
            var request = new BuqiSupplyRequest
            {
                Day = 5,
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = "general",
                PreferredArchetypeId = "buffer",
            };
            Require(service.TryGenerate(request, BuqiSupplyState.CreateInitial(5137L), 0,
                out BuqiSupplyShelf initial, out string initialError), initialError);
            Require(initial.RefreshPricePaid == 0 && initial.NextRefreshPrice == 2,
                "Initial merchant shelf refresh pricing is incorrect.");

            Require(service.TryRefresh(request, initial, out BuqiSupplyShelf first, out string firstError), firstError);
            Require(first.RefreshIndex == 1 && first.RefreshPricePaid == 2 && first.NextRefreshPrice == 3,
                "First refresh pricing is incorrect.");
            Require(service.TryRefresh(request, initial, out BuqiSupplyShelf replay, out string replayError), replayError);
            Require(first.Offers.Select(item => item.DefinitionId)
                    .SequenceEqual(replay.Offers.Select(item => item.DefinitionId)),
                "Refreshing the same frozen shelf is not deterministic.");

            Require(service.TryRefresh(request, first, out BuqiSupplyShelf second, out string secondError), secondError);
            Require(second.RefreshPricePaid == 3 && second.NextRefreshPrice == 4,
                "Second refresh pricing is incorrect.");
            Require(service.TryRefresh(request, second, out BuqiSupplyShelf third, out string thirdError), thirdError);
            Require(third.RefreshPricePaid == 4 && third.NextRefreshPrice == -1,
                "Final refresh pricing is incorrect.");
            Require(!service.TryRefresh(request, third, out _, out _),
                "Merchant refresh count must be capped.");

            var eventRequest = new BuqiSupplyRequest
            {
                Day = 5,
                Source = BuqiSupplySource.Event,
                CandidateCount = 2,
            };
            Require(service.TryGenerate(eventRequest, BuqiSupplyState.CreateInitial(77L), 0,
                out BuqiSupplyShelf eventShelf, out string eventError), eventError);
            Require(eventShelf.Offers.All(item => (item.Sources & BuqiSupplySource.Event) != 0),
                "Event consumer received an incompatible offer.");
            Require(!service.TryRefresh(eventRequest, eventShelf, out _, out _),
                "Non-merchant consumers must not refresh.");
        }

        private static void FormationEvaluatorContract()
        {
            List<BuqiSupplyDefinition> catalog = CreateFormationCatalog();
            BuqiFunctionalFormationRule rule = FormationRule("fast");
            var partial = new List<BuqiSupplyDefinition>
            {
                Find(catalog, "fast-engine"),
                Find(catalog, "fast-bridge"),
            };
            Require(!BuqiSupplyFormationEvaluator.IsFunctional(rule, partial),
                "Partial build was incorrectly classified as functionally formed.");
            partial.Add(Find(catalog, "fast-core"));
            Require(BuqiSupplyFormationEvaluator.IsFunctional(rule, partial),
                "Engine, bridge, and core should be functionally formed.");

            BuqiExactEchoRecipe recipe = EchoRecipe("fast");
            var ordinary = partial.Select(BuqiSupplyOwnedItem.FromDefinition).ToList();
            Require(!BuqiSupplyFormationEvaluator.IsExactEcho(recipe, ordinary),
                "Common unrefined items must not reproduce an exact Echo.");
            var exact = new List<BuqiSupplyOwnedItem>
            {
                Owned("fast-engine", BuqiSupplyQuality.Common, string.Empty, 0),
                Owned("fast-bridge", BuqiSupplyQuality.Improved, "A-02", 1),
                Owned("fast-core", BuqiSupplyQuality.Finalized, "A-06", 3),
            };
            Require(BuqiSupplyFormationEvaluator.IsExactEcho(recipe, exact),
                "Matching definition, quality, refinement, and slot should reproduce an Echo.");
        }

        private static void MonteCarloAcceptanceContract()
        {
            // Benchmark: 10,000 fixed seeds per route, one merchant shelf per day,
            // a conditional refresh from Day 4, and an independent 80% acquisition stream.
            // D3/D6/D9 bands: formed 15-40/60-82/88-96%, core seen
            // 40-65/75-92/94-99%, core acquired 30-50/65-88/88-98%.
            List<BuqiSupplyDefinition> catalog = CreateFormationCatalog();
            var service = new BuqiSupplyService(catalog);
            foreach (string route in new[] { "fast", "buffer", "heal" })
            {
                FormationRates rates = SimulateRoute(service, catalog, route);
                Console.WriteLine(rates.Format(route));

                // Structural formation targets. Exact Echo is intentionally a separate rare chase.
                RequireBetween(rates.Formed[0], 0.15, 0.40, route, "Day 3 functional formation");
                RequireBetween(rates.Formed[1], 0.60, 0.82, route, "Day 6 functional formation");
                RequireBetween(rates.Formed[2], 0.88, 0.96, route, "Day 9 functional formation");
                RequireBetween(rates.CoreSeen[0], 0.40, 0.65, route, "Day 3 core seen");
                RequireBetween(rates.CoreSeen[1], 0.75, 0.92, route, "Day 6 core seen");
                RequireBetween(rates.CoreSeen[2], 0.94, 0.99, route, "Day 9 core seen");
                RequireBetween(rates.CoreAcquired[0], 0.30, 0.50, route, "Day 3 core acquired");
                RequireBetween(rates.CoreAcquired[1], 0.65, 0.88, route, "Day 6 core acquired");
                RequireBetween(rates.CoreAcquired[2], 0.88, 0.98, route, "Day 9 core acquired");
                Require(rates.CoreAcquired[0] <= rates.CoreSeen[0] &&
                        rates.CoreAcquired[1] <= rates.CoreSeen[1] &&
                        rates.CoreAcquired[2] <= rates.CoreSeen[2],
                    $"{route}: acquired core rate exceeded seen rate.");
                Require(rates.ExactEcho[2] >= 0.0 && rates.ExactEcho[2] <= 0.03,
                    $"{route}: exact Echo rate escaped the 0-3% chase band.");
            }
        }

        private static FormationRates SimulateRoute(
            BuqiSupplyService service,
            IReadOnlyList<BuqiSupplyDefinition> catalog,
            string route)
        {
            var formed = new int[3];
            var coreSeen = new int[3];
            var coreAcquired = new int[3];
            var exactEcho = new int[3];
            int[] milestones = { 3, 6, 9 };
            BuqiFunctionalFormationRule formationRule = FormationRule(route);
            BuqiExactEchoRecipe echoRecipe = EchoRecipe(route);
            BuqiSupplyDefinition starter = Find(catalog, $"{route}-engine");

            for (int seed = 1; seed <= MonteCarloSeedCount; seed++)
            {
                int acquisitionCursor = 0;
                long acquisitionSeed = seed ^ 0x5F3759DFL;
                BuqiSupplyState state = BuqiSupplyState.CreateInitial(seed);
                state = service.ShiftAffinity(state, new[] { route, "bridge", "core" });
                state = service.RecordAcquired(state, starter);
                var owned = new List<BuqiSupplyDefinition> { starter };
                bool sawCore = false;
                bool acquiredCore = false;

                for (int day = 1; day <= 9; day++)
                {
                    var request = new BuqiSupplyRequest
                    {
                        Day = day,
                        Source = BuqiSupplySource.Merchant,
                        MerchantPoolId = "general",
                        PreferredArchetypeId = route,
                    };
                    Require(service.TryGenerate(request, state, 0,
                        out BuqiSupplyShelf shelf, out string error), error);
                    BuqiSupplyDefinition picked = PickMissing(route, shelf.Offers, owned);
                    sawCore |= shelf.Offers.Any(item => item.DefinitionId == $"{route}-core");

                    if (picked == null && day >= 4)
                    {
                        Require(service.TryRefresh(request, shelf,
                            out BuqiSupplyShelf refreshed, out string refreshError), refreshError);
                        sawCore |= refreshed.Offers.Any(item => item.DefinitionId == $"{route}-core");
                        picked = PickMissing(route, refreshed.Offers, owned);
                        shelf = refreshed;
                    }

                    state = shelf.NextState;
                    // A separate deterministic stream models budget and player choice.
                    if (picked != null && BuqiRunRandom.Next(
                            acquisitionSeed, ref acquisitionCursor, 10000) < BenchmarkAcquisitionBps)
                    {
                        owned.Add(picked);
                        state = service.RecordAcquired(state, picked);
                        acquiredCore |= picked.DefinitionId == $"{route}-core";
                    }

                    int milestone = Array.IndexOf(milestones, day);
                    if (milestone < 0)
                        continue;
                    if (sawCore)
                        coreSeen[milestone]++;
                    if (acquiredCore)
                        coreAcquired[milestone]++;
                    if (BuqiSupplyFormationEvaluator.IsFunctional(formationRule, owned))
                        formed[milestone]++;
                    if (BuqiSupplyFormationEvaluator.IsExactEcho(
                            echoRecipe,
                            owned.Select(BuqiSupplyOwnedItem.FromDefinition)))
                    {
                        exactEcho[milestone]++;
                    }
                }
            }

            return new FormationRates(
                ToRates(formed),
                ToRates(coreSeen),
                ToRates(coreAcquired),
                ToRates(exactEcho));
        }

        private static BuqiSupplyDefinition PickMissing(
            string route,
            IReadOnlyList<BuqiSupplyDefinition> offers,
            IReadOnlyList<BuqiSupplyDefinition> owned)
        {
            var ownedIds = new HashSet<string>(owned.Select(item => item.DefinitionId), StringComparer.Ordinal);
            return offers
                .Where(item => item.ArchetypeId == route && !ownedIds.Contains(item.DefinitionId))
                .OrderByDescending(item => item.Tags.Contains("core"))
                .ThenByDescending(item => item.Role == BuqiSupplyProductRole.Bridge)
                .ThenBy(item => item.DefinitionId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static List<BuqiSupplyDefinition> CreateFormationCatalog()
        {
            var result = new List<BuqiSupplyDefinition>();
            foreach (string route in new[]
                     {
                         "fast", "buffer", "heal", "chain", "poison", "burn", "freeze", "overload",
                     })
            {
                BuqiSupplyDefinition engine = Definition($"{route}-engine", route,
                    BuqiSupplyProductRole.Mainline, 1, 1, BuqiSupplyQuality.Common,
                    BuqiSupplySource.All, "general");
                engine.Tags.AddRange(new[] { route, "engine" });
                result.Add(engine);

                BuqiSupplyDefinition bridge = Definition($"{route}-bridge", route,
                    BuqiSupplyProductRole.Bridge, 1, 2, BuqiSupplyQuality.Common,
                    BuqiSupplySource.All, "general");
                bridge.Tags.AddRange(new[] { route, "bridge" });
                result.Add(bridge);

                BuqiSupplyDefinition core = Definition($"{route}-core", route,
                    BuqiSupplyProductRole.Mainline, 2, 3, BuqiSupplyQuality.Common,
                    BuqiSupplySource.All, "general");
                core.Tags.AddRange(new[] { route, "core" });
                result.Add(core);
            }
            return result;
        }

        private static BuqiFunctionalFormationRule FormationRule(string route)
        {
            var result = new BuqiFunctionalFormationRule
            {
                ArchetypeId = route,
                MinimumDistinctDefinitions = 3,
            };
            result.RequiredRoles.Add(BuqiSupplyProductRole.Mainline);
            result.RequiredRoles.Add(BuqiSupplyProductRole.Bridge);
            result.RequiredTags.Add("engine");
            result.RequiredTags.Add("core");
            return result;
        }

        private static BuqiExactEchoRecipe EchoRecipe(string route)
        {
            var result = new BuqiExactEchoRecipe { EchoId = $"echo-{route}-exact" };
            result.Parts.Add(Part($"{route}-engine", BuqiSupplyQuality.Common, string.Empty, 0));
            result.Parts.Add(Part($"{route}-bridge", BuqiSupplyQuality.Improved, "A-02", 1));
            result.Parts.Add(Part($"{route}-core", BuqiSupplyQuality.Finalized, "A-06", 3));
            return result;
        }

        private static BuqiExactEchoPart Part(
            string definitionId,
            BuqiSupplyQuality quality,
            string refinementId,
            int anchorSlot)
        {
            return new BuqiExactEchoPart
            {
                DefinitionId = definitionId,
                Quality = quality,
                RefinementId = refinementId,
                AnchorSlot = anchorSlot,
            };
        }

        private static BuqiSupplyOwnedItem Owned(
            string definitionId,
            BuqiSupplyQuality quality,
            string refinementId,
            int anchorSlot)
        {
            return new BuqiSupplyOwnedItem
            {
                DefinitionId = definitionId,
                Quality = quality,
                RefinementId = refinementId,
                AnchorSlot = anchorSlot,
            };
        }

        private static BuqiSupplyDefinition Find(
            IEnumerable<BuqiSupplyDefinition> catalog,
            string definitionId)
        {
            return catalog.First(item => item.DefinitionId == definitionId);
        }

        private static double[] ToRates(int[] counts)
        {
            return counts.Select(count => count / (double)MonteCarloSeedCount).ToArray();
        }

        private static void RequireBetween(
            double actual,
            double minimum,
            double maximum,
            string route,
            string metric)
        {
            Require(actual >= minimum && actual <= maximum,
                $"{route}: {metric} {actual:P2} escaped [{minimum:P0}, {maximum:P0}].");
        }

        private sealed class FormationRates
        {
            public readonly double[] Formed;
            public readonly double[] CoreSeen;
            public readonly double[] CoreAcquired;
            public readonly double[] ExactEcho;

            public FormationRates(
                double[] formed,
                double[] coreSeen,
                double[] coreAcquired,
                double[] exactEcho)
            {
                Formed = formed;
                CoreSeen = coreSeen;
                CoreAcquired = coreAcquired;
                ExactEcho = exactEcho;
            }

            public string Format(string route)
            {
                return $"route={route} " +
                       $"formed={Triplet(Formed)} " +
                       $"coreSeen={Triplet(CoreSeen)} " +
                       $"coreAcquired={Triplet(CoreAcquired)} " +
                       $"exactEcho={Triplet(ExactEcho)}";
            }

            private static string Triplet(IReadOnlyList<double> values)
            {
                return $"{values[0]:P2}/{values[1]:P2}/{values[2]:P2}";
            }
        }

        private static int CountFirstOffer(
            BuqiSupplyService service,
            BuqiSupplyRequest request,
            int seedCount,
            bool pity,
            bool repeat)
        {
            int count = 0;
            for (int seed = 1; seed <= seedCount; seed++)
            {
                BuqiSupplyState state = BuqiSupplyState.CreateInitial(seed);
                if (pity)
                {
                    state.TagMemory["wanted"] = new BuqiSupplyTagMemory
                    {
                        PreferenceBps = 10000,
                        MissStreak = 100,
                    };
                }
                if (repeat)
                    state.LastOfferDefinitionIds.Add("wanted-item");

                Require(service.TryGenerate(request, state, 0,
                    out BuqiSupplyShelf shelf, out string error), error);
                if (shelf.Offers[0].DefinitionId == "wanted-item")
                    count++;
            }
            return count;
        }

        private static List<BuqiSupplyDefinition> CreateBiasCatalog()
        {
            var result = new List<BuqiSupplyDefinition>();
            for (int index = 0; index < 8; index++)
            {
                BuqiSupplyDefinition definition = Definition(
                    index == 0 ? "wanted-item" : $"other-{index}",
                    "neutral",
                    BuqiSupplyProductRole.Wildcard,
                    1,
                    1,
                    BuqiSupplyQuality.Common,
                    BuqiSupplySource.All,
                    "general");
                definition.Tags.Add(index == 0 ? "wanted" : $"other-{index}");
                result.Add(definition);
            }
            return result;
        }

        private static BuqiSupplyRequest StrictMerchantRequest()
        {
            var request = new BuqiSupplyRequest
            {
                Day = 3,
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = "forge",
                MinimumQuality = BuqiSupplyQuality.Improved,
                MaximumQuality = BuqiSupplyQuality.Improved,
            };
            request.AllowedSizes.Add(2);
            request.AllowedArchetypeIds.Add("fast");
            request.AllowedRoles.Add(BuqiSupplyProductRole.Bridge);
            return request;
        }

        private static List<BuqiSupplyDefinition> CreateFilterCatalog()
        {
            var result = new List<BuqiSupplyDefinition>();
            for (int index = 0; index < 4; index++)
            {
                result.Add(Definition($"valid-{index}", "fast", BuqiSupplyProductRole.Bridge,
                    2, 2, BuqiSupplyQuality.Improved, BuqiSupplySource.Merchant, "forge"));
            }

            result.Add(Definition("wrong-day", "fast", BuqiSupplyProductRole.Bridge,
                4, 2, BuqiSupplyQuality.Improved, BuqiSupplySource.Merchant, "forge"));
            result.Add(Definition("wrong-quality", "fast", BuqiSupplyProductRole.Bridge,
                1, 2, BuqiSupplyQuality.Common, BuqiSupplySource.Merchant, "forge"));
            result.Add(Definition("wrong-size", "fast", BuqiSupplyProductRole.Bridge,
                1, 3, BuqiSupplyQuality.Improved, BuqiSupplySource.Merchant, "forge"));
            result.Add(Definition("wrong-archetype", "buffer", BuqiSupplyProductRole.Bridge,
                1, 2, BuqiSupplyQuality.Improved, BuqiSupplySource.Merchant, "forge"));
            result.Add(Definition("wrong-role", "fast", BuqiSupplyProductRole.Counter,
                1, 2, BuqiSupplyQuality.Improved, BuqiSupplySource.Merchant, "forge"));
            result.Add(Definition("wrong-pool", "fast", BuqiSupplyProductRole.Bridge,
                1, 2, BuqiSupplyQuality.Improved, BuqiSupplySource.Merchant, "grove"));
            result.Add(Definition("wrong-source", "fast", BuqiSupplyProductRole.Bridge,
                1, 2, BuqiSupplyQuality.Improved, BuqiSupplySource.Event, "forge"));
            return result;
        }

        internal static List<BuqiSupplyDefinition> CreateGeneralCatalog()
        {
            var result = new List<BuqiSupplyDefinition>();
            string[] archetypes = { "fast", "buffer", "heal", "chain" };
            BuqiSupplyProductRole[] roles =
            {
                BuqiSupplyProductRole.Mainline,
                BuqiSupplyProductRole.Bridge,
                BuqiSupplyProductRole.Counter,
                BuqiSupplyProductRole.Economy,
                BuqiSupplyProductRole.Wildcard,
            };
            for (int index = 0; index < 24; index++)
            {
                BuqiSupplyDefinition definition = Definition(
                    $"item-{index:00}",
                    archetypes[index % archetypes.Length],
                    roles[index % roles.Length],
                    1 + (index % 3),
                    1 + (index % 3),
                    BuqiSupplyQuality.Common,
                    BuqiSupplySource.All,
                    "general");
                definition.Tags.Add(definition.ArchetypeId);
                definition.Tags.Add(definition.Role.ToString().ToLowerInvariant());
                result.Add(definition);
            }
            return result;
        }

        internal static BuqiSupplyDefinition Definition(
            string id,
            string archetype,
            BuqiSupplyProductRole role,
            int minimumDay,
            int size,
            BuqiSupplyQuality quality,
            BuqiSupplySource sources,
            string merchantPool)
        {
            var result = new BuqiSupplyDefinition
            {
                DefinitionId = id,
                ArchetypeId = archetype,
                Role = role,
                MinimumDay = minimumDay,
                MaximumDay = 9,
                Size = size,
                Quality = quality,
                Sources = sources,
                BaseWeight = 100,
            };
            result.MerchantPoolIds.Add(merchantPool);
            return result;
        }

        private static void Run(string name, Action contract, List<string> failures)
        {
            try
            {
                contract();
            }
            catch (Exception exception)
            {
                failures.Add($"{name}: {exception.Message}");
            }
        }

        internal static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
