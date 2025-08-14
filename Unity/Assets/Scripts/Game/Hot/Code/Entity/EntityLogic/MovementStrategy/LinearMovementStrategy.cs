using UnityEngine;

namespace Game.Hot
{
    public class LinearMovementStrategy : IMovementStrategy
    {
        public void Move(Transform transform, BulletData data, float elapseSeconds)
        {
            if (data == null)
            {
                return;
            }
            transform.Translate(Vector3.forward * data.Speed * elapseSeconds, Space.World);
        }
    }
}
