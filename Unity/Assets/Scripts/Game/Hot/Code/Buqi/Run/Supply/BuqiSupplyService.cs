using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Supply
{
    public sealed class BuqiSupplyService
    {
        public const int MerchantSlotCount = 4;
        public const int MerchantOfferCount = 8;
        public const int MerchantShelfSlotCount = 10;
        public const int MaximumRefreshCount = 3;
        public const int FirstRefreshPrice = 2;
        public const int MaximumRefreshPrice = 6;

        private const int NeutralFactorBps = 10000;
        private const int MainlineArchetypeFactorBps = 18000;
        private const int MainlineRoleFactorBps = 14000;
        private const int PurposeRoleFactorBps = 20000;
        private const int WildcardRoleFactorBps = 14000;
        private const int AffinityDecayBps = 6000;
        private const int ExplicitAffinitySignalBps = 2000;
        private const int AcquisitionAffinitySignalBps = 2500;
        private const int MaximumAffinityBps = 10000;
        private const int MissPityStepBps = 1000;
        private const int MaximumMissPityBps = 40000;
        private const int RecentAcquisitionFactorBps = 11500;
        private const int ImmediateRepeatFactorBps = 2500;
        private const int PriorRepeatFactorBps = 6000;
        private const int MaximumCandidateWeight = 1000000;

        private static readonly BuqiSupplySlotPurpose[] s_MerchantPurposes =
        {
            BuqiSupplySlotPurpose.Mainline,
            BuqiSupplySlotPurpose.Bridge,
            BuqiSupplySlotPurpose.CounterOrEconomy,
            BuqiSupplySlotPurpose.Wildcard,
        };

        private readonly List<BuqiSupplyDefinition> m_Definitions;

        public BuqiSupplyService(IEnumerable<BuqiSupplyDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            m_Definitions = definitions
                .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.DefinitionId))
                .Select(definition => definition.Clone())
                .OrderBy(definition => definition.DefinitionId, StringComparer.Ordinal)
                .ThenBy(definition => definition.Quality)
                .ToList();
        }

        public bool TryGenerate(
            BuqiSupplyRequest request,
            BuqiSupplyState source,
            int refreshIndex,
            out BuqiSupplyShelf shelf,
            out string error)
        {
            shelf = null!;
            if (!ValidateRequest(request, source, refreshIndex, out error))
                return false;

            List<BuqiSupplyDefinition> eligible = m_Definitions
                .Where(definition => IsEligible(definition, request))
                .ToList();
            int distinctCount = eligible.Select(definition => definition.DefinitionId)
                .Distinct(StringComparer.Ordinal).Count();
            if (request.ShelfSlotBudget == 0 && distinctCount < request.CandidateCount)
            {
                error = "Eligible supply pool does not contain enough distinct definitions.";
                return false;
            }
            if (eligible.Count == 0)
            {
                error = "Eligible supply pool is empty.";
                return false;
            }

            BuqiSupplyState next = source.Clone();
            int capacity = request.ShelfSlotBudget > 0
                ? request.ShelfSlotBudget
                : request.CandidateCount;
            var offers = new List<BuqiSupplyDefinition>(capacity);
            var purposes = new List<BuqiSupplySlotPurpose>(capacity);
            int occupiedSlotCount = 0;
            int targetOfferCount = request.ShelfSlotBudget > 0
                ? int.MaxValue
                : request.CandidateCount;
            while (offers.Count < targetOfferCount)
            {
                int remainingSlots = request.ShelfSlotBudget - occupiedSlotCount;
                List<BuqiSupplyDefinition> candidates = request.ShelfSlotBudget > 0
                    ? eligible.Where(definition => definition.Size <= remainingSlots).ToList()
                    : eligible;
                if (candidates.Count == 0)
                    break;

                BuqiSupplySlotPurpose purpose = s_MerchantPurposes[
                    offers.Count % s_MerchantPurposes.Length];
                int selectedIndex = DrawWeightedIndex(
                    candidates, request, next, purpose, next.Seed, ref next.Cursor);
                BuqiSupplyDefinition selected = candidates[selectedIndex];
                BuqiSupplyDefinition placed = selected.Clone();
                if (request.ShelfSlotBudget > 0)
                {
                    placed.AnchorSlot = occupiedSlotCount;
                    occupiedSlotCount += placed.Size;
                }
                offers.Add(placed);
                purposes.Add(purpose);
                eligible.RemoveAll(definition => string.Equals(
                    definition.DefinitionId, selected.DefinitionId, StringComparison.Ordinal));
            }

            next.PriorOfferDefinitionIds = new List<string>(next.LastOfferDefinitionIds);
            next.LastOfferDefinitionIds = offers.Select(offer => offer.DefinitionId).ToList();
            next.Generation++;
            RecordSeenTags(next, offers);
            shelf = new BuqiSupplyShelf
            {
                Day = request.Day,
                Source = request.Source,
                MerchantPoolId = request.MerchantPoolId ?? string.Empty,
                RefreshIndex = refreshIndex,
                RefreshPricePaid = refreshIndex == 0 ? 0 : CalculateRefreshPrice(refreshIndex - 1),
                NextRefreshPrice = request.Source != BuqiSupplySource.Merchant ||
                                   refreshIndex >= MaximumRefreshCount
                    ? -1
                    : CalculateRefreshPrice(refreshIndex),
                Offers = offers,
                ShelfSlotCount = request.ShelfSlotBudget,
                EmptySlots = CreateEmptySlots(occupiedSlotCount, request.ShelfSlotBudget),
                SlotPurposes = purposes,
                NextState = next,
            };
            error = string.Empty;
            return true;
        }

        public bool TryRefresh(
            BuqiSupplyRequest request,
            BuqiSupplyShelf current,
            out BuqiSupplyShelf shelf,
            out string error)
        {
            shelf = null!;
            if (request == null || current == null || current.NextState == null)
            {
                error = "Current merchant shelf is required.";
                return false;
            }
            if (request.Source != BuqiSupplySource.Merchant ||
                current.Source != BuqiSupplySource.Merchant)
            {
                error = "Only merchant supply can be refreshed.";
                return false;
            }
            if (current.Day != request.Day ||
                !string.Equals(current.MerchantPoolId, request.MerchantPoolId, StringComparison.Ordinal))
            {
                error = "Refresh request does not match the frozen merchant shelf.";
                return false;
            }
            if (current.RefreshIndex >= MaximumRefreshCount)
            {
                error = "Merchant refresh limit has been reached.";
                return false;
            }

            return TryGenerate(request, current.NextState, current.RefreshIndex + 1, out shelf, out error);
        }

        public BuqiSupplyState ShiftAffinity(BuqiSupplyState source, IEnumerable<string> selectedTags)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            BuqiSupplyState next = source.Clone();
            foreach (BuqiSupplyTagMemory memory in next.TagMemory.Values)
                memory.PreferenceBps = ApplyBasisPoints(memory.PreferenceBps, AffinityDecayBps);

            if (selectedTags == null)
                return next;
            foreach (string tag in selectedTags
                         .Where(tag => !string.IsNullOrWhiteSpace(tag))
                         .Distinct(StringComparer.Ordinal))
            {
                BuqiSupplyTagMemory memory = GetOrCreateMemory(next, tag);
                memory.PreferenceBps = Math.Min(
                    MaximumAffinityBps,
                    memory.PreferenceBps + ExplicitAffinitySignalBps);
            }
            return next;
        }

        public BuqiSupplyState RecordAcquired(BuqiSupplyState source, BuqiSupplyDefinition acquired)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (acquired == null)
                throw new ArgumentNullException(nameof(acquired));

            BuqiSupplyState next = source.Clone();
            foreach (string tag in EnumerateSignals(acquired))
            {
                BuqiSupplyTagMemory memory = GetOrCreateMemory(next, tag);
                memory.PreferenceBps = Math.Min(
                    MaximumAffinityBps,
                    memory.PreferenceBps + AcquisitionAffinitySignalBps);
                memory.AcquiredAge = 0;
            }
            return next;
        }

        public static int CalculateRefreshPrice(int completedRefreshCount)
        {
            if (completedRefreshCount < 0)
                throw new ArgumentOutOfRangeException(nameof(completedRefreshCount));
            return Math.Min(MaximumRefreshPrice, FirstRefreshPrice + completedRefreshCount);
        }

        private static bool ValidateRequest(
            BuqiSupplyRequest request,
            BuqiSupplyState source,
            int refreshIndex,
            out string error)
        {
            if (request == null)
            {
                error = "Supply request is required.";
                return false;
            }
            if (source == null)
            {
                error = "Supply state is required.";
                return false;
            }
            if (request.Day < 1)
            {
                error = "Supply day must be positive.";
                return false;
            }
            if (request.Source != BuqiSupplySource.Merchant &&
                request.Source != BuqiSupplySource.Event &&
                request.Source != BuqiSupplySource.Pve)
            {
                error = "Supply source must identify one consumer.";
                return false;
            }
            if (!Enum.IsDefined(typeof(BuqiSupplyQuality), request.MinimumQuality) ||
                !Enum.IsDefined(typeof(BuqiSupplyQuality), request.MaximumQuality) ||
                request.MinimumQuality > request.MaximumQuality)
            {
                error = "Supply quality range is invalid.";
                return false;
            }
            if (request.CandidateCount < 1 || request.CandidateCount > MerchantSlotCount)
            {
                error = "Supply candidate count must be between one and four.";
                return false;
            }
            if (request.ShelfSlotBudget < 0 ||
                request.ShelfSlotBudget > MerchantShelfSlotCount ||
                request.Source != BuqiSupplySource.Merchant && request.ShelfSlotBudget != 0)
            {
                error = "Merchant shelf slot budget must be between zero and ten.";
                return false;
            }
            if (refreshIndex < 0 || refreshIndex > MaximumRefreshCount)
            {
                error = "Supply refresh index is out of range.";
                return false;
            }
            if (request.Source != BuqiSupplySource.Merchant && refreshIndex != 0)
            {
                error = "Only merchant supply supports refresh indexes.";
                return false;
            }
            if (source.Cursor < 0)
            {
                error = "Supply RNG cursor cannot be negative.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsEligible(BuqiSupplyDefinition definition, BuqiSupplyRequest request)
        {
            if (definition.MinimumDay > request.Day || definition.MaximumDay < request.Day)
                return false;
            if (definition.Quality < request.MinimumQuality || definition.Quality > request.MaximumQuality)
                return false;
            if ((definition.Sources & request.Source) == 0)
                return false;
            if (request.AllowedSizes.Count > 0 && !request.AllowedSizes.Contains(definition.Size))
                return false;
            if (request.AllowedArchetypeIds.Count > 0 && !request.AllowedArchetypeIds.Contains(definition.ArchetypeId))
                return false;
            if (request.AllowedRoles.Count > 0 && !request.AllowedRoles.Contains(definition.Role))
                return false;
            if (request.Source == BuqiSupplySource.Merchant &&
                !string.IsNullOrWhiteSpace(request.MerchantPoolId) &&
                definition.MerchantPoolIds.Count > 0 &&
                !definition.MerchantPoolIds.Contains(request.MerchantPoolId))
            {
                return false;
            }
            return definition.BaseWeight > 0 && definition.Size >= 1 && definition.Size <= 3;
        }

        private static List<int> CreateEmptySlots(int occupiedSlotCount, int shelfSlotCount)
        {
            var result = new List<int>();
            for (int slot = occupiedSlotCount; slot < shelfSlotCount; slot++)
                result.Add(slot);
            return result;
        }

        private static int DrawWeightedIndex(
            IReadOnlyList<BuqiSupplyDefinition> candidates,
            BuqiSupplyRequest request,
            BuqiSupplyState state,
            BuqiSupplySlotPurpose purpose,
            long seed,
            ref int cursor)
        {
            var weights = new int[candidates.Count];
            long total = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                int weight = Math.Min(MaximumCandidateWeight, candidates[index].BaseWeight);
                weight = ApplyFactor(weight, PurposeFactor(candidates[index], request, purpose));
                weight = ApplyMemoryWeight(weight, candidates[index], state);
                weight = ApplyFactor(weight, RepeatFactor(candidates[index], state));
                weights[index] = weight;
                total += weight;
            }
            if (total <= 0 || total > int.MaxValue)
                throw new InvalidOperationException("Supply pool weight is out of range.");

            int roll = BuqiRunRandom.Next(seed, ref cursor, (int)total);
            for (int index = 0; index < weights.Length; index++)
            {
                if (roll < weights[index])
                    return index;
                roll -= weights[index];
            }
            return weights.Length - 1;
        }

        private static int PurposeFactor(
            BuqiSupplyDefinition definition,
            BuqiSupplyRequest request,
            BuqiSupplySlotPurpose purpose)
        {
            switch (purpose)
            {
                case BuqiSupplySlotPurpose.Mainline:
                    if (!string.IsNullOrEmpty(request.PreferredArchetypeId) &&
                        string.Equals(definition.ArchetypeId, request.PreferredArchetypeId, StringComparison.Ordinal))
                    {
                        return MainlineArchetypeFactorBps;
                    }
                    return definition.Role == BuqiSupplyProductRole.Mainline
                        ? MainlineRoleFactorBps
                        : NeutralFactorBps;
                case BuqiSupplySlotPurpose.Bridge:
                    return definition.Role == BuqiSupplyProductRole.Bridge
                        ? PurposeRoleFactorBps
                        : NeutralFactorBps;
                case BuqiSupplySlotPurpose.CounterOrEconomy:
                    return definition.Role == BuqiSupplyProductRole.Counter ||
                           definition.Role == BuqiSupplyProductRole.Economy
                        ? PurposeRoleFactorBps
                        : NeutralFactorBps;
                case BuqiSupplySlotPurpose.Wildcard:
                    return definition.Role == BuqiSupplyProductRole.Wildcard
                        ? WildcardRoleFactorBps
                        : NeutralFactorBps;
                default:
                    return NeutralFactorBps;
            }
        }

        private static int ApplyFactor(int value, int factorBps)
        {
            long scaled = ((long)value * factorBps) / NeutralFactorBps;
            return (int)Math.Max(1, Math.Min(MaximumCandidateWeight, scaled));
        }

        private static int ApplyMemoryWeight(
            int weight,
            BuqiSupplyDefinition definition,
            BuqiSupplyState state)
        {
            int preferenceBps = 0;
            int missStreak = 0;
            bool recentlyAcquired = false;
            foreach (string tag in EnumerateSignals(definition))
            {
                if (!state.TagMemory.TryGetValue(tag, out BuqiSupplyTagMemory memory) || memory == null)
                    continue;
                preferenceBps = Math.Max(preferenceBps, memory.PreferenceBps);
                missStreak = Math.Max(missStreak, memory.MissStreak);
                recentlyAcquired |= memory.AcquiredAge <= 2;
            }

            weight = ApplyFactor(weight, NeutralFactorBps + Math.Min(MaximumAffinityBps, preferenceBps));
            weight = ApplyFactor(weight, NeutralFactorBps + Math.Min(
                MaximumMissPityBps,
                missStreak * MissPityStepBps));
            if (recentlyAcquired)
                weight = ApplyFactor(weight, RecentAcquisitionFactorBps);
            return weight;
        }

        private static int RepeatFactor(BuqiSupplyDefinition definition, BuqiSupplyState state)
        {
            if (state.LastOfferDefinitionIds.Contains(definition.DefinitionId))
                return ImmediateRepeatFactorBps;
            if (state.PriorOfferDefinitionIds.Contains(definition.DefinitionId))
                return PriorRepeatFactorBps;
            return NeutralFactorBps;
        }

        private static void RecordSeenTags(
            BuqiSupplyState state,
            IReadOnlyList<BuqiSupplyDefinition> offers)
        {
            var offeredTags = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiSupplyDefinition offer in offers)
            {
                foreach (string tag in EnumerateSignals(offer))
                    offeredTags.Add(tag);
            }

            foreach (KeyValuePair<string, BuqiSupplyTagMemory> pair in state.TagMemory)
            {
                BuqiSupplyTagMemory memory = pair.Value;
                memory.SeenAge = IncrementAge(memory.SeenAge);
                memory.AcquiredAge = IncrementAge(memory.AcquiredAge);
                if (offeredTags.Contains(pair.Key))
                {
                    memory.SeenAge = 0;
                    memory.MissStreak = 0;
                }
                else if (memory.PreferenceBps > 0)
                {
                    memory.MissStreak = Math.Min(6, memory.MissStreak + 1);
                }
            }

            foreach (string tag in offeredTags)
            {
                BuqiSupplyTagMemory memory = GetOrCreateMemory(state, tag);
                memory.SeenAge = 0;
                memory.MissStreak = 0;
            }
        }

        private static IEnumerable<string> EnumerateSignals(BuqiSupplyDefinition definition)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(definition.ArchetypeId) && seen.Add(definition.ArchetypeId))
                yield return definition.ArchetypeId;
            foreach (string tag in definition.Tags)
            {
                if (!string.IsNullOrWhiteSpace(tag) && seen.Add(tag))
                    yield return tag;
            }
        }

        private static BuqiSupplyTagMemory GetOrCreateMemory(BuqiSupplyState state, string tag)
        {
            if (!state.TagMemory.TryGetValue(tag, out BuqiSupplyTagMemory memory) || memory == null)
            {
                memory = new BuqiSupplyTagMemory();
                state.TagMemory.Add(tag, memory);
            }
            return memory;
        }

        private static int IncrementAge(int age)
        {
            if (age == int.MaxValue)
                return age;
            return Math.Min(int.MaxValue - 1, age + 1);
        }

        private static int ApplyBasisPoints(int value, int factorBps)
        {
            return (int)(((long)value * factorBps) / NeutralFactorBps);
        }
    }
}
