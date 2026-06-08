using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridPathfinder : MonoBehaviour
{
    [Header("References")]
    public FloorGridGizmo grid;

    [Header("Path Settings")]
    public bool allowDiagonals = false;

    [Header("Smoothing")]
    [Range(2, 50)] public int samplesPerSegment = 12;
    public float pathYOffset = 0.05f;

    [Header("Debug / Visualization")]
    public bool drawPathGizmos = true;
    public Color pathColor = Color.yellow;
    public float gizmoRadius = 0.05f;

    [HideInInspector]
    public List<Vector3> lastSmoothPath = new List<Vector3>();

    // =========================================================
    // PUBLIC API (WORLD-SPACE PATHFINDING)
    // =========================================================

    /// <summary>
    /// Computes a smooth world-space path between two world positions.
    /// This is the ONLY API runtime systems (guide bot, AI, etc.) should use.
    /// </summary>
    public bool ComputePathWorld(
        Vector3 worldStart,
        Vector3 worldGoal,
        out List<Vector3> smoothWorldPath)
    {
        smoothWorldPath = null;
        lastSmoothPath.Clear();

        if (grid == null)
            return false;

        // Convert world positions to grid cells
        if (!grid.TryGetCellFromWorld(worldStart, out int sx, out int sy))
            return false;

        if (!grid.TryGetCellFromWorld(worldGoal, out int gx, out int gy))
            return false;

        // Reject invalid or blocked cells
        if (grid.IsCellColliding(sx, sy) || grid.IsCellColliding(gx, gy))
            return false;

        Vector2Int startCell = new Vector2Int(sx, sy);
        Vector2Int goalCell = new Vector2Int(gx, gy);

        // Run A*
        if (!TryFindPath(startCell, goalCell, out List<Vector2Int> cellPath))
            return false;

        // Build smooth world path
        smoothWorldPath = BuildSmoothWorldPath(cellPath);
        lastSmoothPath = smoothWorldPath;

        return smoothWorldPath != null && smoothWorldPath.Count > 1;
    }

    // =========================================================
    // INTERNALS
    // =========================================================

    /// <summary>
    /// Converts grid cell path into a smooth world-space spline.
    /// </summary>
    private List<Vector3> BuildSmoothWorldPath(List<Vector2Int> cells)
    {
        List<Vector3> points = new List<Vector3>(cells.Count);

        foreach (var c in cells)
        {
            points.Add(
                grid.GetCellCenter(c.x, c.y) +
                Vector3.up * pathYOffset
            );
        }

        return BuildCatmullRomPath(points, samplesPerSegment);
    }

    #region A* IMPLEMENTATION (CORE LOGIC)

    private class Node
    {
        public Vector2Int pos;
        public int g, h, f;
        public Node parent;

        public Node(Vector2Int p, int g, int h, Node parent)
        {
            pos = p;
            this.g = g;
            this.h = h;
            f = g + h;
            this.parent = parent;
        }
    }

    private bool TryFindPath(Vector2Int start, Vector2Int goal, out List<Vector2Int> path)
    {
        path = null;

        var open = new List<Node>(64);
        var lookup = new Dictionary<Vector2Int, Node>();
        var closed = new HashSet<Vector2Int>();

        Node startNode = new Node(start, 0, Heuristic(start, goal), null);
        open.Add(startNode);
        lookup[start] = startNode;

        while (open.Count > 0)
        {
            Node current = PopBest(open);
            lookup.Remove(current.pos);

            if (current.pos == goal)
            {
                path = Reconstruct(current);
                return true;
            }

            closed.Add(current.pos);

            foreach (var n in GetNeighbors(current.pos))
            {
                if (!InBounds(n)) continue;
                if (closed.Contains(n)) continue;
                if (grid.IsCellColliding(n.x, n.y)) continue;

                int cost = current.g + StepCost(current.pos, n);

                if (lookup.TryGetValue(n, out Node existing))
                {
                    if (cost < existing.g)
                    {
                        existing.g = cost;
                        existing.f = cost + existing.h;
                        existing.parent = current;
                    }
                }
                else
                {
                    Node node = new Node(n, cost, Heuristic(n, goal), current);
                    open.Add(node);
                    lookup[n] = node;
                }
            }
        }

        return false;
    }

    private IEnumerable<Vector2Int> GetNeighbors(Vector2Int c)
    {
        yield return new Vector2Int(c.x + 1, c.y);
        yield return new Vector2Int(c.x - 1, c.y);
        yield return new Vector2Int(c.x, c.y + 1);
        yield return new Vector2Int(c.x, c.y - 1);

        if (!allowDiagonals) yield break;

        yield return new Vector2Int(c.x + 1, c.y + 1);
        yield return new Vector2Int(c.x - 1, c.y - 1);
        yield return new Vector2Int(c.x + 1, c.y - 1);
        yield return new Vector2Int(c.x - 1, c.y + 1);
    }

    private int StepCost(Vector2Int a, Vector2Int b)
        => (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1) ? 10 : 14;

    private int Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);

        return allowDiagonals
            ? 14 * Mathf.Min(dx, dy) + 10 * Mathf.Abs(dx - dy)
            : (dx + dy) * 10;
    }

    private Node PopBest(List<Node> open)
    {
        int best = 0;
        for (int i = 1; i < open.Count; i++)
        {
            if (open[i].f < open[best].f)
                best = i;
        }

        Node n = open[best];
        open.RemoveAt(best);
        return n;
    }

    private List<Vector2Int> Reconstruct(Node end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        for (Node n = end; n != null; n = n.parent)
            path.Add(n.pos);

        path.Reverse();
        return path;
    }

    private bool InBounds(Vector2Int c)
        => c.x >= 0 && c.x < grid.columns &&
           c.y >= 0 && c.y < grid.rows;

    #endregion

    #region SPLINE

    private static List<Vector3> BuildCatmullRomPath(List<Vector3> pts, int samples)
    {
        List<Vector3> result = new List<Vector3>();
        if (pts == null || pts.Count < 2) return result;

        samples = Mathf.Max(2, samples);

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = (i == 0) ? pts[i] : pts[i - 1];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = (i + 2 < pts.Count) ? pts[i + 2] : p2;

            for (int s = 0; s < samples; s++)
            {
                float t = s / (float)samples;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        result.Add(pts[^1]);
        return result;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    #endregion

    // =========================================================
    // GIZMOS (EDITOR + RUNTIME)
    // =========================================================

    private void OnDrawGizmos()
    {
        if (!drawPathGizmos || lastSmoothPath == null || lastSmoothPath.Count < 2)
            return;

        Gizmos.color = pathColor;

        for (int i = 1; i < lastSmoothPath.Count; i++)
        {
            Gizmos.DrawLine(lastSmoothPath[i - 1], lastSmoothPath[i]);
            Gizmos.DrawSphere(lastSmoothPath[i], gizmoRadius);
        }
    }
}