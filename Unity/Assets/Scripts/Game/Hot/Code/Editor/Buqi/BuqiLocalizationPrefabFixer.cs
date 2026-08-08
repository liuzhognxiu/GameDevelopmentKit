#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
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
                ["不器阵列"] = "Buqi.Deploy.Title",
                ["拖拽上阵"] = "Buqi.Deploy.DragHint",
                ["阵容编辑"] = "Buqi.Deploy.DeckEdit",
                ["选择一件装备查看详情"] = "Buqi.Deploy.SelectHint",
                ["01  02  03  04  05  06  07  08"] = "Buqi.Deploy.SlotNumbers",
                ["阵列变更仅在确认后生效"] = "Buqi.Deploy.ApplyHint",
                ["重置"] = "Buqi.Deploy.Reset",
            },
            ["BuqiItemDetailForm.prefab"] = new Dictionary<string, string>
            {
                ["装备详情"] = "Buqi.ItemDetail.Title",
                ["关闭"] = "Buqi.ItemDetail.Close",
                ["无改造"] = "Buqi.ItemDetail.NoRefinement",
                ["Damage"] = "Buqi.ItemDetail.DamageTag",
                ["W8-000"] = "Buqi.ItemDetail.SampleId",
            },
            ["BuqiMessageForm.prefab"] = new Dictionary<string, string>
            {
                ["INFO"] = "Buqi.Message.InfoTag",
            },
            ["BuqiRunShellForm.prefab"] = new Dictionary<string, string>
            {
                ["不器  |  DEMO UI GALLERY"] = "Buqi.RunShell.Title",
                ["R"] = "Buqi.RunShell.RestartTag",
                ["继续"] = "Buqi.RunShell.Continue",
                ["<"] = "Buqi.RunShell.BackTag",
                ["当前阶段"] = "Buqi.RunShell.CurrentPhase",
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
                ["SLOT 01"] = "Buqi.BoardSlot.Slot01",
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
                ["01"] = "Buqi.Phase.Index01",
                [">"] = "Buqi.Phase.Next",
            },
            ["ResourceChipWidget.prefab"] = new Dictionary<string, string>
            {
                ["06"] = "Buqi.Resource.Value06",
                ["金币"] = "Buqi.Resource.Coins",
                ["正常"] = "Buqi.Resource.Normal",
                ["+"] = "Buqi.Resource.Plus",
            },
        };

        [MenuItem("Game/Buqi/Fix Prefab Localization Keys")]
        public static void FixAllBuqiPrefabs()
        {
            string[] roots =
            {
                "Assets/Res/UI/UIForm/Hot/Buqi",
                "Assets/Res/UI/UIPrefab/Buqi",
            };

            int changedPrefabs = 0;
            int changedTexts = 0;
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;
                foreach (string prefabPath in Directory.GetFiles(root, "*.prefab", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(prefabPath);
                    if (!s_PrefabKeyMap.TryGetValue(fileName, out Dictionary<string, string> keyMap))
                        continue;

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
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
                        EditorUtility.SetDirty(prefab);
                        changedPrefabs++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BuqiLocalizationPrefabFixer] 完成：修改 {changedPrefabs} 个 prefab，替换 {changedTexts} 处文本。");
        }
    }
}
#endif
