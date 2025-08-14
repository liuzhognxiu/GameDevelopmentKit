using UnityEngine;

namespace Game.Hot
{
    public interface IMovementStrategy
    {
        void Move(Transform transform, BulletData data, float elapseSeconds);
    }
}
