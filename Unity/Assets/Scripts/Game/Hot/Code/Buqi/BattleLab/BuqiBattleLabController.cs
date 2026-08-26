using System;
using System.Collections.Generic;

namespace Game.Hot.Buqi.BattleLab
{
    /// <summary>
    /// 战斗实验室的纯 C# 状态边界。失败命令不会发布新视图。
    /// </summary>
    public sealed class BuqiBattleLabController
    {
        private readonly BuqiBattleLabCatalog m_Catalog;
        private readonly BuqiBattleLabBoard m_PlayerBoard;
        private readonly BuqiBattleLabBoard m_CustomEnemyBoard;
        private BuqiBattleLabPhase m_Phase;
        private BuqiBattleLabHeroDefinition m_PlayerHero;
        private BuqiBattleLabOpponentMode m_OpponentMode;
        private string m_SelectedPresetId;
        private BuqiBattleLabHeroDefinition m_CustomEnemyHero;
        private int m_PlayerInstanceCounter;
        private int m_EnemyInstanceCounter;
        private int m_SimulationCount;
        private BuqiBattleLabView m_View;

        private BuqiBattleLabController(BuqiBattleLabCatalog catalog)
        {
            m_Catalog = catalog;
            m_PlayerBoard = new BuqiBattleLabBoard(catalog.BoardSlotCount);
            m_CustomEnemyBoard = new BuqiBattleLabBoard(catalog.BoardSlotCount);
            m_Phase = BuqiBattleLabPhase.HeroSelection;
            m_OpponentMode = BuqiBattleLabOpponentMode.Preset;
            m_SimulationCount = 0;
            m_SelectedPresetId = catalog.PresetOpponents.Count == 0
                ? null
                : catalog.PresetOpponents[0].EchoId;
            RebuildView();
        }

        public BuqiBattleLabView View => m_View;

        public static bool TryCreate(
            BuqiBattleLabCatalog catalog,
            out BuqiBattleLabController controller,
            out string error)
        {
            controller = null;
            error = string.Empty;
            if (catalog == null ||
                catalog.BoardSlotCount < 8 ||
                catalog.BoardSlotCount > 10 ||
                catalog.Heroes == null ||
                catalog.Items == null ||
                catalog.PresetOpponents == null)
            {
                error = "不器战斗实验室目录不可用";
                return false;
            }

            controller = new BuqiBattleLabController(catalog);
            return true;
        }

        public BuqiBattleLabCommandResult SelectPlayerHero(string heroId)
        {
            BuqiBattleLabHeroDefinition hero = FindHero(heroId);
            if (hero == null)
                return Reject("英雄不存在");

            m_PlayerHero = hero;
            return Accept();
        }

        public BuqiBattleLabCommandResult EnterWorkbench()
        {
            if (m_PlayerHero == null)
                return Reject("请先选择我方英雄");

            m_Phase = BuqiBattleLabPhase.Workbench;
            return Accept();
        }

        public BuqiBattleLabCommandResult ReturnToHeroSelection()
        {
            m_Phase = BuqiBattleLabPhase.HeroSelection;
            return Accept();
        }

        public BuqiBattleLabCommandResult SelectOpponentMode(
            BuqiBattleLabOpponentMode mode)
        {
            if (!IsWorkbench)
                return Reject("请先进入工作台");
            if (mode != BuqiBattleLabOpponentMode.Preset &&
                mode != BuqiBattleLabOpponentMode.Custom)
                return Reject("敌人模式无效");

            m_OpponentMode = mode;
            return Accept();
        }

        public BuqiBattleLabCommandResult SelectPresetOpponent(string echoId)
        {
            if (!IsWorkbench)
                return Reject("请先进入工作台");
            BuqiBattleLabPresetOpponent opponent = FindPresetOpponent(echoId);
            if (opponent == null)
                return Reject("预设敌人不存在");

            m_SelectedPresetId = opponent.EchoId;
            return Accept();
        }

        public BuqiBattleLabCommandResult SelectCustomEnemyHero(string heroId)
        {
            if (!IsWorkbench)
                return Reject("请先进入工作台");
            BuqiBattleLabHeroDefinition hero = FindHero(heroId);
            if (hero == null)
                return Reject("英雄不存在");

            m_CustomEnemyHero = hero;
            return Accept();
        }

        public BuqiBattleLabPlacementPreview PreviewLibrary(
            BuqiBattleLabSide side,
            string definitionId,
            int anchorSlot)
        {
            if (!IsWorkbench)
                return RejectedPreview(side, anchorSlot, 0, "请先进入工作台");
            if (!TryGetEditableBoard(side, out BuqiBattleLabBoard board, out string reason))
                return RejectedPreview(side, anchorSlot, 0, reason);

            BuqiBattleLabItemDefinition item = FindItem(definitionId);
            if (item == null)
                return RejectedPreview(side, anchorSlot, 0, "道具不存在");
            if (!item.Enabled)
                return RejectedPreview(side, anchorSlot, item.Size, item.Error);

            return WithSide(
                board.Preview(item.DefinitionId, item.Size, anchorSlot, string.Empty),
                side);
        }

        public BuqiBattleLabCommandResult AddFromLibrary(
            BuqiBattleLabSide side,
            string definitionId,
            int anchorSlot)
        {
            if (!IsWorkbench)
                return Reject("请先进入工作台");
            if (!TryGetEditableBoard(side, out BuqiBattleLabBoard board, out string reason))
                return Reject(reason);

            BuqiBattleLabItemDefinition item = FindItem(definitionId);
            if (item == null)
                return Reject("道具不存在");
            if (!item.Enabled)
                return Reject(item.Error);

            BuqiBattleLabPlacementPreview preview = board.Preview(
                item.DefinitionId,
                item.Size,
                anchorSlot,
                string.Empty);
            if (!preview.Accepted)
                return Reject(preview.Reason);

            string instanceId = FormatNextInstanceId(side);
            var placement = new BuqiBattleLabPlacement(
                instanceId,
                item.DefinitionId,
                item.DisplayName,
                item.Size,
                item.Quality,
                anchorSlot,
                string.Empty);
            if (!board.TryAdd(placement, out reason))
                return Reject(reason);

            IncrementInstanceCounter(side);
            return Accept();
        }

        public BuqiBattleLabPlacementPreview PreviewMove(
            BuqiBattleLabSide side,
            string instanceId,
            int anchorSlot)
        {
            if (!IsWorkbench)
                return RejectedPreview(side, anchorSlot, 0, "请先进入工作台");
            if (!TryGetEditableBoard(side, out BuqiBattleLabBoard board, out string reason))
                return RejectedPreview(side, anchorSlot, 0, reason);

            BuqiBattleLabPlacement placement = FindPlacement(board, instanceId);
            if (placement == null)
                return RejectedPreview(side, anchorSlot, 0, "来源位置没有道具");

            return WithSide(
                board.Preview(
                    placement.DefinitionId,
                    placement.Size,
                    anchorSlot,
                    placement.InstanceId),
                side);
        }

        public BuqiBattleLabCommandResult Move(
            BuqiBattleLabSide sourceSide,
            string instanceId,
            BuqiBattleLabSide targetSide,
            int anchorSlot)
        {
            if (!IsWorkbench)
                return Reject("请先进入工作台");
            if (!IsKnownSide(sourceSide) || !IsKnownSide(targetSide))
                return Reject("棋盘阵营无效");
            if (IsPresetEnemySide(sourceSide) || IsPresetEnemySide(targetSide))
                return Reject("预设敌人不可编辑");
            if (sourceSide != targetSide)
                return Reject("不能转移双方已有实例");

            BuqiBattleLabBoard board = GetBoard(sourceSide);
            if (!board.TryMove(instanceId, anchorSlot, out string reason))
                return Reject(reason);
            return Accept();
        }

        public BuqiBattleLabCommandResult Remove(
            BuqiBattleLabSide side,
            string instanceId)
        {
            if (!IsWorkbench)
                return Reject("请先进入工作台");
            if (!TryGetEditableBoard(side, out BuqiBattleLabBoard board, out string reason))
                return Reject(reason);
            if (!board.TryRemove(instanceId, out reason))
                return Reject(reason);
            return Accept();
        }

        public BuqiBattleLabCommandResult Clear(BuqiBattleLabSide side)
        {
            if (!IsWorkbench)
                return Reject("请先进入工作台");
            if (!TryGetEditableBoard(side, out BuqiBattleLabBoard board, out string reason))
                return Reject(reason);
            if (!board.Clear())
                return Reject("棋盘已经为空");
            return Accept();
        }

        private bool IsWorkbench => m_Phase == BuqiBattleLabPhase.Workbench;

        private BuqiBattleLabHeroDefinition FindHero(string heroId)
        {
            if (string.IsNullOrEmpty(heroId))
                return null;
            for (int index = 0; index < m_Catalog.Heroes.Count; index++)
            {
                BuqiBattleLabHeroDefinition hero = m_Catalog.Heroes[index];
                if (hero != null && string.Equals(
                        hero.HeroId, heroId, StringComparison.Ordinal))
                    return hero;
            }
            return null;
        }

        private BuqiBattleLabItemDefinition FindItem(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId))
                return null;
            for (int index = 0; index < m_Catalog.Items.Count; index++)
            {
                BuqiBattleLabItemDefinition item = m_Catalog.Items[index];
                if (item != null && string.Equals(
                        item.DefinitionId, definitionId, StringComparison.Ordinal))
                    return item;
            }
            return null;
        }

        private BuqiBattleLabPresetOpponent FindPresetOpponent(string echoId)
        {
            if (string.IsNullOrEmpty(echoId))
                return null;
            for (int index = 0; index < m_Catalog.PresetOpponents.Count; index++)
            {
                BuqiBattleLabPresetOpponent opponent = m_Catalog.PresetOpponents[index];
                if (opponent != null && string.Equals(
                        opponent.EchoId, echoId, StringComparison.Ordinal))
                    return opponent;
            }
            return null;
        }

        private bool TryGetEditableBoard(
            BuqiBattleLabSide side,
            out BuqiBattleLabBoard board,
            out string reason)
        {
            board = null;
            if (!IsKnownSide(side))
            {
                reason = "棋盘阵营无效";
                return false;
            }
            if (IsPresetEnemySide(side))
            {
                reason = "预设敌人不可编辑";
                return false;
            }

            board = GetBoard(side);
            reason = string.Empty;
            return true;
        }

        private bool IsPresetEnemySide(BuqiBattleLabSide side)
        {
            return side == BuqiBattleLabSide.Enemy &&
                   m_OpponentMode == BuqiBattleLabOpponentMode.Preset;
        }

        private static bool IsKnownSide(BuqiBattleLabSide side)
        {
            return side == BuqiBattleLabSide.Player ||
                   side == BuqiBattleLabSide.Enemy;
        }

        private BuqiBattleLabBoard GetBoard(BuqiBattleLabSide side)
        {
            return side == BuqiBattleLabSide.Player
                ? m_PlayerBoard
                : m_CustomEnemyBoard;
        }

        private static BuqiBattleLabPlacement FindPlacement(
            BuqiBattleLabBoard board,
            string instanceId)
        {
            IReadOnlyList<BuqiBattleLabPlacement> placements = board.CopyPlacements();
            for (int index = 0; index < placements.Count; index++)
            {
                if (string.Equals(
                        placements[index].InstanceId,
                        instanceId,
                        StringComparison.Ordinal))
                    return placements[index];
            }
            return null;
        }

        private string FormatNextInstanceId(BuqiBattleLabSide side)
        {
            int next = side == BuqiBattleLabSide.Player
                ? m_PlayerInstanceCounter + 1
                : m_EnemyInstanceCounter + 1;
            string sideName = side == BuqiBattleLabSide.Player ? "player" : "enemy";
            return $"lab-{sideName}-{next:D4}";
        }

        private void IncrementInstanceCounter(BuqiBattleLabSide side)
        {
            if (side == BuqiBattleLabSide.Player)
                m_PlayerInstanceCounter++;
            else
                m_EnemyInstanceCounter++;
        }

        private BuqiBattleLabCommandResult Accept()
        {
            RebuildView();
            return new BuqiBattleLabCommandResult(true, string.Empty, m_View);
        }

        private BuqiBattleLabCommandResult Reject(string reason)
        {
            return new BuqiBattleLabCommandResult(false, reason, m_View);
        }

        private void RebuildView()
        {
            m_View = new BuqiBattleLabView(
                m_Phase,
                m_PlayerHero,
                m_OpponentMode,
                m_SelectedPresetId,
                m_CustomEnemyHero,
                m_PlayerBoard.View,
                m_CustomEnemyBoard.View,
                m_SimulationCount);
        }

        private static BuqiBattleLabPlacementPreview WithSide(
            BuqiBattleLabPlacementPreview preview,
            BuqiBattleLabSide side)
        {
            return new BuqiBattleLabPlacementPreview(
                side,
                preview.AnchorSlot,
                preview.Span,
                preview.CoveredSlots,
                preview.Accepted,
                preview.Reason);
        }

        private static BuqiBattleLabPlacementPreview RejectedPreview(
            BuqiBattleLabSide side,
            int anchorSlot,
            int span,
            string reason)
        {
            return new BuqiBattleLabPlacementPreview(
                side,
                anchorSlot,
                span,
                Array.Empty<int>(),
                false,
                reason);
        }
    }
}
