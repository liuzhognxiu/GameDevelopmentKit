using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;
using Game.Hot.Buqi.DemoUI.Deployment;
using Game.Hot.Buqi.Run.Core;
using Game.Hot.Buqi.Run.Battle;
using Game.Hot.Buqi.Run.Economy;
using Game.Hot.Buqi.Run.Encounter;
using Game.Hot.Buqi.Run.Integration;

namespace Game.Hot.Buqi.DemoUI
{
    public sealed class BuqiUIDemoController
    {
        private readonly BuqiUIDemoCatalog m_Catalog;
        private readonly BuqiRunDemoOrchestrator m_Orchestrator;

        private BuqiUIDemoController(BuqiUIDemoCatalog catalog, BuqiRunDemoOrchestrator orchestrator)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            m_Orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            RefreshView();
        }

        public BuqiUIDemoView View { get; private set; }

        public BattleReplayData CurrentReplay => m_Orchestrator.BuildReplayData();

        public static BuqiUIDemoController Create(BuqiUIDemoCatalog catalog)
        {
            if (!TryCreate(catalog, null, out BuqiUIDemoController controller, out string error))
                throw new InvalidOperationException(error);
            return controller;
        }

        public static bool TryCreate(
            BuqiUIDemoCatalog catalog,
            BuqiUIDemoControllerOptions options,
            out BuqiUIDemoController controller,
            out string error)
        {
            return TryCreateCore(catalog, options, false, out controller, out error);
        }

        public static bool TryCreateNewRun(
            BuqiUIDemoCatalog catalog,
            BuqiUIDemoControllerOptions options,
            out BuqiUIDemoController controller,
            out string error)
        {
            return TryCreateCore(catalog, options, true, out controller, out error);
        }

        private static bool TryCreateCore(
            BuqiUIDemoCatalog catalog,
            BuqiUIDemoControllerOptions options,
            bool startNewRun,
            out BuqiUIDemoController controller,
            out string error)
        {
            controller = null;
            if (catalog == null)
            {
                error = "试玩配置不可用。";
                return false;
            }

            try
            {
                var orchestrator = new BuqiRunDemoOrchestrator(catalog, options);
                bool initialized = startNewRun
                    ? orchestrator.Restart(out error)
                    : orchestrator.TryInitialize(out error);
                if (!initialized)
                    return false;
                if (!orchestrator.TrySynchronizeBazaar(out error))
                    return false;

                controller = new BuqiUIDemoController(catalog, orchestrator);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = BuqiPlayerText.Error(exception.Message);
                return false;
            }
        }

        public BuqiUIDemoCommandResult Execute(BuqiUIDemoCommand command)
        {
            if (command == null)
                return Rejected("操作指令不可用。"  );

            switch (command.Type)
            {
                case BuqiUIDemoCommandType.OpenDragDeploy:
                    return CanConfigureDeployment(View.Phase)
                        ? AcceptedWithoutMutation()
                        : Rejected("当前阶段不能调整装备栏。"  );

                case BuqiUIDemoCommandType.PreviousPhase:
                    return Rejected("当前阶段不能返回。"  );

                case BuqiUIDemoCommandType.Restart:
                    if (!m_Orchestrator.Restart(out string restartError))
                        return Rejected(restartError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.BuyOffer:
                    if (!m_Orchestrator.TryPurchase(command.PrimaryId, command.Slot, out string buyError))
                        return Rejected(buyError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.RefreshShop:
                    if (!m_Orchestrator.TryRefreshShop(out string refreshError))
                        return Rejected(refreshError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.SellItem:
                    if (!m_Orchestrator.TrySellBoardItem(command.PrimaryId, out string sellError))
                        return Rejected(sellError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.SelectChoice:
                    if (!m_Orchestrator.TryResolveEvent(command.PrimaryId, out string eventError))
                        return Rejected(eventError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.SelectOperation:
                    if (!m_Orchestrator.TrySelectOperation(command.PrimaryId, out string operationError))
                        return Rejected(operationError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.SelectPveDifficulty:
                    if (!m_Orchestrator.TrySelectPveDifficulty(command.PrimaryId, out string pveError))
                        return Rejected(pveError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.SelectTribulationRoute:
                    if (!TryParseTribulationRoute(command.PrimaryId, out BuqiTribulationRoute route))
                        return Rejected("最终挑战路线无效。"  );
                    if (!m_Orchestrator.TrySelectTribulationRoute(route, Math.Max(0, command.Slot), out string routeError))
                        return Rejected(routeError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.ResolveTribulationStage:
                    if (!m_Orchestrator.TryResolveCurrentTribulationStage(out string stageError))
                        return Rejected(stageError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.ApplyDeployment:
                    if (!m_Orchestrator.TryApplyDeployment(command.Deployment, out string deploymentError))
                        return Rejected(deploymentError);
                    RefreshView();
                    return Accepted();

                case BuqiUIDemoCommandType.NextPhase:
                    if (!m_Orchestrator.TryAdvance(out string advanceError))
                        return Rejected(advanceError);
                    RefreshView();
                    return Accepted();

                default:
                    return Rejected("暂不支持此操作。"  );
            }
        }

        public static bool CanConfigureDeployment(BuqiUIDemoPhase phase)
        {
            return phase == BuqiUIDemoPhase.OperationChoice
                || phase == BuqiUIDemoPhase.Shop
                || phase == BuqiUIDemoPhase.Event
                || phase == BuqiUIDemoPhase.PveSelection
                || phase == BuqiUIDemoPhase.TribulationRoute
                || phase == BuqiUIDemoPhase.TribulationStage;
        }

        private void RefreshView()
        {
            BuqiRunDemoState state = m_Orchestrator.State;
            m_Catalog.SetRuntimeItemDefinitions(
                state.Economy.Items.Values.Select(item =>
                    new KeyValuePair<string, string>(item.InstanceId, item.DefinitionId)));
            BuqiUIDemoPhase phase = ResolvePhase(state);
            BuqiUIDemoView view = new BuqiUIDemoView
            {
                Phase = phase,
                Period = state.Economy.Run.Period,
                Coins = state.Economy.Run.Coins,
                Wins = state.Economy.Run.Wins,
                Lives = state.Economy.Run.Lives,
                Round = state.Economy.Run.Day,
                DaoSeals = state.Economy.Run.DaoSeals,
                TribulationOmen = state.Economy.Run.CurrentOmen,
                TribulationStage = state.Economy.Run.TribulationStage,
                ContextTitle = BuildTitle(state, phase),
                ContextBody = BuildBody(state, phase),
                PrimaryCommandLabel = BuildPrimaryLabel(phase),
                SecondaryCommandLabel = string.Empty,
                VisitedPhases = new List<BuqiUIDemoPhase>(state.VisitedPhases),
                BoardSlots = BuildBoardSlots(state.Economy),
                StorageSlots = BuildStorageSlots(state.Economy),
                Choices = BuildChoices(state, phase),
                ShopOffers = BuildOffers(state, phase),
                Opponent = BuildOpponent(state),
                Facts = BuildFacts(state),
            };

            View = view;
        }

        private IReadOnlyList<BuqiDemoItemView> BuildBoardSlots(BuqiRunEconomySnapshot economy)
        {
            var result = new BuqiDemoItemView[economy.Run.BoardInstanceIds.Count];
            for (int slot = 0; slot < economy.Run.BoardInstanceIds.Count; slot++)
            {
                if (result[slot] != null)
                    continue;

                string instanceId = economy.Run.BoardInstanceIds[slot];
                if (string.IsNullOrEmpty(instanceId) ||
                    !economy.Items.TryGetValue(instanceId, out BuqiRunItemInstance item))
                {
                    result[slot] = new BuqiDemoItemView
                    {
                        Empty = true,
                        Slot = slot,
                    };
                    continue;
                }

                BuqiUIDemoItemDefinition definition = m_Catalog.FindItem(item.DefinitionId);
                int size = definition?.Size ?? 1;
                BuqiRunSellQuote sellQuote = m_EconomyService.QuoteBoardSale(economy, instanceId);
                int sellPrice = sellQuote.Success ? sellQuote.ExpectedRefund : 0;
                for (int offset = 0; offset < size && slot + offset < result.Length; offset++)
                {
                    result[slot + offset] = new BuqiDemoItemView
                    {
                        Id = instanceId,
                        Name = definition?.Name ?? "未命名装备",
                        Description = BuildItemDescription(item, definition),
                        Size = size,
                        Price = definition?.Price ?? 0,
                        SellPrice = sellPrice,
                        CooldownTicks = definition?.CooldownTicks ?? 0,
                        EffectDescription = definition?.EffectDescription ?? string.Empty,
                        Quality = item.Quality.ToString(),
                        ArchetypeId = definition?.ArchetypeId ?? string.Empty,
                        Role = definition?.Role ?? string.Empty,
                        PositionHint = definition?.PositionHint ?? string.Empty,
                        UpgradeSummary = definition?.UpgradeSummary ?? string.Empty,
                        Tags = definition?.Tags == null
                            ? new List<string>()
                            : new List<string>(definition.Tags),
                        AnchorSlot = slot,
                        Slot = slot + offset,
                    };
                }
            }

            for (int slot = 0; slot < result.Length; slot++)
            {
                result[slot] ??= new BuqiDemoItemView
                {
                    Empty = true,
                    Slot = slot,
                };
            }

            return result;
        }

        private IReadOnlyList<BuqiDemoItemView> BuildStorageSlots(BuqiRunEconomySnapshot economy)
        {
            var result = new List<BuqiDemoItemView>(economy.Run.StorageInstanceIds.Count);
            for (int slot = 0; slot < economy.Run.StorageInstanceIds.Count; slot++)
            {
                string instanceId = economy.Run.StorageInstanceIds[slot];
                if (string.IsNullOrEmpty(instanceId) ||
                    !economy.Items.TryGetValue(instanceId, out BuqiRunItemInstance item))
                {
                    result.Add(new BuqiDemoItemView
                    {
                        Empty = true,
                        Slot = slot,
                    });
                    continue;
                }

                BuqiUIDemoItemDefinition definition = m_Catalog.FindItem(item.DefinitionId);
                result.Add(new BuqiDemoItemView
                {
                    Id = instanceId,
                    Name = definition?.Name ?? "未命名装备",
                    Description = BuildItemDescription(item, definition),
                    Size = definition?.Size ?? 1,
                    Price = definition?.Price ?? 0,
                    Slot = slot,
                });
            }

            return result;
        }

        private IReadOnlyList<BuqiDemoChoiceView> BuildChoices(BuqiRunDemoState state, BuqiUIDemoPhase phase)
        {
            if (phase == BuqiUIDemoPhase.OperationChoice)
            {
                return new[]
                {
                    new BuqiDemoChoiceView { Id = "bazaar", Title = "商店", Description = "查看并购买装备，也可直接离开。" },
                    new BuqiDemoChoiceView { Id = "event", Title = "事件", Description = "从三项事件中选择一个结果。" },
                    new BuqiDemoChoiceView { Id = "meditate", Title = "训练", Description = "保留当前装备栏并进入下一阶段。" },
                };
            }

            if (phase == BuqiUIDemoPhase.PveSelection && state.PveSelection != null)
            {
                return state.PveSelection.Cards.Select(card => new BuqiDemoChoiceView
                {
                    Id = card.ChoiceId,
                    Title = DifficultyTitle(card.Difficulty),
                    Description = BuqiText.Format(
                        "威胁 {0} · 装备 {1} · 胜利进度 +{2}",
                        card.Threat.Rank,
                        card.Threat.EquippedItemCount,
                        card.Reward.VictoryProgress),
                }).ToList();
            }

            if (phase == BuqiUIDemoPhase.TribulationRoute)
            {
                return new[]
                {
                    new BuqiDemoChoiceView { Id = "face-thunder", Title = "直接挑战 · 迎雷", Description = "不消耗资源，按当前难度进入挑战。" },
                    new BuqiDemoChoiceView { Id = "shatter-artifact", Title = "装备挑战 · 化劫", Description = "利用当前装备降低挑战压力。" },
                    new BuqiDemoChoiceView { Id = "question-heart", Title = "资源调整 · 问心", Description = "消耗结算点数降低当前挑战强度。" },
                };
            }

            if (phase == BuqiUIDemoPhase.TribulationStage)
            {
                return new[]
                {
                    new BuqiDemoChoiceView { Id = "resolve", Title = "开始挑战 · 应劫", Description = $"推进第 {state.Economy.Run.TribulationStage}/3 阶挑战。" },
                };
            }

            if (phase != BuqiUIDemoPhase.Event || state.Encounter == null)
                return Array.Empty<BuqiDemoChoiceView>();

            var result = new List<BuqiDemoChoiceView>();
            foreach (string eventId in state.Encounter.CandidateIds)
            {
                BuqiDemoChoiceView source = m_Catalog.EventChoices.FirstOrDefault(choice =>
                    string.Equals(choice.Id, eventId, StringComparison.Ordinal));
                result.Add(new BuqiDemoChoiceView
                {
                    Id = eventId,
                    Title = BuqiPlayerText.Sanitize(source?.Title, "事件选项"),
                    Description = BuqiPlayerText.Sanitize(source?.Description, "选择后立即结算。"),
                });
            }

            return result;
        }

        private IReadOnlyList<BuqiDemoOfferView> BuildOffers(BuqiRunDemoState state, BuqiUIDemoPhase phase)
        {
            if (phase != BuqiUIDemoPhase.Shop || state.Encounter == null)
                return Array.Empty<BuqiDemoOfferView>();

            var result = new List<BuqiDemoOfferView>();
            foreach (string definitionId in state.Encounter.CandidateIds)
            {
                BuqiUIDemoItemDefinition definition = m_Catalog.FindItem(definitionId);
                if (definition == null)
                    continue;

                result.Add(new BuqiDemoOfferView
                {
                    Id = definitionId,
                    Item = BuqiUIDemoCatalog.ItemView(definition),
                    Price = definition.Price,
                    Span = definition.Size,
                    Sold = state.Encounter.PurchasedCandidateIds.Contains(definitionId),
                });
            }

            return result;
        }

        private BuqiDemoOpponentView BuildOpponent(BuqiRunDemoState state)
        {
            if (state.Battle == null || state.Battle.Request?.Right == null)
                return null;

            var items = new List<BuqiDemoItemView>();
            foreach (ItemInstance item in state.Battle.Request.Right.Items.OrderBy(value => value.AnchorSlot))
            {
                BuqiUIDemoItemDefinition definition = m_Catalog.FindItem(item.DefinitionId);
                items.Add(new BuqiDemoItemView
                {
                    Id = item.InstanceId,
                    Name = definition?.Name ?? "未命名装备",
                    Description = BuildBattleItemDescription(item, definition),
                    Size = definition?.Size ?? 1,
                    Slot = item.AnchorSlot,
                });
            }

            return new BuqiDemoOpponentView
            {
                Id = state.Battle.OpponentId,
                Name = BuqiPlayerText.Sanitize(state.Battle.Replay?.RightName, "未知对手"),
                Build = "公开装备栏",
                Items = items,
            };
        }

        private IReadOnlyList<BuqiDemoFactView> BuildFacts(BuqiRunDemoState state)
        {
            if (state.BattleSummary == null || state.BattleSummary.FactLines == null)
                return Array.Empty<BuqiDemoFactView>();

            var facts = new List<BuqiDemoFactView>();
            for (int index = 0; index < state.BattleSummary.FactLines.Count; index++)
            {
                facts.Add(new BuqiDemoFactView
                {
                    Title = index == 0 ? OutcomeTitle(state) : BuqiPlayerText.Format("战斗记录 {0}", index + 1),
                    Body = BuqiPlayerText.Sanitize(state.BattleSummary.FactLines[index], "战斗记录已生成。"),
                    Tick = index,
                });
            }

            if (facts.Count == 0 && state.Battle != null)
            {
                facts.Add(new BuqiDemoFactView
                {
                    Title = OutcomeTitle(state),
                    Body = BuqiBattleText.Termination(state.Battle.Result?.TerminationReason),
                    Tick = 0,
                });
            }

            return facts;
        }

        private static string OutcomeTitle(BuqiRunDemoState state)
        {
            if (state.LastRawOutcome == BuqiRunRawBattleOutcome.Draw)
                return "平局";
            return state.LastRawOutcome == BuqiRunRawBattleOutcome.OpponentWin ? "失败" : "胜利";
        }

        private static BuqiUIDemoPhase ResolvePhase(BuqiRunDemoState state)
        {
            switch (state.Presentation)
            {
                case BuqiRunDemoPresentation.OperationChoice:
                    return BuqiUIDemoPhase.OperationChoice;
                case BuqiRunDemoPresentation.Encounter:
                    return state.Encounter != null && state.Encounter.Kind == BuqiRunEncounterKind.Event
                        ? BuqiUIDemoPhase.Event
                        : BuqiUIDemoPhase.Shop;
                case BuqiRunDemoPresentation.BattleReplay:
                    return BuqiUIDemoPhase.BattleReplay;
                case BuqiRunDemoPresentation.BattleSummary:
                    return BuqiUIDemoPhase.BattleSummary;
                case BuqiRunDemoPresentation.PveSelection:
                    return BuqiUIDemoPhase.PveSelection;
                case BuqiRunDemoPresentation.DaySettlement:
                    return BuqiUIDemoPhase.RoundSettlement;
                case BuqiRunDemoPresentation.TribulationRoute:
                    return BuqiUIDemoPhase.TribulationRoute;
                case BuqiRunDemoPresentation.TribulationStage:
                    return BuqiUIDemoPhase.TribulationStage;
                case BuqiRunDemoPresentation.RunTerminal:
                default:
                    return BuqiUIDemoPhase.RunTerminal;
            }
        }

        private static string BuildTitle(BuqiRunDemoState state, BuqiUIDemoPhase phase)
        {
            switch (phase)
            {
                case BuqiUIDemoPhase.OperationChoice:
                    string period = state.Economy.Run.Period == BuqiRunPeriod.MorningOperation ? "晨" : "昼";
                    return BuqiPlayerText.Format("第 {0} 日 · {1} · 经营", state.Economy.Run.Day, period);
                case BuqiUIDemoPhase.PveSelection:
                    return $"第 {state.Economy.Run.Day} 日 · 昏 · 电脑对战选择";
                case BuqiUIDemoPhase.TribulationRoute:
                    return "最终挑战路线 · 九日试炼";
                case BuqiUIDemoPhase.TribulationStage:
                    return $"最终挑战 · 天劫 {state.Economy.Run.TribulationStage}/3";
                case BuqiUIDemoPhase.Shop:
                case BuqiUIDemoPhase.Event:
                    return $"第 {state.Economy.Run.Day} 日 · 经营 {state.Economy.Run.EncounterIndex + 1}/{BuqiRunRules.EncountersPerDay}";
                case BuqiUIDemoPhase.BattleReplay:
                    return state.Economy.Run.Phase == BuqiRunPhase.PveBattle ? "电脑对战" : "异步对战";
                case BuqiUIDemoPhase.BattleSummary:
                    string summaryKind = state.Battle == null
                        ? "战斗"
                        : state.Battle.Kind == Game.Hot.Buqi.Run.Core.BuqiRunBattleKind.Pve ? "电脑对战" : "异步对战";
                    return state.LastRawOutcome == BuqiRunRawBattleOutcome.Draw
                        ? $"{summaryKind}总结 · 平局按玩家胜利结算"
                        : $"{summaryKind}总结";
                case BuqiUIDemoPhase.RoundSettlement:
                    return $"第 {state.Economy.Run.Day} 日结算";
                case BuqiUIDemoPhase.RunTerminal:
                    return state.Economy.Run.Outcome == BuqiRunOutcome.Victory ? "本局胜利" : "本局失败";
                default:
                    return string.Empty;
            }
        }

        private static string BuildBody(BuqiRunDemoState state, BuqiUIDemoPhase phase)
        {
            switch (phase)
            {
                case BuqiUIDemoPhase.OperationChoice:
                    return "选择本阶段的经营行动；当前装备栏保持可见。";
                case BuqiUIDemoPhase.PveSelection:
                    return "选择初级、中级或高难挑战；点击后直接进入战斗，当前装备栏只读。";
                case BuqiUIDemoPhase.TribulationRoute:
                    return $"九日试炼已完成。当前结算点数 {state.Economy.Run.DaoSeals}，挑战强度 {state.Economy.Run.CurrentOmen}。";
                case BuqiUIDemoPhase.TribulationStage:
                    return "确认后开始当前阶段挑战。";
                case BuqiUIDemoPhase.Shop:
                    return "点击固定商品预览；将商品拖到棋盘合法位置完成购买，或直接离开商店。";
                case BuqiUIDemoPhase.Event:
                    return "选择一个事件结果，选择后立即结算。";
                case BuqiUIDemoPhase.BattleReplay:
                    return state.Economy.Run.Phase == BuqiRunPhase.PveBattle
                        ? "左侧为玩家装备栏，右侧为电脑对手。"
                        : "左侧为玩家装备栏，右侧为异步对手记录。";
                case BuqiUIDemoPhase.BattleSummary:
                    return state.Battle == null || state.Battle.Kind == Game.Hot.Buqi.Run.Core.BuqiRunBattleKind.Pve
                        ? (state.LastRawOutcome == BuqiRunRawBattleOutcome.Draw
                            ? "电脑对战结果为平局，本局按玩家胜利结算。"
                            : "电脑对战回放与战斗记录总结。")
                        : (state.LastRawOutcome == BuqiRunRawBattleOutcome.Draw
                            ? "异步对战结果为平局，本局按玩家胜利结算。"
                            : "异步对战回放与战斗记录总结。");
                case BuqiUIDemoPhase.RoundSettlement:
                    return BuqiText.Format(
                        "第 {0} 日结束：金币 {1}，胜场 {2}/{3}，生命 {4}/{5}。继续进入下一日。",
                        state.Economy.Run.Day,
                        state.Economy.Run.Coins,
                        state.Economy.Run.Wins,
                        BuqiRunRules.WinsToVictory,
                        state.Economy.Run.Lives,
                        BuqiRunRules.StartingLives);
                case BuqiUIDemoPhase.RunTerminal:
                    return state.Economy.Run.Outcome == BuqiRunOutcome.Victory
                        ? "已达到目标胜场。"
                        : "本局生命已归零。";
                default:
                    return string.Empty;
            }
        }

        private static string BuildPrimaryLabel(BuqiUIDemoPhase phase)
        {
            switch (phase)
            {
                case BuqiUIDemoPhase.OperationChoice:
                case BuqiUIDemoPhase.PveSelection:
                case BuqiUIDemoPhase.TribulationRoute:
                case BuqiUIDemoPhase.TribulationStage:
                    return string.Empty;
                case BuqiUIDemoPhase.Shop:
                    return "离开商店";
                case BuqiUIDemoPhase.Event:
                    return string.Empty;
                case BuqiUIDemoPhase.RunTerminal:
                    return "重新开始";
                default:
                    return "继续";
            }
        }

        private static string DifficultyTitle(BuqiPveDifficulty difficulty)
        {
            return difficulty switch
            {
                BuqiPveDifficulty.Initial => "初阶",
                BuqiPveDifficulty.Intermediate => "进阶",
                _ => "险阶",
            };
        }

        private static bool TryParseTribulationRoute(string id, out BuqiTribulationRoute route)
        {
            switch (id)
            {
                case "face-thunder": route = BuqiTribulationRoute.FaceThunder; return true;
                case "shatter-artifact": route = BuqiTribulationRoute.ShatterArtifact; return true;
                case "question-heart": route = BuqiTribulationRoute.QuestionHeart; return true;
                default: route = BuqiTribulationRoute.None; return false;
            }
        }

        private string BuildItemDescription(BuqiRunItemInstance item, BuqiUIDemoItemDefinition definition)
        {
            string quality = QualityName(item.Quality);
            string refinement = RefinementName(item.RefinementId);
            string baseText = definition == null ? "装备说明不可用" : definition.Description;
            return BuqiPlayerText.Format("{0} | {1} | 改造：{2}", baseText, quality, refinement);
        }

        private static string BuildBattleItemDescription(ItemInstance item, BuqiUIDemoItemDefinition definition)
        {
            string baseText = definition == null ? "装备说明不可用" : definition.Description;
            return BuqiPlayerText.Format(
                "{0} | {1}",
                baseText,
                BuqiBattleText.Quality((Game.Hot.Buqi.Battle.BuqiQuality)item.Quality));
        }

        private BuqiUIDemoCommandResult Accepted()
        {
            return new BuqiUIDemoCommandResult { Accepted = true, View = View };
        }

        private BuqiUIDemoCommandResult AcceptedWithoutMutation()
        {
            return new BuqiUIDemoCommandResult { Accepted = true, View = View };
        }

        private BuqiUIDemoCommandResult Rejected(string reason)
        {
            return new BuqiUIDemoCommandResult { Accepted = false, Reason = BuqiPlayerText.Error(reason), View = View };
        }

        private static string QualityName(BuqiRunItemQuality quality)
        {
            switch (quality)
            {
                case BuqiRunItemQuality.Common: return "普通";
                case BuqiRunItemQuality.Improved: return "强化";
                case BuqiRunItemQuality.Finalized: return "高级";
                default: return "未知品质";
            }
        }

        private string RefinementName(string refinementId)
        {
            if (string.IsNullOrEmpty(refinementId))
                return "无";
            BuqiRefinementConfigRow row = m_Catalog.SourceCatalog?.Refinements?
                .FirstOrDefault(value => string.Equals(value.RefinementId, refinementId, StringComparison.Ordinal));
            return BuqiPlayerText.Sanitize(row?.DisplayName, "未知改造");
        }
    }
}
