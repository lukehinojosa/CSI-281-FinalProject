using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;

public class Pathfinding : MonoBehaviour
{
    private Grid grid;

    void Awake()
    {
        grid = GetComponent<Grid>();
    }

    // This is the main method that finds a path from startPos to targetPos.
    public List<Node> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        Node startNode = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);
        
        // If the agent or target are slightly inside a wall, snap to the nearest valid node.
        if (!startNode.isWalkable)
        {
            startNode = GetClosestWalkableNeighbor(startNode);
        }

        if (!targetNode.isWalkable)
        {
            targetNode = GetClosestWalkableNeighbor(targetNode);
        }

        // If after checking neighbors we still have bad nodes, we can't path.
        if (startNode == null || targetNode == null || !startNode.isWalkable || !targetNode.isWalkable)
        {
            return null;
        }
        
        int maxSize = (int)(grid.gridWorldSize.x / (grid.nodeRadius * 2) * grid.gridWorldSize.y / (grid.nodeRadius * 2));

        // A* Algorithm Core
        Heap<Node> openSet = new Heap<Node>(maxSize);
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            // O(1) operation to get the best node
            Node currentNode = openSet.RemoveFirst();
            closedSet.Add(currentNode);

            // If the target node has been found, done.
            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            // Evaluate the neighbors of the current node.
            foreach (Node neighbour in grid.GetNeighbours(currentNode))
            {
                if (!neighbour.isWalkable || closedSet.Contains(neighbour))
                {
                    continue; // Skip unwalkable nodes or nodes already evaluated.
                }

                int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour);
                if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newMovementCostToNeighbour;
                    neighbour.hCost = GetDistance(neighbour, targetNode);
                    neighbour.parent = currentNode;

                    if (!openSet.Contains(neighbour))
                    {
                        openSet.Add(neighbour);
                    }
                    else
                    {
                        // Update priority in heap if a shorter path to an existing node is found
                        openSet.UpdateItem(neighbour);
                    }
                }
            }
        }

        // No path was found.
        return null; 
    }
    
    // Helper method to find a valid node if the requested one is blocked
    private Node GetClosestWalkableNeighbor(Node node)
    {
        Node bestNode = null;
        float minDst = float.MaxValue;

        foreach (Node neighbor in grid.GetNeighbours(node))
        {
            if (neighbor.isWalkable)
            {
                float dst = GetDistance(node, neighbor);
                if (dst < minDst)
                {
                    minDst = dst;
                    bestNode = neighbor;
                }
            }
        }
        return bestNode; // Returns null if no walkable neighbors found
    }

    // Reconstructs the path by working backwards from the end node.
    private List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        path.Reverse(); // The path is backwards, so reverse it.
        return path;
    }

    // Calculates the distance between two nodes for G and H costs.
    // This heuristic is for a grid allowing diagonal movement.
    private int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        // 14 is the cost of a diagonal move, 10 is the cost of a cardinal move.
        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }
}