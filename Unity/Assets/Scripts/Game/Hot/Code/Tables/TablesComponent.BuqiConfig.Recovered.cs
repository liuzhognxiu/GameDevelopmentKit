using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot
{
    public partial class TablesComponent
    {
        public BuqiConfigCatalog BuqiConfig { get; private set; }
        public IItemDefinitionProvider BuqiItemDefinitions { get; private set; }

        partial void PostResolveRef()
        {
            if (!BuqiGeneratedConfigAdapter.HasGeneratedTables(this))
                return;

            if (!BuqiGeneratedConfigAdapter.TryReadFromTables(
                    this,
                    out BuqiConfigCatalog catalog,
                    out List<string> adapterErrors))
            {
                throw new InvalidOperationException(BuqiText.Format(
                    "Buqi generated table adapter failed:\n{0}",
                    string.Join("\n", adapterErrors)));
            }

            List<string> validationErrors = BuqiConfigValidator.Validate(catalog);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(BuqiText.Format(
                    "Buqi config validation failed:\n{0}",
                    string.Join("\n", validationErrors)));
            }

            BuqiConfig = catalog;
            BuqiItemDefinitions = new BuqiDefinitionProvider(catalog);
        }
    }
}
