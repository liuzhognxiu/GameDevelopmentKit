//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;
using System.Linq;
using System.Collections.Generic;

namespace Game.Hot
{
    /// <summary>
    /// 武器类。
    /// </summary>
    public class Weapon : Entity
    {
        private const string AttachPoint = "Weapon Point";

        [SerializeField]
        private WeaponData m_WeaponData = null;

        private float m_NextAttackTime = 0f;
        private EntityLogic m_Owner = null;

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

            m_WeaponData = userData as WeaponData;
            if (m_WeaponData == null)
            {
                Log.Error("Weapon data is invalid.");
                return;
            }

            GameEntry.Entity.AttachEntity(Entity, m_WeaponData.OwnerId, AttachPoint);
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnAttachTo(EntityLogic parentEntity, Transform parentTransform, object userData)
#else
        protected internal override void OnAttachTo(EntityLogic parentEntity, Transform parentTransform, object userData)
#endif
        {
            base.OnAttachTo(parentEntity, parentTransform, userData);

            m_Owner = parentEntity;
            Name = Utility.Text.Format("Weapon of {0}", parentEntity.Name);
            CachedTransform.localPosition = Vector3.zero;
        }

        private BulletType? m_OverriddenBulletType = null;

        public void OverrideBulletType(BulletType? bulletType)
        {
            m_OverriddenBulletType = bulletType;
        }

        public void TryAttack()
        {
            if (Time.time < m_NextAttackTime)
            {
                return;
            }

            m_NextAttackTime = Time.time + m_WeaponData.AttackInterval;

            BulletType bulletType = m_OverriddenBulletType ?? m_WeaponData.BulletType;

            int targetId = 0;
            float turnSpeed = 0f;
            if (bulletType == BulletType.Seeking)
            {
                targetId = FindNearestEnemy();
                if (targetId != 0)
                {
                    turnSpeed = 5f; // Hardcoded turn speed
                }
            }

            BulletData bulletData = new BulletData(GameEntry.Entity.GenerateSerialId(), m_WeaponData.BulletId, m_WeaponData.OwnerId, m_WeaponData.OwnerCamp, m_WeaponData.Attack, m_WeaponData.BulletSpeed, bulletType, 0, 0, targetId, turnSpeed)
            {
                Position = CachedTransform.position
            };

            GameEntry.Entity.ShowBullet(bulletData);
            GameEntry.Sound.PlaySound(m_WeaponData.BulletSoundId);
        }

        private int FindNearestEnemy()
        {
            EntityComponent entityComponent = Game.GameEntry.Entity;
            if (entityComponent == null)
            {
                return 0;
            }

            var entities = entityComponent.GetAllLoadedEntities();
            Log.Info("Found {0} entities.", entities.Length);
            UnityGameFramework.Runtime.Entity nearestEnemy = null;
            float minDistance = float.MaxValue;

            foreach (var entity in entities)
            {
                var asteroid = entity.Logic as Asteroid;
                if (asteroid != null)
                {
                    Log.Info("Checking aircraft {0}. IsDead: {1}", asteroid.name, asteroid.IsDead);
                }

                if (asteroid != null && !asteroid.IsDead)
                {
                    float distance = Vector3.Distance(this.CachedTransform.position, entity.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestEnemy = entity;
                    }
                }
            }

            if (nearestEnemy != null)
            {
                Log.Info("Nearest enemy is {0}", nearestEnemy.name);
                return nearestEnemy.Id;
            }

            return 0;
        }
    }
}
