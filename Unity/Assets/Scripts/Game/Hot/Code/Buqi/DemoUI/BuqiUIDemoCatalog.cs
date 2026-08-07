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
                error = "不器演示配置不可用。";
                return false;
            }
            if (source.Items.Count < 7 || source.Refinements.Count < 3 || source.Echoes.Count < 1)
            {
                error = "不器演示配置需要至少 7 件装备、3 个改造和 1 个对手。";
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
                    Description = BuqiText.Format("{0} | 冷却 {1}", effect, item.BaseCooldownTicks),
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
            result.PreparationChoices.Add(Choice("prepare-coin", "保留金币", "保持资源，稳定进入商店"));
            result.PreparationChoices.Add(Choice("prepare-shield", "开局护盾", "下场战斗获得额外护盾"));
            result.PreparationChoices.Add(Choice("prepare-scout", "深度侦察", "查看更多对手快照信息"));
            result.EventChoices.Add(Choice("event-coins", "拾取金币", "获得 4 金币"));
            result.EventChoices.Add(Choice("event-item", "拾取装备", "获得一件普通装备"));
            result.EventChoices.Add(Choice("event-life", "恢复生命", "恢复 1 点单局生命"));

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
