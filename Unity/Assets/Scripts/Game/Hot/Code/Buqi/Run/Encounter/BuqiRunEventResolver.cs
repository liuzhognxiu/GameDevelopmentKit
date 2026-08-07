using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.Run.Encounter
{
    public sealed class BuqiRunEventResolver
    {
        private const string EncounterRequired = "Encounter is required.";
        private const string EncounterIdRequired = "Encounter id is required.";
        private const string EventIdRequired = "Event id is required.";
        private const string InvalidEncounterKind = "Encounter is not an event.";
        private const string EncounterAlreadyResolved = "Encounter is already resolved.";
        private const string EventChoiceUnavailable = "Event id is not available in the frozen candidate list.";
        private const string EventChoiceMissing = "Event id was not found in the event catalog.";

        private readonly IBuqiRunEventCatalog m_Catalog;

        public BuqiRunEventResolver(IBuqiRunEventCatalog catalog)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public bool TryResolve(
            BuqiRunEncounterState encounter,
            string eventId,
            out BuqiRunEncounterState resolvedEncounter,
            out BuqiRunEncounterDelta delta,
            out string error)
        {
            if (encounter == null)
            {
                throw new ArgumentNullException(nameof(encounter), EncounterRequired);
            }

            resolvedEncounter = null!;
            delta = null!;

            if (string.IsNullOrEmpty(encounter.EncounterId))
            {
                error = EncounterIdRequired;
                return false;
            }

            if (string.IsNullOrEmpty(eventId))
            {
                error = EventIdRequired;
                return false;
            }

            if (encounter.Kind != BuqiRunEncounterKind.Event)
            {
                error = InvalidEncounterKind;
                return false;
            }

            if (encounter.Resolved)
            {
                error = EncounterAlreadyResolved;
                return false;
            }

            if (!Contains(encounter.CandidateIds, eventId))
            {
                error = EventChoiceUnavailable;
                return false;
            }

            if (!m_Catalog.TryGet(eventId, out BuqiRunEncounterDelta selectedDelta))
            {
                error = EventChoiceMissing;
                return false;
            }

            resolvedEncounter = encounter.Clone();
            resolvedEncounter.Resolved = true;
            resolvedEncounter.SelectedChoiceId = eventId;
            resolvedEncounter.ResolutionId = $"{encounter.EncounterId}:{eventId}";
            delta = selectedDelta.Clone();
            error = string.Empty;
            return true;
        }

        private static bool Contains(IReadOnlyList<string> candidateIds, string eventId)
        {
            if (candidateIds == null)
            {
                return false;
            }

            for (int index = 0; index < candidateIds.Count; index++)
            {
                if (candidateIds[index] == eventId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
