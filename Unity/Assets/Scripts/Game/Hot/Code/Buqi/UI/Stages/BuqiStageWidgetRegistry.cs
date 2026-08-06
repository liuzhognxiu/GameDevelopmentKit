using System;
using System.Collections.Generic;
using Game.Hot.Buqi.DemoUI;
using UnityEngine;

namespace Game.Hot.Buqi.UI.Stages
{
    public sealed class BuqiStageWidgetRegistry
    {
        private readonly Dictionary<BuqiUIDemoPhase, IBuqiStageWidget> m_Stages =
            new Dictionary<BuqiUIDemoPhase, IBuqiStageWidget>();
        private IBuqiStageWidget m_Current;

        public BuqiStageWidgetRegistry(IEnumerable<MonoBehaviour> components)
        {
            if (components == null)
                throw new ArgumentNullException(nameof(components));
            foreach (MonoBehaviour component in components)
            {
                if (!(component is IBuqiStageWidget stage))
                    throw new ArgumentException("Every stage component must implement IBuqiStageWidget.");
                if (stage.Phase == BuqiUIDemoPhase.BattleReplay)
                    throw new ArgumentException("BattleReplay is rendered by BattleForm.");
                if (m_Stages.ContainsKey(stage.Phase))
                    throw new ArgumentException(GameFramework.Utility.Text.Format("Duplicate Buqi UI stage: {0}", stage.Phase));
                m_Stages.Add(stage.Phase, stage);
                stage.Clear();
            }
        }

        public int Count => m_Stages.Count;

        public bool Contains(BuqiUIDemoPhase phase)
        {
            return m_Stages.ContainsKey(phase);
        }

        public bool Show(BuqiUIDemoView view, Action<BuqiUIDemoCommand> submit)
        {
            m_Current?.Clear();
            m_Current = null;
            if (view == null || !m_Stages.TryGetValue(view.Phase, out IBuqiStageWidget stage))
                return false;
            m_Current = stage;
            stage.Render(view, submit);
            return true;
        }

        public void Clear()
        {
            foreach (IBuqiStageWidget stage in m_Stages.Values)
                stage.Clear();
            m_Current = null;
        }
    }
}
