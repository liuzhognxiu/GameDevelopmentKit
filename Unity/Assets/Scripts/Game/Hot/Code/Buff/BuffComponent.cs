using System.Collections.Generic;
using UnityGameFramework.Runtime;
using System.Linq;

namespace Game.Hot
{
    public class BuffComponent : EntityLogic
    {
        private readonly List<Buff> m_Buffs = new List<Buff>();

        public void AddBuff(Buff buff, Game.Hot.Entity target, float duration)
        {
            buff.Apply(target, duration);
            m_Buffs.Add(buff);
        }

        public void RemoveBuff(Buff buff)
        {
            buff.End();
            m_Buffs.Remove(buff);
        }

        public T GetBuff<T>() where T : Buff
        {
            return m_Buffs.OfType<T>().FirstOrDefault();
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);

            for (int i = m_Buffs.Count - 1; i >= 0; i--)
            {
                Buff buff = m_Buffs[i];
                buff.Tick(elapseSeconds);
                if (buff.TimeRemaining <= 0)
                {
                    RemoveBuff(buff);
                }
            }
        }
    }
}
