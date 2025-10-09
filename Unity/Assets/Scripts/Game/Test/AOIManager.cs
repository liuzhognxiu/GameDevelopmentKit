// Scripts/AOIManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class AOIManager
{
    private SparseMatrix<GameObject> matrix;
    public float AoiRadius;
    private float gridSize;
    private Dictionary<GameObject, CrossNode<GameObject>> objectToNodeMap = new Dictionary<GameObject, CrossNode<GameObject>>();

    public AOIManager(float aoiRadius, float gridSize = 1f)
    {
        matrix = new SparseMatrix<GameObject>();
        this.AoiRadius = aoiRadius;
        this.gridSize = Mathf.Max(gridSize, 0.01f);
    }

    public void AddObject(GameObject obj, Vector2 worldPos)
    {
        int row = Mathf.FloorToInt(worldPos.y / gridSize);
        int col = Mathf.FloorToInt(worldPos.x / gridSize);

        // 检查是否已存在，避免重复插入导致消失
        if (objectToNodeMap.ContainsKey(obj) && objectToNodeMap[obj] != null)
        {
            RemoveObject(obj);
        }

        matrix.Insert(row, col, obj);
        var node = matrix.GetAllNodes().Find(n => n.Data == obj);
        objectToNodeMap[obj] = node;

        obj.SetActive(true); // 确保对象未被隐藏
    }

    public void RemoveObject(GameObject obj)
    {
        if (objectToNodeMap.TryGetValue(obj, out var node) && node != null)
        {
            matrix.RemoveNode(node);
            objectToNodeMap.Remove(obj);
            obj.SetActive(false); // 隐藏对象而不是销毁
        }
    }

    public void UpdateObjectPosition(GameObject obj, Vector2 newWorldPos)
    {
        if (!objectToNodeMap.TryGetValue(obj, out var node) || node == null)
        {
            Log.Warning("Object not found in map.");
            return;
        }

        int newRow = Mathf.FloorToInt(newWorldPos.y / gridSize);
        int newCol = Mathf.FloorToInt(newWorldPos.x / gridSize);

        if (newRow == node.Row && newCol == node.Col) return;

        // 先移除旧节点
        matrix.RemoveNode(node);

        // 再插入新节点
        matrix.Insert(newRow, newCol, obj);
        var newNode = matrix.GetAllNodes().Find(n => n.Data == obj);
        objectToNodeMap[obj] = newNode;

        obj.SetActive(true);
        NotifyAOIChanges(obj, new Vector2(node.Col * gridSize, node.Row * gridSize), newWorldPos);
    }

    public List<GameObject> GetNearbyObjects(Vector2 playerWorldPos)
    {
        List<GameObject> result = new List<GameObject>();

        int playerRow = Mathf.FloorToInt(playerWorldPos.y / gridSize);
        int playerCol = Mathf.FloorToInt(playerWorldPos.x / gridSize);

        float aoiRadiusInGrids = AoiRadius / gridSize;
        int minRow = Mathf.FloorToInt(playerRow - aoiRadiusInGrids);
        int maxRow = Mathf.CeilToInt(playerRow + aoiRadiusInGrids);
        int minCol = Mathf.FloorToInt(playerCol - aoiRadiusInGrids);
        int maxCol = Mathf.CeilToInt(playerCol + aoiRadiusInGrids);

        var allNodes = matrix.GetAllNodes();
        foreach (var node in allNodes)
        {
            if (node.Row >= minRow && node.Row <= maxRow &&
                node.Col >= minCol && node.Col <= maxCol)
            {
                Vector2 nodeWorldPos = new Vector2(node.Col * gridSize, node.Row * gridSize);
                float dist = Vector2.Distance(nodeWorldPos, playerWorldPos);
                if (dist <= AoiRadius && node.Data != null && node.Data.activeSelf)
                    result.Add(node.Data);
            }
        }
        return result;
    }

    private void NotifyAOIChanges(GameObject obj, Vector2 oldWorldPos, Vector2 newWorldPos)
    {
        var oldNearby = GetNearbyObjects(oldWorldPos);
        var newNearby = GetNearbyObjects(newWorldPos);

        foreach (var nearbyObj in oldNearby)
        {
            if (!newNearby.Contains(nearbyObj))
                OnLeaveAOI(obj, nearbyObj);
        }

        foreach (var nearbyObj in newNearby)
        {
            if (!oldNearby.Contains(nearbyObj))
                OnEnterAOI(obj, nearbyObj);
        }
    }

    private void OnEnterAOI(GameObject source, GameObject target)
    {
        Log.Info($"{source.name} enters AOI of {target.name}");
    }

    private void OnLeaveAOI(GameObject source, GameObject target)
    {
        Log.Info($"{source.name} leaves AOI of {target.name}");
    }

    public void HighlightNearby(List<GameObject> nearby, Color highlightColor, Color defaultColor)
    {
        var allNodes = matrix.GetAllNodes();
        foreach (var node in allNodes)
        {
            if (node.Data != null && node.Data.GetComponent<SpriteRenderer>() != null)
                node.Data.GetComponent<SpriteRenderer>().color = defaultColor;
        }

        foreach (var obj in nearby)
        {
            if (obj != null && obj.GetComponent<SpriteRenderer>() != null)
                obj.GetComponent<SpriteRenderer>().color = highlightColor;
        }
    }

    public SparseMatrix<GameObject> GetMatrix() { return matrix; }
}