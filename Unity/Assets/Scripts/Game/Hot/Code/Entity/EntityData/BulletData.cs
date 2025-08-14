//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using UnityEngine;

namespace Game.Hot
{
    [Serializable]
    public class BulletData : EntityData
    {
        [SerializeField]
        private int m_OwnerId = 0;

        [SerializeField]
        private CampType m_OwnerCamp = CampType.Unknown;

        [SerializeField]
        private int m_Attack = 0;

        [SerializeField]
        private float m_Speed = 0f;

        [SerializeField]
        private BulletType m_BulletType = BulletType.Linear;

        [SerializeField]
        private float m_CurveFrequency = 5f;

        [SerializeField]
        private float m_CurveAmplitude = 0.5f;

        [SerializeField]
        private int m_TargetId = 0;

        [SerializeField]
        private float m_TurnSpeed = 0f;

        public BulletData(int entityId, int typeId, int ownerId, CampType ownerCamp, int attack, float speed, BulletType bulletType, float curveFrequency, float curveAmplitude, int targetId, float turnSpeed)
            : base(entityId, typeId)
        {
            m_OwnerId = ownerId;
            m_OwnerCamp = ownerCamp;
            m_Attack = attack;
            m_Speed = speed;
            m_BulletType = bulletType;
            m_CurveFrequency = curveFrequency;
            m_CurveAmplitude = curveAmplitude;
            m_TargetId = targetId;
            m_TurnSpeed = turnSpeed;
        }

        // Keep original constructor for backward compatibility
        public BulletData(int entityId, int typeId, int ownerId, CampType ownerCamp, int attack, float speed)
            : this(entityId, typeId, ownerId, ownerCamp, attack, speed, BulletType.Linear, 0f, 0f, 0, 0f)
        {
        }

        public int OwnerId => m_OwnerId;

        public CampType OwnerCamp => m_OwnerCamp;

        public int Attack => m_Attack;  

        public float Speed => m_Speed;

        public BulletType BulletType => m_BulletType;

        public float CurveFrequency => m_CurveFrequency;

        public float CurveAmplitude => m_CurveAmplitude;

        public int TargetId => m_TargetId;

        public float TurnSpeed => m_TurnSpeed;
    }
}
