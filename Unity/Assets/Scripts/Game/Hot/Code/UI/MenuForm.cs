//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using Game;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public partial class MenuForm : StarForceUIForm
    {
        private ProcedureMenu m_ProcedureMenu = null;

        public void OnStartButtonClick()
        {
            m_ProcedureMenu.StartGame();
        }

        public void OnSettingButtonClick()
        {
            GameEntry.UI.OpenUIForm(UIFormId.SettingForm);
        }

        public void OnAboutButtonClick()
        {
            GameEntry.UI.OpenUIForm(UIFormId.AboutForm);
        }

        public void OnQuitButtonClick()
        {
            GameEntry.UI.OpenUIForm(UIFormId.DialogForm, new DialogParams()
            {
                Mode = 2,
                Title = GameEntry.Localization.GetString("AskQuitGame.Title"),
                Message = GameEntry.Localization.GetString("AskQuitGame.Message"),
                OnClickConfirm = delegate(object userData) { UnityGameFramework.Runtime.GameEntry.Shutdown(ShutdownType.Quit); },
            });
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnOpen(object userData)
#else
        protected internal override void OnOpen(object userData)
#endif
        {
            base.OnOpen(userData);
            
            InitBind(GetComponent<CodeBind.CSCodeBindMono>());

            m_ProcedureMenu = (ProcedureMenu)userData;
            if (m_ProcedureMenu == null)
            {
                Log.Warning("ProcedureMenu is invalid when open MenuForm.");
                return;
            }

            StartExButton.onClick.AddListener(OnStartButtonClick);
            SettingExButton.onClick.AddListener(OnSettingButtonClick);
            AboutExButton.onClick.AddListener(OnAboutButtonClick);
            QuitExButton.onClick.AddListener(OnQuitButtonClick);
            
            QuitExButton.gameObject.SetActive(Application.platform != RuntimePlatform.IPhonePlayer);
        }

#if UNITY_2017_3_OR_NEWER
        protected override void OnClose(bool isShutdown, object userData)
#else
        protected internal override void OnClose(bool isShutdown, object userData)
#endif
        {
            m_ProcedureMenu = null;
            
            StartExButton.onClick.RemoveAllListeners();
            SettingExButton.onClick.RemoveAllListeners();
            AboutExButton.onClick.RemoveAllListeners();
            QuitExButton.onClick.RemoveAllListeners();
            ClearBind();

            base.OnClose(isShutdown, userData);
        }
    }
}