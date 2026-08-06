using System;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;

namespace Game.Hot.Buqi.UI.Stages
{
    public interface IBuqiStageWidget
    {
        BuqiUIDemoPhase Phase { get; }
        GameObject Root { get; }
        void Render(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit);
        void Clear();
    }
}
