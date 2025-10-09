// Scripts/TestSparseMatrix.cs
using UnityEngine;
using UnityGameFramework.Runtime; // 添加此引用

public class TestSparseMatrix : MonoBehaviour
{
    void Start()
    {
        var matrix = new SparseMatrix<string>();

        matrix.Insert(2, 3, "Wall");
        matrix.Insert(-1, 5, "NPC");  // 测试负坐标
        matrix.Insert(2, 4, "Item");

        Log.Info(matrix.Get(2, 3));  // "Wall"
        Log.Info(matrix.Get(-1, 5));  // "NPC"

        var nodes = matrix.GetAllNodes();
        foreach (var node in nodes)
            Log.Info($"({node.Row},{node.Col}): {node.Data}");

        // 测试移除
        var nodeToRemove = nodes.Find(n => n.Row == -1 && n.Col == 5);
        matrix.RemoveNode(nodeToRemove);
        Log.Info(matrix.Get(-1, 5));  // null

        // 测试整行移除
        matrix.RemoveFromRow(2);
    }
}