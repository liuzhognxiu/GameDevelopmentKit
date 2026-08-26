using System;
using System.Collections.Generic;
using System.Linq;
using Game.Hot.Buqi.Battle;
using Game.Hot.Buqi.Config;

namespace Game.Hot.Buqi.BattleLab
{
    /// <summary>
    /// 不器战斗实验室的跨端行为契约。Unity EditMode 与 .NET 无头端共用同一组检查。
    /// </summary>
    public static class BuqiBattleLabContractChecks
    {
        public static List<string> RunAll()
        {
            var failures = new List<string>();
            RunCheck("目录投影", CheckCatalogProjection, failures);
            RunCheck("棋盘尺寸", CheckBoardSlotRange, failures);
            RunCheck("原子棋盘", CheckAtomicBoards, failures);
            RunCheck("状态控制器", CheckStateController, failures);
            RunCheck("工作台阶段守卫", CheckWorkbenchPhaseInvariant, failures);
            RunCheck("控制器成功路由", CheckControllerSuccessfulRouting, failures);
            RunCheck("无效内容", CheckInvalidContentProjection, failures);
            RunCheck("畸形效果", CheckMalformedEffectProjection, failures);
            RunCheck("只读模型", CheckModelDefensiveCopies, failures);
            return failures;
        }

        private static void CheckWorkbenchPhaseInvariant(List<string> failures)
        {
            BuqiBattleLabController controller = CreateController(8, failures);
            if (controller == null)
                return;

            BuqiBattleLabView initialView = controller.View;
            ExpectRejected(
                controller,
                controller.SelectOpponentMode(BuqiBattleLabOpponentMode.Custom),
                "请先进入工作台",
                "英雄选择阶段切换敌人模式",
                failures);
            ExpectRejected(
                controller,
                controller.SelectPresetOpponent("echo-balanced"),
                "请先进入工作台",
                "英雄选择阶段选择预设敌人",
                failures);
            ExpectRejected(
                controller,
                controller.SelectCustomEnemyHero("guarded"),
                "请先进入工作台",
                "英雄选择阶段选择自定义敌方英雄",
                failures);

            foreach (BuqiBattleLabSide side in new[]
                     {
                         BuqiBattleLabSide.Player,
                         BuqiBattleLabSide.Enemy,
                     })
            {
                BuqiBattleLabPlacementPreview libraryPreview = controller.PreviewLibrary(
                    side, "small", 0);
                BuqiBattleLabPlacementPreview movePreview = controller.PreviewMove(
                    side, "missing", 0);
                Expect(
                    !libraryPreview.Accepted &&
                    libraryPreview.Side == side &&
                    libraryPreview.Reason == "请先进入工作台" &&
                    !movePreview.Accepted &&
                    movePreview.Side == side &&
                    movePreview.Reason == "请先进入工作台",
                    BuqiText.Format("工作台阶段守卫：{0}预览未被拒绝", side),
                    failures);
                ExpectRejected(
                    controller,
                    controller.AddFromLibrary(side, "small", 0),
                    "请先进入工作台",
                    BuqiText.Format("英雄选择阶段添加 {0}", side),
                    failures);
                ExpectRejected(
                    controller,
                    controller.Move(side, "missing", side, 0),
                    "请先进入工作台",
                    BuqiText.Format("英雄选择阶段移动 {0}", side),
                    failures);
                ExpectRejected(
                    controller,
                    controller.Remove(side, "missing"),
                    "请先进入工作台",
                    BuqiText.Format("英雄选择阶段移除 {0}", side),
                    failures);
                ExpectRejected(
                    controller,
                    controller.Clear(side),
                    "请先进入工作台",
                    BuqiText.Format("英雄选择阶段清空 {0}", side),
                    failures);
            }

            Expect(
                ReferenceEquals(initialView, controller.View),
                "工作台阶段守卫：拒绝命令发布了新视图",
                failures);

            ExpectAcceptedPublished(
                controller,
                () => controller.SelectPlayerHero("balanced"),
                "阶段守卫选择我方英雄",
                failures);
            ExpectAcceptedPublished(
                controller,
                controller.EnterWorkbench,
                "阶段守卫进入工作台",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectOpponentMode(BuqiBattleLabOpponentMode.Custom),
                "阶段守卫进入后切换自定义",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectPresetOpponent("echo-balanced"),
                "阶段守卫进入后选择预设",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectCustomEnemyHero("guarded"),
                "阶段守卫进入后选择敌方英雄",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.AddFromLibrary(BuqiBattleLabSide.Player, "small", 0),
                "阶段守卫进入后添加我方道具",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.AddFromLibrary(BuqiBattleLabSide.Enemy, "small", 0),
                "阶段守卫进入后添加敌方道具",
                failures);

            BuqiBattleLabHeroDefinition playerHero = controller.View.PlayerHero;
            BuqiBattleLabHeroDefinition enemyHero = controller.View.CustomEnemyHero;
            BuqiBattleLabBoardView playerBoard = controller.View.PlayerBoard;
            BuqiBattleLabBoardView enemyBoard = controller.View.CustomEnemyBoard;
            string presetId = controller.View.SelectedPresetId;
            BuqiBattleLabOpponentMode mode = controller.View.OpponentMode;
            ExpectAcceptedPublished(
                controller,
                controller.ReturnToHeroSelection,
                "阶段守卫返回英雄选择",
                failures);
            Expect(
                controller.View.Phase == BuqiBattleLabPhase.HeroSelection &&
                ReferenceEquals(controller.View.PlayerHero, playerHero) &&
                ReferenceEquals(controller.View.CustomEnemyHero, enemyHero) &&
                ReferenceEquals(controller.View.PlayerBoard, playerBoard) &&
                ReferenceEquals(controller.View.CustomEnemyBoard, enemyBoard) &&
                controller.View.SelectedPresetId == presetId &&
                controller.View.OpponentMode == mode,
                "工作台阶段守卫：返回英雄选择未保留工作台状态",
                failures);
            BuqiBattleLabView returnedView = controller.View;
            ExpectRejected(
                controller,
                controller.AddFromLibrary(BuqiBattleLabSide.Player, "small", 1),
                "请先进入工作台",
                "返回英雄选择后添加",
                failures);
            Expect(
                ReferenceEquals(returnedView, controller.View),
                "工作台阶段守卫：返回后拒绝命令发布了新视图",
                failures);
        }

        private static void CheckControllerSuccessfulRouting(List<string> failures)
        {
            foreach (int slotCount in new[] { 8, 10 })
                CheckControllerSuccessfulRouting(slotCount, failures);
        }

        private static void CheckControllerSuccessfulRouting(
            int slotCount,
            List<string> failures)
        {
            BuqiBattleLabController controller = CreateController(slotCount, failures);
            if (controller == null)
                return;

            ExpectAcceptedPublished(
                controller,
                () => controller.SelectPlayerHero("balanced"),
                BuqiText.Format("{0} 格选择我方英雄", slotCount),
                failures);
            ExpectAcceptedPublished(
                controller,
                controller.EnterWorkbench,
                BuqiText.Format("{0} 格进入工作台", slotCount),
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectOpponentMode(BuqiBattleLabOpponentMode.Custom),
                BuqiText.Format("{0} 格切换自定义敌人", slotCount),
                failures);

            foreach (BuqiBattleLabSide side in new[]
                     {
                         BuqiBattleLabSide.Player,
                         BuqiBattleLabSide.Enemy,
                     })
            {
                ExpectAcceptedPublished(
                    controller,
                    () => controller.AddFromLibrary(side, "small", 0),
                    BuqiText.Format("{0} 格 {1} 添加 1 格道具", slotCount, side),
                    failures);
                ExpectAcceptedPublished(
                    controller,
                    () => controller.AddFromLibrary(side, "m-middle", 2),
                    BuqiText.Format("{0} 格 {1} 添加 2 格道具", slotCount, side),
                    failures);
                ExpectAcceptedPublished(
                    controller,
                    () => controller.AddFromLibrary(side, "a-first", 5),
                    BuqiText.Format("{0} 格 {1} 添加 3 格道具", slotCount, side),
                    failures);
            }

            ExpectRoutedSizes(controller.View.PlayerBoard, "lab-player", slotCount, failures);
            ExpectRoutedSizes(controller.View.CustomEnemyBoard, "lab-enemy", slotCount, failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.Move(
                    BuqiBattleLabSide.Player,
                    "lab-player-0001",
                    BuqiBattleLabSide.Player,
                    1),
                BuqiText.Format("{0} 格我方同侧移动", slotCount),
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.Move(
                    BuqiBattleLabSide.Enemy,
                    "lab-enemy-0001",
                    BuqiBattleLabSide.Enemy,
                    1),
                BuqiText.Format("{0} 格敌方同侧移动", slotCount),
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.Remove(BuqiBattleLabSide.Player, "lab-player-0002"),
                BuqiText.Format("{0} 格移除我方道具", slotCount),
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.Remove(BuqiBattleLabSide.Enemy, "lab-enemy-0002"),
                BuqiText.Format("{0} 格移除敌方道具", slotCount),
                failures);

            BuqiBattleLabBoardView enemyBeforePlayerClear = controller.View.CustomEnemyBoard;
            ExpectAcceptedPublished(
                controller,
                () => controller.Clear(BuqiBattleLabSide.Player),
                BuqiText.Format("{0} 格清空我方棋盘", slotCount),
                failures);
            Expect(
                controller.View.PlayerBoard.Placements.Count == 0 &&
                ReferenceEquals(controller.View.CustomEnemyBoard, enemyBeforePlayerClear),
                BuqiText.Format("控制器成功路由：{0} 格清空我方影响敌方", slotCount),
                failures);
            BuqiBattleLabBoardView playerBeforeEnemyClear = controller.View.PlayerBoard;
            ExpectAcceptedPublished(
                controller,
                () => controller.Clear(BuqiBattleLabSide.Enemy),
                BuqiText.Format("{0} 格清空敌方棋盘", slotCount),
                failures);
            Expect(
                controller.View.CustomEnemyBoard.Placements.Count == 0 &&
                ReferenceEquals(controller.View.PlayerBoard, playerBeforeEnemyClear),
                BuqiText.Format("控制器成功路由：{0} 格清空敌方影响我方", slotCount),
                failures);

            ExpectAcceptedPublished(
                controller,
                () => controller.SelectOpponentMode(BuqiBattleLabOpponentMode.Preset),
                BuqiText.Format("{0} 格切回预设敌人", slotCount),
                failures);
            BuqiBattleLabView beforeInvalidSide = controller.View;
            ExpectRejected(
                controller,
                controller.Move(
                    (BuqiBattleLabSide)999,
                    "missing",
                    BuqiBattleLabSide.Enemy,
                    0),
                "棋盘阵营无效",
                BuqiText.Format("{0} 格非法来源 side", slotCount),
                failures);
            ExpectRejected(
                controller,
                controller.Move(
                    BuqiBattleLabSide.Enemy,
                    "missing",
                    (BuqiBattleLabSide)999,
                    0),
                "棋盘阵营无效",
                BuqiText.Format("{0} 格非法目标 side", slotCount),
                failures);
            Expect(
                ReferenceEquals(beforeInvalidSide, controller.View),
                BuqiText.Format("控制器成功路由：{0} 格非法 side 发布了新视图", slotCount),
                failures);
        }

        private static BuqiBattleLabController CreateController(
            int slotCount,
            List<string> failures)
        {
            BuqiConfigCatalog source = CreateSource(slotCount);
            source.Items.Add(Item("small", "小型", BuqiSize.S, 30));
            string controllerError = string.Empty;
            if (!BuqiBattleLabCatalog.TryCreate(
                    source, out BuqiBattleLabCatalog catalog, out string catalogError) ||
                !BuqiBattleLabController.TryCreate(
                    catalog, out BuqiBattleLabController controller, out controllerError))
            {
                failures.Add(BuqiText.Format(
                    "控制器 fixture：{0} 格创建失败：{1}{2}",
                    slotCount,
                    catalogError,
                    controllerError));
                return null;
            }
            return controller;
        }

        private static void ExpectRoutedSizes(
            BuqiBattleLabBoardView board,
            string instancePrefix,
            int slotCount,
            List<string> failures)
        {
            Expect(
                board.Placements.Count == 3 &&
                board.Placements[0].Size == 1 &&
                board.Placements[1].Size == 2 &&
                board.Placements[2].Size == 3 &&
                board.OccupiedInstanceIds[0] == instancePrefix + "-0001" &&
                board.OccupiedInstanceIds[1] == null &&
                board.OccupiedInstanceIds[2] == instancePrefix + "-0002" &&
                board.OccupiedInstanceIds[3] == instancePrefix + "-0002" &&
                board.OccupiedInstanceIds[4] == null &&
                board.OccupiedInstanceIds[5] == instancePrefix + "-0003" &&
                board.OccupiedInstanceIds[6] == instancePrefix + "-0003" &&
                board.OccupiedInstanceIds[7] == instancePrefix + "-0003" &&
                board.OccupiedInstanceIds.Count == slotCount,
                BuqiText.Format(
                    "控制器成功路由：{0} 格 {1} 的 1/2/3 格占用错误",
                    slotCount,
                    instancePrefix),
                failures);
        }

        private static void ExpectAcceptedPublished(
            BuqiBattleLabController controller,
            Func<BuqiBattleLabCommandResult> command,
            string label,
            List<string> failures)
        {
            BuqiBattleLabView priorView = controller.View;
            BuqiBattleLabCommandResult result = command();
            Expect(
                result.Accepted &&
                string.IsNullOrEmpty(result.Reason) &&
                ReferenceEquals(result.View, controller.View) &&
                !ReferenceEquals(priorView, controller.View),
                BuqiText.Format(
                    "控制器成功路由：{0}未正确发布视图：{1}",
                    label,
                    result.Reason),
                failures);
        }

        private static void CheckStateController(List<string> failures)
        {
            Expect(
                !BuqiBattleLabController.TryCreate(
                    null, out BuqiBattleLabController missingController, out string createError) &&
                missingController == null &&
                createError == "不器战斗实验室目录不可用",
                "状态控制器：空目录未被安全拒绝",
                failures);

            BuqiConfigCatalog source = CreateSource(8);
            source.Items.Add(Item("small", "小型", BuqiSize.S, 30));
            string controllerError = string.Empty;
            if (!BuqiBattleLabCatalog.TryCreate(
                    source, out BuqiBattleLabCatalog catalog, out string catalogError) ||
                !BuqiBattleLabController.TryCreate(
                    catalog, out BuqiBattleLabController controller, out controllerError))
            {
                failures.Add(BuqiText.Format(
                    "状态控制器：创建失败：{0}{1}", catalogError, controllerError));
                return;
            }

            Expect(
                !typeof(BuqiBattleLabController).GetProperty(
                    nameof(BuqiBattleLabController.View)).CanWrite,
                "状态控制器：公开视图属性不应提供 setter",
                failures);
            Expect(
                controller.View.Phase == BuqiBattleLabPhase.HeroSelection &&
                controller.View.OpponentMode == BuqiBattleLabOpponentMode.Preset &&
                controller.View.PlayerHero == null &&
                controller.View.CustomEnemyHero == null &&
                controller.View.SelectedPresetId == "echo-balanced",
                "状态控制器：初始状态错误",
                failures);

            BuqiBattleLabView initialView = controller.View;
            ExpectRejected(
                controller,
                controller.EnterWorkbench(),
                "请先选择我方英雄",
                "未选英雄进入工作台",
                failures);
            Expect(
                ReferenceEquals(initialView, controller.View),
                "状态控制器：拒绝进入工作台发布了新视图",
                failures);
            BuqiBattleLabView beforeHeroSelection = controller.View;
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectPlayerHero("balanced"),
                "选择我方英雄",
                failures);
            BuqiBattleLabHeroDefinition balanced = controller.View.PlayerHero;
            Expect(
                balanced != null && balanced.HeroId == "balanced",
                "状态控制器：我方英雄选择未发布",
                failures);
            Expect(
                !ReferenceEquals(beforeHeroSelection, controller.View),
                "状态控制器：成功选择英雄未重建视图",
                failures);
            ExpectAcceptedPublished(
                controller,
                controller.EnterWorkbench,
                "进入工作台",
                failures);
            Expect(
                controller.View.Phase == BuqiBattleLabPhase.Workbench &&
                ReferenceEquals(controller.View.PlayerHero, balanced),
                "状态控制器：进入工作台未保留我方英雄",
                failures);
            ExpectAcceptedPublished(
                controller,
                controller.ReturnToHeroSelection,
                "返回英雄选择",
                failures);
            Expect(
                controller.View.Phase == BuqiBattleLabPhase.HeroSelection &&
                ReferenceEquals(controller.View.PlayerHero, balanced),
                "状态控制器：返回英雄选择未保留我方英雄",
                failures);
            ExpectAcceptedPublished(
                controller,
                controller.EnterWorkbench,
                "再次进入工作台",
                failures);

            ExpectAcceptedPublished(
                controller,
                () => controller.AddFromLibrary(BuqiBattleLabSide.Player, "small", 0),
                "添加我方第一件道具",
                failures);
            ExpectPlacement(
                controller.View.PlayerBoard, "lab-player-0001", "small", 0,
                "我方第一件道具", failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.AddFromLibrary(BuqiBattleLabSide.Player, "small", 1),
                "添加我方第二件道具",
                failures);
            ExpectPlacement(
                controller.View.PlayerBoard, "lab-player-0002", "small", 1,
                "我方第二件道具", failures);

            BuqiBattleLabView beforeIllegalAdd = controller.View;
            ExpectRejected(
                controller,
                controller.AddFromLibrary(BuqiBattleLabSide.Player, "small", 1),
                "与小型重叠",
                "非法第三次添加",
                failures);
            Expect(
                ReferenceEquals(beforeIllegalAdd, controller.View),
                "状态控制器：非法添加发布了新视图",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.AddFromLibrary(BuqiBattleLabSide.Player, "small", 2),
                "非法添加后的下一次合法添加",
                failures);
            ExpectPlacement(
                controller.View.PlayerBoard, "lab-player-0003", "small", 2,
                "我方第三件道具", failures);

            BuqiBattleLabView beforePresetAdd = controller.View;
            ExpectRejected(
                controller,
                controller.AddFromLibrary(BuqiBattleLabSide.Enemy, "small", 0),
                "预设敌人不可编辑",
                "预设敌方添加",
                failures);
            Expect(
                ReferenceEquals(beforePresetAdd, controller.View),
                "状态控制器：预设敌方添加发布了新视图",
                failures);
            ExpectPresetEnemyEditingRejected(controller, failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectOpponentMode(BuqiBattleLabOpponentMode.Custom),
                "切换自定义敌人",
                failures);
            BuqiBattleLabPlacementPreview enemyPreview = controller.PreviewLibrary(
                BuqiBattleLabSide.Enemy, "small", 0);
            Expect(
                enemyPreview.Accepted && enemyPreview.Side == BuqiBattleLabSide.Enemy,
                "状态控制器：敌方预览未重包为真实 side",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.AddFromLibrary(BuqiBattleLabSide.Enemy, "small", 0),
                "添加敌方第一件道具",
                failures);
            ExpectPlacement(
                controller.View.CustomEnemyBoard, "lab-enemy-0001", "small", 0,
                "敌方第一件道具", failures);
            BuqiBattleLabPlacementPreview enemyMovePreview = controller.PreviewMove(
                BuqiBattleLabSide.Enemy, "lab-enemy-0001", 1);
            Expect(
                enemyMovePreview.Accepted &&
                enemyMovePreview.Side == BuqiBattleLabSide.Enemy,
                "状态控制器：敌方移动预览未重包为真实 side",
                failures);
            BuqiBattleLabView beforeCrossMove = controller.View;
            ExpectRejected(
                controller,
                controller.Move(
                    BuqiBattleLabSide.Player,
                    "lab-player-0001",
                    BuqiBattleLabSide.Enemy,
                    2),
                "不能转移双方已有实例",
                "跨双方移动",
                failures);
            Expect(
                ReferenceEquals(beforeCrossMove, controller.View),
                "状态控制器：跨双方移动发布了新视图",
                failures);

            BuqiBattleLabView beforeUnknown = controller.View;
            ExpectRejected(
                controller,
                controller.SelectPlayerHero("missing-hero"),
                "英雄不存在",
                "未知我方英雄",
                failures);
            ExpectRejected(
                controller,
                controller.SelectCustomEnemyHero("missing-hero"),
                "英雄不存在",
                "未知敌方英雄",
                failures);
            ExpectRejected(
                controller,
                controller.SelectPresetOpponent("missing-preset"),
                "预设敌人不存在",
                "未知预设",
                failures);
            ExpectRejected(
                controller,
                controller.AddFromLibrary(BuqiBattleLabSide.Player, "missing-item", 3),
                "道具不存在",
                "未知道具",
                failures);
            Expect(
                ReferenceEquals(beforeUnknown, controller.View),
                "状态控制器：未知内容改写了视图",
                failures);

            CheckControllerStatePreservation(catalog, failures);
        }

        private static void CheckControllerStatePreservation(
            BuqiBattleLabCatalog catalog,
            List<string> failures)
        {
            if (!BuqiBattleLabController.TryCreate(
                    catalog, out BuqiBattleLabController controller, out string error))
            {
                failures.Add(BuqiText.Format("状态控制器：状态保留控制器创建失败：{0}", error));
                return;
            }

            ExpectAcceptedPublished(
                controller,
                () => controller.SelectPlayerHero("balanced"),
                "状态保留选择我方英雄并准备进入",
                failures);
            ExpectAcceptedPublished(
                controller,
                controller.EnterWorkbench,
                "状态保留进入工作台",
                failures);

            ExpectAcceptedPublished(
                controller,
                () => controller.SelectPresetOpponent("echo-balanced"),
                "状态保留选择预设 A",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectOpponentMode(BuqiBattleLabOpponentMode.Custom),
                "状态保留切换自定义敌人",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectCustomEnemyHero("guarded"),
                "状态保留选择 guarded",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.AddFromLibrary(BuqiBattleLabSide.Enemy, "small", 0),
                "状态保留添加敌方道具",
                failures);

            string savedPresetId = controller.View.SelectedPresetId;
            BuqiBattleLabHeroDefinition savedEnemyHero = controller.View.CustomEnemyHero;
            BuqiBattleLabBoardView savedEnemyBoard = controller.View.CustomEnemyBoard;
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectOpponentMode(BuqiBattleLabOpponentMode.Preset),
                "状态保留切回预设敌人",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectOpponentMode(BuqiBattleLabOpponentMode.Custom),
                "状态保留再次切回自定义敌人",
                failures);
            Expect(
                controller.View.SelectedPresetId == savedPresetId &&
                ReferenceEquals(controller.View.CustomEnemyHero, savedEnemyHero) &&
                ReferenceEquals(controller.View.CustomEnemyBoard, savedEnemyBoard),
                "状态控制器：敌人模式切换未保留预设 ID、自定义英雄和棋盘",
                failures);

            ExpectAcceptedPublished(
                controller,
                () => controller.SelectPlayerHero("balanced"),
                "状态保留选择我方英雄",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.AddFromLibrary(BuqiBattleLabSide.Player, "small", 0),
                "状态保留添加我方道具",
                failures);
            BuqiBattleLabBoardView savedPlayerBoard = controller.View.PlayerBoard;
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectPlayerHero("survivor"),
                "状态保留更换我方英雄",
                failures);
            Expect(
                controller.View.PlayerHero.HeroId == "survivor" &&
                ReferenceEquals(controller.View.PlayerBoard, savedPlayerBoard),
                "状态控制器：更换我方英雄清空了我方棋盘",
                failures);
            ExpectAcceptedPublished(
                controller,
                () => controller.SelectCustomEnemyHero("balanced"),
                "状态保留更换自定义敌方英雄",
                failures);
            Expect(
                controller.View.CustomEnemyHero.HeroId == "balanced" &&
                ReferenceEquals(controller.View.CustomEnemyBoard, savedEnemyBoard),
                "状态控制器：更换敌方英雄清空了敌方棋盘",
                failures);
        }

        private static void ExpectPresetEnemyEditingRejected(
            BuqiBattleLabController controller,
            List<string> failures)
        {
            BuqiBattleLabView expectedView = controller.View;
            BuqiBattleLabPlacementPreview libraryPreview = controller.PreviewLibrary(
                BuqiBattleLabSide.Enemy, "small", 0);
            BuqiBattleLabPlacementPreview movePreview = controller.PreviewMove(
                BuqiBattleLabSide.Enemy, "missing", 0);
            Expect(
                !libraryPreview.Accepted &&
                libraryPreview.Side == BuqiBattleLabSide.Enemy &&
                libraryPreview.Reason == "预设敌人不可编辑" &&
                !movePreview.Accepted &&
                movePreview.Side == BuqiBattleLabSide.Enemy &&
                movePreview.Reason == "预设敌人不可编辑",
                "状态控制器：预设敌方预览没有给出具体禁用原因",
                failures);
            ExpectRejected(
                controller,
                controller.Move(
                    BuqiBattleLabSide.Enemy,
                    "missing",
                    BuqiBattleLabSide.Enemy,
                    0),
                "预设敌人不可编辑",
                "预设敌方移动",
                failures);
            ExpectRejected(
                controller,
                controller.Remove(BuqiBattleLabSide.Enemy, "missing"),
                "预设敌人不可编辑",
                "预设敌方移除",
                failures);
            ExpectRejected(
                controller,
                controller.Clear(BuqiBattleLabSide.Enemy),
                "预设敌人不可编辑",
                "预设敌方清空",
                failures);
            Expect(
                ReferenceEquals(expectedView, controller.View),
                "状态控制器：预设敌方编辑拒绝改写了视图",
                failures);
        }

        private static void ExpectRejected(
            BuqiBattleLabController controller,
            BuqiBattleLabCommandResult result,
            string reason,
            string label,
            List<string> failures)
        {
            Expect(
                !result.Accepted && result.Reason == reason &&
                ReferenceEquals(result.View, controller.View),
                BuqiText.Format(
                    "状态控制器：{0}拒绝错误：{1}", label, result.Reason),
                failures);
        }

        private static void ExpectPlacement(
            BuqiBattleLabBoardView board,
            string instanceId,
            string definitionId,
            int anchorSlot,
            string label,
            List<string> failures)
        {
            Expect(
                board.Placements.Any(placement =>
                    placement.InstanceId == instanceId &&
                    placement.DefinitionId == definitionId &&
                    placement.AnchorSlot == anchorSlot),
                BuqiText.Format("状态控制器：{0}实例错误", label),
                failures);
        }

        private static void CheckCatalogProjection(List<string> failures)
        {
            BuqiConfigCatalog source = CreateSource(8);
            if (!BuqiBattleLabCatalog.TryCreate(
                    source, out BuqiBattleLabCatalog catalog, out string error))
            {
                failures.Add(BuqiText.Format("目录投影：创建失败：{0}", error));
                return;
            }

            Expect(catalog.BoardSlotCount == 8, "目录投影：棋盘格数不是 8", failures);
            Expect(
                catalog.Heroes.Select(hero => hero.HeroId).SequenceEqual(
                    new[] { "balanced", "guarded", "survivor" }, StringComparer.Ordinal),
                "目录投影：英雄顺序不稳定",
                failures);
            ExpectHero(catalog.Heroes[0], "归衡者", 100, 0, 0, "归衡者", failures);
            ExpectHero(catalog.Heroes[1], "铁衣客", 85, 20, 0, "铁衣客", failures);
            ExpectHero(catalog.Heroes[2], "长生客", 115, 0, 4, "长生客", failures);

            string[] itemIds = catalog.Items.Select(item => item.DefinitionId).ToArray();
            string[] sortedItemIds = itemIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Expect(
                itemIds.SequenceEqual(sortedItemIds, StringComparer.Ordinal),
                "目录投影：道具未按 DefinitionId 序号排序",
                failures);
            Expect(
                catalog.Items.All(item => item.Quality == BuqiQuality.Normal),
                "目录投影：卡库道具不是普通品质",
                failures);

            BuqiBattleLabPresetOpponent opponent = catalog.PresetOpponents.Single();
            Expect(
                !ReferenceEquals(opponent.Snapshot, source.Echoes[0].Snapshot),
                "目录投影：预设快照仍引用配置对象",
                failures);
            source.Echoes[0].Snapshot.Items[0].InstanceId = "mutated-source";
            Expect(
                opponent.Snapshot.Items[0].InstanceId == "echo-item",
                "目录投影：修改配置快照污染了预设快照",
                failures);
        }

        private static void CheckBoardSlotRange(List<string> failures)
        {
            Expect(
                BuqiBattleLabCatalog.TryCreate(CreateSource(10), out _, out string tenSlotError),
                BuqiText.Format("棋盘尺寸：10 格创建失败：{0}", tenSlotError),
                failures);

            bool accepted = BuqiBattleLabCatalog.TryCreate(
                CreateSource(7), out _, out string sevenSlotError);
            Expect(!accepted, "棋盘尺寸：7 格被错误接受", failures);
            Expect(
                sevenSlotError == "战斗实验室棋盘只支持 8 至 10 格",
                BuqiText.Format("棋盘尺寸：7 格错误不精确：{0}", sevenSlotError),
                failures);
        }

        private static void CheckAtomicBoards(List<string> failures)
        {
            Expect(
                !typeof(BuqiBattleLabBoard).GetProperty(
                    nameof(BuqiBattleLabBoard.View)).CanWrite,
                "原子棋盘：公开视图属性不应提供 setter",
                failures);
            CheckBoardConstructorRange(failures);

            foreach (int slotCount in new[] { 8, 10 })
            {
                var board = new BuqiBattleLabBoard(slotCount);
                var small = new BuqiBattleLabPlacement(
                    "p-1", "small", "小型", 1,
                    BuqiQuality.Normal, 0, string.Empty);
                var medium = new BuqiBattleLabPlacement(
                    "p-2", "medium", "中型", 2,
                    BuqiQuality.Normal, slotCount - 2, "medium-note");

                Expect(
                    board.View.SlotCount == slotCount &&
                    board.View.OccupiedInstanceIds.Count == slotCount,
                    BuqiText.Format("原子棋盘：{0} 格视图尺寸错误", slotCount),
                    failures);
                Expect(
                    board.TryAdd(small, out string reason),
                    BuqiText.Format("原子棋盘：{0} 格添加小型道具失败：{1}", slotCount, reason),
                    failures);
                Expect(
                    board.TryAdd(medium, out reason),
                    BuqiText.Format("原子棋盘：{0} 格添加中型道具失败：{1}", slotCount, reason),
                    failures);

                BuqiBattleLabBoardView beforeLargePreview = board.View;
                IReadOnlyList<BuqiBattleLabPlacement> beforeLargeSequence =
                    board.CopyPlacements();
                BuqiBattleLabPlacementPreview largePreview = board.Preview(
                    "large", 3, slotCount - 2, string.Empty);
                Expect(
                    !largePreview.Accepted && largePreview.Reason == "需要连续 3 格",
                    BuqiText.Format(
                        "原子棋盘：{0} 格大型道具越界错误不精确：{1}",
                        slotCount,
                        largePreview.Reason),
                    failures);
                Expect(
                    largePreview.CoveredSlots.SequenceEqual(
                        new[] { slotCount - 2, slotCount - 1 }),
                    BuqiText.Format("原子棋盘：{0} 格大型道具预览范围错误", slotCount),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    beforeLargePreview,
                    beforeLargeSequence,
                    BuqiText.Format("原子棋盘：{0} 格失败预览改写了棋盘", slotCount),
                    failures);

                BuqiBattleLabPlacementPreview negativePreview = board.Preview(
                    "small", 2, -1, string.Empty);
                Expect(
                    !negativePreview.Accepted &&
                    negativePreview.Reason == "目标位置无效" &&
                    negativePreview.CoveredSlots.SequenceEqual(new[] { 0 }),
                    BuqiText.Format("原子棋盘：{0} 格负锚点预览未正确裁剪", slotCount),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    beforeLargePreview,
                    beforeLargeSequence,
                    BuqiText.Format("原子棋盘：{0} 格负锚点预览改写了棋盘", slotCount),
                    failures);

                var overlap = new BuqiBattleLabPlacement(
                    "p-overlap", "overlap", "重叠", 1,
                    BuqiQuality.Normal, 0, string.Empty);
                BuqiBattleLabBoardView beforeOverlap = board.View;
                IReadOnlyList<BuqiBattleLabPlacement> beforeOverlapSequence =
                    board.CopyPlacements();
                Expect(
                    !board.TryAdd(overlap, out reason) && reason == "与小型重叠",
                    BuqiText.Format("原子棋盘：{0} 格重叠错误不精确：{1}", slotCount, reason),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    beforeOverlap,
                    beforeOverlapSequence,
                    BuqiText.Format("原子棋盘：{0} 格失败重叠改写了棋盘", slotCount),
                    failures);

                var duplicate = new BuqiBattleLabPlacement(
                    "p-1", "duplicate", "重复", 1,
                    BuqiQuality.Normal, 3, string.Empty);
                Expect(
                    !board.TryAdd(duplicate, out reason) && reason == "同一实例不能重复放置",
                    BuqiText.Format("原子棋盘：{0} 格重复实例错误不精确：{1}", slotCount, reason),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    beforeOverlap,
                    beforeOverlapSequence,
                    BuqiText.Format("原子棋盘：{0} 格重复实例改写了棋盘", slotCount),
                    failures);

                CheckRejectedIdentities(slotCount, failures);

                Expect(
                    !board.TryMove("p-2", 0, out reason) && reason == "与小型重叠",
                    BuqiText.Format("原子棋盘：{0} 格重叠移动错误不精确：{1}", slotCount, reason),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    beforeOverlap,
                    beforeOverlapSequence,
                    BuqiText.Format("原子棋盘：{0} 格重叠移动改写了棋盘", slotCount),
                    failures);

                Expect(
                    !board.TryMove("p-2", slotCount - 1, out reason) &&
                    reason == "需要连续 2 格",
                    BuqiText.Format("原子棋盘：{0} 格越界移动错误不精确：{1}", slotCount, reason),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    beforeOverlap,
                    beforeOverlapSequence,
                    BuqiText.Format("原子棋盘：{0} 格越界移动改写了棋盘", slotCount),
                    failures);

                Expect(
                    !board.TryMove("missing", 1, out reason) && reason == "来源位置没有道具",
                    BuqiText.Format("原子棋盘：{0} 格未知来源错误不精确：{1}", slotCount, reason),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    beforeOverlap,
                    beforeOverlapSequence,
                    BuqiText.Format("原子棋盘：{0} 格未知来源移动改写了棋盘", slotCount),
                    failures);

                Expect(
                    !board.TryRemove("missing", out reason) && reason == "来源位置没有道具",
                    BuqiText.Format("原子棋盘：{0} 格未知移除错误不精确：{1}", slotCount, reason),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    beforeOverlap,
                    beforeOverlapSequence,
                    BuqiText.Format("原子棋盘：{0} 格未知移除改写了棋盘", slotCount),
                    failures);

                BuqiBattleLabBoardView beforeMove = board.View;
                Expect(
                    board.TryMove("p-2", 1, out reason),
                    BuqiText.Format("原子棋盘：{0} 格移动中型道具失败：{1}", slotCount, reason),
                    failures);
                BuqiBattleLabPlacement moved = board.View.Placements.Single(
                    placement => placement.InstanceId == "p-2");
                Expect(
                    !ReferenceEquals(beforeMove, board.View) &&
                    moved.DefinitionId == medium.DefinitionId &&
                    moved.DisplayName == medium.DisplayName &&
                    moved.Size == medium.Size &&
                    moved.Quality == medium.Quality &&
                    moved.AnchorSlot == 1 &&
                    moved.AnnotationId == medium.AnnotationId &&
                    HasOccupiedSlots(
                        board.View,
                        new[] { "p-1", "p-2", "p-2" }),
                    BuqiText.Format("原子棋盘：{0} 格成功移动未正确发布视图", slotCount),
                    failures);

                BuqiBattleLabBoardView beforeRemove = board.View;
                Expect(
                    board.TryRemove("p-1", out reason),
                    BuqiText.Format("原子棋盘：{0} 格移除小型道具失败：{1}", slotCount, reason),
                    failures);
                Expect(
                    !ReferenceEquals(beforeRemove, board.View) &&
                    board.View.Placements.Count == 1 &&
                    board.View.Placements[0].InstanceId == "p-2" &&
                    HasOccupiedSlots(
                        board.View,
                        new[] { null, "p-2", "p-2" }),
                    BuqiText.Format("原子棋盘：{0} 格成功移除未正确发布视图", slotCount),
                    failures);

                BuqiBattleLabBoardView beforeClear = board.View;
                Expect(
                    board.Clear() &&
                    !ReferenceEquals(beforeClear, board.View) &&
                    board.View.Placements.Count == 0 &&
                    board.View.OccupiedInstanceIds.All(instanceId => instanceId == null),
                    BuqiText.Format("原子棋盘：{0} 格清空失败", slotCount),
                    failures);

                BuqiBattleLabBoardView emptyView = board.View;
                IReadOnlyList<BuqiBattleLabPlacement> emptySequence =
                    board.CopyPlacements();
                Expect(
                    !board.Clear(),
                    BuqiText.Format("原子棋盘：{0} 格空棋盘被重复清空", slotCount),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    emptyView,
                    emptySequence,
                    BuqiText.Format("原子棋盘：{0} 格空棋盘清空发布了新视图", slotCount),
                    failures);
            }
        }

        private static void CheckBoardConstructorRange(List<string> failures)
        {
            foreach (int slotCount in new[] { -1, 7, 11 })
            {
                try
                {
                    _ = new BuqiBattleLabBoard(slotCount);
                    failures.Add(BuqiText.Format(
                        "原子棋盘：非法棋盘尺寸 {0} 未抛出 ArgumentOutOfRangeException",
                        slotCount));
                }
                catch (ArgumentOutOfRangeException)
                {
                }
                catch (Exception exception)
                {
                    failures.Add(BuqiText.Format(
                        "原子棋盘：非法棋盘尺寸 {0} 抛出 {1}",
                        slotCount,
                        exception.GetType().Name));
                }
            }

            var nineSlotBoard = new BuqiBattleLabBoard(9);
            Expect(
                nineSlotBoard.View.SlotCount == 9 &&
                nineSlotBoard.View.OccupiedInstanceIds.Count == 9,
                "原子棋盘：合法 9 格棋盘创建失败",
                failures);
        }

        private static void CheckRejectedIdentities(
            int slotCount,
            List<string> failures)
        {
            var cases = new[]
            {
                new
                {
                    Placement = new BuqiBattleLabPlacement(
                        null, "valid", "空实例", 1,
                        BuqiQuality.Normal, 0, string.Empty),
                    Reason = "实例标识不可用",
                    Label = "null 实例标识",
                },
                new
                {
                    Placement = new BuqiBattleLabPlacement(
                        string.Empty, "valid", "空实例", 1,
                        BuqiQuality.Normal, 0, string.Empty),
                    Reason = "实例标识不可用",
                    Label = "empty 实例标识",
                },
                new
                {
                    Placement = new BuqiBattleLabPlacement(
                        "valid", null, "空定义", 1,
                        BuqiQuality.Normal, 0, string.Empty),
                    Reason = "道具定义不可用",
                    Label = "null 道具定义",
                },
                new
                {
                    Placement = new BuqiBattleLabPlacement(
                        "valid", string.Empty, "空定义", 1,
                        BuqiQuality.Normal, 0, string.Empty),
                    Reason = "道具定义不可用",
                    Label = "empty 道具定义",
                },
                new
                {
                    Placement = new BuqiBattleLabPlacement(
                        null, string.Empty, "混合非法", 0,
                        BuqiQuality.Normal, -1, string.Empty),
                    Reason = "实例标识不可用",
                    Label = "混合非法实例",
                },
                new
                {
                    Placement = new BuqiBattleLabPlacement(
                        "valid", null, "混合非法", 0,
                        BuqiQuality.Normal, -1, string.Empty),
                    Reason = "道具定义不可用",
                    Label = "混合非法定义",
                },
            };

            foreach (var testCase in cases)
            {
                var board = new BuqiBattleLabBoard(slotCount);
                BuqiBattleLabBoardView beforeView = board.View;
                IReadOnlyList<BuqiBattleLabPlacement> beforeSequence =
                    board.CopyPlacements();
                bool accepted = board.TryAdd(testCase.Placement, out string reason);
                Expect(
                    !accepted && reason == testCase.Reason,
                    BuqiText.Format(
                        "原子棋盘：{0} 格{1}错误不精确：{2}",
                        slotCount,
                        testCase.Label,
                        reason),
                    failures);
                ExpectBoardUnchanged(
                    board,
                    beforeView,
                    beforeSequence,
                    BuqiText.Format(
                        "原子棋盘：{0} 格{1}改写了棋盘",
                        slotCount,
                        testCase.Label),
                    failures);
            }
        }

        private static bool HasOccupiedSlots(
            BuqiBattleLabBoardView view,
            IReadOnlyList<string> expectedPrefix)
        {
            if (view.OccupiedInstanceIds.Count != view.SlotCount)
                return false;

            for (int index = 0; index < view.SlotCount; index++)
            {
                string expected = index < expectedPrefix.Count
                    ? expectedPrefix[index]
                    : null;
                if (!string.Equals(
                        view.OccupiedInstanceIds[index],
                        expected,
                        StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static void ExpectBoardUnchanged(
            BuqiBattleLabBoard board,
            BuqiBattleLabBoardView expectedView,
            IReadOnlyList<BuqiBattleLabPlacement> expectedSequence,
            string failure,
            List<string> failures)
        {
            Expect(
                ReferenceEquals(expectedView, board.View) &&
                SamePlacementSequence(expectedSequence, board.CopyPlacements()),
                failure,
                failures);
        }

        private static bool SamePlacementSequence(
            IReadOnlyList<BuqiBattleLabPlacement> left,
            IReadOnlyList<BuqiBattleLabPlacement> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int index = 0; index < left.Count; index++)
            {
                if (!ReferenceEquals(left[index], right[index]))
                    return false;
            }
            return true;
        }

        private static void CheckInvalidContentProjection(List<string> failures)
        {
            bool accepted = BuqiBattleLabCatalog.TryCreate(null, out _, out string unavailableError);
            Expect(!accepted, "无效内容：空配置被错误接受", failures);
            Expect(
                unavailableError == "不器战斗实验室配置不可用",
                BuqiText.Format("无效内容：空配置错误不精确：{0}", unavailableError),
                failures);

            BuqiConfigCatalog source = CreateSource(8);
            source.Items[0].Size = (BuqiSize)4;
            source.Echoes[0].Snapshot.Items.Clear();
            if (!BuqiBattleLabCatalog.TryCreate(
                    source, out BuqiBattleLabCatalog catalog, out string error))
            {
                failures.Add(BuqiText.Format("无效内容：目录不应丢弃无效行：{0}", error));
                return;
            }

            BuqiBattleLabItemDefinition invalidItem = catalog.Items.Single(
                item => item.DefinitionId == "z-last");
            Expect(!invalidItem.Enabled, "无效内容：非法尺寸道具仍被启用", failures);
            Expect(
                invalidItem.Error == "道具尺寸必须为 1 至 3 格",
                BuqiText.Format("无效内容：非法尺寸错误不精确：{0}", invalidItem.Error),
                failures);
            Expect(
                catalog.PresetOpponents.Single().ValidationErrors.Count > 0,
                "无效内容：预设快照校验错误未被保留",
                failures);
        }

        private static void CheckModelDefensiveCopies(List<string> failures)
        {
            var tags = new List<string> { "before" };
            var itemDefinition = new BuqiBattleLabItemDefinition(
                "copy-item", "复制契约", "复制契约", 1, BuqiQuality.Normal, 30,
                "copy", "copy", "copy", tags, true, string.Empty);
            tags[0] = "after";
            Expect(
                itemDefinition.Tags[0] == "before",
                "只读模型：道具标签仍引用构造参数",
                failures);

            var snapshot = new BuildSnapshot
            {
                SnapshotId = "copy-snapshot",
                ContentVersion = "copy-v1",
                InitialExecution = 100,
                Items = new List<ItemInstance>
                {
                    new ItemInstance
                    {
                        InstanceId = "copy-instance",
                        DefinitionId = "copy-item",
                        Quality = (int)BuqiQuality.Normal,
                    },
                },
            };
            var validationErrors = new List<string> { "before" };
            var opponent = new BuqiBattleLabPresetOpponent(
                "copy-opponent", "复制对手", "copy", snapshot, validationErrors);
            snapshot.Items[0].InstanceId = "after";
            validationErrors[0] = "after";
            Expect(
                opponent.Snapshot.Items[0].InstanceId == "copy-instance" &&
                opponent.ValidationErrors[0] == "before",
                "只读模型：预设对手仍引用构造参数",
                failures);
            BuildSnapshot exposedSnapshot = opponent.Snapshot;
            exposedSnapshot.Items[0].InstanceId = "mutated-view";
            Expect(
                opponent.Snapshot.Items[0].InstanceId == "copy-instance",
                "只读模型：调用方可改写预设对手快照",
                failures);

            var placements = new List<BuqiBattleLabPlacement>
            {
                new BuqiBattleLabPlacement(
                    "copy-placement", "copy-item", "复制契约", 1,
                    BuqiQuality.Normal, 0, string.Empty),
            };
            var occupiedInstanceIds = new List<string> { "copy-placement" };
            var board = new BuqiBattleLabBoardView(8, placements, occupiedInstanceIds);
            placements.Clear();
            occupiedInstanceIds[0] = "after";
            Expect(
                board.Placements.Count == 1 &&
                board.OccupiedInstanceIds[0] == "copy-placement",
                "只读模型：棋盘视图仍引用构造参数",
                failures);

            var coveredSlots = new List<int> { 2, 3 };
            var preview = new BuqiBattleLabPlacementPreview(
                BuqiBattleLabSide.Player, 2, 2, coveredSlots, true, string.Empty);
            coveredSlots[0] = 7;
            Expect(
                preview.CoveredSlots[0] == 2,
                "只读模型：落点预览仍引用构造参数",
                failures);
        }

        private static void CheckMalformedEffectProjection(List<string> failures)
        {
            BuqiConfigCatalog source = CreateSource(8);
            BuqiItemConfigRow nullEffects = Item(
                "effects-null", "空效果列表", BuqiSize.S, 30);
            nullEffects.Effects = null;
            source.Items.Add(nullEffects);

            BuqiItemConfigRow nullEffectEntry = Item(
                "effect-entry-null", "空效果项", BuqiSize.S, 30);
            nullEffectEntry.Effects.Add(null);
            source.Items.Add(nullEffectEntry);

            source.Echoes[0].EchoId = "echo-effects-null";
            source.Echoes[0].Snapshot.Items[0].DefinitionId = "effects-null";
            source.Echoes.Add(new BuqiEchoConfigRow
            {
                EchoId = "echo-effect-entry-null",
                DisplayName = "空效果项道影",
                Build = "balanced",
                Snapshot = new BuqiBuildSnapshotConfigRow
                {
                    SnapshotId = "echo-effect-entry-null-snapshot",
                    ArchetypeId = "balanced",
                    InitialExecution = 100,
                    Items = new List<BuqiItemInstanceConfigRow>
                    {
                        new BuqiItemInstanceConfigRow
                        {
                            InstanceId = "echo-effect-entry-null-item",
                            DefinitionId = "effect-entry-null",
                            Quality = BuqiQuality.Normal,
                            AnchorSlot = 0,
                        },
                    },
                },
            });

            if (!BuqiBattleLabCatalog.TryCreate(
                    source, out BuqiBattleLabCatalog catalog, out string error))
            {
                failures.Add(BuqiText.Format("畸形效果：目录不应丢弃畸形行：{0}", error));
                return;
            }

            BuqiBattleLabItemDefinition nullEffectsItem = catalog.Items.Single(
                item => item.DefinitionId == "effects-null");
            Expect(!nullEffectsItem.Enabled, "畸形效果：空效果列表道具仍被启用", failures);
            Expect(
                nullEffectsItem.Error == "道具效果列表不可为空",
                BuqiText.Format("畸形效果：空效果列表错误不精确：{0}", nullEffectsItem.Error),
                failures);

            BuqiBattleLabItemDefinition nullEffectEntryItem = catalog.Items.Single(
                item => item.DefinitionId == "effect-entry-null");
            Expect(!nullEffectEntryItem.Enabled, "畸形效果：含空效果项道具仍被启用", failures);
            Expect(
                nullEffectEntryItem.Error == "道具效果列表不能包含空项",
                BuqiText.Format("畸形效果：空效果项错误不精确：{0}", nullEffectEntryItem.Error),
                failures);

            Expect(
                source.Items.Single(item => item.DefinitionId == "effects-null").Effects == null &&
                source.Items.Single(item => item.DefinitionId == "effect-entry-null").Effects[0] == null,
                "畸形效果：目录投影改写了源配置",
                failures);

            BuqiBattleLabPresetOpponent nullEffectsOpponent = catalog.PresetOpponents.Single(
                opponent => opponent.EchoId == "echo-effects-null");
            Expect(
                nullEffectsOpponent.ValidationErrors.Any(
                    validationError => validationError.Contains("effects-null")),
                "畸形效果：空效果列表定义仍可用于预设模拟",
                failures);

            BuqiBattleLabPresetOpponent nullEffectEntryOpponent = catalog.PresetOpponents.Single(
                opponent => opponent.EchoId == "echo-effect-entry-null");
            Expect(
                nullEffectEntryOpponent.ValidationErrors.Any(
                    validationError => validationError.Contains("effect-entry-null")),
                "畸形效果：含空效果项定义仍可用于预设模拟",
                failures);
        }

        private static BuqiConfigCatalog CreateSource(int boardSlotCount)
        {
            var source = new BuqiConfigCatalog
            {
                Global = new BuqiGlobalConfigRow
                {
                    ContentVersion = "battle-lab-contract-v1",
                    BoardSlotCount = boardSlotCount,
                },
            };

            // 故意逆序插入，契约要求投影按 DefinitionId 序号排序。
            source.Items.Add(Item("z-last", "后置法门", BuqiSize.S, 30));
            source.Items.Add(Item("m-middle", "中置法门", BuqiSize.M, 40));
            source.Items.Add(Item("a-first", "前置法门", BuqiSize.L, 50));
            source.Echoes.Add(new BuqiEchoConfigRow
            {
                EchoId = "echo-balanced",
                DisplayName = "归衡道影",
                Build = "balanced",
                Snapshot = new BuqiBuildSnapshotConfigRow
                {
                    SnapshotId = "echo-snapshot",
                    ArchetypeId = "balanced",
                    InitialExecution = 100,
                    InitialBuffer = 0,
                    InitialNoiseDebt = 0,
                    Items = new List<BuqiItemInstanceConfigRow>
                    {
                        new BuqiItemInstanceConfigRow
                        {
                            InstanceId = "echo-item",
                            DefinitionId = "a-first",
                            Quality = BuqiQuality.Normal,
                            AnchorSlot = 0,
                        },
                    },
                },
            });
            return source;
        }

        private static BuqiItemConfigRow Item(
            string definitionId,
            string displayName,
            BuqiSize size,
            int cooldownTicks)
        {
            return new BuqiItemConfigRow
            {
                DefinitionId = definitionId,
                DisplayName = displayName,
                EffectDescription = "契约道具",
                Size = size,
                BaseCooldownTicks = cooldownTicks,
                ArchetypeId = "balanced",
                Role = "contract",
                PositionHint = "任意",
                Tags = new List<string> { "contract" },
            };
        }

        private static void ExpectHero(
            BuqiBattleLabHeroDefinition hero,
            string displayName,
            int initialExecution,
            int initialBuffer,
            int initialNoiseDebt,
            string label,
            List<string> failures)
        {
            Expect(
                hero.DisplayName == displayName &&
                hero.InitialExecution == initialExecution &&
                hero.InitialBuffer == initialBuffer &&
                hero.InitialNoiseDebt == initialNoiseDebt,
                BuqiText.Format("目录投影：{0}英雄参数错误", label),
                failures);
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (!condition)
                failures.Add(failure);
        }

        private static void RunCheck(
            string name,
            Action<List<string>> check,
            List<string> failures)
        {
            try
            {
                check(failures);
            }
            catch (Exception exception)
            {
                failures.Add(BuqiText.Format("{0}：检查抛出 {1}: {2}",
                    name, exception.GetType().Name, exception.Message));
            }
        }
    }
}
