using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.Run.Battle
{
    public sealed class BuqiLocalOpponentPoolAdapter
    {
        private readonly List<string> m_PveOpponentIds;
        private readonly List<string> m_PvpOpponentIds;

        public BuqiLocalOpponentPoolAdapter(
            IEnumerable<string> pveOpponentIds,
            IEnumerable<string> pvpOpponentIds)
        {
            m_PveOpponentIds = ToList(pveOpponentIds);
            m_PvpOpponentIds = ToList(pvpOpponentIds);
        }

        public bool TryCreate(
            BuqiConfigCatalog catalog,
            out BuqiLocalOpponentPool pool,
            out string error)
        {
            pool = null;
            error = string.Empty;

            if (catalog == null || catalog.Global == null)
            {
                error = "catalog is unavailable";
                return false;
            }

            var provider = new BuqiDefinitionProvider(catalog);
            var echoesById = new Dictionary<string, BuqiEchoConfigRow>(System.StringComparer.Ordinal);
            var errors = new List<string>();
            if (catalog.Echoes != null)
            {
                foreach (BuqiEchoConfigRow echo in catalog.Echoes)
                {
                    if (echo == null || string.IsNullOrEmpty(echo.EchoId))
                        continue;

                    if (echoesById.ContainsKey(echo.EchoId))
                    {
                        errors.Add("duplicate catalog echo id: " + echo.EchoId);
                        continue;
                    }

                    echoesById.Add(echo.EchoId, echo);
                }
            }

            var candidatePool = new BuqiLocalOpponentPool();
            var assignedIds = new HashSet<string>(System.StringComparer.Ordinal);
            AddAssignments(
                m_PveOpponentIds,
                BuqiRunOpponentSource.PvePreset,
                candidatePool.Pve,
                catalog.Global.ContentVersion,
                provider,
                echoesById,
                assignedIds,
                errors);
            AddAssignments(
                m_PvpOpponentIds,
                BuqiRunOpponentSource.LocalPlayerPreset,
                candidatePool.Pvp,
                catalog.Global.ContentVersion,
                provider,
                echoesById,
                assignedIds,
                errors);

            if (errors.Count > 0)
            {
                error = string.Join(" | ", errors);
                return false;
            }

            pool = candidatePool;
            return true;
        }

        private static void AddAssignments(
            List<string> opponentIds,
            BuqiRunOpponentSource source,
            List<BuqiRunOpponent> destination,
            string contentVersion,
            IItemDefinitionProvider definitions,
            Dictionary<string, BuqiEchoConfigRow> echoesById,
            HashSet<string> assignedIds,
            List<string> errors)
        {
            foreach (string opponentId in opponentIds)
            {
                if (string.IsNullOrEmpty(opponentId))
                {
                    errors.Add("missing opponent assignment id");
                    continue;
                }

                if (!assignedIds.Add(opponentId))
                {
                    errors.Add("duplicate opponent assignment: " + opponentId);
                    continue;
                }

                if (!echoesById.TryGetValue(opponentId, out BuqiEchoConfigRow echo))
                {
                    errors.Add("missing configured opponent: " + opponentId);
                    continue;
                }

                BuildSnapshot build = BuqiRunBattleSnapshotUtility.CreateBuildSnapshot(
                    echo.Snapshot,
                    contentVersion,
                    opponentId + ":",
                    opponentId + ":");
                if (!BuqiBoardValidator.Validate(build, definitions, out List<string> buildErrors))
                {
                    errors.Add("illegal opponent assignment: " + opponentId + " -> " + string.Join("; ", buildErrors));
                    continue;
                }

                destination.Add(new BuqiRunOpponent
                {
                    OpponentId = opponentId,
                    DisplayName = ResolveDisplayName(echo),
                    Source = source,
                    Build = build,
                });
            }
        }

        private static string ResolveDisplayName(BuqiEchoConfigRow echo)
        {
            return string.IsNullOrEmpty(echo.DisplayName) ? echo.EchoId : echo.DisplayName;
        }

        private static List<string> ToList(IEnumerable<string> source)
        {
            if (source == null)
                return new List<string>();

            var result = new List<string>();
            foreach (string item in source)
                result.Add(item);
            return result;
        }
    }
}
