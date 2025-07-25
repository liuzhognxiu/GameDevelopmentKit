using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniFramework.Event;

public class UIBattleWindow : MonoBehaviour
{
    private readonly EventGroup _eventGroup = new EventGroup();
    private GameObject _overView;
    private Text _scoreLabel;

    private void Awake()
    {
        _overView = this.transform.Find("OverView").gameObject;
        _scoreLabel = this.transform.Find("ScoreView/Score").GetComponent<Text>();
        _scoreLabel.text = "Score : 0";

        var restartBtn = this.transform.Find("OverView/ReplayButton").GetComponent<Button>();
        restartBtn.onClick.AddListener(OnClickReplayBtn);

        var homeBtn = this.transform.Find("OverView/HomeButton").GetComponent<Button>();
        homeBtn.onClick.AddListener(OnClickHomeBtn);

    }
    private void OnDestroy()
    {
        _eventGroup.RemoveAllListener();
    }

    private void OnClickReplayBtn()
    {
    }
    private void OnClickHomeBtn()
    {
    }
    private void OnHandleEventMessage(IEventMessage message)
    {
       
    }
}