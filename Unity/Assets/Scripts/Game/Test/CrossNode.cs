using UnityEngine;

public class CrossNode<T>
{
    /// <summary>
    /// 当前实例关联的数据值。
    /// </summary>
    public T Data;

    /// <summary>
    /// 表示十字链表结构中指定方向的相邻节点。
    /// </summary>
    /// <remarks>
    /// 可通过这些字段在左、右、上、下方向导航到相邻节点。
    /// 如果某个方向没有相邻节点，则该字段为 null。
    /// </remarks>
    public CrossNode<T> Left, Right, Up, Down;

    /// <summary>
    /// 表示在网格或矩阵中的零基行列索引。
    /// </summary>
    public int Row, Col;

    public CrossNode(T data, int row, int col)
    {
        Data = data;
        Row = row;
        Col = col;
    }

    public override string ToString()
    {
        return $"Data: {Data}, Position: ({Row}, {Col})";
    }

    public void Clear()
    {
        Data = default;
        Left = Right = Up = Down = null;
        Row = Col = 0;
    }
}