namespace Game.Hot.Buqi.Battle
{
    public static class BuqiBattleText
    {
        public static string Outcome(BattleOutcome outcome)
        {
            switch (outcome)
            {
                case BattleOutcome.LeftWin: return "左侧胜利";
                case BattleOutcome.RightWin: return "右侧胜利";
                case BattleOutcome.Draw: return "平局";
                case BattleOutcome.InvalidBuild: return "装备栏无效";
                case BattleOutcome.Aborted: return "战斗中止";
                default: return "战斗结果未知";
            }
        }

        public static string Termination(string reason)
        {
            switch (reason)
            {
                case "Normal": return "正常结束";
                case "Storm": return "沙暴中结束";
                case "InvalidBuild": return "装备栏无效";
                case "Aborted": return "战斗中止";
                default: return "战斗已结束";
            }
        }

        public static string EventReason(string reasonCode)
        {
            switch (reasonCode)
            {
                case "Damage": return "伤害";
                case "BurnDamage": return "灼烧伤害";
                case "PoisonDamage": return "中毒伤害";
                case "StormDamage": return "沙暴伤害";
                case "NoiseAccident": return "过载事故";
                case "NoiseRemainder": return "过载余数";
                case "BufferGain": return "护盾增加";
                case "BufferAbsorb": return "护盾抵消";
                case "BurnShieldAbsorb": return "护盾抵消灼烧";
                case "CriticalApplied": return "暴击倍率";
                case "RageGained": return "获得怒气";
                case "RageConsumed": return "消耗怒气";
                case "RageRemainder": return "怒气余数";
                case "EnrageStarted": return "进入狂怒";
                case "AmmoConsumed": return "消耗弹药";
                case "AmmoRefilled": return "补充弹药";
                case "AmmoUnlimited": return "无限弹药";
                case "FlightStarted": return "进入飞行";
                case "FlightRefreshed": return "刷新飞行";
                case "FlightEnded": return "结束飞行";
                case "FlightEndDamage": return "停飞伤害";
                case "FlightEndBufferAbsorb": return "护盾抵消停飞伤害";
                case "FlightFreezeMitigation": return "飞行减免冻结";
                case "FlightDelayMitigation": return "飞行减免迟滞";
                case "Heal": return "治疗";
                case "Regen": return "持续恢复";
                case "A06OpeningNoise": return "开场过载";
                default: return string.IsNullOrEmpty(reasonCode) ? "未知效果" : "效果触发";
            }
        }

        public static string Quality(BuqiQuality quality)
        {
            switch (quality)
            {
                case BuqiQuality.Normal: return "普通";
                case BuqiQuality.Improved: return "强化";
                case BuqiQuality.Fixed: return "高级";
                default: return "未知品质";
            }
        }
    }
}
