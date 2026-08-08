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
        MorningOperation = 0,
        NoonOperation = 1,
        DuskPve = 2,
        NightPvp = 3,
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
