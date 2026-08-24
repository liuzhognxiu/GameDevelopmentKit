using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Supply;

namespace Game.Hot.Buqi.DemoUI
{
    public sealed class BuqiBazaarSupplySnapshotOffer
    {
        public string OfferId = string.Empty;
        public BuqiSupplyQuality Quality;
        public int AnchorSlot;
        public int Size;
        public bool Purchased;
        public Game.Hot.BuqiItemCategory Category;
        public BuqiSupplySlotPurpose Purpose;
        public string Role = string.Empty;

        public BuqiBazaarSupplySnapshotOffer Clone()
        {
            return (BuqiBazaarSupplySnapshotOffer)MemberwiseClone();
        }
    }

    public sealed class BuqiBazaarSupplySnapshot
    {
        public string EncounterKey = string.Empty;
        public string MerchantId = string.Empty;
        public string PreferredArchetypeId = string.Empty;
        public int Balance;
        public int RefreshIndex;
        public int RefreshPricePaid;
        public int NextRefreshPrice;
        public int ShelfSlotCount;
        public Game.Hot.BuqiMerchantSpecialty MerchantSpecialty;
        public List<BuqiBazaarSupplySnapshotOffer> Offers = new List<BuqiBazaarSupplySnapshotOffer>();
        public List<int> EmptyShelfSlots = new List<int>();
        public BuqiSupplyState SupplyState = null!;

        public BuqiBazaarSupplySnapshot Clone()
        {
            var clone = (BuqiBazaarSupplySnapshot)MemberwiseClone();
            clone.Offers = Offers?.Select(offer => offer.Clone()).ToList()
                ?? new List<BuqiBazaarSupplySnapshotOffer>();
            clone.EmptyShelfSlots = EmptyShelfSlots == null
                ? new List<int>()
                : new List<int>(EmptyShelfSlots);
            clone.SupplyState = SupplyState?.Clone();
            return clone;
        }
    }

    public sealed class BuqiBazaarSupplyContext
    {
        public long RunSeed;
        public int Day;
        public int EncounterIndex;
        public int Balance;
        public IReadOnlyList<string> OwnedDefinitionIds = Array.Empty<string>();
        public IReadOnlyList<string> PurchasedOfferIds = Array.Empty<string>();
    }

    public interface IBuqiBazaarSupplyRuntime : IBuqiBazaarSupplyViewSource
    {
        void Reset();

        bool TryOpen(
            BuqiBazaarSupplyContext context,
            out IReadOnlyList<string> offerDefinitionIds,
            out string error);

        bool TryRestore(
            BuqiBazaarSupplyContext context,
            IReadOnlyList<string> offerDefinitionIds,
            out string error);

        bool TryRefresh(
            BuqiBazaarSupplyContext context,
            out IReadOnlyList<string> offerDefinitionIds,
            out int cost,
            out string error);

        bool RecordPurchase(string offerDefinitionId, int balance, out string error);
    }

    public interface IBuqiBazaarSupplyPersistence
    {
        bool TryCaptureSnapshot(
            out BuqiBazaarSupplySnapshot snapshot,
            out string error);

        bool TryRestore(
            BuqiBazaarSupplyContext context,
            BuqiBazaarSupplySnapshot snapshot,
            out string error);
    }

    public sealed class BuqiBazaarSupplyViewSource :
        IBuqiBazaarSupplyRuntime,
        IBuqiBazaarSupplyPersistence
    {
        private const int PreferenceMerchantFactorBps = 18000;
        private const int BasisPoints = 10000;

        private readonly Dictionary<string, ItemProfile> m_Items;
        private readonly List<MerchantProfile> m_Merchants;
        private readonly List<BuqiSupplyDefinition> m_Definitions;
        private readonly BuqiSupplyService m_Service;

        private string m_EncounterKey = string.Empty;
        private MerchantProfile m_CurrentMerchant;
        private BuqiSupplyShelf m_CurrentShelf;
        private string m_PreferredArchetypeId = string.Empty;
        private int m_Balance;
        private readonly HashSet<string> m_PurchasedOfferIds =
            new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, string> m_OfferRoles =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private BuqiBazaarSupplyViewSource(
            Dictionary<string, ItemProfile> items,
            List<MerchantProfile> merchants,
            List<BuqiSupplyDefinition> definitions)
        {
            m_Items = items;
            m_Merchants = merchants;
            m_Definitions = definitions;
            m_Service = new BuqiSupplyService(definitions);
        }

        public IReadOnlyList<string> MerchantIds =>
            m_Merchants.Select(merchant => merchant.Row.MerchantId).ToArray();

        public static bool TryCreate(
            BuqiConfigCatalog catalog,
            out BuqiBazaarSupplyViewSource source,
            out string error)
        {
            source = null;
            if (catalog?.Items == null || catalog.Merchants == null)
            {
                error = "装备与商人配置不可用。";
                return false;
            }
            if (catalog.Items.Count == 0 || catalog.Merchants.Count != 8)
            {
                error = "商店供应需要装备配置和 8 名商人。";
                return false;
            }

            var items = new Dictionary<string, ItemProfile>(StringComparer.Ordinal);
            foreach (BuqiItemConfigRow row in catalog.Items)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.DefinitionId) ||
                    string.IsNullOrWhiteSpace(row.ArchetypeId) ||
                    row.UnlockDay < 1 || row.UnlockDay > BuqiRunRules.RunDayCount ||
                    !Enum.IsDefined(typeof(Game.Hot.BuqiItemCategory), row.Category) ||
                    row.Category == Game.Hot.BuqiItemCategory.Unknown ||
                    !items.TryAdd(row.DefinitionId, new ItemProfile(row)))
                {
                    error = "装备编号、流派和解锁日期必须有效且不能重复。";
                    return false;
                }
            }

            var merchants = new List<MerchantProfile>(catalog.Merchants.Count);
            var merchantIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiMerchantConfigRow row in catalog.Merchants)
            {
                if (!TryCreateMerchant(row, items, merchantIds, out MerchantProfile merchant, out error))
                    return false;
                merchants.Add(merchant);
            }

            var poolMembership = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (MerchantProfile merchant in merchants)
            {
                foreach (string definitionId in merchant.PoolItemIds)
                {
                    if (!poolMembership.TryGetValue(definitionId, out List<string> pools))
                    {
                        pools = new List<string>();
                        poolMembership.Add(definitionId, pools);
                    }
                    pools.Add(merchant.Row.MerchantId);
                }
            }

            List<string> uncoveredDefinitionIds = items.Keys
                .Where(definitionId => !poolMembership.ContainsKey(definitionId))
                .OrderBy(definitionId => definitionId, StringComparer.Ordinal)
                .ToList();
            // Legacy merchant rows are curated anchors; matching slot builds project later catalog additions.
            foreach (string definitionId in uncoveredDefinitionIds)
            {
                ItemProfile item = items[definitionId];
                List<MerchantProfile> matchingMerchants = merchants
                    .Where(merchant => IsMerchantAllowed(merchant, item) &&
                        merchant.Slots.Any(slot =>
                            slot.Builds.Contains(item.Row.ArchetypeId)))
                    .ToList();
                if (matchingMerchants.Count == 0)
                {
                    error = $"装备“{definitionId}”没有匹配流派的商人货位。";
                    return false;
                }

                List<MerchantProfile> availableOnUnlockDay = matchingMerchants
                    .Where(merchant => merchant.Row.MinDay <= item.Row.UnlockDay)
                    .ToList();
                if (availableOnUnlockDay.Count > 0)
                    matchingMerchants = availableOnUnlockDay;

                var pools = new List<string>(matchingMerchants.Count);
                foreach (MerchantProfile merchant in matchingMerchants)
                {
                    merchant.PoolItemIds.Add(definitionId);
                    pools.Add(merchant.Row.MerchantId);
                }
                poolMembership.Add(definitionId, pools);
            }

            var definitions = new List<BuqiSupplyDefinition>(items.Count * 3);
            foreach (ItemProfile item in items.Values.OrderBy(value => value.Row.DefinitionId, StringComparer.Ordinal))
            {
                List<string> pools = poolMembership[item.Row.DefinitionId];
                foreach (BuqiSupplyQuality quality in Enum.GetValues(typeof(BuqiSupplyQuality)))
                {
                    var definition = new BuqiSupplyDefinition
                    {
                        DefinitionId = item.Row.DefinitionId,
                        ArchetypeId = item.Row.ArchetypeId,
                        Category = item.Row.Category,
                        Role = MapRole(item.Row.Role),
                        MinimumDay = item.Row.UnlockDay,
                        MaximumDay = BuqiRunRules.RunDayCount,
                        Size = (int)item.Row.Size,
                        Quality = quality,
                        Sources = BuqiSupplySource.Merchant,
                        BaseWeight = 100,
                        Tags = Normalize(item.Row.Tags.Concat(new[] { item.Row.ArchetypeId, item.Row.Role })),
                        MerchantPoolIds = new List<string>(pools),
                    };
                    definitions.Add(definition);
                }
            }
            if (definitions.Count == 0)
            {
                error = "商人货池中没有可用装备。";
                return false;
            }

            source = new BuqiBazaarSupplyViewSource(items, merchants, definitions);
            error = string.Empty;
            return true;
        }

        public bool TryOpen(
            BuqiBazaarSupplyContext context,
            out IReadOnlyList<string> offerDefinitionIds,
            out string error)
        {
            offerDefinitionIds = Array.Empty<string>();
            if (!ValidateContext(context, out error))
                return false;

            string encounterKey = CreateEncounterKey(context);
            if (m_CurrentShelf != null && string.Equals(m_EncounterKey, encounterKey, StringComparison.Ordinal))
            {
                m_Balance = context.Balance;
                offerDefinitionIds = GetOfferIds(m_CurrentShelf);
                error = string.Empty;
                return true;
            }

            string preferredArchetypeId = ResolvePreference(context.OwnedDefinitionIds);
            MerchantProfile merchant = SelectMerchant(context, preferredArchetypeId);
            if (merchant == null)
            {
                error = "当前日期没有可用商人。";
                return false;
            }

            BuqiSupplyState state = CreateInitialState(context, merchant, preferredArchetypeId);
            if (!TryGenerateShelf(
                    context.Day,
                    merchant,
                    preferredArchetypeId,
                    state,
                    0,
                    out BuqiSupplyShelf shelf,
                    out Dictionary<string, string> roles,
                    out error))
            {
                return false;
            }

            var persistedPurchases = new HashSet<string>(
                context.PurchasedOfferIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            IReadOnlyList<string> generatedOfferIds = GetOfferIds(shelf);
            foreach (string purchased in persistedPurchases)
            {
                if (!generatedOfferIds.Contains(purchased, StringComparer.Ordinal))
                {
                    error = "Persisted purchase does not belong to the restored shelf.";
                    return false;
                }
            }
            m_EncounterKey = encounterKey;
            m_PreferredArchetypeId = preferredArchetypeId;
            m_CurrentMerchant = merchant;
            m_CurrentShelf = shelf;
            m_OfferRoles = roles;
            m_Balance = context.Balance;
            m_PurchasedOfferIds.Clear();
            foreach (string purchased in persistedPurchases)
                m_PurchasedOfferIds.Add(purchased);
            ApplyPurchasedSlots(shelf, m_PurchasedOfferIds);
            offerDefinitionIds = generatedOfferIds;
            error = string.Empty;
            return true;
        }

        public bool TryRestore(
            BuqiBazaarSupplyContext context,
            IReadOnlyList<string> offerDefinitionIds,
            out string error)
        {
            if (offerDefinitionIds == null || offerDefinitionIds.Count < 1 ||
                offerDefinitionIds.Count > BuqiSupplyService.MerchantShelfSlotCount)
            {
                error = $"恢复的货架必须包含 1 到 {BuqiSupplyService.MerchantShelfSlotCount} 件商品。";
                return false;
            }
            string previousEncounterKey = m_EncounterKey;
            MerchantProfile previousMerchant = m_CurrentMerchant;
            BuqiSupplyShelf previousShelf = m_CurrentShelf;
            string previousPreference = m_PreferredArchetypeId;
            int previousBalance = m_Balance;
            var previousPurchases = new HashSet<string>(m_PurchasedOfferIds, StringComparer.Ordinal);
            Dictionary<string, string> previousRoles = m_OfferRoles;
            bool restored = false;

            try
            {
                m_EncounterKey = string.Empty;
                m_CurrentMerchant = null;
                m_CurrentShelf = null;
                m_PreferredArchetypeId = string.Empty;
                m_PurchasedOfferIds.Clear();
                m_OfferRoles = new Dictionary<string, string>(StringComparer.Ordinal);

                if (!TryOpen(context, out IReadOnlyList<string> current, out error))
                    return false;
                if (current.SequenceEqual(offerDefinitionIds, StringComparer.Ordinal))
                {
                    restored = RestorePurchases(context, out error);
                    return restored;
                }

                for (int refresh = 0; refresh < BuqiSupplyService.MaximumRefreshCount; refresh++)
                {
                    var replay = CloneContext(context, int.MaxValue);
                    if (!TryRefresh(replay, out current, out _, out error))
                        return false;
                    if (!current.SequenceEqual(offerDefinitionIds, StringComparer.Ordinal))
                        continue;

                    m_Balance = context.Balance;
                    restored = RestorePurchases(context, out error);
                    return restored;
                }

                error = "存档中的货架与当前供应序列不一致。";
                return false;
            }
            finally
            {
                if (!restored)
                {
                    m_EncounterKey = previousEncounterKey;
                    m_CurrentMerchant = previousMerchant;
                    m_CurrentShelf = previousShelf;
                    m_PreferredArchetypeId = previousPreference;
                    m_Balance = previousBalance;
                    m_PurchasedOfferIds.Clear();
                    foreach (string purchased in previousPurchases)
                        m_PurchasedOfferIds.Add(purchased);
                    m_OfferRoles = previousRoles;
                }
            }
        }

        public bool TryRefresh(
            BuqiBazaarSupplyContext context,
            out IReadOnlyList<string> offerDefinitionIds,
            out int cost,
            out string error)
        {
            offerDefinitionIds = Array.Empty<string>();
            cost = 0;
            if (!ValidateContext(context, out error) || m_CurrentShelf == null ||
                !string.Equals(m_EncounterKey, CreateEncounterKey(context), StringComparison.Ordinal))
            {
                error = string.IsNullOrEmpty(error)
                    ? "当前商人货架尚未打开。"
                    : error;
                return false;
            }
            if (m_CurrentShelf.NextRefreshPrice < 0)
            {
                error = "本次商店的刷新次数已用尽。";
                return false;
            }
            cost = m_CurrentShelf.NextRefreshPrice;
            if (context.Balance < cost)
            {
                error = "金币不足，无法刷新货架。";
                cost = 0;
                return false;
            }

            int refreshIndex = m_CurrentShelf.RefreshIndex + 1;
            if (!TryGenerateShelf(
                    context.Day,
                    m_CurrentMerchant,
                    m_PreferredArchetypeId,
                    m_CurrentShelf.NextState,
                    refreshIndex,
                    out BuqiSupplyShelf shelf,
                    out Dictionary<string, string> roles,
                    out error))
            {
                cost = 0;
                return false;
            }

            m_CurrentShelf = shelf;
            m_OfferRoles = roles;
            m_Balance = context.Balance - cost;
            m_PurchasedOfferIds.Clear();
            offerDefinitionIds = GetOfferIds(shelf);
            error = string.Empty;
            return true;
        }

        public bool RecordPurchase(string offerDefinitionId, int balance, out string error)
        {
            if (m_CurrentShelf == null || string.IsNullOrWhiteSpace(offerDefinitionId) ||
                !GetOfferIds(m_CurrentShelf).Contains(offerDefinitionId, StringComparer.Ordinal))
            {
                error = "所购商品不在当前货架上。";
                return false;
            }
            if (!m_PurchasedOfferIds.Add(offerDefinitionId))
            {
                error = "该商品已记录为已购买。";
                return false;
            }
            if (balance < 0)
            {
                m_PurchasedOfferIds.Remove(offerDefinitionId);
                error = "购买后的金币不能为负数。";
                return false;
            }

            m_Balance = balance;
            BuqiSupplyDefinition acquired = m_CurrentShelf.Offers.First(offer =>
                string.Equals(offer.DefinitionId, offerDefinitionId, StringComparison.Ordinal));
            m_CurrentShelf.NextState = m_Service.RecordAcquired(m_CurrentShelf.NextState, acquired);
            foreach (int slot in Enumerable.Range(acquired.AnchorSlot, acquired.Size))
            {
                if (!m_CurrentShelf.EmptySlots.Contains(slot))
                    m_CurrentShelf.EmptySlots.Add(slot);
            }
            m_CurrentShelf.EmptySlots.Sort();
            error = string.Empty;
            return true;
        }

        public void Reset()
        {
            m_EncounterKey = string.Empty;
            m_CurrentMerchant = null;
            m_CurrentShelf = null;
            m_PreferredArchetypeId = string.Empty;
            m_Balance = 0;
            m_PurchasedOfferIds.Clear();
            m_OfferRoles = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public bool TryGetCurrentSupply(out BuqiBazaarSupplyView supply)
        {
            if (m_CurrentShelf == null || m_CurrentMerchant == null)
            {
                supply = null;
                return false;
            }

            int refreshPrice = Math.Max(0, m_CurrentShelf.NextRefreshPrice);
            bool canRefresh = m_CurrentShelf.NextRefreshPrice >= 0;
            supply = new BuqiBazaarSupplyView
            {
                MerchantId = m_CurrentMerchant.Row.MerchantId,
                MerchantName = m_CurrentMerchant.Row.DisplayName,
                MerchantSpecialty = BuildSpecialty(m_CurrentMerchant, m_PreferredArchetypeId),
                MerchantNonWeaponOnly = m_CurrentMerchant.Row.Specialty == Game.Hot.BuqiMerchantSpecialty.NonWeaponOnly,
                PreferredArchetypeId = m_PreferredArchetypeId,
                Balance = m_Balance,
                RefreshCount = m_CurrentShelf.RefreshIndex,
                CanRefresh = canRefresh,
                RefreshPrice = refreshPrice,
                RefreshPriceLabel = canRefresh ? $"刷新 {refreshPrice} 金币" : "刷新次数已用尽",
                ShelfSlotCount = m_CurrentShelf.ShelfSlotCount,
                SupplyRngCursor = m_CurrentShelf.NextState.Cursor,
                SupplyGeneration = m_CurrentShelf.NextState.Generation,
                OfferIds = GetOfferIds(m_CurrentShelf),
                PurchasedOfferIds = m_PurchasedOfferIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                Offers = m_CurrentShelf.Offers.Select(offer => new BuqiBazaarOfferLayoutView
                {
                    OfferId = offer.DefinitionId,
                    AnchorSlot = offer.AnchorSlot,
                    Size = offer.Size,
                    Purchased = m_PurchasedOfferIds.Contains(offer.DefinitionId),
                    Category = offer.Category,
                }).ToArray(),
                EmptyShelfSlots = new List<int>(m_CurrentShelf.EmptySlots),
                OfferRoles = new Dictionary<string, string>(m_OfferRoles, StringComparer.Ordinal),
            };
            return true;
        }

        public bool TryCaptureSnapshot(
            out BuqiBazaarSupplySnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (m_CurrentShelf == null || m_CurrentMerchant == null ||
                m_CurrentShelf.NextState == null)
            {
                error = "Current merchant shelf is not open.";
                return false;
            }

            snapshot = new BuqiBazaarSupplySnapshot
            {
                EncounterKey = m_EncounterKey,
                MerchantId = m_CurrentMerchant.Row.MerchantId,
                PreferredArchetypeId = m_PreferredArchetypeId,
                Balance = m_Balance,
                RefreshIndex = m_CurrentShelf.RefreshIndex,
                RefreshPricePaid = m_CurrentShelf.RefreshPricePaid,
                NextRefreshPrice = m_CurrentShelf.NextRefreshPrice,
                ShelfSlotCount = m_CurrentShelf.ShelfSlotCount,
                MerchantSpecialty = m_CurrentMerchant.Row.Specialty,
                EmptyShelfSlots = new List<int>(m_CurrentShelf.EmptySlots),
                SupplyState = m_CurrentShelf.NextState.Clone(),
            };
            for (int index = 0; index < m_CurrentShelf.Offers.Count; index++)
            {
                BuqiSupplyDefinition offer = m_CurrentShelf.Offers[index];
                snapshot.Offers.Add(new BuqiBazaarSupplySnapshotOffer
                {
                    OfferId = offer.DefinitionId,
                    Quality = offer.Quality,
                    Category = offer.Category,
                    AnchorSlot = offer.AnchorSlot,
                    Size = offer.Size,
                    Purchased = m_PurchasedOfferIds.Contains(offer.DefinitionId),
                    Purpose = index < m_CurrentShelf.SlotPurposes.Count
                        ? m_CurrentShelf.SlotPurposes[index]
                        : BuqiSupplySlotPurpose.Wildcard,
                    Role = m_OfferRoles.TryGetValue(offer.DefinitionId, out string role)
                        ? role
                        : string.Empty,
                });
            }
            error = string.Empty;
            return true;
        }

        public bool TryRestore(
            BuqiBazaarSupplyContext context,
            BuqiBazaarSupplySnapshot snapshot,
            out string error)
        {
            if (!ValidateContext(context, out error) || snapshot == null)
                return false;

            string previousEncounterKey = m_EncounterKey;
            MerchantProfile previousMerchant = m_CurrentMerchant;
            BuqiSupplyShelf previousShelf = m_CurrentShelf;
            string previousPreference = m_PreferredArchetypeId;
            int previousBalance = m_Balance;
            var previousPurchases = new HashSet<string>(m_PurchasedOfferIds, StringComparer.Ordinal);
            Dictionary<string, string> previousRoles = m_OfferRoles;
            bool restored = false;
            try
            {
                if (!TryBuildShelfFromSnapshot(context, snapshot, out BuqiSupplyShelf shelf,
                        out HashSet<string> purchases, out Dictionary<string, string> roles, out error))
                    return false;

                m_EncounterKey = snapshot.EncounterKey;
                m_CurrentMerchant = m_Merchants.First(merchant =>
                    string.Equals(merchant.Row.MerchantId, snapshot.MerchantId, StringComparison.Ordinal));
                m_CurrentShelf = shelf;
                m_PreferredArchetypeId = snapshot.PreferredArchetypeId ?? string.Empty;
                m_Balance = context.Balance;
                m_PurchasedOfferIds.Clear();
                foreach (string purchase in purchases)
                    m_PurchasedOfferIds.Add(purchase);
                m_OfferRoles = roles;
                restored = true;
                return true;
            }
            finally
            {
                if (!restored)
                {
                    m_EncounterKey = previousEncounterKey;
                    m_CurrentMerchant = previousMerchant;
                    m_CurrentShelf = previousShelf;
                    m_PreferredArchetypeId = previousPreference;
                    m_Balance = previousBalance;
                    m_PurchasedOfferIds.Clear();
                    foreach (string purchase in previousPurchases)
                        m_PurchasedOfferIds.Add(purchase);
                    m_OfferRoles = previousRoles;
                }
            }
        }

        private bool TryGenerateShelf(
            int day,
            MerchantProfile merchant,
            string preferredArchetypeId,
            BuqiSupplyState source,
            int refreshIndex,
            out BuqiSupplyShelf shelf,
            out Dictionary<string, string> roles,
            out string error)
        {
            shelf = null;
            roles = new Dictionary<string, string>(StringComparer.Ordinal);
            List<SlotProfile> activeSlots = merchant.Slots
                .Where(slot => slot.Row.MinUnlockDay <= day && slot.Row.MaxUnlockDay >= day)
                .OrderByDescending(slot => slot.Row.Weight)
                .ThenBy(slot => slot.Row.SlotId, StringComparer.Ordinal)
                .ToList();
            if (activeSlots.Count == 0)
            {
                error = "No merchant slots are active for the current day.";
                return false;
            }

            BuqiSupplyState state = source.Clone();
            var offers = new List<BuqiSupplyDefinition>(BuqiSupplyService.MerchantShelfSlotCount);
            var purposes = new List<BuqiSupplySlotPurpose>(BuqiSupplyService.MerchantShelfSlotCount);
            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            var remainingCounts = activeSlots.ToDictionary(
                slot => slot,
                slot => Math.Max(1, slot.Row.Count));
            int occupiedSlotCount = 0;
            while (occupiedSlotCount < BuqiSupplyService.MerchantShelfSlotCount)
            {
                int remainingSlots = BuqiSupplyService.MerchantShelfSlotCount - occupiedSlotCount;
                List<SlotProfile> legalSlots = activeSlots.Where(slot =>
                        remainingCounts[slot] > 0 &&
                        FilterDefinitions(merchant, slot, day, selectedIds)
                            .Any(definition => definition.Size <= remainingSlots))
                    .ToList();
                if (legalSlots.Count == 0)
                    break;

                SlotProfile selectedSlot = offers.Count == 0
                    ? legalSlots[0]
                    : DrawSlot(legalSlots, remainingCounts, state.Seed, ref state.Cursor);
                remainingCounts[selectedSlot]--;
                List<BuqiSupplyDefinition> candidates = FilterDefinitions(
                        merchant, selectedSlot, day, selectedIds)
                    .Where(definition => definition.Size <= remainingSlots)
                    .ToList();

                var request = new BuqiSupplyRequest
                {
                    Day = day,
                    Source = BuqiSupplySource.Merchant,
                    MerchantPoolId = merchant.Row.MerchantId,
                    PreferredArchetypeId = preferredArchetypeId,
                    CandidateCount = 1,
                };
                var slotService = new BuqiSupplyService(candidates);
                if (!slotService.TryGenerate(request, state, refreshIndex,
                        out BuqiSupplyShelf selected, out error))
                    return false;

                BuqiSupplyDefinition offer = selected.Offers[0];
                offer.AnchorSlot = occupiedSlotCount;
                occupiedSlotCount += offer.Size;
                offers.Add(offer);
                purposes.Add(MapPurpose(offer.Role));
                selectedIds.Add(offer.DefinitionId);
                roles[offer.DefinitionId] = selectedSlot == null
                    ? $"General shelf / {BuildOfferRole(null, offer)}"
                    : BuildOfferRole(selectedSlot, offer);
                state = selected.NextState;
            }

            if (offers.Count == 0)
            {
                error = "Merchant shelf has no legal offer for the current day.";
                return false;
            }
            shelf = new BuqiSupplyShelf
            {
                Day = day,
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = merchant.Row.MerchantId,
                RefreshIndex = refreshIndex,
                RefreshPricePaid = refreshIndex == 0
                    ? 0
                    : BuqiSupplyService.CalculateRefreshPrice(refreshIndex - 1),
                NextRefreshPrice = refreshIndex >= BuqiSupplyService.MaximumRefreshCount
                    ? -1
                    : BuqiSupplyService.CalculateRefreshPrice(refreshIndex),
                ShelfSlotCount = BuqiSupplyService.MerchantShelfSlotCount,
                Offers = offers,
                EmptySlots = CreateEmptySlots(offers, new HashSet<string>(StringComparer.Ordinal)),
                SlotPurposes = purposes,
                NextState = state,
            };
            error = string.Empty;
            return true;
        }

        private bool TryBuildShelfFromSnapshot(
            BuqiBazaarSupplyContext context,
            BuqiBazaarSupplySnapshot snapshot,
            out BuqiSupplyShelf shelf,
            out HashSet<string> purchases,
            out Dictionary<string, string> roles,
            out string error)
        {
            shelf = null;
            purchases = new HashSet<string>(StringComparer.Ordinal);
            roles = new Dictionary<string, string>(StringComparer.Ordinal);
            if (snapshot.SupplyState == null || snapshot.SupplyState.Cursor < 0 ||
                snapshot.SupplyState.Generation < 0 ||
                snapshot.SupplyState.LastOfferDefinitionIds == null ||
                snapshot.SupplyState.PriorOfferDefinitionIds == null ||
                snapshot.SupplyState.TagMemory == null ||
                snapshot.SupplyState.TagMemory.Any(pair =>
                    string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) ||
                snapshot.ShelfSlotCount != BuqiSupplyService.MerchantShelfSlotCount ||
                snapshot.RefreshIndex < 0 || snapshot.RefreshIndex > BuqiSupplyService.MaximumRefreshCount ||
                snapshot.Offers == null || snapshot.Offers.Count < 1 ||
                snapshot.Offers.Count > snapshot.ShelfSlotCount ||
                string.IsNullOrWhiteSpace(snapshot.MerchantId) ||
                !string.Equals(snapshot.EncounterKey, CreateEncounterKey(context), StringComparison.Ordinal) ||
                snapshot.Balance != context.Balance)
            {
                error = "Bazaar supply snapshot metadata is invalid.";
                return false;
            }

            int expectedRefreshPricePaid = snapshot.RefreshIndex == 0
                ? 0
                : BuqiSupplyService.CalculateRefreshPrice(snapshot.RefreshIndex - 1);
            int expectedNextRefreshPrice = snapshot.RefreshIndex >= BuqiSupplyService.MaximumRefreshCount
                ? -1
                : BuqiSupplyService.CalculateRefreshPrice(snapshot.RefreshIndex);
            if (snapshot.RefreshPricePaid != expectedRefreshPricePaid ||
                snapshot.NextRefreshPrice != expectedNextRefreshPrice)
            {
                error = "Bazaar supply snapshot refresh metadata is invalid.";
                return false;
            }

            MerchantProfile merchant = m_Merchants.FirstOrDefault(candidate =>
                string.Equals(candidate.Row.MerchantId, snapshot.MerchantId, StringComparison.Ordinal));
            if (merchant == null || merchant.Row.MinDay > context.Day || merchant.Row.MaxDay < context.Day ||
                snapshot.MerchantSpecialty != merchant.Row.Specialty)
            {
                error = "Bazaar supply snapshot merchant is not available.";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var occupied = new bool[snapshot.ShelfSlotCount];
            var offers = new List<BuqiSupplyDefinition>(snapshot.Offers.Count);
            var purposes = new List<BuqiSupplySlotPurpose>(snapshot.Offers.Count);
            int expectedAnchorSlot = 0;
            foreach (BuqiBazaarSupplySnapshotOffer saved in snapshot.Offers)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.OfferId) ||
                    !seen.Add(saved.OfferId) || saved.AnchorSlot != expectedAnchorSlot ||
                    saved.Size < 1 || saved.Size > 3 ||
                    saved.AnchorSlot + saved.Size > snapshot.ShelfSlotCount ||
                    !Enum.IsDefined(typeof(BuqiSupplySlotPurpose), saved.Purpose))
                {
                    error = "Bazaar supply snapshot contains an invalid offer span.";
                    return false;
                }
                expectedAnchorSlot += saved.Size;
                BuqiSupplyDefinition definition = m_Definitions.FirstOrDefault(candidate =>
                    string.Equals(candidate.DefinitionId, saved.OfferId, StringComparison.Ordinal) &&
                    candidate.Quality == saved.Quality &&
                    merchant.PoolItemIds.Contains(candidate.DefinitionId) &&
                    candidate.MinimumDay <= context.Day && candidate.MaximumDay >= context.Day);
                if (definition == null || definition.Size != saved.Size ||
                    definition.Category != saved.Category || !IsMerchantAllowed(merchant, definition.Category))
                {
                    error = "Bazaar supply snapshot contains an unknown or incompatible offer.";
                    return false;
                }
                BuqiSupplyDefinition placed = definition.Clone();
                placed.AnchorSlot = saved.AnchorSlot;
                offers.Add(placed);
                purposes.Add(saved.Purpose);
                if (saved.Purchased)
                    purchases.Add(saved.OfferId);
                if (!saved.Purchased)
                {
                    for (int slot = saved.AnchorSlot; slot < saved.AnchorSlot + saved.Size; slot++)
                    {
                        if (occupied[slot])
                        {
                            error = "Bazaar supply snapshot contains overlapping offers.";
                            return false;
                        }
                        occupied[slot] = true;
                    }
                }
                roles[saved.OfferId] = saved.Role ?? string.Empty;
            }

            var authoritativePurchases = new HashSet<string>(
                context.PurchasedOfferIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (!purchases.SetEquals(authoritativePurchases))
            {
                error = "Bazaar supply snapshot purchases do not match the restore context.";
                return false;
            }

            List<int> expectedEmptySlots = CreateEmptySlots(occupied);
            if (snapshot.EmptyShelfSlots == null ||
                !snapshot.EmptyShelfSlots.SequenceEqual(expectedEmptySlots))
            {
                error = "Bazaar supply snapshot empty slots do not match its offers.";
                return false;
            }
            shelf = new BuqiSupplyShelf
            {
                Day = context.Day,
                Source = BuqiSupplySource.Merchant,
                MerchantPoolId = snapshot.MerchantId,
                RefreshIndex = snapshot.RefreshIndex,
                RefreshPricePaid = snapshot.RefreshPricePaid,
                NextRefreshPrice = snapshot.NextRefreshPrice,
                ShelfSlotCount = snapshot.ShelfSlotCount,
                Offers = offers,
                EmptySlots = expectedEmptySlots,
                SlotPurposes = purposes,
                NextState = snapshot.SupplyState.Clone(),
            };
            error = string.Empty;
            return true;
        }

        private static void ApplyPurchasedSlots(
            BuqiSupplyShelf shelf,
            IEnumerable<string> purchasedOfferIds)
        {
            shelf.EmptySlots = CreateEmptySlots(
                shelf.Offers,
                new HashSet<string>(purchasedOfferIds ?? Array.Empty<string>(), StringComparer.Ordinal));
        }

        private static List<int> CreateEmptySlots(
            IReadOnlyList<BuqiSupplyDefinition> offers,
            ISet<string> purchasedOfferIds)
        {
            var occupied = new bool[BuqiSupplyService.MerchantShelfSlotCount];
            foreach (BuqiSupplyDefinition offer in offers ?? Array.Empty<BuqiSupplyDefinition>())
            {
                if (offer == null || purchasedOfferIds.Contains(offer.DefinitionId))
                    continue;
                for (int slot = offer.AnchorSlot;
                     slot < offer.AnchorSlot + offer.Size && slot < occupied.Length;
                     slot++)
                {
                    if (slot >= 0)
                        occupied[slot] = true;
                }
            }
            return CreateEmptySlots(occupied);
        }

        private static List<int> CreateEmptySlots(bool[] occupied)
        {
            var result = new List<int>();
            for (int slot = 0; slot < occupied.Length; slot++)
            {
                if (!occupied[slot])
                    result.Add(slot);
            }
            return result;
        }

        private List<BuqiSupplyDefinition> FilterDefinitions(
            MerchantProfile merchant,
            SlotProfile slot,
            int day,
            HashSet<string> selectedIds)
        {
            return m_Definitions.Where(definition =>
                    !selectedIds.Contains(definition.DefinitionId) &&
                    merchant.PoolItemIds.Contains(definition.DefinitionId) &&
                    IsMerchantAllowed(merchant, definition.Category) &&
                    definition.MinimumDay <= day && definition.MaximumDay >= day &&
                    slot.Builds.Contains(definition.ArchetypeId) &&
                    slot.Sizes.Contains(definition.Size) &&
                    slot.Qualities.Contains(definition.Quality) &&
                    (string.IsNullOrEmpty(slot.Row.RequiredTag) ||
                     definition.Tags.Contains(slot.Row.RequiredTag)))
                .ToList();
        }

        private MerchantProfile SelectMerchant(BuqiBazaarSupplyContext context, string preferredArchetypeId)
        {
            List<MerchantProfile> eligible = m_Merchants
                .Where(merchant => merchant.Row.MinDay <= context.Day &&
                                   merchant.Row.MaxDay >= context.Day &&
                                   merchant.PoolItemIds.Count(definitionId =>
                                   m_Items[definitionId].Row.UnlockDay <= context.Day) > 0)
                .ToList();
            if (eligible.Count == 0)
                return null;

            int cursor = context.Day * 31 + context.EncounterIndex * 17;
            var weights = new int[eligible.Count];
            int total = 0;
            for (int index = 0; index < eligible.Count; index++)
            {
                int weight = eligible[index].Row.Weight;
                if (!string.IsNullOrEmpty(preferredArchetypeId) &&
                    eligible[index].ContainsBuild(preferredArchetypeId, m_Items))
                {
                    weight = (int)(((long)weight * PreferenceMerchantFactorBps) / BasisPoints);
                }
                weights[index] = Math.Max(1, weight);
                total += weights[index];
            }

            int roll = BuqiRunRandom.Next(context.RunSeed, ref cursor, total);
            for (int index = 0; index < weights.Length; index++)
            {
                if (roll < weights[index])
                    return eligible[index];
                roll -= weights[index];
            }
            return eligible[eligible.Count - 1];
        }

        private BuqiSupplyState CreateInitialState(
            BuqiBazaarSupplyContext context,
            MerchantProfile merchant,
            string preferredArchetypeId)
        {
            long seed = context.RunSeed ^ StableHash(merchant.Row.MerchantId) ^
                        ((long)context.Day << 32) ^ (uint)context.EncounterIndex;
            BuqiSupplyState state = BuqiSupplyState.CreateInitial(seed);
            var signals = new List<string>();
            if (!string.IsNullOrEmpty(preferredArchetypeId))
                signals.Add(preferredArchetypeId);
            foreach (string definitionId in context.OwnedDefinitionIds ?? Array.Empty<string>())
            {
                if (!m_Items.TryGetValue(definitionId, out ItemProfile item))
                    continue;
                signals.Add(item.Row.ArchetypeId);
                signals.AddRange(item.Row.Tags);
            }
            return m_Service.ShiftAffinity(state, signals);
        }

        private string ResolvePreference(IReadOnlyList<string> ownedDefinitionIds)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string definitionId in ownedDefinitionIds ?? Array.Empty<string>())
            {
                if (!m_Items.TryGetValue(definitionId, out ItemProfile item) ||
                    string.Equals(item.Row.ArchetypeId, "shared", StringComparison.Ordinal))
                {
                    continue;
                }
                counts.TryGetValue(item.Row.ArchetypeId, out int count);
                counts[item.Row.ArchetypeId] = count + 1;
            }
            return counts.OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key)
                .FirstOrDefault() ?? string.Empty;
        }

        private static bool TryCreateMerchant(
            BuqiMerchantConfigRow row,
            IReadOnlyDictionary<string, ItemProfile> items,
            HashSet<string> merchantIds,
            out MerchantProfile merchant,
            out string error)
        {
            merchant = null;
            if (row == null || string.IsNullOrWhiteSpace(row.MerchantId) ||
                string.IsNullOrWhiteSpace(row.DisplayName) ||
                !merchantIds.Add(row.MerchantId) ||
                row.MinDay < 1 || row.MinDay > row.MaxDay ||
                row.MaxDay > BuqiRunRules.RunDayCount || row.Weight <= 0 ||
                !Enum.IsDefined(typeof(Game.Hot.BuqiMerchantSpecialty), row.Specialty) ||
                row.PoolItemIds == null || row.PoolItemIds.Count < 1 ||
                row.Slots == null || row.Slots.Count != BuqiSupplyService.MerchantSlotCount)
            {
                error = "商人的编号、日期、权重、货池和 4 个货位必须有效。";
                return false;
            }

            var pool = new HashSet<string>(StringComparer.Ordinal);
            foreach (string definitionId in row.PoolItemIds)
            {
                if (!items.TryGetValue(definitionId, out ItemProfile item) ||
                    !pool.Add(definitionId) ||
                    !IsMerchantAllowed(row.Specialty, item.Row.Category))
                {
                    error = $"商人“{row.MerchantId}”的货池包含未知或重复装备。";
                    return false;
                }
            }

            var slots = new List<SlotProfile>(row.Slots.Count);
            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuqiMerchantSlotConfigRow slot in row.Slots)
            {
                if (!TryCreateSlot(row.MerchantId, slot, slotIds, out SlotProfile profile, out error))
                    return false;
                slots.Add(profile);
            }
            merchant = new MerchantProfile(row, pool, slots);
            error = string.Empty;
            return true;
        }

        private static bool IsMerchantAllowed(MerchantProfile merchant, ItemProfile item)
        {
            return merchant != null && item != null &&
                IsMerchantAllowed(merchant.Row.Specialty, item.Row.Category);
        }

        private static bool IsMerchantAllowed(
            MerchantProfile merchant,
            Game.Hot.BuqiItemCategory category)
        {
            return merchant != null && IsMerchantAllowed(merchant.Row.Specialty, category);
        }

        private static bool IsMerchantAllowed(
            BuqiMerchantConfigRow row,
            Game.Hot.BuqiItemCategory category)
        {
            return row != null && IsMerchantAllowed(row.Specialty, category);
        }

        private static bool IsMerchantAllowed(
            Game.Hot.BuqiMerchantSpecialty specialty,
            Game.Hot.BuqiItemCategory category)
        {
            return specialty != Game.Hot.BuqiMerchantSpecialty.NonWeaponOnly ||
                category == Game.Hot.BuqiItemCategory.NonWeapon;
        }

        private static bool TryCreateSlot(
            string merchantId,
            BuqiMerchantSlotConfigRow row,
            HashSet<string> slotIds,
            out SlotProfile slot,
            out string error)
        {
            slot = null;
            List<string> builds = Split(row?.BuildFilter);
            List<int> sizes = ParseSizes(row?.SizeFilter);
            List<BuqiSupplyQuality> qualities = ParseQualities(row?.QualityFilter);
            if (row == null || string.IsNullOrWhiteSpace(row.SlotId) || !slotIds.Add(row.SlotId) ||
                string.IsNullOrWhiteSpace(row.SlotKind) || string.IsNullOrWhiteSpace(row.RequiredTag) ||
                builds.Count == 0 || sizes.Count == 0 || qualities.Count == 0 ||
                row.MinUnlockDay < 1 || row.MinUnlockDay > row.MaxUnlockDay ||
                row.MaxUnlockDay > BuqiRunRules.RunDayCount || row.Weight <= 0 || row.Count <= 0)
            {
                error = $"商人“{merchantId}”包含无效货位。";
                return false;
            }
            slot = new SlotProfile(row, builds, sizes, qualities);
            error = string.Empty;
            return true;
        }

        private static SlotProfile DrawSlot(
            IReadOnlyList<SlotProfile> slots,
            IReadOnlyDictionary<SlotProfile, int> remainingCounts,
            long seed,
            ref int cursor)
        {
            List<SlotProfile> available = slots.Where(slot => remainingCounts[slot] > 0).ToList();
            if (available.Count == 0)
                available = slots.ToList();
            int total = available.Sum(slot => slot.Row.Weight);
            int roll = BuqiRunRandom.Next(seed, ref cursor, total);
            foreach (SlotProfile slot in available)
            {
                if (roll < slot.Row.Weight)
                    return slot;
                roll -= slot.Row.Weight;
            }
            return available[available.Count - 1];
        }

        private bool RestorePurchases(BuqiBazaarSupplyContext context, out string error)
        {
            m_Balance = context.Balance;
            m_PurchasedOfferIds.Clear();
            foreach (string purchased in context.PurchasedOfferIds ?? Array.Empty<string>())
            {
                if (!GetOfferIds(m_CurrentShelf).Contains(purchased, StringComparer.Ordinal))
                {
                    error = "存档中的已购商品不属于恢复后的货架。";
                    return false;
                }
                m_PurchasedOfferIds.Add(purchased);
            }
            error = string.Empty;
            return true;
        }

        private static BuqiBazaarSupplyContext CloneContext(BuqiBazaarSupplyContext source, int balance)
        {
            return new BuqiBazaarSupplyContext
            {
                RunSeed = source.RunSeed,
                Day = source.Day,
                EncounterIndex = source.EncounterIndex,
                Balance = balance,
                OwnedDefinitionIds = source.OwnedDefinitionIds,
                PurchasedOfferIds = source.PurchasedOfferIds,
            };
        }

        private static bool ValidateContext(BuqiBazaarSupplyContext context, out string error)
        {
            if (context == null || context.Day < 1 || context.Day > BuqiRunRules.RunDayCount ||
                context.EncounterIndex < 0 || context.EncounterIndex >= BuqiRunRules.EncountersPerDay ||
                context.Balance < 0)
            {
                error = "商店供应的日期、经营序号或金币无效。";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static string CreateEncounterKey(BuqiBazaarSupplyContext context)
        {
            return $"{context.RunSeed}:{context.Day}:{context.EncounterIndex}";
        }

        private static IReadOnlyList<string> GetOfferIds(BuqiSupplyShelf shelf)
        {
            return shelf.Offers.Select(offer => offer.DefinitionId).ToArray();
        }

        private static BuqiSupplyProductRole MapRole(string role)
        {
            switch (role)
            {
                case "bridge":
                case "pivot":
                    return BuqiSupplyProductRole.Bridge;
                case "counter":
                    return BuqiSupplyProductRole.Counter;
                case "economy":
                    return BuqiSupplyProductRole.Economy;
                case "starter":
                case "core":
                case "amplifier":
                case "finisher":
                    return BuqiSupplyProductRole.Mainline;
                default:
                    return BuqiSupplyProductRole.Wildcard;
            }
        }

        private static BuqiSupplySlotPurpose MapPurpose(BuqiSupplyProductRole role)
        {
            switch (role)
            {
                case BuqiSupplyProductRole.Mainline: return BuqiSupplySlotPurpose.Mainline;
                case BuqiSupplyProductRole.Bridge: return BuqiSupplySlotPurpose.Bridge;
                case BuqiSupplyProductRole.Counter:
                case BuqiSupplyProductRole.Economy: return BuqiSupplySlotPurpose.CounterOrEconomy;
                default: return BuqiSupplySlotPurpose.Wildcard;
            }
        }

        private static string BuildOfferRole(SlotProfile slot, BuqiSupplyDefinition offer)
        {
            switch (slot?.Row.SlotKind)
            {
                case "Bridge": return "桥接位";
                case "Counter": return "反制位";
                case "Economy": return "经济位";
                case "Quality": return "品质位";
                case "Stage": return "阶段位";
                case "Size": return "尺寸位";
            }
            switch (offer.Role)
            {
                case BuqiSupplyProductRole.Mainline: return "主线位";
                case BuqiSupplyProductRole.Bridge: return "桥接位";
                case BuqiSupplyProductRole.Counter: return "反制位";
                case BuqiSupplyProductRole.Economy: return "经济位";
                default: return "机动位";
            }
        }

        private static string BuildSpecialty(MerchantProfile merchant, string preferredArchetypeId)
        {
            string preference = string.IsNullOrEmpty(preferredArchetypeId)
                ? "均衡"
                : BuildName(preferredArchetypeId);
            string specialty = merchant.Row.Specialty == Game.Hot.BuqiMerchantSpecialty.NonWeaponOnly
                ? "非武器专营"
                : "受约束供应";
            return $"{specialty} · 子池 {merchant.PoolItemIds.Count} 件 · 当前偏好 {preference}";
        }

        private static string BuildName(string archetypeId)
        {
            switch (archetypeId)
            {
                case "fast": return "攻击";
                case "buffer": return "护盾";
                case "heal": return "恢复";
                case "chain": return "连锁";
                case "poison": return "毒蚀";
                case "burn": return "灼烧";
                case "freeze": return "冻结";
                case "overload": return "过载";
                default: return "均衡";
            }
        }

        private static List<string> Normalize(IEnumerable<string> values)
        {
            return values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> Split(string value)
        {
            return (value ?? string.Empty).Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => token.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static List<int> ParseSizes(string value)
        {
            var result = new List<int>();
            foreach (string token in Split(value))
            {
                int size = token == "S" ? 1 : token == "M" ? 2 : token == "L" ? 3 : 0;
                if (size == 0)
                    return new List<int>();
                if (!result.Contains(size))
                    result.Add(size);
            }
            return result;
        }

        private static List<BuqiSupplyQuality> ParseQualities(string value)
        {
            var result = new List<BuqiSupplyQuality>();
            foreach (string token in Split(value))
            {
                BuqiSupplyQuality quality;
                if (token == "Normal") quality = BuqiSupplyQuality.Common;
                else if (token == "Improved") quality = BuqiSupplyQuality.Improved;
                else if (token == "Fixed") quality = BuqiSupplyQuality.Finalized;
                else return new List<BuqiSupplyQuality>();
                if (!result.Contains(quality))
                    result.Add(quality);
            }
            return result;
        }

        private static long StableHash(string value)
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 1099511628211L;
                }
                return hash;
            }
        }

        private sealed class ItemProfile
        {
            public ItemProfile(BuqiItemConfigRow row)
            {
                Row = row;
            }

            public BuqiItemConfigRow Row { get; }
        }

        private sealed class MerchantProfile
        {
            public MerchantProfile(
                BuqiMerchantConfigRow row,
                HashSet<string> poolItemIds,
                List<SlotProfile> slots)
            {
                Row = row;
                PoolItemIds = poolItemIds;
                Slots = slots;
            }

            public BuqiMerchantConfigRow Row { get; }
            public HashSet<string> PoolItemIds { get; }
            public List<SlotProfile> Slots { get; }

            public bool ContainsBuild(
                string archetypeId,
                IReadOnlyDictionary<string, ItemProfile> items)
            {
                return PoolItemIds.Any(id => items.TryGetValue(id, out ItemProfile item) &&
                    string.Equals(item.Row.ArchetypeId, archetypeId, StringComparison.Ordinal));
            }
        }

        private sealed class SlotProfile
        {
            public SlotProfile(
                BuqiMerchantSlotConfigRow row,
                IEnumerable<string> builds,
                IEnumerable<int> sizes,
                IEnumerable<BuqiSupplyQuality> qualities)
            {
                Row = row;
                Builds = new HashSet<string>(builds, StringComparer.Ordinal);
                Sizes = new HashSet<int>(sizes);
                Qualities = new HashSet<BuqiSupplyQuality>(qualities);
            }

            public BuqiMerchantSlotConfigRow Row { get; }
            public HashSet<string> Builds { get; }
            public HashSet<int> Sizes { get; }
            public HashSet<BuqiSupplyQuality> Qualities { get; }
        }
    }
}
