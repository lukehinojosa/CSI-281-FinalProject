using UnityEngine;
using System.Collections.Generic;

public class FieldOfView : MonoBehaviour
{
    public int viewRadius;
    public HashSet<Node> visibleNodes = new HashSet<Node>();
    private Grid grid;

    void Start()
    {
        grid = FindObjectOfType<Grid>();
    }

    void Update()
    {
        CalculateFieldOfView();
    }

    public void CalculateFieldOfView()
    {
        visibleNodes.Clear();
        Node originNode = grid.NodeFromWorldPoint(transform.position);
        if (originNode == null) return;
        
        int w = Mathf.RoundToInt(grid.gridWorldSize.x / (grid.nodeRadius * 2));
        int h = Mathf.RoundToInt(grid.gridWorldSize.y / (grid.nodeRadius * 2));

        ShadowCaster.ComputeVisibility(
            w, h, 
            new Vector2Int(originNode.gridX, originNode.gridY), 
            viewRadius,
            (x, y) => {
                Node n = grid.NodeFromGridPoint(x, y);
                return n != null && !n.isWalkable;
            },
            (x, y) => {
                Node n = grid.NodeFromGridPoint(x, y);
                if (n != null) visibleNodes.Add(n);
            }
        );
    }
}