using System;

namespace Game.Hot.Buqi.DemoUI
{
    public static class BuqiRestartPolicy
    {
        public static bool CanRestart(bool errorVisible, BuqiUIDemoPhase? phase)
        {
            return errorVisible || phase == BuqiUIDemoPhase.RunTerminal;
        }

        public static bool TryDispatch(
            bool errorVisible,
            BuqiUIDemoPhase? phase,
            Action restartCommand)
        {
            if (restartCommand == null || !CanRestart(errorVisible, phase))
                return false;

            restartCommand();
            return true;
        }
    }
}
