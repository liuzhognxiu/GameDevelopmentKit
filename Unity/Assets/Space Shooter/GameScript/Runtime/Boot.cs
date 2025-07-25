using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Event;

public class Boot : MonoBehaviour
{

    void Awake()
    {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        DontDestroyOnLoad(this.gameObject);
    }
    void Start()
    {


        // 切换到主页面场景
        // SceneEventDefine.ChangeToHomeScene.SendEventMessage();
    }
}