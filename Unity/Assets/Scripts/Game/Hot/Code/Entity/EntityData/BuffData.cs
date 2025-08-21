using System;
using UnityEngine;

namespace Game.Hot
{
    [Serializable]
    public class BuffData : EntityData
    {
        [SerializeField]
        private float m_Duration = 0f;

        [SerializeField]
        private BulletBuffType m_BuffType = default(BulletBuffType);

        public BuffData(int entityId, int typeId) : base(entityId, typeId)
        {
            DRBuff drBuff = HotEntry.Tables.DTBuff.GetOrDefault(TypeId);
            if (drBuff == null)
            {
                return;
            }

            m_Duration = drBuff.Duration;
            m_BuffType = drBuff.BuffType;
        }

        public float Duration
        {
            get
            {
                return m_Duration;
            }
        }

        public BulletBuffType BuffType
        {
            get
            {
                return m_BuffType;
            }
        }
    }
}