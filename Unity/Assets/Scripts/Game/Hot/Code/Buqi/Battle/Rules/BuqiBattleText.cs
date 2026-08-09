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
                case "Overtime": return "超时结束";
                case "HardCap": return "达到时长上限";
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
                case "OvertimeDamage": return "持续伤害";
                case "NoiseAccident": return "过载事故";
                case "BufferGain": return "护盾增加";
                case "BufferAbsorb": return "护盾抵消";
                case "BurnShieldAbsorb": return "护盾抵消灼烧";
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
