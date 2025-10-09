// Scripts/SparseMatrix.cs
using System.Collections.Generic;
using UnityEngine;

public class SparseMatrix<T>
{
    private Dictionary<int, CrossNode<T>> rowHeads = new Dictionary<int, CrossNode<T>>();
    private Dictionary<int, CrossNode<T>> colHeads = new Dictionary<int, CrossNode<T>>();

    // 插入节点（按Col有序插入行链表，按Row有序插入列链表）
    public void Insert(int row, int col, T value)
    {
        var node = new CrossNode<T>(value, row, col);
        InsertToRow(node, row);
        InsertToCol(node, col);
    }

    private void InsertToRow(CrossNode<T> node, int row)
    {
        if (!rowHeads.TryGetValue(row, out var head) || head.Col > node.Col)
        {
            node.Right = head;
            if (head != null) head.Left = node;
            rowHeads[row] = node;
        }
        else
        {
            var current = head;
            while (current.Right != null && current.Right.Col < node.Col)
                current = current.Right;
            node.Right = current.Right;
            node.Left = current;
            if (current.Right != null) current.Right.Left = node;
            current.Right = node;
        }
    }

    private void InsertToCol(CrossNode<T> node, int col)
    {
        if (!colHeads.TryGetValue(col, out var head) || head.Row > node.Row)
        {
            node.Down = head;
            if (head != null) head.Up = node;
            colHeads[col] = node;
        }
        else
        {
            var current = head;
            while (current.Down != null && current.Down.Row < node.Row)
                current = current.Down;
            node.Down = current.Down;
            node.Up = current;
            if (current.Down != null) current.Down.Up = node;
            current.Down = node;
        }
    }

    // 获取值
    public T Get(int row, int col)
    {
        if (!rowHeads.TryGetValue(row, out var current))
            return default;

        while (current != null)
        {
            if (current.Col == col) return current.Data;
            if (current.Col > col) break;
            current = current.Right;
        }
        return default;
    }

    // 获取所有节点（用于遍历/调试）
    public List<CrossNode<T>> GetAllNodes()
    {
        var nodes = new List<CrossNode<T>>();
        foreach (var row in rowHeads.Keys)
        {
            var current = rowHeads[row];
            while (current != null)
            {
                nodes.Add(current);
                current = current.Right;
            }
        }
        return nodes;
    }

    // 移除单个节点
    public void RemoveNode(CrossNode<T> node)
    {
        // 断开行链表
        if (node.Left != null)
            node.Left.Right = node.Right;
        else if (rowHeads[node.Row] == node)
            rowHeads[node.Row] = node.Right;
        if (node.Right != null)
            node.Right.Left = node.Left;

        // 断开列链表
        if (node.Up != null)
            node.Up.Down = node.Down;
        else if (colHeads[node.Col] == node)
            colHeads[node.Col] = node.Down;
        if (node.Down != null)
            node.Down.Up = node.Up;

        // 清理节点
        node.Left = node.Right = node.Up = node.Down = null;
    }

    // 移除整行
    public void RemoveFromRow(int row)
    {
        if (!rowHeads.TryGetValue(row, out var current))
            return;

        while (current != null)
        {
            var next = current.Right;
            RemoveNode(current);
            current = next;
        }
        rowHeads.Remove(row);
    }

    // 移除整列
    public void RemoveFromCol(int col)
    {
        if (!colHeads.TryGetValue(col, out var current))
            return;

        while (current != null)
        {
            var next = current.Down;
            RemoveNode(current);
            current = next;
        }
        colHeads.Remove(col);
    }

    // 可视化（Gizmos）
    public void DrawGizmos(float gridSize = 1f)
    {
        var nodes = GetAllNodes();
        foreach (var node in nodes)
        {
            Vector3 pos = new Vector3(node.Col * gridSize, node.Row * gridSize, 0);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pos, 0.2f * gridSize);  // 调整大小

            if (node.Right != null)
            {
                Vector3 rightPos = new Vector3(node.Right.Col * gridSize, node.Right.Row * gridSize, 0);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(pos, rightPos);
            }

            if (node.Down != null)
            {
                Vector3 downPos = new Vector3(node.Down.Col * gridSize, node.Down.Row * gridSize, 0);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pos, downPos);
            }
        }
    }
}