using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class UniformScaleCompensator : MonoBehaviour
{
    [Tooltip("Uniform size multiplier applied after compensation. Tweak this until it looks right.")]
    public float size = 0.1f;

    [ContextMenu("Compensate Scale")]
    public void Compensate()
    {
        if (transform.parent == null)
        {
            Debug.LogWarning("[UniformScaleCompensator] No parent found.");
            return;
        }

        Vector3 ps = transform.parent.lossyScale;

        float x = Mathf.Approximately(ps.x, 0f) ? 1f : 1f / ps.x;
        float y = Mathf.Approximately(ps.y, 0f) ? 1f : 1f / ps.y;
        float z = Mathf.Approximately(ps.z, 0f) ? 1f : 1f / ps.z;

#if UNITY_EDITOR
        Undo.RecordObject(transform, "Compensate Scale");
#endif
        transform.localScale = new Vector3(x, y, z) * size;

        Debug.Log($"[UniformScaleCompensator] Scale set to {transform.localScale}");
    }
}