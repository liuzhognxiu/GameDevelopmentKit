//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public partial class DialogForm : StarForceUIForm
    {
        private int m_DialogMode = 1;
        private bool m_PauseGame = false;
        private object m_UserData = null;
        private GameFrameworkAction<object> m_OnClickConfirm = null;
        private GameFrameworkAction<object> m_OnClickCancel = null;
        private GameFrameworkAction<object> m_OnClickOther = null;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            InitBind(GetComponent<CodeBind.CSCodeBindMono>());

            Group1ConfirmExButton.onClick.AddListener(OnConfirmButtonClick);
            Group2CancelExButton.onClick.AddListener(OnCancelButtonClick);
            Group2ConfirmExButton.onClick.AddListener(OnConfirmButtonClick);
            Group3CancelExButton.onClick.AddListener(OnCancelButtonClick);
            Group3ConfirmExButton.onClick.AddListener(OnConfirmButtonClick);
            Group3OtherExButton.onClick.AddListener(OnOtherButtonClick);
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            m_DialogMode = 1;
            m_PauseGame = false;
            m_UserData = userData;
            RefreshDialogMode();

            if (userData is DialogParams dialogParams)
            {
                SetDialog(dialogParams.Title, dialogParams.Message, dialogParams.UserData);
                SetDialogMode(dialogParams.Mode);
                SetOnClickConfirm(dialogParams.OnClickConfirm);
                SetOnClickCancel(dialogParams.OnClickCancel);
                SetOnClickOther(dialogParams.OnClickOther);
            }
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            Group1ConfirmExButton.onClick.RemoveListener(OnConfirmButtonClick);
            Group2CancelExButton.onClick.RemoveListener(OnCancelButtonClick);
            Group2ConfirmExButton.onClick.RemoveListener(OnConfirmButtonClick);
            Group3CancelExButton.onClick.RemoveListener(OnCancelButtonClick);
            Group3ConfirmExButton.onClick.RemoveListener(OnConfirmButtonClick);
            Group3OtherExButton.onClick.RemoveListener(OnOtherButtonClick);

            m_DialogMode = 1;
            m_PauseGame = false;
            m_UserData = null;
            m_OnClickConfirm = null;
            m_OnClickCancel = null;
            m_OnClickOther = null;

            base.OnClose(isShutdown, userData);
        }

        public void SetDialog(string title, string message, object userData = null)
        {
            TitleText.text = title;
            MessageText.text = message;
            m_UserData = userData;
        }

        public void SetDialogMode(int mode)
        {
            m_DialogMode = mode;
            RefreshDialogMode();
        }

        private void RefreshDialogMode()
        {
            ButtonGroup1Transform.gameObject.SetActive(m_DialogMode == 1);
            ButtonGroup2Transform.gameObject.SetActive(m_DialogMode == 2);
            ButtonGroup3Transform.gameObject.SetActive(m_DialogMode == 3);
        }

        public int DialogMode
        {
            get { return m_DialogMode; }
        }

        public bool PauseGame
        {
            get { return m_PauseGame; }
            set { m_PauseGame = value; }
        }

        public object UserData
        {
            get { return m_UserData; }
        }

        public void OnConfirmButtonClick()
        {
            m_OnClickConfirm?.Invoke(m_UserData);
            FadeClose();
        }

        public void OnCancelButtonClick()
        {
            m_OnClickCancel?.Invoke(m_UserData);
            FadeClose();
        }

        public void OnOtherButtonClick()
        {
            m_OnClickOther?.Invoke(m_UserData);
            FadeClose();
        }

        public void SetOnClickConfirm(GameFrameworkAction<object> onClickConfirm)
        {
            m_OnClickConfirm = onClickConfirm;
        }

        public void SetOnClickCancel(GameFrameworkAction<object> onClickCancel)
        {
            m_OnClickCancel = onClickCancel;
        }

        public void SetOnClickOther(GameFrameworkAction<object> onClickOther)
        {
            m_OnClickOther = onClickOther;
        }
    }
}
