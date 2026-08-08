using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Encounter
{
    public sealed class BuqiRunEncounterService
    {
        private const int EncounterKindCount = 2;
        private const int ShopCandidateCount = 4;
        private const int EventCandidateCount = 3;
        private const string InvalidPhase = "Run phase must be Encounter.";
        private const string InvalidEncounterIndex = "Encounter index is out of range.";
        private const string ResolvedCurrentEncounter = "Current encounter has already been resolved.";
        private const string CurrentEncounterMismatch = "Current encounter does not match the active day or encounter index.";
        private const string EmptyShopPool = "Shop offer pool is empty.";
        private const string EmptyEventPool = "Event pool is empty.";

        private readonly IBuqiRunEncounterCatalog m_Catalog;

        public BuqiRunEncounterService(IBuqiRunEncounterCatalog catalog)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public bool TryGetOrCreate(
            BuqiRunState run,
            BuqiRunEncounterState current,
            out BuqiRunEncounterState encounter,
            out string error)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            if (run.Phase != BuqiRunPhase.Encounter)
            {
                encounter = null!;
                error = InvalidPhase;
                return false;
            }

            if (run.EncounterIndex < 0 || run.EncounterIndex >= BuqiRunRules.EncountersPerDay)
            {
                encounter = null!;
                error = InvalidEncounterIndex;
                return false;
            }

            if (current != null && !string.IsNullOrEmpty(current.EncounterId))
            {
                if (current.Resolved)
                {
                    encounter = null!;
                    error = ResolvedCurrentEncounter;
                    return false;
                }

                if (current.Day != run.Day || current.EncounterIndex != run.EncounterIndex)
                {
                    encounter = null!;
                    error = CurrentEncounterMismatch;
                    return false;
                }

                encounter = current.Clone();
                error = string.Empty;
                return true;
            }

            List<string> shopPool = SanitizePool(m_Catalog.ShopOfferIds);
            if (shopPool.Count == 0)
            {
                encounter = null!;
                error = EmptyShopPool;
                return false;
            }

            List<string> eventPool = SanitizePool(m_Catalog.EventIds);
            if (eventPool.Count == 0)
            {
                encounter = null!;
                error = EmptyEventPool;
                return false;
            }

            int cursor = run.RngCursor;
            BuqiRunEncounterKind kind = BuqiRunRandom.Next(run.RunSeed, ref cursor, EncounterKindCount) == 0
                ? BuqiRunEncounterKind.Shop
                : BuqiRunEncounterKind.Event;

            IReadOnlyList<string> pool = kind == BuqiRunEncounterKind.Shop
                ? shopPool
                : eventPool;
            int candidateCount = kind == BuqiRunEncounterKind.Shop ? ShopCandidateCount : EventCandidateCount;

            encounter = new BuqiRunEncounterState
            {
                EncounterId = CreateEncounterId(run.Day, run.EncounterIndex, kind),
                Kind = kind,
                Day = run.Day,
                EncounterIndex = run.EncounterIndex,
                NextRngCursor = cursor,
                Resolved = false,
                CandidateIds = DrawCandidates(run.RunSeed, ref cursor, pool, candidateCount),
            };
            encounter.NextRngCursor = cursor;
            error = string.Empty;
            return true;
        }

        private static List<string> SanitizePool(IReadOnlyList<string> source)
        {
            var result = new List<string>();
            if (source == null)
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                string candidateId = source[index];
                if (string.IsNullOrWhiteSpace(candidateId))
                {
                    continue;
                }

                if (seen.Add(candidateId))
                {
                    result.Add(candidateId);
                }
            }

            return result;
        }

        private static string CreateEncounterId(int day, int encounterIndex, BuqiRunEncounterKind kind)
        {
            return $"day-{day}-enc-{encounterIndex}-{kind.ToString().ToLowerInvariant()}";
        }

        private static List<string> DrawCandidates(
            long seed,
            ref int cursor,
            IReadOnlyList<string> source,
            int maxCount)
        {
            if (source.Count <= maxCount)
            {
                return new List<string>(source);
            }

            var remaining = new List<string>(source);
            var result = new List<string>(maxCount);
            for (int index = 0; index < maxCount; index++)
            {
                int selectedIndex = BuqiRunRandom.Next(seed, ref cursor, remaining.Count);
                result.Add(remaining[selectedIndex]);
                remaining.RemoveAt(selectedIndex);
            }

            return result;
        }
    }
}
