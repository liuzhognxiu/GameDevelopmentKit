using UnityEngine;

namespace Game.Hot
{
    public class CurveMovementStrategy : IMovementStrategy
    {
        private float m_Time;
        private readonly float m_Frequency;
        private readonly float m_Amplitude;

        public CurveMovementStrategy(float frequency, float amplitude)
        {
            m_Frequency = frequency;
            m_Amplitude = amplitude;
            m_Time = 0f;
        }

        public void Move(Transform transform, BulletData data, float elapseSeconds)
        {
            if (data == null)
            {
                return;
            }

            m_Time += elapseSeconds;

            Vector3 forwardMovement = Vector3.forward * data.Speed * elapseSeconds;
            
            float sineOffset = Mathf.Sin(m_Time * m_Frequency) * m_Amplitude;
            Vector3 curveMovement = Vector3.right * sineOffset * elapseSeconds;

            transform.Translate(forwardMovement + curveMovement, Space.World);
        }
    }
}
