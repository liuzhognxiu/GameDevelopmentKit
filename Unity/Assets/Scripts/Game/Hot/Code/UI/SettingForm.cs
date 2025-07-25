//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using CodeBind;
using GameFramework.Localization;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    [MonoCodeBind('_')]
    public partial class SettingForm : StarForceUIForm
    {
        private Language m_SelectedLanguage = Language.Unspecified;

        public void OnMusicMuteChanged(bool isOn)
        {
            GameEntry.Sound.Mute("Music", !isOn);
            MusicVolumeSlider.gameObject.SetActive(isOn);
        }

        public void OnMusicVolumeChanged(float volume)
        {
            GameEntry.Sound.SetVolume("Music", volume);
        }

        public void OnSoundMuteChanged(bool isOn)
        {
            GameEntry.Sound.Mute("Sound.Default", !isOn);
            SoundVolumeSlider.gameObject.SetActive(isOn);
        }

        public void OnSoundVolumeChanged(float volume)
        {
            GameEntry.Sound.SetVolume("Sound.Default", volume);
        }

        public void OnUISoundMuteChanged(bool isOn)
        {
            GameEntry.Sound.Mute("UISound", !isOn);
            UISoundVolumeSlider.gameObject.SetActive(isOn);
        }

        public void OnUISoundVolumeChanged(float volume)
        {
            GameEntry.Sound.SetVolume("UISound", volume);
        }

        public void OnEnglishSelected(bool isOn)
        {
            if (!isOn)
            {
                return;
            }
            PlayUISound(10001);
            m_SelectedLanguage = Language.English;
            RefreshLanguageTips();
        }

        public void OnChineseSimplifiedSelected(bool isOn)
        {
            if (!isOn)
            {
                return;
            }
            PlayUISound(10001);
            m_SelectedLanguage = Language.ChineseSimplified;
            RefreshLanguageTips();
        }

        public void OnChineseTraditionalSelected(bool isOn)
        {
            if (!isOn)
            {
                return;
            }
            PlayUISound(10001);
            m_SelectedLanguage = Language.ChineseTraditional;
            RefreshLanguageTips();
        }

        public void OnKoreanSelected(bool isOn)
        {
            if (!isOn)
            {
                return;
            }
            PlayUISound(10001);
            m_SelectedLanguage = Language.Korean;
            RefreshLanguageTips();
        }

        public void OnSubmitButtonClick()
        {
            if (m_SelectedLanguage == GameEntry.Localization.Language)
            {
                Close();
                return;
            }

            GameEntry.Setting.SetString(Constant.Setting.Language, m_SelectedLanguage.ToString());
            GameEntry.Setting.Save();

            GameEntry.Sound.StopMusic();
            UnityGameFramework.Runtime.GameEntry.Shutdown(ShutdownType.Restart);
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnOpen(object userData)
#else
        protected internal override void OnOpen(object userData)
#endif
        {
            base.OnOpen(userData);

            MusicMuteToggle.isOn = !GameEntry.Sound.IsMuted("Music");
            MusicVolumeSlider.value = GameEntry.Sound.GetVolume("Music");

            SoundMuteToggle.isOn = !GameEntry.Sound.IsMuted("Sound.Default");
            SoundVolumeSlider.value = GameEntry.Sound.GetVolume("Sound.Default");

            UISoundMuteToggle.isOn = !GameEntry.Sound.IsMuted("UISound");
            UISoundVolumeSlider.value = GameEntry.Sound.GetVolume("UISound");

            m_SelectedLanguage = GameEntry.Localization.Language;
            switch (m_SelectedLanguage)
            {
                case Language.English:
                    EnglishToggle.SetIsOnWithoutNotify(true);
                    break;

                case Language.ChineseSimplified:
                    ChineseSimplifiedToggle.SetIsOnWithoutNotify(true);
                    break;

                case Language.ChineseTraditional:
                    ChineseTraditionalToggle.SetIsOnWithoutNotify(true);
                    break;

                case Language.Korean:
                    KoreanToggle.SetIsOnWithoutNotify(true);
                    break;

                default:
                    break;
            }
        }
#if UNITY_2017_3_OR_NEWER
        protected override void OnInit(object userData)
#else
        protected internal override void OnInit(object userData)
#endif
        {
            base.OnInit(userData);
            InitBind(GetComponent<CodeBind.CSCodeBindMono>());

        }
#if UNITY_2017_3_OR_NEWER
        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
#else
        protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
#endif
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);

            if (LanguageTipsCanvasGroup.gameObject.activeSelf)
            {
                LanguageTipsCanvasGroup.alpha = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * Time.time);
            }
        }

        private void RefreshLanguageTips()
        {
            LanguageTipsCanvasGroup.gameObject.SetActive(m_SelectedLanguage != GameEntry.Localization.Language);
        }
    }
}
