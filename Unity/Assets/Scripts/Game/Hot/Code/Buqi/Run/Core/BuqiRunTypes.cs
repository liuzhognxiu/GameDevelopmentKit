namespace Game.Hot.Buqi.Run.Core
{
    public enum BuqiRunPhase
    {
        Encounter = 0,
        PveBattle = 1,
        PvpBattle = 2,
        DaySettlement = 3,
        RunTerminal = 4,
        TribulationRoute = 5,
        TribulationStage = 6,
    }

    public enum BuqiRunPeriod
    {
        Hour1Operation = 0,
        MorningOperation = Hour1Operation,
        Hour2Operation = 1,
        NoonOperation = Hour2Operation,
        Hour3Pve = 2,
        DuskPve = Hour3Pve,
        Hour4Operation = 3,
        Hour5Operation = 4,
        Hour6Pvp = 5,
        NightPvp = Hour6Pvp,
    }

    public enum BuqiTribulationRoute
    {
        None = 0,
        FaceThunder = 1,
        ShatterArtifact = 2,
        QuestionHeart = 3,
    }

    public enum BuqiRunOutcome
    {
        None = 0,
        Victory = 1,
        Defeat = 2,
    }

    public enum BuqiRunBattleKind
    {
        Pve = 0,
        Pvp = 1,
    }

    public enum BuqiRunRawBattleOutcome
    {
        PlayerWin = 0,
        OpponentWin = 1,
        Draw = 2,
    }
}
