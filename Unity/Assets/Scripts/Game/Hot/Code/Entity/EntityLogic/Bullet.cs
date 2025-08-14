//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using UnityEngine;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    /// <summary>
    /// 子弹类。
    /// </summary>
    public class Bullet : Entity
    {
        [SerializeField]
        private BulletData m_BulletData = null;

        private IMovementStrategy m_MovementStrategy;

        public void SetMovementStrategy(IMovementStrategy strategy)
        {
            m_MovementStrategy = strategy;
        }

        public ImpactData GetImpactData()
        {
            return new ImpactData(m_BulletData.OwnerCamp, 0, m_BulletData.Attack, 0);
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnInit(object userData)
#else
        protected internal override void OnInit(object userData)
#endif
        {
            base.OnInit(userData);
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnShow(object userData)
#else
        protected internal override void OnShow(object userData)
#endif
        {
            base.OnShow(userData);

            m_BulletData = userData as BulletData;
            if (m_BulletData == null)
            {
                Log.Error("Bullet data is invalid.");
                return;
            }

            // Strategy Factory logic
            switch (m_BulletData.BulletType)
            {
                case BulletType.Seeking:
                    SetMovementStrategy(new SeekingMovementStrategy());
                    break;

                case BulletType.Curve:
                    SetMovementStrategy(new CurveMovementStrategy(m_BulletData.CurveFrequency, m_BulletData.CurveAmplitude));
                    break;

                case BulletType.Linear:
                default:
                    SetMovementStrategy(new LinearMovementStrategy());
                    break;
            }
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnHide(bool isShutdown, object userData)
#else
        protected internal override void OnHide(bool isShutdown, object userData)
#endif
        {
            m_MovementStrategy = null; // Clear strategy on hide
            base.OnHide(isShutdown, userData);
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
#else
        protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
#endif
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);

            m_MovementStrategy?.Move(CachedTransform, m_BulletData, elapseSeconds);
        }
    }
}
