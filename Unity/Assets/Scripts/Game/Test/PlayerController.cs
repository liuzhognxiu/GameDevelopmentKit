// Scripts/PlayerController.cs
using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    public AOIManager AoiManager;
    public float Speed = 5f;
    public Color HighlightColor = Color.green;
    public Color DefaultColor = Color.gray;

    private Vector2 lastGridPos;
    // 将字段命名首字母改为大写，符合 ET0502 规范
    public TextMeshProUGUI AoiCountText;
    void Start()
    {
        lastGridPos = new Vector2(transform.position.x, transform.position.y);
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        transform.Translate(new Vector3(h, v, 0) * Speed * Time.deltaTime);

        Vector2 currentWorldPos = transform.position;
        AoiManager.UpdateObjectPosition(gameObject, currentWorldPos);  // 更新玩家位置

        var nearby = AoiManager.GetNearbyObjects(currentWorldPos);
        AoiManager.HighlightNearby(nearby, HighlightColor, DefaultColor);

        if (AoiCountText != null)
            AoiCountText.text = $"AOI Objects: {nearby.Count}";
    }

    void OnDrawGizmos()
    {
        if (AoiManager == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AoiManager.AoiRadius);
    }
}