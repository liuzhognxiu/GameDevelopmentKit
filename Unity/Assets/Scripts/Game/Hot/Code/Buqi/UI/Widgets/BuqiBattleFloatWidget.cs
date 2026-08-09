using System.Collections.Generic;
using System.Text;
using Game.Hot.Buqi.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Hot.Buqi.UI.Widgets
{
    [DisallowMultipleComponent]
    public sealed class BuqiBattleFloatWidget : MonoBehaviour
    {
        [SerializeField]
        private Text m_ValueText = null;

        [SerializeField]
        private Image m_Background = null;

        [SerializeField]
        private CanvasGroup m_CanvasGroup = null;

        public void Render(BattleReplayFeedbackEvent feedback, float normalizedAge = 0f)
        {
            if (feedback == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);
            if (m_ValueText != null)
            {
                m_ValueText.text = Format(feedback);
                m_ValueText.color = ColorFor(feedback.Kind);
            }
            if (m_Background != null)
                m_Background.color = BackgroundFor(feedback.Kind);
            if (m_CanvasGroup != null)
                m_CanvasGroup.alpha = Mathf.Clamp01(1f - Mathf.Max(0f, normalizedAge));
        }

        public void Render(IReadOnlyList<BattleReplayFeedbackEvent> feedback, float presentationSeconds)
        {
            if (feedback == null || feedback.Count == 0)
            {
                Clear();
                return;
            }

            var builder = new StringBuilder();
            float newestAge = 1f;
            BattleReplayFeedbackKind newestKind = feedback[0].Kind;
            for (int index = 0; index < feedback.Count; index++)
            {
                BattleReplayFeedbackEvent item = feedback[index];
                if (index > 0)
                    builder.Append('\n');
                builder.Append(Format(item));
                float age = Mathf.Clamp01(
                    (presentationSeconds - item.StartSeconds) / item.DurationSeconds);
                if (age <= newestAge)
                {
                    newestAge = age;
                    newestKind = item.Kind;
                }
            }

            gameObject.SetActive(true);
            if (m_ValueText != null)
            {
                m_ValueText.text = builder.ToString();
                m_ValueText.color = ColorFor(newestKind);
            }
            if (m_Background != null)
                m_Background.color = BackgroundFor(newestKind);
            if (m_CanvasGroup != null)
                m_CanvasGroup.alpha = Mathf.Clamp01(1f - newestAge);
        }

        public void Clear()
        {
            if (m_ValueText != null)
                m_ValueText.text = string.Empty;
            if (m_CanvasGroup != null)
                m_CanvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private static string Format(BattleReplayFeedbackEvent feedback)
        {
            switch (feedback.Kind)
            {
                case BattleReplayFeedbackKind.Attack:
                    return BuqiText.Format("攻击 {0}", feedback.Value);
                case BattleReplayFeedbackKind.Damage:
                    return BuqiText.Format("-{0}", feedback.Value);
                case BattleReplayFeedbackKind.Guard:
                    return BuqiText.Format("护盾 +{0}", feedback.Value);
                case BattleReplayFeedbackKind.Heal:
                    return BuqiText.Format("治疗 +{0}", feedback.Value);
                default:
                    return feedback.Value.ToString();
            }
        }

        private static Color ColorFor(BattleReplayFeedbackKind kind)
        {
            switch (kind)
            {
                case BattleReplayFeedbackKind.Damage:
                    return new Color32(255, 107, 92, 255);
                case BattleReplayFeedbackKind.Guard:
                    return new Color32(99, 204, 255, 255);
                case BattleReplayFeedbackKind.Heal:
                    return new Color32(92, 222, 145, 255);
                default:
                    return new Color32(255, 210, 96, 255);
            }
        }

        private static Color BackgroundFor(BattleReplayFeedbackKind kind)
        {
            Color color = ColorFor(kind);
            return new Color(color.r * 0.18f, color.g * 0.18f, color.b * 0.18f, 0.9f);
        }
    }
}
