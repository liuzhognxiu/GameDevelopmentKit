using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Run.Core;

namespace Game.Hot.Buqi.Run.Supply
{
    public sealed class BuqiSupplyCatalogItem
    {
        public string DefinitionId = string.Empty;
        public string ArchetypeId = string.Empty;
        public int Size;
        public List<string> Tags = new List<string>();
    }

    public sealed class BuqiSupplyAvailabilityRule
    {
        public BuqiSupplyProductRole Role;
        public int MinimumDay = 1;
        public int MaximumDay = BuqiRunRules.ContentScheduleDayCount;
        public BuqiSupplyQuality Quality;
        public BuqiSupplySource Sources = BuqiSupplySource.All;
        public int BaseWeight = 100;
        public string RefinementId = string.Empty;
        public List<string> MerchantPoolIds = new List<string>();
    }

    public sealed class BuqiSupplyChannelProfile
    {
        public string ChannelId = string.Empty;
        public BuqiSupplySource Source = BuqiSupplySource.Merchant;
        public string MerchantPoolId = string.Empty;
        public int UnlockDay = 1;
        public int RetireDay = BuqiRunRules.ContentScheduleDayCount;
        public BuqiSupplyQuality MinimumQuality = BuqiSupplyQuality.Common;
        public BuqiSupplyQuality MaximumQuality = BuqiSupplyQuality.Finalized;
        public int CandidateCount = BuqiSupplyService.MerchantSlotCount;
        public List<int> AllowedSizes = new List<int>();
        public List<string> AllowedArchetypeIds = new List<string>();
        public List<BuqiSupplyProductRole> AllowedRoles = new List<BuqiSupplyProductRole>();
    }

    public static class BuqiSupplyIntegration
    {
        public static bool TryCreateDefinition(
            BuqiSupplyCatalogItem item,
            BuqiSupplyAvailabilityRule rule,
            out BuqiSupplyDefinition definition,
            out string error)
        {
            definition = null!;
            if (item == null || rule == null)
            {
                error = "Supply item metadata and availability rule are required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(item.DefinitionId) ||
                string.IsNullOrWhiteSpace(item.ArchetypeId))
            {
                error = "Supply definition and archetype ids are required.";
                return false;
            }
            if (item.Size < 1 || item.Size > 3)
            {
                error = "Supply item size must be between one and three.";
                return false;
            }
            if (!IsValidDayWindow(rule.MinimumDay, rule.MaximumDay))
            {
                error = "Supply availability must fit within the nine-Day run.";
                return false;
            }
            if (!Enum.IsDefined(typeof(BuqiSupplyProductRole), rule.Role) ||
                !Enum.IsDefined(typeof(BuqiSupplyQuality), rule.Quality))
            {
                error = "Supply role or quality is invalid.";
                return false;
            }
            if (!IsValidSources(rule.Sources) || rule.BaseWeight <= 0)
            {
                error = "Supply sources and base weight must be valid.";
                return false;
            }

            definition = new BuqiSupplyDefinition
            {
                DefinitionId = item.DefinitionId.Trim(),
                ArchetypeId = item.ArchetypeId.Trim(),
                Role = rule.Role,
                MinimumDay = rule.MinimumDay,
                MaximumDay = rule.MaximumDay,
                Size = item.Size,
                Quality = rule.Quality,
                Sources = rule.Sources,
                BaseWeight = rule.BaseWeight,
                RefinementId = rule.RefinementId?.Trim() ?? string.Empty,
                Tags = NormalizeStrings(item.Tags),
                MerchantPoolIds = NormalizeStrings(rule.MerchantPoolIds),
            };
            error = string.Empty;
            return true;
        }

        public static bool TryCreateRequest(
            int runDay,
            BuqiSupplyChannelProfile profile,
            string preferredArchetypeId,
            out BuqiSupplyRequest request,
            out string error)
        {
            request = null!;
            if (profile == null || string.IsNullOrWhiteSpace(profile.ChannelId))
            {
                error = "Supply channel profile and id are required.";
                return false;
            }
            if (!IsSingleSource(profile.Source))
            {
                error = "Supply channel must identify one consumer.";
                return false;
            }
            if (!IsValidDayWindow(profile.UnlockDay, profile.RetireDay) || runDay < 1)
            {
                error = "Supply channel Day window is invalid.";
                return false;
            }
            int contentDay = BuqiRunRules.GetContentScheduleDay(runDay);
            if (contentDay < profile.UnlockDay || contentDay > profile.RetireDay)
            {
                error = "Supply channel is not unlocked for the current Day.";
                return false;
            }
            if (!Enum.IsDefined(typeof(BuqiSupplyQuality), profile.MinimumQuality) ||
                !Enum.IsDefined(typeof(BuqiSupplyQuality), profile.MaximumQuality) ||
                profile.MinimumQuality > profile.MaximumQuality)
            {
                error = "Supply channel quality range is invalid.";
                return false;
            }
            if (profile.CandidateCount < 1 ||
                profile.CandidateCount > BuqiSupplyService.MerchantSlotCount)
            {
                error = "Supply channel candidate count must be between one and four.";
                return false;
            }
            if (profile.Source == BuqiSupplySource.Merchant &&
                string.IsNullOrWhiteSpace(profile.MerchantPoolId))
            {
                error = "Merchant supply requires a pool id.";
                return false;
            }
            if (profile.Source != BuqiSupplySource.Merchant &&
                !string.IsNullOrWhiteSpace(profile.MerchantPoolId))
            {
                error = "Reward supply cannot declare a merchant pool id.";
                return false;
            }

            List<int> sizes = NormalizeSizes(profile.AllowedSizes);
            if (sizes.Count != (profile.AllowedSizes?.Distinct().Count() ?? 0))
            {
                error = "Supply channel contains an invalid size.";
                return false;
            }
            List<BuqiSupplyProductRole> roles = NormalizeRoles(profile.AllowedRoles);
            if (roles.Count != (profile.AllowedRoles?.Distinct().Count() ?? 0))
            {
                error = "Supply channel contains an invalid role.";
                return false;
            }

            request = new BuqiSupplyRequest
            {
                Day = runDay,
                Source = profile.Source,
                MerchantPoolId = profile.Source == BuqiSupplySource.Merchant
                    ? profile.MerchantPoolId.Trim()
                    : string.Empty,
                PreferredArchetypeId = preferredArchetypeId?.Trim() ?? string.Empty,
                MinimumQuality = profile.MinimumQuality,
                MaximumQuality = profile.MaximumQuality,
                CandidateCount = profile.CandidateCount,
                AllowedSizes = sizes,
                AllowedArchetypeIds = NormalizeStrings(profile.AllowedArchetypeIds),
                AllowedRoles = roles,
            };
            error = string.Empty;
            return true;
        }

        public static BuqiSupplyState ApplyBuildPreference(
            BuqiSupplyService service,
            BuqiSupplyState source,
            string archetypeId,
            IEnumerable<string> buildTags)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(archetypeId))
                throw new ArgumentException("Archetype id is required.", nameof(archetypeId));

            var signals = new List<string> { archetypeId.Trim() };
            if (buildTags != null)
                signals.AddRange(buildTags);
            return service.ShiftAffinity(source, NormalizeStrings(signals));
        }

        public static IReadOnlyList<string> GetOfferDefinitionIds(BuqiSupplyShelf shelf)
        {
            if (shelf == null)
                throw new ArgumentNullException(nameof(shelf));

            var result = new List<string>(shelf.Offers.Count);
            foreach (BuqiSupplyDefinition offer in shelf.Offers)
            {
                if (offer == null || string.IsNullOrWhiteSpace(offer.DefinitionId))
                    throw new InvalidOperationException("Supply shelf contains an invalid offer.");
                result.Add(offer.DefinitionId);
            }
            return result;
        }

        private static bool IsValidDayWindow(int minimumDay, int maximumDay)
        {
            return minimumDay >= 1 && minimumDay <= maximumDay &&
                   maximumDay <= BuqiRunRules.ContentScheduleDayCount;
        }

        private static bool IsValidSources(BuqiSupplySource sources)
        {
            return sources != BuqiSupplySource.None &&
                   (sources & ~BuqiSupplySource.All) == BuqiSupplySource.None;
        }

        private static bool IsSingleSource(BuqiSupplySource source)
        {
            return source == BuqiSupplySource.Merchant ||
                   source == BuqiSupplySource.Event ||
                   source == BuqiSupplySource.Pve;
        }

        private static List<string> NormalizeStrings(IEnumerable<string> source)
        {
            var result = new List<string>();
            if (source == null)
                return result;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in source)
            {
                string normalized = value?.Trim() ?? string.Empty;
                if (normalized.Length > 0 && seen.Add(normalized))
                    result.Add(normalized);
            }
            return result;
        }

        private static List<int> NormalizeSizes(IEnumerable<int> source)
        {
            if (source == null)
                return new List<int>();
            return source.Where(size => size >= 1 && size <= 3).Distinct().ToList();
        }

        private static List<BuqiSupplyProductRole> NormalizeRoles(
            IEnumerable<BuqiSupplyProductRole> source)
        {
            if (source == null)
                return new List<BuqiSupplyProductRole>();
            return source.Where(role => Enum.IsDefined(typeof(BuqiSupplyProductRole), role))
                .Distinct().ToList();
        }
    }
}
