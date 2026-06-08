using System.Collections.Generic;
using UnityEngine;

public static class PathResampler
{
    /// Resample a polyline (e.g., NavMeshPath corners) into evenly spaced points.
    public static void Resample(IReadOnlyList<Vector3> corners, float spacing, List<Vector3> outPoints)
    {
        outPoints.Clear();
        if (corners == null || corners.Count < 2 || spacing <= 0.001f) return;

        Vector3 prev = corners[0];
        outPoints.Add(prev);

        float distAcc = 0f;

        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 curr = corners[i];
            float segLen = Vector3.Distance(prev, curr);
            if (segLen < 0.0001f) continue;

            Vector3 dir = (curr - prev) / segLen;

            while (distAcc + segLen >= spacing)
            {
                float t = spacing - distAcc;
                Vector3 p = prev + dir * t;
                outPoints.Add(p);

                // move along the segment
                prev = p;
                segLen -= t;
                distAcc = 0f;
            }

            distAcc += segLen;
            prev = curr;
        }

        // Ensure final corner is included
        if (Vector3.Distance(outPoints[outPoints.Count - 1], corners[corners.Count - 1]) > 0.05f)
            outPoints.Add(corners[corners.Count - 1]);
    }
}
