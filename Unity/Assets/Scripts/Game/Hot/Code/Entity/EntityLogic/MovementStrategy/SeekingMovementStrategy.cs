using UnityEngine;
using UnityGameFramework.Runtime;
using BulletData = Game.Hot.BulletData;

namespace Game.Hot
{
    public class SeekingMovementStrategy : IMovementStrategy
    {
        public void Move(Transform transform, BulletData data, float elapseSeconds)
        {
            if (data == null)
            {
                return;
            }

            // Get the target entity
            var targetEntity = GameEntry.Entity.GetEntity(data.TargetId);
            if (targetEntity == null)
            {
                // Target is gone, continue straight
                transform.Translate(Vector3.forward * data.Speed * elapseSeconds, Space.World);
                return;
            }

            // Direction to the target
            Vector3 directionToTarget = (targetEntity.transform.position - transform.position).normalized;

            // Rotate towards the target
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, data.TurnSpeed * elapseSeconds);

            // Move forward
            transform.Translate(Vector3.forward * data.Speed * elapseSeconds, Space.World);
        }
    }
}
