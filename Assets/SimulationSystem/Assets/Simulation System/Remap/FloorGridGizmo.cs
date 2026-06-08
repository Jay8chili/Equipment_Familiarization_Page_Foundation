using UnityEngine;

[ExecuteAlways]
public class FloorGridGizmo : MonoBehaviour
{
    [Header("Grid")]
    [Min(1)] public int columns = 10;
    [Min(1)] public int rows = 10;
    [Min(0.01f)] public float cellSize = 1f;

    [Header("Origin")]
    public Vector3 originOffset = Vector3.zero;

    [Header("Ground Snap")]
    public bool snapOriginToGround = true;
    public LayerMask groundMask = ~0;
    public float groundRaycastHeight = 50f;
    public float groundRaycastDistance = 200f;

    [Header("Cell Collision (Occupancy)")]
    public LayerMask occupancyMask = ~0;
    [Min(0.001f)] public float cellTestHeight = 1f;
    [Min(0f)] public float cellTestYOffset = 0.5f;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Display")]
    public bool drawGridLines = true;
    public bool drawFilledCells = true;
    public Color lineColor = new Color(0f, 1f, 1f, 0.8f);
    public Color emptyCellColor = new Color(0f, 0.8f, 0.8f, 0.05f);
    public Color filledCellColor = new Color(1f, 0.2f, 0.2f, 0.18f);

    private static readonly Collider[] OverlapBuffer = new Collider[8];

    public Vector3 OriginWorld
    {
        get
        {
            Vector3 origin = transform.TransformPoint(originOffset);

            if (!snapOriginToGround) return origin;

            Vector3 rayStart = origin + Vector3.up * groundRaycastHeight;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRaycastDistance, groundMask))
                origin.y = hit.point.y;

            return origin;
        }
    }

    public Vector3 GetCellCenter(int col, int row)
    {
        Vector3 o = OriginWorld;
        return o + new Vector3((col + 0.5f) * cellSize, 0f, (row + 0.5f) * cellSize);
    }

    /// <summary>
    /// ✅ World → Grid conversion (used by pathfinder & bot)
    /// </summary>
    public bool TryGetCellFromWorld(Vector3 worldPos, out int col, out int row)
    {
        Vector3 o = OriginWorld;
        Vector3 local = worldPos - o;

        col = Mathf.FloorToInt(local.x / cellSize);
        row = Mathf.FloorToInt(local.z / cellSize);

        return col >= 0 && col < columns && row >= 0 && row < rows;
    }

    public bool IsCellColliding(int col, int row)
    {
        Vector3 floorCenter = GetCellCenter(col, row);
        Vector3 center = floorCenter + Vector3.up * cellTestYOffset;
        Vector3 halfExtents = new Vector3(cellSize * 0.5f, cellTestHeight * 0.5f, cellSize * 0.5f);

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            OverlapBuffer,
            Quaternion.identity,
            occupancyMask,
            triggerInteraction
        );

        return hitCount > 0;
    }

    private void OnDrawGizmos()
    {
        Vector3 o = OriginWorld;

        if (drawFilledCells)
        {
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    bool filled = IsCellColliding(c, r);
                    Gizmos.color = filled ? filledCellColor : emptyCellColor;

                    Vector3 floorCenter = GetCellCenter(c, r);
                    Vector3 cubeCenter = floorCenter + Vector3.up * cellTestYOffset;
                    Vector3 size = new Vector3(cellSize, cellTestHeight, cellSize);

                    Gizmos.DrawCube(cubeCenter, size);
                }
            }
        }

        if (drawGridLines)
        {
            Gizmos.color = lineColor;

            float width = columns * cellSize;
            float height = rows * cellSize;

            for (int c = 0; c <= columns; c++)
            {
                float x = c * cellSize;
                Gizmos.DrawLine(o + new Vector3(x, 0f, 0f), o + new Vector3(x, 0f, height));
            }

            for (int r = 0; r <= rows; r++)
            {
                float z = r * cellSize;
                Gizmos.DrawLine(o + new Vector3(0f, 0f, z), o + new Vector3(width, 0f, z));
            }
        }
    }
}