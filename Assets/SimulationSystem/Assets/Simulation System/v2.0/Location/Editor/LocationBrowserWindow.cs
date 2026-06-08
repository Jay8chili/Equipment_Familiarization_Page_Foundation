using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor window that lets you pick a location and see every
/// LocationTag-bearing GameObject that belongs to it.
/// While the window is open, matching objects are highlighted in the Scene View
/// with colored wireframe bounds and name labels.
/// Accessible via Window > Location System > Location Browser
/// or through the LocationManager inspector button.
/// </summary>
public class LocationBrowserWindow : EditorWindow
{
    private LocationData locationData;
    private int selectedLocationIndex = 0;
    private bool showAllLocations = false;
    private Vector2 scrollPos;
    private List<LocationTag> results = new List<LocationTag>();

    // ── Highlight settings ──
    private bool enableHighlight = true;
    private Color highlightColor = new Color(0f, 1f, 0.4f, 1f);
    private bool showLabels = true;
    private float wireThickness = 2f;

    [MenuItem("Window/Location System/Location Browser")]
    public static void Open()
    {
        LocationBrowserWindow window = GetWindow<LocationBrowserWindow>("Location Browser");
        window.minSize = new Vector2(320, 400);
        window.Show();
    }

    private void OnEnable()
    {
        FindLocationData();
        RefreshResults();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    private void OnHierarchyChange()
    {
        RefreshResults();
        Repaint();
    }

    private void FindLocationData()
    {
        if (locationData != null) return;

        LocationManager manager = FindFirstObjectByType<LocationManager>();
        if (manager != null && manager.locationData != null)
        {
            locationData = manager.locationData;
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:LocationData");
        if (guids.Length > 0)
            locationData = AssetDatabase.LoadAssetAtPath<LocationData>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // ──────────────────────────────────────────────
    //  Inspector GUI
    // ──────────────────────────────────────────────
    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Location Browser", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ── LocationData reference ──
        LocationData newData = (LocationData)EditorGUILayout.ObjectField(
            "Location Data", locationData, typeof(LocationData), false);

        if (newData != locationData)
        {
            locationData = newData;
            RefreshResults();
        }

        if (locationData == null)
        {
            EditorGUILayout.HelpBox("Assign or create a LocationData asset.", MessageType.Warning);
            return;
        }

        if (locationData.locations.Count == 0)
        {
            EditorGUILayout.HelpBox("No locations defined in LocationData.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4);

        // ── Show All toggle ──
        bool prevAll = showAllLocations;
        showAllLocations = EditorGUILayout.Toggle("Show All Locations", showAllLocations);
        if (showAllLocations != prevAll)
        {
            RefreshResults();
            SceneView.RepaintAll();
        }

        // ── Location dropdown ──
        if (!showAllLocations)
        {
            string[] names = locationData.locations.ToArray();
            int prevIndex = selectedLocationIndex;
            selectedLocationIndex = EditorGUILayout.Popup("Filter Location", selectedLocationIndex, names);
            if (selectedLocationIndex != prevIndex)
            {
                RefreshResults();
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.Space(6);

        // ── Highlight settings ──
        EditorGUILayout.LabelField("Scene Highlight", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;

        bool prevHighlight = enableHighlight;
        enableHighlight = EditorGUILayout.Toggle("Enable Highlight", enableHighlight);
        if (enableHighlight != prevHighlight)
            SceneView.RepaintAll();

        if (enableHighlight)
        {
            Color prevColor = highlightColor;
            highlightColor = EditorGUILayout.ColorField("Highlight Color", highlightColor);
            if (highlightColor != prevColor)
                SceneView.RepaintAll();

            showLabels = EditorGUILayout.Toggle("Show Name Labels", showLabels);
            wireThickness = EditorGUILayout.Slider("Wire Thickness", wireThickness, 1f, 6f);
        }

        EditorGUI.indentLevel--;

        EditorGUILayout.Space(6);

        if (GUILayout.Button("Refresh"))
        {
            RefreshResults();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Objects found: {results.Count}", EditorStyles.miniLabel);
        EditorGUILayout.Space(2);

        // ── Results list ──
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (LocationTag tag in results)
        {
            if (tag == null) continue;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.ObjectField(tag.gameObject, typeof(GameObject), true);

            string locLabel = tag.allLocations ? "ALL" : string.Join(", ", tag.GetLocationNames(locationData));
            GUIStyle miniRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            EditorGUILayout.LabelField(locLabel, miniRight, GUILayout.MaxWidth(200));

            if (GUILayout.Button("Select", GUILayout.Width(52)))
            {
                Selection.activeGameObject = tag.gameObject;
                EditorGUIUtility.PingObject(tag.gameObject);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    // ──────────────────────────────────────────────
    //  Scene View drawing
    // ──────────────────────────────────────────────
    private void OnSceneGUI(SceneView sceneView)
    {
        if (!enableHighlight || results == null || results.Count == 0)
            return;

        // Prepare label style once per frame.
        GUIStyle labelStyle = null;
        if (showLabels)
        {
            labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = highlightColor;
            labelStyle.fontSize = 11;
            Texture2D bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0, 0, 0, 0.55f));
            bg.Apply();
            labelStyle.normal.background = bg;
            labelStyle.padding = new RectOffset(4, 4, 2, 2);
        }

        foreach (LocationTag tag in results)
        {
            if (tag == null) continue;

            GameObject go = tag.gameObject;
            Bounds bounds = GetWorldBounds(go);

            // ── Draw wireframe box ──
            DrawWireBounds(bounds, highlightColor, wireThickness);

            // ── Draw name label above the object ──
            if (showLabels && labelStyle != null)
            {
                Vector3 labelPos = bounds.center + Vector3.up * (bounds.extents.y + 0.15f);
                Handles.Label(labelPos, go.name, labelStyle);
            }
        }
    }

    /// <summary>
    /// Draws a wireframe box for the given world-space Bounds.
    /// Falls back to a small cube at the transform position if bounds are zero.
    /// </summary>
    private static void DrawWireBounds(Bounds b, Color color, float thickness)
    {
        if (b.size == Vector3.zero)
            return;

        Vector3 c = b.center;
        Vector3 e = b.extents;

        // 8 corners
        Vector3 ftl = c + new Vector3(-e.x, e.y, e.z);
        Vector3 ftr = c + new Vector3(e.x, e.y, e.z);
        Vector3 fbl = c + new Vector3(-e.x, -e.y, e.z);
        Vector3 fbr = c + new Vector3(e.x, -e.y, e.z);
        Vector3 btl = c + new Vector3(-e.x, e.y, -e.z);
        Vector3 btr = c + new Vector3(e.x, e.y, -e.z);
        Vector3 bbl = c + new Vector3(-e.x, -e.y, -e.z);
        Vector3 bbr = c + new Vector3(e.x, -e.y, -e.z);

        Handles.color = color;

        // Front face
        Handles.DrawLine(ftl, ftr, thickness);
        Handles.DrawLine(ftr, fbr, thickness);
        Handles.DrawLine(fbr, fbl, thickness);
        Handles.DrawLine(fbl, ftl, thickness);

        // Back face
        Handles.DrawLine(btl, btr, thickness);
        Handles.DrawLine(btr, bbr, thickness);
        Handles.DrawLine(bbr, bbl, thickness);
        Handles.DrawLine(bbl, btl, thickness);

        // Connecting edges
        Handles.DrawLine(ftl, btl, thickness);
        Handles.DrawLine(ftr, btr, thickness);
        Handles.DrawLine(fbl, bbl, thickness);
        Handles.DrawLine(fbr, bbr, thickness);
    }

    /// <summary>
    /// Computes the combined world-space bounds of a GameObject's Renderers.
    /// Falls back to a small box around the transform position when no renderers exist.
    /// </summary>
    private static Bounds GetWorldBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one * 0.3f);

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        return combined;
    }

    // ──────────────────────────────────────────────
    //  Refresh logic
    // ──────────────────────────────────────────────
    private void RefreshResults()
    {
        results.Clear();

        if (locationData == null || locationData.locations.Count == 0) return;

        LocationTag[] allTags = Resources.FindObjectsOfTypeAll<LocationTag>();

        foreach (LocationTag tag in allTags)
        {
            if (!tag.gameObject.scene.isLoaded) continue;

            if (showAllLocations)
            {
                results.Add(tag);
            }
            else
            {
                string targetName = locationData.locations[selectedLocationIndex];
                if (tag.BelongsToLocation(targetName, locationData))
                    results.Add(tag);
            }
        }

        results.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name));
    }
}