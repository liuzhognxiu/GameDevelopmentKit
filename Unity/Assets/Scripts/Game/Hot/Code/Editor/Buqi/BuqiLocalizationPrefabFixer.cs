#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Editor.Buqi
{
    /// <summary>
    /// 将 Buqi 系列 UI prefab 的静态中文文本替换为本地化 Key。
    /// 只处理 prefab 中静态写死的 Text 内容（运行时被代码 SetText 覆盖的动态文本不受影响）。
    /// 菜单：Game/Buqi/Fix Prefab Localization Keys
    /// </summary>
    internal static class BuqiLocalizationPrefabFixer
    {
        private static readonly Dictionary<string, Dictionary<string, string>> s_PrefabKeyMap = new Dictionary<string, Dictionary<string, string>>
        {
            ["BuqiConfirmForm.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["确认操作"] = "Buqi.Confirm.Title",
                ["确认"] = "Buqi.Confirm.Confirm",
                ["取消"] = "Buqi.Confirm.Cancel",
            },
            ["BuqiDragDeployForm.prefab"] = new Dictionary<string, string>
            {
                ["放置校验"] = "Buqi.Deploy.PlacementCheck",
                ["道具详情"] = "Buqi.Deploy.ItemDetail",
                ["确认上阵"] = "Buqi.Deploy.ConfirmDeploy",
                ["待上阵道具"] = "Buqi.Deploy.PendingItems",
                ["取消"] = "Buqi.Deploy.Cancel",
                ["仓库  >  阵列  |  拖动调整位置 |  拖回仓库撤下"] = "Buqi.Deploy.Header",
                ["仓库  >  阵列  |  拖动调整位置  |  拖回仓库撤下"] = "Buqi.Deploy.Header",
                ["不器阵列"] = "Buqi.Deploy.Title",
                ["拖拽上阵"] = "Buqi.Deploy.DragHint",
                ["阵容编辑"] = "Buqi.Deploy.DeckEdit",
                ["选择一件装备查看详情"] = "Buqi.Deploy.SelectHint",
                ["01  02  03  04  05  06  07  08"] = "Buqi.Deploy.SlotNumbers",
                ["阵列变更仅在确认后生效"] = "Buqi.Deploy.ApplyHint",
                ["第 3 回合  |  金币 12  |  胜场 4  |  生命2  |  对手 清虚真人"] = "Buqi.Deploy.Context",
                ["第 3 回合  |  金币 12  |  胜场 4  |  生命 2  |  对手 清虚真人"] = "Buqi.Deploy.Context",
                ["重置"] = "Buqi.Deploy.Reset",
            },
            ["BuqiItemDetailForm.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["装备详情"] = "Buqi.ItemDetail.Title",
                ["关闭"] = "Buqi.ItemDetail.Close",
                ["无改造"] = "Buqi.ItemDetail.NoRefinement",
                ["Damage"] = "Buqi.ItemDetail.DamageTag",
                ["W8-000"] = "Buqi.ItemDetail.SampleId",
            },
            ["BuqiMessageForm.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["INFO"] = "Buqi.Message.InfoTag",
            },
            ["BuqiRunShellForm.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["不器  |  DEMO UI GALLERY"] = "Buqi.RunShell.Title",
                ["R"] = "Buqi.RunShell.RestartTag",
                ["继续"] = "Buqi.RunShell.Continue",
                ["<"] = "Buqi.RunShell.BackTag",
                ["当前阶段"] = "Buqi.RunShell.CurrentPhase",
                ["1x"] = "Buqi.Battle.Speed1x",
                ["2x"] = "Buqi.Battle.Speed2x",
                [">>|"] = "Buqi.Battle.SkipEnd",
                ["Record"] = "Buqi.RunShell.DayRecord",
            },
            ["BuqiDeploySlotWidget.prefab"] = new Dictionary<string, string>
            {
                ["空位"] = "Buqi.Slot.Empty",
                ["01"] = "Buqi.Slot.Number",
                ["×"] = "Buqi.Slot.CloseMark",
            },
            ["BuqiDraggableItemWidget.prefab"] = new Dictionary<string, string>
            {
                ["W8-000"] = "Buqi.Item.SampleId",
                ["仓库 01"] = "Buqi.Item.StorageTag",
                ["占用 1 格"] = "Buqi.Item.SpaceTag",
            },
            ["BattleForm.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["不器 \u00b7 战斗回放"] = "Buqi.Battle.Title",
                ["左侧构筑"] = "Buqi.Battle.LeftBuild",
                ["右侧构筑"] = "Buqi.Battle.RightBuild",
                ["战斗证据"] = "Buqi.Battle.Evidence",
                ["终局事实"] = "Buqi.Battle.FinalFacts",
                ["尚无事件"] = "Buqi.Battle.NoEvents",
                ["战斗推演中"] = "Buqi.Battle.Simulating",
                ["暂停"] = "Buqi.Battle.Pause",
                ["继续"] = "Buqi.Battle.Continue",
                ["实时"] = "Buqi.Battle.Live",
                ["生命值 --  护盾 --  过载 --"] = "Buqi.Battle.StatsPlaceholder",
                ["TICK 000 / 000"] = "Buqi.Battle.TickPlaceholder",
                ["1x"] = "Buqi.Battle.Speed1x",
                ["2x"] = "Buqi.Battle.Speed2x",
                ["4x"] = "Buqi.Battle.Speed4x",
                ["LIVE"] = "Buqi.Battle.LiveBadge",
                ["<"] = "Buqi.Battle.PrevTick",
                [">"] = "Buqi.Battle.NextTick",
                [">>"] = "Buqi.Battle.SkipEnd",
                ["R"] = "Buqi.Battle.RestartTag",
                ["Replay error"] = "Buqi.Battle.ReplayError",
            },
            // ---- Widget prefab（Form 子物体，同样会被 StarForceUIForm.OnInit 当 key 查询）----
            ["BattleLogWidget.prefab"] = new Dictionary<string, string>
            {
                ["T000  --  0"] = "Buqi.Log.Placeholder",
            },
            ["BoardSlotWidget.prefab"] = new Dictionary<string, string>
            {
                ["LOCKED"] = "Buqi.BoardSlot.Locked",
                ["SIZE 1"] = "Buqi.BoardSlot.Size1",
                ["ITEM"] = "Buqi.BoardSlot.Item",
                ["SLOT 01"] = "Buqi.BoardSlot.SlotLabel",
            },
            ["ChoiceCardWidget.prefab"] = new Dictionary<string, string>
            {
                ["COST 0"] = "Buqi.Choice.Cost0",
                ["UNAVAILABLE"] = "Buqi.Choice.Unavailable",
                ["CHOICE"] = "Buqi.Choice.Title",
                ["Choice description"] = "Buqi.Choice.Description",
            },
            ["FactRowWidget.prefab"] = new Dictionary<string, string>
            {
                ["关键装备完成有效伤害"] = "Buqi.Fact.KeyItemDamage",
                ["跳到 T000"] = "Buqi.Fact.JumpTo",
                ["终局事实"] = "Buqi.Fact.Title",
            },
            ["ItemCardWidget.prefab"] = new Dictionary<string, string>
            {
                ["W8-000"] = "Buqi.Item.SampleId",
                ["Damage"] = "Buqi.ItemDetail.DamageTag",
                ["冻"] = "Buqi.Item.Frozen",
                ["1格  充能 0  冻结 0"] = "Buqi.Item.StatsPlaceholder",
            },
            ["OfferCardWidget.prefab"] = new Dictionary<string, string>
            {
                ["SOLD"] = "Buqi.Offer.Sold",
                ["PRICE 0"] = "Buqi.Offer.Price0",
                ["LOCKED"] = "Buqi.Offer.Locked",
                ["OFFER"] = "Buqi.Offer.Title",
                ["BUY"] = "Buqi.Offer.Buy",
                ["DETAILS"] = "Buqi.Offer.Details",
                ["Offer description"] = "Buqi.Offer.Description",
            },
            ["OpponentSnapshotWidget.prefab"] = new Dictionary<string, string>
            {
                ["空置"] = "Buqi.Opponent.Empty",
                ["主要威胁：公开装备触发关系"] = "Buqi.Opponent.Threat",
                ["连续 8 格构筑  \u00b7  公开情报"] = "Buqi.Opponent.Intel",
                ["已知风险：未公开改造"] = "Buqi.Opponent.Risk",
                ["方向  高速构筑"] = "Buqi.Opponent.Direction",
                ["对手快照"] = "Buqi.Opponent.Title",
            },
            ["PhaseStepWidget.prefab"] = new Dictionary<string, string>
            {
                ["起始选择"] = "Buqi.Phase.StartingChoice",
                ["01"] = "Buqi.Phase.IndexLabel",
                [">"] = "Buqi.Phase.Next",
            },
            ["ResourceChipWidget.prefab"] = new Dictionary<string, string>
            {
                ["06"] = "Buqi.Resource.ValueLabel",
                ["金币"] = "Buqi.Resource.Coins",
                ["正常"] = "Buqi.Resource.Normal",
                ["+"] = "Buqi.Resource.Plus",
            },
            ["StarterSelectionWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["起始选择"] = "Buqi.Phase.StartingChoice",
                ["选择本局的第一件装备。"] = "Buqi.Stage.StarterSelection.Description",
            },
            ["OpponentIntelWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["对手快照"] = "Buqi.Opponent.Title",
                ["只展示公开的棋盘和构筑信息。"] = "Buqi.Stage.OpponentIntel.Description",
            },
            ["PreparationChoiceWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["战前准备"] = "Buqi.Stage.PreparationChoice.Title",
                ["选择本回合的准备收益。"] = "Buqi.Stage.PreparationChoice.Description",
            },
            ["ShopWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["商店"] = "Buqi.Stage.Shop.Title",
                ["购买装备、刷新或锁定当前报价。"] = "Buqi.Stage.Shop.Description",
            },
            ["EventWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["事件"] = "Buqi.Stage.Event.Title",
                ["在收益与风险之间做出选择。"] = "Buqi.Stage.Event.Description",
            },
            ["ModificationWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["改造"] = "Buqi.Stage.Modification.Title",
                ["为装备添加收益与代价并存的改造。"] = "Buqi.Stage.Modification.Description",
            },
            ["BoardEditorWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["棋盘编辑"] = "Buqi.Stage.BoardEditor.Title",
                ["点选装备，再选择 8 格棋盘中的目标位。"] = "Buqi.Stage.BoardEditor.Description",
            },
            ["PredictionWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["胜负预测"] = "Buqi.Stage.Prediction.Title",
                ["战斗前记录你对结果的判断。"] = "Buqi.Stage.Prediction.Description",
            },
            ["BattleSummaryWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["战斗总结"] = "Buqi.Stage.BattleSummary.Title",
                ["从真实战斗日志中提取可回溯事实。"] = "Buqi.Stage.BattleSummary.Description",
            },
            ["RoundSettlementWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["回合结算"] = "Buqi.Stage.RoundSettlement.Title",
                ["结算胜场、单局生命与金币变化。"] = "Buqi.Stage.RoundSettlement.Description",
            },
            ["RunTerminalWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["DEMO"] = "Buqi.Common.DemoTag",
                ["单局结束"] = "Buqi.Stage.RunTerminal.Title",
                ["查看本局构筑摘要并重新开始。"] = "Buqi.Stage.RunTerminal.Description",
            },
            ["OperationChoiceWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["当前周天"] = "Buqi.Stage.Shared.CurrentBoard",
                ["\u6F14\u793A"] = "Buqi.Common.DemoTag",
                ["\u7ECF\u8425\u9009\u62E9"] = "Buqi.Stage.OperationChoice.Title",
                ["\u9009\u62E9\u574A\u5E02\u3001\u673A\u7F18\u6216\u9759\u4FEE\uFF1B\u5F53\u524D\u5468\u5929\u4FDD\u6301\u53EF\u89C1\u3002"] = "Buqi.Stage.OperationChoice.Description",
            },
            ["PveSelectionStageWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["当前周天"] = "Buqi.Stage.Shared.CurrentBoard",
                ["\u6F14\u793A"] = "Buqi.Common.DemoTag",
                ["PVE \u9009\u5173"] = "Buqi.Stage.PveSelection.Title",
                ["\u9009\u62E9\u521D\u9636\u3001\u8FDB\u9636\u6216\u9669\u9636\u540E\u76F4\u63A5\u8FDB\u5165\u6218\u6597\u3002"] = "Buqi.Stage.PveSelection.Description",
            },
            ["TribulationRouteWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["\u6F14\u793A"] = "Buqi.Common.DemoTag",
                ["\u6E21\u52AB\u8DEF\u7EBF"] = "Buqi.Stage.TribulationRoute.Title",
                ["\u4E5D\u65E5\u591C\u6218\u540E\u9009\u62E9\u4E00\u6761\u6E21\u52AB\u8DEF\u7EBF\u3002"] = "Buqi.Stage.TribulationRoute.Description",
            },
            ["TribulationStageWidget.prefab"] = new Dictionary<string, string>
            {
                ["--"] = "Buqi.Common.Placeholder",
                ["\u6F14\u793A"] = "Buqi.Common.DemoTag",
                ["\u4E09\u9636\u6BB5\u5929\u52AB"] = "Buqi.Stage.TribulationStage.Title",
                ["\u5E94\u52AB\u5E76\u63A8\u8FDB\u5F53\u524D\u9636\u6BB5\u3002"] = "Buqi.Stage.TribulationStage.Description",
            },
        };

        [MenuItem("Game/Buqi/Fix Prefab Localization Keys")]
        public static void FixAllBuqiPrefabs()
        {
            string[] roots =
            {
                "Assets/Res/UI/UIPrefab/Buqi/Stages",
                "Assets/Res/UI/UIPrefab/Buqi",
                "Assets/Res/UI/UIForm/Hot/Buqi",
            };

            int changedPrefabs = 0;
            int changedTexts = 0;
            foreach (string prefabPath in EnumeratePrefabAssetPaths(roots))
            {
                string fileName = Path.GetFileName(prefabPath);
                if (!s_PrefabKeyMap.TryGetValue(fileName, out Dictionary<string, string> keyMap))
                    continue;

                GameObject prefab = null;
                try
                {
                    prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[BuqiLocalizationPrefabFixer] 无法加载 prefab: {prefabPath}");
                        continue;
                    }

                    Text[] texts = prefab.GetComponentsInChildren<Text>(true);
                    bool anyChanged = false;
                    foreach (Text text in texts)
                    {
                        if (text == null || string.IsNullOrEmpty(text.text))
                            continue;
                        if (keyMap.TryGetValue(text.text, out string key))
                        {
                            string oldText = text.text;
                            text.text = key;
                            anyChanged = true;
                            changedTexts++;
                            Debug.Log($"[BuqiLocalizationPrefabFixer] {fileName}: '{oldText}' 替换为 '{key}'");
                        }
                    }

                    if (anyChanged)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                        changedPrefabs++;
                    }
                }
                finally
                {
                    if (prefab != null)
                        PrefabUtility.UnloadPrefabContents(prefab);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BuqiLocalizationPrefabFixer] 完成：修改 {changedPrefabs} 个 prefab，替换 {changedTexts} 处文本。");
            ValidateAllBuqiPrefabs();
        }

        [MenuItem("Game/Buqi/Validate Prefab Localization Keys")]
        public static void ValidateAllBuqiPrefabs()
        {
            string[] roots =
            {
                "Assets/Res/UI/UIPrefab/Buqi/Stages",
                "Assets/Res/UI/UIPrefab/Buqi",
                "Assets/Res/UI/UIForm/Hot/Buqi",
            };

            HashSet<string> knownKeys = new HashSet<string>(s_PrefabKeyMap.Values.SelectMany(map => map.Values));
            List<string> errors = new List<string>();
            int prefabCount = 0;
            int textCount = 0;
            foreach (string prefabPath in EnumeratePrefabAssetPaths(roots))
            {
                string fileName = Path.GetFileName(prefabPath);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    errors.Add($"无法加载 prefab: {prefabPath}");
                    continue;
                }

                prefabCount++;
                foreach (Text text in prefab.GetComponentsInChildren<Text>(true))
                {
                    if (text == null || string.IsNullOrEmpty(text.text))
                        continue;

                    textCount++;
                    SerializedObject serializedText = new SerializedObject(text);
                    SerializedProperty serializedValue = serializedText.FindProperty("m_Text");
                    string value = serializedValue?.stringValue ?? text.text;
                    if (!knownKeys.Contains(value))
                        errors.Add($"{fileName}/{GetHierarchyPath(text.transform)}: m_Text='{value}'");
                }
            }

            if (errors.Count > 0)
                throw new System.InvalidOperationException(
                    $"[BuqiLocalizationPrefabFixer] 校验失败，共 {errors.Count} 处未绑定 Key：\n{string.Join("\n", errors)}");

            Debug.Log($"[BuqiLocalizationPrefabFixer] 校验通过：{prefabCount} 个 prefab，{textCount} 个非空 Text.m_Text 均为已知本地化 Key。");
        }

        private static IEnumerable<string> EnumeratePrefabAssetPaths(IEnumerable<string> roots)
        {
            HashSet<string> visited = new HashSet<string>();
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string systemPath in Directory.GetFiles(root, "*.prefab", SearchOption.TopDirectoryOnly))
                {
                    string assetPath = systemPath.Replace('\\', '/');
                    if (visited.Add(assetPath))
                        yield return assetPath;
                }
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }
            return path;
        }
    }
}
#endif
