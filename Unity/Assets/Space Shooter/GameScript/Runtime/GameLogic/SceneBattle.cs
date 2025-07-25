using System.Collections;
using System.Collections.Generic;
using UnityEngine;


internal class SceneBattle : MonoBehaviour
{
    public GameObject CanvasDesktop;
    
    private BattleRoom _battleRoom;

    private void Start()
    {
        _battleRoom = new BattleRoom();
        _battleRoom.IntRoom();
    }
    private void OnDestroy()
    {
   
    }
    private void Update()
    {
        if (_battleRoom != null)
            _battleRoom.UpdateRoom();
    }
}