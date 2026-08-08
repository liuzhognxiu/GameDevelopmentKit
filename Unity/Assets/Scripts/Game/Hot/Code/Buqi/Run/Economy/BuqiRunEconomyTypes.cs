namespace Game.Hot.Buqi.Run.Economy
{
    public enum BuqiRunItemQuality
    {
        Common = 0,
        Improved = 1,
        Finalized = 2,
    }

    public sealed class BuqiRunItemInstance
    {
        public string InstanceId = string.Empty;
        public string DefinitionId = string.Empty;
        public BuqiRunItemQuality Quality;
        public string RefinementId = string.Empty;

        public BuqiRunItemInstance Clone()
        {
            return (BuqiRunItemInstance)MemberwiseClone();
        }
    }

    public sealed class BuqiRunItemDefinition
    {
        public string DefinitionId = string.Empty;
        public int Size;
        public int BuyPrice;
        public int SellPrice;
        public int UpgradePrice;
        public int RefinementPrice;
    }

    public interface IBuqiRunItemCatalog
    {
        bool TryGet(string definitionId, out BuqiRunItemDefinition definition);
    }

    public sealed class BuqiRunEconomyResult
    {
        public bool Success;
        public string FailureReason = string.Empty;
        public BuqiRunEconomySnapshot Snapshot = null!;
        public string AffectedInstanceId = string.Empty;
    }
}
