using System;
using System.Collections.Generic;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.DemoUI
{
    public sealed class BuqiUIDemoItemDefinition
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public int Size;
        public int Price;
    }

    public sealed class BuqiUIDemoCatalog
    {
        public List<BuqiUIDemoItemDefinition> Items = new List<BuqiUIDemoItemDefinition>();
        public List<BuqiDemoChoiceView> StarterChoices = new List<BuqiDemoChoiceView>();
        public List<BuqiDemoChoiceView> PreparationChoices = new List<BuqiDemoChoiceView>();
        public List<BuqiDemoChoiceView> EventChoices = new List<BuqiDemoChoiceView>();
        public List<BuqiDemoChoiceView> Modifications = new List<BuqiDemoChoiceView>();
        public List<BuqiDemoOfferView> ShopOffers = new List<BuqiDemoOfferView>();
        public BuqiDemoOpponentView Opponent = new BuqiDemoOpponentView();

        public static bool TryCreate(BuqiConfigCatalog source, out BuqiUIDemoCatalog catalog, out string error)
        {
            catalog = null;
            error = string.Empty;
            if (source == null || source.Items == null || source.Refinements == null || source.Echoes == null)
            {
                error = "Buqi demo config is unavailable.";
                return false;
            }
            if (source.Items.Count < 7 || source.Refinements.Count < 3 || source.Echoes.Count < 1)
            {
                error = "Buqi demo config requires 7 items, 3 modifications, and 1 opponent.";
                return false;
            }

            var result = new BuqiUIDemoCatalog();
            var items = new List<BuqiItemConfigRow>(source.Items);
            items.Sort((left, right) => string.Compare(left.DefinitionId, right.DefinitionId, StringComparison.Ordinal));
            foreach (BuqiItemConfigRow item in items)
            {
                string effect = item.Effects.Count > 0 ? item.Effects[0].Effect.ToString() : "--";
                result.Items.Add(new BuqiUIDemoItemDefinition
                {
                    Id = item.DefinitionId,
                    Name = string.IsNullOrEmpty(item.DisplayName) ? item.DefinitionId : item.DisplayName,
                    Description = BuqiText.Format("{0} | \u51B7\u5374 {1}", effect, item.BaseCooldownTicks),
                    Size = (int)item.Size,
                    Price = item.BasePrice > 0 ? item.BasePrice : (int)item.Size + 1,
                });
            }

            for (int index = 0; index < 3; index++)
            {
                BuqiUIDemoItemDefinition item = result.Items[index];
                result.StarterChoices.Add(new BuqiDemoChoiceView
                {
                    Id = item.Id,
                    Title = item.Name,
                    Description = item.Description,
                });
            }
            result.PreparationChoices.Add(Choice("prepare-coin", "\u4FDD\u7559\u91D1\u5E01", "\u4FDD\u6301\u8D44\u6E90\uFF0C\u7A33\u5B9A\u8FDB\u5165\u5546\u5E97"));
            result.PreparationChoices.Add(Choice("prepare-shield", "\u5F00\u5C40\u62A4\u76FE", "\u4E0B\u573A\u6218\u6597\u83B7\u5F97\u989D\u5916\u62A4\u76FE"));
            result.PreparationChoices.Add(Choice("prepare-scout", "\u6DF1\u5EA6\u4FA6\u5BDF", "\u67E5\u770B\u66F4\u591A\u5BF9\u624B\u5FEB\u7167\u4FE1\u606F"));
            result.EventChoices.Add(Choice("event-coins", "\u62FE\u53D6\u91D1\u5E01", "\u83B7\u5F97 4 \u91D1\u5E01"));
            result.EventChoices.Add(Choice("event-item", "\u62FE\u53D6\u88C5\u5907", "\u83B7\u5F97\u4E00\u4EF6\u666E\u901A\u88C5\u5907"));
            result.EventChoices.Add(Choice("event-life", "\u6062\u590D\u751F\u547D", "\u6062\u590D 1 \u70B9\u5355\u5C40\u751F\u547D"));

            var refinements = new List<BuqiRefinementConfigRow>(source.Refinements);
            refinements.Sort((left, right) => string.Compare(left.RefinementId, right.RefinementId, StringComparison.Ordinal));
            for (int index = 0; index < 3; index++)
            {
                BuqiRefinementConfigRow row = refinements[index];
                result.Modifications.Add(Choice(row.RefinementId, row.DisplayName, row.Summary));
            }

            for (int index = 0; index < 4; index++)
            {
                BuqiUIDemoItemDefinition item = result.Items[index + 3];
                result.ShopOffers.Add(new BuqiDemoOfferView
                {
                    Id = BuqiText.Format("offer-{0}", index + 1),
                    Item = ItemView(item),
                    Price = item.Price,
                });
            }

            var echoes = new List<BuqiEchoConfigRow>(source.Echoes);
            echoes.Sort((left, right) => string.Compare(left.EchoId, right.EchoId, StringComparison.Ordinal));
            BuqiEchoConfigRow echo = echoes[0];
            var opponentItems = new List<BuqiDemoItemView>();
            if (echo.Snapshot != null)
            {
                foreach (BuqiItemInstanceConfigRow instance in echo.Snapshot.Items)
                {
                    BuqiUIDemoItemDefinition item = result.FindItem(instance.DefinitionId);
                    if (item != null)
                    {
                        BuqiDemoItemView view = ItemView(item);
                        view.Slot = instance.AnchorSlot;
                        opponentItems.Add(view);
                    }
                }
            }
            result.Opponent = new BuqiDemoOpponentView
            {
                Id = echo.EchoId,
                Name = string.IsNullOrEmpty(echo.DisplayName) ? echo.EchoId : echo.DisplayName,
                Build = echo.Build,
                Items = opponentItems,
            };

            catalog = result;
            return true;
        }

        public BuqiUIDemoItemDefinition FindItem(string id)
        {
            return Items.Find(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        }

        internal static BuqiDemoItemView ItemView(BuqiUIDemoItemDefinition item)
        {
            return new BuqiDemoItemView
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Size = item.Size,
                Price = item.Price,
            };
        }

        private static BuqiDemoChoiceView Choice(string id, string title, string description)
        {
            return new BuqiDemoChoiceView { Id = id, Title = title, Description = description };
        }
    }
}
