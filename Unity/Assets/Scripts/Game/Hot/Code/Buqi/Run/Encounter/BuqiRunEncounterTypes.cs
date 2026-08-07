using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Encounter
{
    public enum BuqiRunEncounterKind
    {
        Shop = 0,
        Event = 1,
    }

    public sealed class BuqiRunEncounterState
    {
        public string EncounterId = string.Empty;
        public BuqiRunEncounterKind Kind;
        public int Day;
        public int EncounterIndex;
        public int NextRngCursor;
        public bool Resolved;
        public string ResolutionId = string.Empty;
        public string SelectedChoiceId = string.Empty;
        public List<string> CandidateIds = new List<string>();

        public BuqiRunEncounterState Clone()
        {
            return new BuqiRunEncounterState
            {
                EncounterId = EncounterId,
                Kind = Kind,
                Day = Day,
                EncounterIndex = EncounterIndex,
                NextRngCursor = NextRngCursor,
                Resolved = Resolved,
                ResolutionId = ResolutionId,
                SelectedChoiceId = SelectedChoiceId,
                CandidateIds = new List<string>(CandidateIds),
            };
        }
    }

    public sealed class BuqiRunEncounterDelta
    {
        public int Coins;
        public int Lives;
        public string GrantedItemDefinitionId = string.Empty;
        public string GrantedRefinementId = string.Empty;

        public BuqiRunEncounterDelta Clone()
        {
            return new BuqiRunEncounterDelta
            {
                Coins = Coins,
                Lives = Lives,
                GrantedItemDefinitionId = GrantedItemDefinitionId,
                GrantedRefinementId = GrantedRefinementId,
            };
        }
    }

    public interface IBuqiRunEncounterCatalog
    {
        IReadOnlyList<string> ShopOfferIds { get; }

        IReadOnlyList<string> EventIds { get; }
    }

    public interface IBuqiRunEventCatalog
    {
        bool TryGet(string eventId, out BuqiRunEncounterDelta delta);
    }
}
