using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.Tests
{
    public static class BuqiSupplyConfigTestSuite
    {
        private const int ContractCount = 4;

        public static List<string> RunAll()
        {
            var failures = new List<string>();
            Run("unknown-item-category", UnknownItemCategory, failures);
            Run("invalid-merchant-specialty", InvalidMerchantSpecialty, failures);
            Run("weapon-in-non-weapon-pool", WeaponInNonWeaponPool, failures);
            Run("valid-classification", ValidClassification, failures);
            return failures;
        }

        public static int Main()
        {
            List<string> failures = RunAll();
            foreach (string failure in failures)
                Console.Error.WriteLine(failure);
            Console.WriteLine($"supply-config-contracts={ContractCount - failures.Count}/{ContractCount}");
            return failures.Count == 0 ? 0 : 1;
        }

        private static void UnknownItemCategory()
        {
            Require(new BuqiItemConfigRow().Category == Game.Hot.BuqiItemCategory.Unknown,
                "An omitted item category must remain invalid by default.");
            BuqiConfigCatalog catalog = BuqiBazaarSupplyViewSourceTestSuite.CreateCatalog();
            catalog.Items[0].Category = Game.Hot.BuqiItemCategory.Unknown;

            List<string> errors = BuqiConfigValidator.Validate(catalog);
            Require(errors.Any(error => error.Contains("物品分类")),
                "Unknown item category must be rejected by config validation.");
        }

        private static void InvalidMerchantSpecialty()
        {
            BuqiConfigCatalog catalog = BuqiBazaarSupplyViewSourceTestSuite.CreateCatalog();
            catalog.Merchants[0].Specialty = (Game.Hot.BuqiMerchantSpecialty)99;

            List<string> errors = BuqiConfigValidator.Validate(catalog);
            Require(errors.Any(error => error.Contains("invalid specialty")),
                "Undefined merchant specialty must be rejected by config validation.");
        }

        private static void WeaponInNonWeaponPool()
        {
            BuqiConfigCatalog catalog = BuqiBazaarSupplyViewSourceTestSuite.CreateCatalog();
            BuqiMerchantConfigRow merchant = catalog.Merchants[0];
            merchant.Specialty = Game.Hot.BuqiMerchantSpecialty.NonWeaponOnly;
            catalog.Items.First(item => merchant.PoolItemIds.Contains(item.DefinitionId)).Category =
                Game.Hot.BuqiItemCategory.Weapon;

            List<string> errors = BuqiConfigValidator.Validate(catalog);
            Require(errors.Any(error => error.Contains("non-weapon specialty cannot reference weapon")),
                "Non-weapon merchant pools must reject weapon-classified items.");
        }

        private static void ValidClassification()
        {
            BuqiConfigCatalog catalog = BuqiBazaarSupplyViewSourceTestSuite.CreateCatalog();
            List<string> errors = BuqiConfigValidator.Validate(catalog);
            Require(!errors.Any(error => error.Contains("物品分类") || error.Contains("specialty")),
                "Valid item categories and merchant specialties must pass their config contracts.");
        }

        private static void Run(string name, Action test, List<string> failures)
        {
            try
            {
                test();
            }
            catch (Exception exception)
            {
                failures.Add($"{name}: {exception.Message}");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
