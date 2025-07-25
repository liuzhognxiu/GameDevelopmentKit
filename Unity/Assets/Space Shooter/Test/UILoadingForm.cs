
using CodeBind;
using Game;
using TMPro;
using UnityEngine;

namespace SpaceShooter.Test
{
    [MonoCodeBind('_')]
    public partial class UILoadingForm : AUGuiForm
    {
        // private TextMeshProUGUI _infoText;

        private float m_Timer;
        private int m_Countdown;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // _infoText.text = "Loading";
            m_Timer = 0f;
            m_Countdown = 0;
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);

            m_Timer += elapseSeconds;
            if (m_Timer >= 0.2f)
            {
                m_Timer = 0f;
                m_Countdown++;
                if (m_Countdown > 6)
                {
                    m_Countdown = 0;
                }

                string tips = "Loading";
                for (int i = 0; i < m_Countdown; i++)
                {
                    tips += ".";
                }
                // _infoText.text = tips;
            }
        }
    }
}
