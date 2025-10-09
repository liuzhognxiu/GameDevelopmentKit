// Scripts/GameManager.cs
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public AOIManager Aoi; 
    public GameObject EnemyPrefab;
    public float GridSize = 1f;  // 可在Inspector调整
    private Color defaultColor = Color.white; // 添加默认颜色字段
    public PlayerController Play;

    void Start()
    {
        Aoi = new AOIManager(10f, GridSize);  // aoiRadius=10f
        Play.AoiManager = Aoi;
        Aoi.AddObject(Play.gameObject,new Vector2(0,0));
        for (int i = 0; i < 100; i++)
        {
            Vector2 worldPos = new Vector2(Random.Range(-50f, 50f), Random.Range(-50f, 50f));
            var enemy = Instantiate(EnemyPrefab, Vector3.zero, Quaternion.identity);
            enemy.GetComponent<SpriteRenderer>().color = defaultColor;
            Aoi.AddObject(enemy, worldPos);
        }
    }

    void OnDrawGizmos()
    {
        if (Aoi != null)
            Aoi.GetMatrix().DrawGizmos(GridSize);
    }
}