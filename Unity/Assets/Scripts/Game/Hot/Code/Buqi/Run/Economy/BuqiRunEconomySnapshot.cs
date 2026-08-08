using System.Collections.Generic;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Economy
{
    public sealed class BuqiRunEconomySnapshot
    {
        public BuqiRunState Run = null!;
        public int NextItemOrdinal = 1;
        public Dictionary<string, BuqiRunItemInstance> Items =
            new Dictionary<string, BuqiRunItemInstance>();

        public static BuqiRunEconomySnapshot CreateInitial(long runSeed, string contentVersion = "")
        {
            return new BuqiRunEconomySnapshot
            {
                Run = BuqiRunState.CreateInitial(runSeed, contentVersion),
            };
        }

        public string CreateInstanceId()
        {
            while (true)
            {
                string instanceId = $"run-{Run.RunSeed}-item-{NextItemOrdinal++}";
                if (!IsInstanceIdOccupied(instanceId))
                    return instanceId;
            }
        }

        public BuqiRunEconomySnapshot Clone()
        {
            var clone = new BuqiRunEconomySnapshot
            {
                Run = Run.Clone(),
                NextItemOrdinal = NextItemOrdinal,
            };

            foreach (KeyValuePair<string, BuqiRunItemInstance> pair in Items)
            {
                clone.Items.Add(pair.Key, pair.Value.Clone());
            }

            return clone;
        }

        private bool IsInstanceIdOccupied(string instanceId)
        {
            if (Items.ContainsKey(instanceId))
                return true;

            return Run.BoardInstanceIds.Contains(instanceId) || Run.StorageInstanceIds.Contains(instanceId);
        }
    }
}
