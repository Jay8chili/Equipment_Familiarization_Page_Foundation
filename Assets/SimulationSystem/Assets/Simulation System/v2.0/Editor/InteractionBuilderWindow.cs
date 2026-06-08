// ════════════════════════════════════════════════════════════════════════════
//  InteractionBuilderWindow.cs
//  Place inside any Editor/ folder in your project.
//
//  Menu: Tools → Interaction Builder
//
//  Builds DetectInteraction, GazeInteraction, and GrabInteraction GameObjects
//  using the exact same mesh / collider / visual-overlay pipeline as the
//  SimulationStepWizard — Box or Voxel collider modes, combined visual mesh
//  overlay, kinematic Rigidbody, and UniformScaleCompensator auto-sizing.
//
//  Global refs (assigned once at the top, shared by all entries):
//    • Detect Prefab, Gaze Prefab, Grab Prefab
//    • Left / Right Index Finger (Detect With Hand mode)
//
//  For Detect: also populates ObjectsToBeDetectedList.
//  For Gaze:   also wires an optional Ray Origin transform.
//  For Grab:   duplicates the mesh source and parents it as a grandchild,
//              identical to the Wizard's Grab path.
// ════════════════════════════════════════════════════════════════════════════

using SimulationSystem.V02.StateInteractions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationSystem.V02.Editor
{
    public class InteractionBuilderWindow : EditorWindow
    {
        // ── Constants ─────────────────────────────────────────────────────────
        private const string WINDOW_TITLE = "Interaction Builder";
        private const float ICON_SIZE_MIN = 0.2f;
        private const float ICON_SIZE_MAX = 1.5f;
        private const float ICON_SIZE_COEFF = 0.45f;
        private const string VISUAL_PREFIX = "__GeneratedVisual_";

        // ── Interaction type ──────────────────────────────────────────────────
        private enum EntryType { Detect, Gaze, Grab }

        // ══════════════════════════════════════════════════════════════════════
        //  GLOBAL REFS  (assigned once, shared by all entries of that type)
        // ══════════════════════════════════════════════════════════════════════

        // Prefabs
        private GameObject _detectPrefab;
        private GameObject _gazePrefab;
        private GameObject _grabPrefab;

        // Parent transforms
        private Transform _detectParent;
        private Transform _gazeParent;
        private Transform _grabParent;

        // Hand fingers — used by any Detect With Hand entry
        private GameObject _leftIndexFinger;
        private GameObject _rightIndexFinger;

        // Shell offset — extrudes visual mesh vertices along welded normals
        private float _shellOffset = 0.005f;

        // Foldout state for the global refs section
        private bool _globalFoldout = true;

        // ── Per-entry data ────────────────────────────────────────────────────
        [System.Serializable]
        private class BuilderEntry
        {
            public string label = "Entry";
            public bool expanded = true;
            public EntryType type = EntryType.Detect;

            // Mesh / position source
            public bool separateSources = false;
            public GameObject combinedSource;
            public GameObject meshSource;
            public GameObject positionSource;

            // Collider
            public SimToolColliderMode colliderMode = SimToolColliderMode.Box;
            public SimToolVoxelSettings voxelSettings = new SimToolVoxelSettings();
            public bool voxelFoldout = false;

            // Detect only
            public List<GameObject> detectObjects = new List<GameObject>();
            public bool detectObjectsGrabbable = true;
            public DetectInteractionMode detectMode = DetectInteractionMode.DetectWithGrab;

            // Gaze only
            public Transform gazeRayOrigin;


        }

        // ── Window state ──────────────────────────────────────────────────────
        private List<BuilderEntry> _entries = new List<BuilderEntry>();
        private Vector2 _scrollPos;

        // Scene-pick state
        private bool _pickFromScene = false;
        private int _pickEntryIndex = -1;
        private enum PickTarget { Combined, Mesh, Position }
        private PickTarget _pickTarget;

        // Colours
        private static readonly Color COL_PICK_ACTIVE = new Color(0.3f, 0.7f, 1f);
        private static readonly Color COL_BUILD = new Color(0.4f, 0.9f, 0.5f);
        private static readonly Color COL_REMOVE = new Color(1f, 0.4f, 0.4f);
        private static readonly Color COL_SECTION = new Color(0.18f, 0.18f, 0.18f, 0.55f);
        private static readonly Color COL_GLOBALS_BG = new Color(0.15f, 0.15f, 0.25f, 0.6f);

        // Header style (lazy)
        private GUIStyle _headerStyle;

        // ── Menu item ─────────────────────────────────────────────────────────
        [MenuItem("Tools/Interaction Builder")]
        public static void Open() =>
            GetWindow<InteractionBuilderWindow>(WINDOW_TITLE).minSize = new Vector2(420, 560);

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        private void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; _pickFromScene = false; }

        // ══════════════════════════════════════════════════════════════════════
        //  MAIN GUI
        // ══════════════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            BuildStyles();

            // Window header
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = COL_SECTION;
            GUILayout.Box(WINDOW_TITLE, _headerStyle, GUILayout.ExpandWidth(true), GUILayout.Height(28));
            GUI.backgroundColor = prev;
            EditorGUILayout.Space(6);

            // ── Global refs ───────────────────────────────────────────────────
            DrawGlobalRefs();

            EditorGUILayout.Space(4);

            // ── Entries ───────────────────────────────────────────────────────
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            for (int i = 0; i < _entries.Count; i++)
                DrawEntry(i);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);

            // Add buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Detect", GUILayout.Height(26))) AddEntry(EntryType.Detect);
            if (GUILayout.Button("+ Gaze", GUILayout.Height(26))) AddEntry(EntryType.Gaze);
            if (GUILayout.Button("+ Grab", GUILayout.Height(26))) AddEntry(EntryType.Grab);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Build All
            GUI.backgroundColor = COL_BUILD;
            if (GUILayout.Button("Build All", GUILayout.Height(32))) BuildAll();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
        }

        // ── Global refs section ───────────────────────────────────────────────

        private void DrawGlobalRefs()
        {
            var prevBG = GUI.backgroundColor;
            GUI.backgroundColor = COL_GLOBALS_BG;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBG;

            _globalFoldout = EditorGUILayout.Foldout(_globalFoldout, "Global References", true, EditorStyles.boldLabel);

            if (_globalFoldout)
            {
                EditorGUILayout.Space(2);

                DrawSectionLabel("Prefabs");
                _detectPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Detect Prefab", "Prefab instantiated as the root for every Detect entry. Leave empty to create a bare GameObject."),
                    _detectPrefab, typeof(GameObject), false);
                _gazePrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Gaze Prefab", "Prefab instantiated as the root for every Gaze entry. Leave empty to create a bare GameObject."),
                    _gazePrefab, typeof(GameObject), false);
                _grabPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Grab Prefab", "Prefab instantiated for every Grab entry when no Target Object is set. Leave empty to create a bare GameObject."),
                    _grabPrefab, typeof(GameObject), false);

                EditorGUILayout.Space(4);

                EditorGUILayout.Space(4);

                DrawSectionLabel("Parent Transforms");
                _detectParent = (Transform)EditorGUILayout.ObjectField(
                    new GUIContent("Detect Parent", "Scene transform that DetectInteraction GameObjects will be parented under."),
                    _detectParent, typeof(Transform), true);
                _gazeParent = (Transform)EditorGUILayout.ObjectField(
                    new GUIContent("Gaze Parent", "Scene transform that GazeInteraction GameObjects will be parented under."),
                    _gazeParent, typeof(Transform), true);
                _grabParent = (Transform)EditorGUILayout.ObjectField(
                    new GUIContent("Grab Parent", "Scene transform that GrabInteraction GameObjects will be parented under."),
                    _grabParent, typeof(Transform), true);

                EditorGUILayout.Space(4);

                DrawSectionLabel("Hand Fingers  (Detect With Hand mode)");
                _leftIndexFinger = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Left Index Finger", "Left hand index finger GameObject (with SphereCollider). Used when Detect Mode is 'Detect With Hand'."),
                    _leftIndexFinger, typeof(GameObject), true);
                _rightIndexFinger = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Right Index Finger", "Right hand index finger GameObject (with SphereCollider). Used when Detect Mode is 'Detect With Hand'."),
                    _rightIndexFinger, typeof(GameObject), true);

                if (_leftIndexFinger == null || _rightIndexFinger == null)
                    EditorGUILayout.HelpBox(
                        "Assign both finger objects to enable Detect With Hand mode.",
                        MessageType.Info);

                EditorGUILayout.Space(4);
                DrawSectionLabel("Detect / Gaze Zone Offset");
                _shellOffset = EditorGUILayout.Slider(
                    new GUIContent("Shell Offset",
                        "Extrudes the Detect/Gaze visual mesh vertices outward along their welded normals " +
                        "by this world-unit amount — eliminates z-fighting for any mesh shape."),
                    _shellOffset, 0f, 0.05f);
                EditorGUILayout.LabelField(
                    $"Zone mesh offset: {_shellOffset * 100f:F2} cm outside source mesh.",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void AddEntry(EntryType t)
        {
            int n = _entries.Count + 1;
            _entries.Add(new BuilderEntry { label = $"{t} {n}", type = t });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ENTRY DRAWER
        // ══════════════════════════════════════════════════════════════════════

        private void DrawEntry(int i)
        {
            var e = _entries[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── Header row ────────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            // Minimize / expand toggle
            if (GUILayout.Button(e.expanded ? "▼" : "▶", GUILayout.Width(22), GUILayout.Height(18)))
                e.expanded = !e.expanded;

            // Editable name + fixed type badge (type is set at creation, not changeable)
            e.label = EditorGUILayout.TextField(e.label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"[{e.type}]", EditorStyles.miniLabel, GUILayout.Width(58));
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = COL_BUILD;
            if (GUILayout.Button("Build", GUILayout.Width(52), GUILayout.Height(18)))
            {
                string err = Validate(e);
                if (err != null) EditorUtility.DisplayDialog("Validation Error", err, "OK");
                else Build(e);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = COL_REMOVE;
            if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
            {
                _entries.RemoveAt(i);
                if (_pickFromScene && _pickEntryIndex == i) _pickFromScene = false;
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            if (!e.expanded) { EditorGUILayout.EndVertical(); EditorGUILayout.Space(2); return; }

            EditorGUILayout.Space(4);

            // Type-specific layout — each method owns its own field order matching the wizard
            switch (e.type)
            {
                case EntryType.Detect: DrawDetectFields(i, e); break;
                case EntryType.Gaze: DrawGazeFields(i, e); break;
                case EntryType.Grab: DrawGrabFields(i, e); break;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        // ── Shared: Mesh & Position Source + Collider Generation ─────────────
        // Called by all three type-specific draw methods — always drawn last,
        // matching the wizard's field order per type.
        private void DrawSourceAndCollider(int entryIndex, BuilderEntry e)
        {
            GUILayout.Space(6);
            DrawSectionLabel("Mesh & Position Source");
            e.separateSources = EditorGUILayout.Toggle(
                new GUIContent("Separate Mesh / Position",
                    "Enable to use two different scene objects: one for the collider shape (mesh) " +
                    "and one for the world position where the interaction is placed.\n\n" +
                    "Disable to use a single scene object for both."),
                e.separateSources);
            GUILayout.Space(4);

            if (!e.separateSources)
            {
                DrawPickRow(entryIndex, "Source Object",
                    "Scene object whose mesh defines the collider shape AND whose position places the interaction.",
                    ref e.combinedSource, PickTarget.Combined);
                if (IsPickingFor(entryIndex, PickTarget.Combined))
                    EditorGUILayout.HelpBox("Click an object in the Scene View.", MessageType.Warning);
            }
            else
            {
                DrawPickRow(entryIndex, "Mesh Source",
                    "Scene object whose mesh shapes the collider. Position is ignored.",
                    ref e.meshSource, PickTarget.Mesh);
                DrawPickRow(entryIndex, "Position Source",
                    "Scene object whose world position places the interaction. Mesh is ignored.",
                    ref e.positionSource, PickTarget.Position);
                if (_pickFromScene && _pickEntryIndex == entryIndex)
                    EditorGUILayout.HelpBox(
                        $"Click an object in the Scene View to set {_pickTarget}.",
                        MessageType.Warning);
            }

            GUILayout.Space(6);
            DrawSectionLabel("Collider Generation");
            e.colliderMode = (SimToolColliderMode)EditorGUILayout.EnumPopup(
                new GUIContent("Collider Mode",
                    "Box: single BoxCollider sized from the source mesh bounds. Fast and simple.\n" +
                    "Voxel: compound BoxColliders generated by voxelizing the mesh. More accurate for complex shapes."),
                e.colliderMode);

            if (e.colliderMode == SimToolColliderMode.Voxel)
            {
                EditorGUI.indentLevel++;
                e.voxelSettings.resolution = EditorGUILayout.IntSlider(
                    new GUIContent("Resolution", "Voxel grid divisions along the longest axis. Higher = more accurate but more colliders."),
                    e.voxelSettings.resolution, 4, 64);
                e.voxelSettings.hollow = EditorGUILayout.Toggle(
                    new GUIContent("Hollow (Surface Only)", "Keep only surface voxels — dramatically reduces collider count."),
                    e.voxelSettings.hollow);
                e.voxelSettings.mergeBoxes = EditorGUILayout.Toggle(
                    new GUIContent("Merge Adjacent Boxes", "Greedy-merge neighbouring voxels into larger boxes to reduce total count."),
                    e.voxelSettings.mergeBoxes);
                e.voxelSettings.skinPadding = EditorGUILayout.Slider(
                    new GUIContent("Skin Padding", "Extra size added to each box to close tiny gaps between voxels."),
                    e.voxelSettings.skinPadding, 0f, 0.05f);
                EditorGUI.indentLevel--;
            }
        }

        // ── Detect fields  (wizard order: Mode → Objects → Source → Collider) ─
        private void DrawDetectFields(int entryIndex, BuilderEntry e)
        {
            DrawSectionLabel("Detect Mode");
            e.detectMode = (DetectInteractionMode)EditorGUILayout.EnumPopup(
                new GUIContent("Detect Mode",
                    "Detect With Grab: player grabs objects and brings them into the zone.\n\n" +
                    "Detect With Hand: player's index fingers are the detect objects."),
                e.detectMode);
            GUILayout.Space(4);

            if (e.detectMode == DetectInteractionMode.DetectWithGrab)
            {
                DrawSectionLabel("Objects To Detect");
                EditorGUILayout.HelpBox(
                    "Scene objects the player must bring into this trigger zone.",
                    MessageType.None);

                int removeObj = -1;
                for (int j = 0; j < e.detectObjects.Count; j++)
                {
                    EditorGUILayout.BeginHorizontal();
                    e.detectObjects[j] = (GameObject)EditorGUILayout.ObjectField(
                        new GUIContent($"  [{j}]", "A scene object the player must carry into the detect zone."),
                        e.detectObjects[j], typeof(GameObject), true);
                    if (GUILayout.Button(new GUIContent("✕", "Remove this object."), GUILayout.Width(22)))
                        removeObj = j;
                    EditorGUILayout.EndHorizontal();
                }
                if (removeObj >= 0) { e.detectObjects.RemoveAt(removeObj); GUIUtility.ExitGUI(); }
                if (GUILayout.Button(new GUIContent("  + Add Object To Detect", "Add a scene object to the detect list.")))
                    e.detectObjects.Add(null);

                GUILayout.Space(4);
                e.detectObjectsGrabbable = EditorGUILayout.Toggle(
                    new GUIContent("Objects Are Grabbable",
                        "When ticked, each Object To Detect is also set up as a GrabInteraction " +
                        "using the Grab Prefab from Global References."),
                    e.detectObjectsGrabbable);
            }
            else
            {
                bool fingersOk = _leftIndexFinger != null && _rightIndexFinger != null;
                EditorGUILayout.HelpBox(
                    fingersOk
                        ? $"✔  Left: {_leftIndexFinger.name}   Right: {_rightIndexFinger.name}\n" +
                          "Both fingers will be added to ObjectsToBeDetectedList on build."
                        : "⚠  Assign both index finger objects in Global References above.",
                    fingersOk ? MessageType.Info : MessageType.Warning);
            }

            DrawSourceAndCollider(entryIndex, e);
        }

        // ── Gaze fields  (wizard order: Ray Origin → Source → Collider) ───────
        private void DrawGazeFields(int entryIndex, BuilderEntry e)
        {
            EditorGUILayout.HelpBox(
                "Gaze detects the object this component lives on (no list needed).",
                MessageType.None);
            GUILayout.Space(4);

            DrawSectionLabel("Ray Origin");
            e.gazeRayOrigin = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Ray Origin Transform",
                    "Leave empty to use Camera.main at runtime."),
                e.gazeRayOrigin, typeof(Transform), true);

            DrawSourceAndCollider(entryIndex, e);
        }

        // ── Grab fields  (wizard order: Source → Collider, nothing else) ──────
        private void DrawGrabFields(int entryIndex, BuilderEntry e)
        {
            DrawSourceAndCollider(entryIndex, e);
        }

        // ── Scene pick row ────────────────────────────────────────────────────
        private void DrawPickRow(int entryIndex, string label,
                                  ref GameObject field, PickTarget target) =>
            DrawPickRow(entryIndex, label, null, ref field, target);

        private void DrawPickRow(int entryIndex, string label, string tooltip,
                                  ref GameObject field, PickTarget target)
        {
            EditorGUILayout.BeginHorizontal();
            var content = string.IsNullOrEmpty(tooltip) ? new GUIContent(label) : new GUIContent(label, tooltip);
            field = (GameObject)EditorGUILayout.ObjectField(content, field, typeof(GameObject), true);
            bool active = IsPickingFor(entryIndex, target);
            GUI.backgroundColor = active ? COL_PICK_ACTIVE : Color.white;
            if (GUILayout.Button(active ? "Cancel" : "Pick", GUILayout.Width(54)))
            {
                if (active) _pickFromScene = false;
                else { _pickFromScene = true; _pickEntryIndex = entryIndex; _pickTarget = target; }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private bool IsPickingFor(int idx, PickTarget t) =>
            _pickFromScene && _pickEntryIndex == idx && _pickTarget == t;

        // ══════════════════════════════════════════════════════════════════════
        //  SCENE PICKING
        // ══════════════════════════════════════════════════════════════════════

        private void OnSceneGUI(SceneView sv)
        {
            if (!_pickFromScene) return;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Event ev = Event.current;

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
                GameObject picked = Physics.Raycast(ray, out RaycastHit hit)
                    ? hit.collider.gameObject
                    : HandleUtility.PickGameObject(ev.mousePosition, false);

                if (picked != null && _pickEntryIndex < _entries.Count)
                {
                    var e = _entries[_pickEntryIndex];
                    if (!e.separateSources || _pickTarget == PickTarget.Combined)
                        e.combinedSource = picked;
                    else if (_pickTarget == PickTarget.Mesh)
                        e.meshSource = picked;
                    else
                        e.positionSource = picked;

                    _pickFromScene = false;
                    Repaint();
                    ev.Use();
                }
            }

            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                _pickFromScene = false;
                Repaint();
                ev.Use();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  VALIDATION
        // ══════════════════════════════════════════════════════════════════════

        private string Validate(BuilderEntry e)
        {
            if (!e.separateSources && e.combinedSource == null)
                return "Assign a Source Object (or pick one from the scene).";
            if (e.separateSources && e.meshSource == null && e.positionSource != null)
                return "Mesh Source is empty. Assign a Mesh Source or disable Separate Mesh / Position.";
            if (e.separateSources && e.meshSource == null)
                return "Assign a Mesh Source.";
            if (e.separateSources && e.positionSource == null)
                return "Assign a Position Source.";

            if (e.type == EntryType.Detect &&
                e.detectMode == DetectInteractionMode.DetectWithHand)
            {
                if (_leftIndexFinger == null || _rightIndexFinger == null)
                    return "Assign both Left and Right Index Finger in Global References for Detect With Hand mode.";
            }

            if (e.type == EntryType.Detect &&
                e.detectMode == DetectInteractionMode.DetectWithGrab)
            {
                // Empty OTD list is allowed — detect zone builds without wiring any objects.
                // Only reject null slots inside a non-empty list.
                for (int i = 0; i < e.detectObjects.Count; i++)
                    if (e.detectObjects[i] == null)
                        return $"Objects To Detect slot [{i}] is empty — remove the slot or assign an object.";
            }

            return null;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD ALL / BUILD SINGLE
        // ══════════════════════════════════════════════════════════════════════

        private void BuildAll()
        {
            int built = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                string err = Validate(_entries[i]);
                if (err != null)
                { Debug.LogWarning($"[Interaction Builder] Entry '{_entries[i].label}' skipped: {err}"); continue; }
                Build(_entries[i]);
                built++;
            }
            Debug.Log($"[Interaction Builder] Built {built}/{_entries.Count} interaction(s).");
        }

        private void Build(BuilderEntry e)
        {
            switch (e.type)
            {
                case EntryType.Detect: BuildDetect(e); break;
                case EntryType.Gaze: BuildGaze(e); break;
                case EntryType.Grab: BuildGrab(e); break;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD — DETECT
        // ══════════════════════════════════════════════════════════════════════

        // ── BuildGrabForOTD ───────────────────────────────────────────────────
        // Creates a GrabInteraction for a single OTD object using the same pipeline
        // as BuildGrab. The OTD object itself is the mesh source and position source.
        // Returns the grab GO, or null on failure.
        private GameObject BuildGrabForOTD(GameObject otdObj)
        {
            // Instantiate grab prefab or bare GO
            GameObject go;
            if (_grabPrefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(_grabPrefab);
                Undo.RegisterCreatedObjectUndo(go, "Create Grab GO");
                go.name = "Grab_" + otdObj.name;
            }
            else
            {
                go = new GameObject("Grab_" + otdObj.name);
                Undo.RegisterCreatedObjectUndo(go, "Create Grab GO");
                Debug.LogWarning("[Interaction Builder] No Grab Prefab in Global References — bare GameObject created for OTD grab.");
            }

            // Parent under grabParent
            ApplyParent(go, _grabParent, "Grab");

            // Position / rotation from the OTD object
            go.transform.position = otdObj.transform.position;
            go.transform.rotation = otdObj.transform.rotation;

            // Duplicate mesh source
            GameObject meshCopy = null;
            if (!PrefabUtility.IsPartOfPrefabAsset(otdObj))
            {
                Vector3 originalWorldScale = otdObj.transform.lossyScale;
                meshCopy = Object.Instantiate(otdObj);
                Undo.RegisterCreatedObjectUndo(meshCopy, "Duplicate Mesh Source");
                meshCopy.name = otdObj.name;
                Undo.RecordObject(otdObj, "Rename & Disable Original");
                otdObj.name = $"UsedIn_Grab_{otdObj.name}";
                otdObj.SetActive(false);
                meshCopy.transform.localScale = originalWorldScale;
            }
            else
            {
                meshCopy = otdObj;
            }

            ParentMeshUnderGrabInteraction(go, meshCopy);

            ApplyColliderFromMeshSource(go, meshCopy,
                SimToolColliderMode.Box, new SimToolVoxelSettings(),
                cleanAllBoxColliders: true, forceSolidVoxels: true);

            AddKinematicRigidbody(go);

            var grab = go.GetComponent<GrabInteraction>() ?? go.AddComponent<GrabInteraction>();
            if (grab == null)
            {
                Debug.LogError($"[Interaction Builder] AddComponent<GrabInteraction> failed on '{go.name}'.");
                return null;
            }

            EditorUtility.SetDirty(go);
            Debug.Log($"[Interaction Builder] Built OTD Grab '{go.name}' for '{otdObj.name}'.");
            return go;
        }

        private static void ApplyParent(GameObject go, Transform parent, string type)
        {
            if (parent == null) return;
            if (PrefabUtility.IsPartOfPrefabAsset(parent.gameObject))
            {
                Debug.LogWarning(
                    $"[Interaction Builder] {type} Parent '{parent.name}' is a prefab asset — "
                    + "parenting skipped. Assign a scene instance as the parent instead.");
                return;
            }
            go.transform.SetParent(parent, worldPositionStays: false);
        }

        private void BuildDetect(BuilderEntry e)
        {
            GameObject meshObj = e.separateSources ? e.meshSource : e.combinedSource;
            GameObject positionObj = e.separateSources ? e.positionSource : e.combinedSource;

            // 1 — Instantiate / resolve GO using global detect prefab
            GameObject go = InstantiateOrCreate(_detectPrefab, "Detect_" + meshObj.name, "Create Detect GO");

            // 2 — Parent under detectParent
            ApplyParent(go, _detectParent, "Detect");

            // 3 — Position
            go.transform.position = positionObj.transform.position;

            // 4 — Collider
            ApplyColliderFromMeshSource(go, meshObj, e.colliderMode, e.voxelSettings);

            // 4b — Visual overlay
            AddMeshVisualOverlay(go, meshObj, _detectPrefab, e.label, _shellOffset);

            // 5 — Auto-size icon compensator on first child
            { var mf = go.GetComponent<MeshFilter>(); AutoSizeIconCompensator(go, mf != null ? mf.sharedMesh : null, 0); }

            // 6 — Kinematic Rigidbody
            AddKinematicRigidbody(go);

            // 7 — DetectInteraction component
            var detect = go.GetComponent<DetectInteraction>()
                      ?? go.GetComponentInChildren<DetectInteraction>(includeInactive: true)
                      ?? go.AddComponent<DetectInteraction>();

            // 8 — Populate ObjectsToBeDetectedList
            var so = new SerializedObject(detect);
            var list = so.FindProperty("ObjectsToBeDetectedList");
            list.ClearArray();

            if (e.detectMode == DetectInteractionMode.DetectWithHand)
            {
                // Use global finger refs
                var fingers = new List<GameObject> { _leftIndexFinger, _rightIndexFinger };
                int slot = 0;
                foreach (var f in fingers)
                {
                    if (f == null) continue;
                    list.InsertArrayElementAtIndex(slot);
                    list.GetArrayElementAtIndex(slot).objectReferenceValue = f;
                    slot++;
                }
            }
            else
            {
                // For each OTD entry: resolve or build a GrabInteraction, then
                // add that grab GO to ObjectsToBeDetectedList.
                int slot = 0;
                for (int i = 0; i < e.detectObjects.Count; i++)
                {
                    GameObject otdObj = e.detectObjects[i];
                    if (otdObj == null) continue;

                    GameObject grabGO = otdObj;

                    if (e.detectObjectsGrabbable)
                    {
                        // Check if a GrabInteraction already exists on or in the object.
                        var existingGrab = otdObj.GetComponent<GrabInteraction>()
                                        ?? otdObj.GetComponentInChildren<GrabInteraction>(includeInactive: true)
                                        ?? otdObj.GetComponentInParent<GrabInteraction>();

                        if (existingGrab != null)
                        {
                            // Already has a grab — just reference its GO, don't create another.
                            grabGO = existingGrab.gameObject;
                            Debug.Log($"[Interaction Builder] OTD '{otdObj.name}': existing GrabInteraction found on "
                                      + $"'{grabGO.name}' — reusing, not creating a new one.");
                        }
                        else
                        {
                            // Build a fresh GrabInteraction for this OTD object.
                            grabGO = BuildGrabForOTD(otdObj);
                            if (grabGO == null)
                            {
                                Debug.LogWarning($"[Interaction Builder] OTD '{otdObj.name}': failed to build GrabInteraction — skipping slot.");
                                continue;
                            }
                        }
                    }

                    list.InsertArrayElementAtIndex(slot);
                    list.GetArrayElementAtIndex(slot).objectReferenceValue = grabGO;
                    slot++;
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(detect);

            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            Debug.Log($"[Interaction Builder] Built Detect '{go.name}' — mesh: '{meshObj.name}', position: '{positionObj.name}'.");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD — GAZE
        // ══════════════════════════════════════════════════════════════════════

        private void BuildGaze(BuilderEntry e)
        {
            GameObject meshObj = e.separateSources ? e.meshSource : e.combinedSource;
            GameObject positionObj = e.separateSources ? e.positionSource : e.combinedSource;

            // 1 — Instantiate / resolve GO using global gaze prefab
            GameObject go = InstantiateOrCreate(_gazePrefab, "Gaze_" + meshObj.name, "Create Gaze GO");

            // 2 — Parent under gazeParent
            ApplyParent(go, _gazeParent, "Gaze");

            // 3 — Position
            go.transform.position = positionObj.transform.position;

            // 4 — Collider
            ApplyColliderFromMeshSource(go, meshObj, e.colliderMode, e.voxelSettings);

            // 4b — Visual overlay
            AddMeshVisualOverlay(go, meshObj, _gazePrefab, e.label, _shellOffset);

            // 5 — Auto-size icon compensator on first child
            { var mf = go.GetComponent<MeshFilter>(); AutoSizeIconCompensator(go, mf != null ? mf.sharedMesh : null, 0); }

            // 6 — GazeInteraction component
            var gaze = go.GetComponent<GazeInteraction>()
                    ?? go.GetComponentInChildren<GazeInteraction>(includeInactive: true)
                    ?? go.AddComponent<GazeInteraction>();

            // 8 — Ray origin (optional, per-entry)
            if (e.gazeRayOrigin != null)
            {
                var so = new SerializedObject(gaze);
                var ray = so.FindProperty("objectToCastRay");
                if (ray != null) { ray.objectReferenceValue = e.gazeRayOrigin; so.ApplyModifiedProperties(); }
            }

            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            Debug.Log($"[Interaction Builder] Built Gaze '{go.name}' — mesh: '{meshObj.name}', position: '{positionObj.name}'.");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD — GRAB
        // ══════════════════════════════════════════════════════════════════════

        private void BuildGrab(BuilderEntry e)
        {
            GameObject meshObj = e.separateSources ? e.meshSource : e.combinedSource;
            GameObject positionObj = e.separateSources ? e.positionSource : e.combinedSource;

            // 1 — Resolve GO: global grab prefab → bare GO
            GameObject go;
            if (_grabPrefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(_grabPrefab);
                Undo.RegisterCreatedObjectUndo(go, "Create Grab GO");
                go.name = "Grab_" + meshObj.name;
            }
            else
            {
                go = new GameObject("Grab_" + meshObj.name);
                Undo.RegisterCreatedObjectUndo(go, "Create Grab GO");
                Debug.LogWarning("[Interaction Builder] No Grab Prefab in Global References — a bare GameObject was created.");
            }

            // 2 — Parent under grabParent
            ApplyParent(go, _grabParent, "Grab");

            // 3 — Position / rotation
            go.transform.position = positionObj.transform.position;
            go.transform.rotation = positionObj.transform.rotation;

            // 3 — Duplicate mesh source (rename & disable original)
            GameObject meshCopy = null;
            if (meshObj != null && !PrefabUtility.IsPartOfPrefabAsset(meshObj))
            {
                Vector3 originalWorldScale = meshObj.transform.lossyScale;
                meshCopy = Object.Instantiate(meshObj);
                Undo.RegisterCreatedObjectUndo(meshCopy, "Duplicate Mesh Source");
                meshCopy.name = meshObj.name;

                Undo.RecordObject(meshObj, "Rename & Disable Original Mesh Source");
                meshObj.name = $"UsedIn_Grab_{meshObj.name}";
                meshObj.SetActive(false);
                meshCopy.transform.localScale = originalWorldScale;
            }
            else
            {
                meshCopy = meshObj;
            }

            // 4 — Parent copy as grandchild FIRST (so collider sizing is correct)
            ParentMeshUnderGrabInteraction(go, meshCopy);

            // 5 — Collider sized from the now-placed copy
            ApplyColliderFromMeshSource(go, meshCopy, e.colliderMode, e.voxelSettings,
                cleanAllBoxColliders: true, forceSolidVoxels: true);

            // 6 — Auto-size icon compensator on SECOND child (Grab structure)
            {
                Mesh visualMesh = null;
                if (meshCopy != null)
                {
                    var mfCopy = meshCopy.GetComponentInChildren<MeshFilter>(includeInactive: true);
                    if (mfCopy != null) visualMesh = mfCopy.sharedMesh;
                }
                AutoSizeIconCompensator(go, visualMesh, childIndex: 1);
            }

            // 7 — Kinematic Rigidbody
            AddKinematicRigidbody(go);

            // 8 — GrabInteraction component
            var grab = go.GetComponent<GrabInteraction>();
            if (grab == null)
            {
                grab = go.AddComponent<GrabInteraction>();
                if (grab == null)
                {
                    Debug.LogError($"[Interaction Builder] AddComponent<GrabInteraction> failed on '{go.name}'. " +
                                   "Ensure the object is a scene instance, not a prefab asset.");
                    return;
                }
            }

            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            Debug.Log($"[Interaction Builder] Built Grab '{go.name}' — mesh: '{meshObj.name}', position: '{positionObj.name}'.");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SHARED BUILD HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static GameObject InstantiateOrCreate(
            GameObject prefab, string fallbackName, string undoLabel)
        {
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(go, undoLabel);
                go.name = fallbackName;
            }
            else
            {
                go = new GameObject(fallbackName);
                Undo.RegisterCreatedObjectUndo(go, undoLabel);
                Debug.LogWarning($"[Interaction Builder] No prefab assigned — bare GameObject '{fallbackName}' created.");
            }
            return go;
        }

        private static List<(Mesh mesh, Transform transform)> CollectMeshSources(
            GameObject meshObj, out bool fromChildren)
        {
            var results = new List<(Mesh, Transform)>();
            fromChildren = false;
            if (meshObj == null) return results;

            var rootMF = meshObj.GetComponent<MeshFilter>();
            if (rootMF != null && rootMF.sharedMesh != null)
            { results.Add((rootMF.sharedMesh, meshObj.transform)); return results; }

            var rootSMR = meshObj.GetComponent<SkinnedMeshRenderer>();
            if (rootSMR != null && rootSMR.sharedMesh != null)
            { results.Add((rootSMR.sharedMesh, meshObj.transform)); return results; }

            foreach (var mf in meshObj.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                if (mf.gameObject == meshObj || mf.sharedMesh == null) continue;
                results.Add((mf.sharedMesh, mf.transform));
            }
            foreach (var smr in meshObj.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
            {
                if (smr.gameObject == meshObj || smr.sharedMesh == null) continue;
                results.Add((smr.sharedMesh, smr.transform));
            }

            fromChildren = results.Count > 0;
            return results;
        }

        private static void ApplyColliderFromMeshSource(
            GameObject go, GameObject meshObj,
            SimToolColliderMode mode, SimToolVoxelSettings v,
            bool cleanAllBoxColliders = false, bool forceSolidVoxels = false)
        {
            var sources = CollectMeshSources(meshObj, out bool fromChildren);
            if (sources.Count == 0) { ApplyBoxFallback(go); return; }

            if (mode == SimToolColliderMode.Voxel)
            {
                foreach (var old in go.GetComponents<Collider>())
                {
                    if (old.isTrigger) { Undo.DestroyObjectImmediate(old); continue; }
                    if (cleanAllBoxColliders && old is BoxCollider) Undo.DestroyObjectImmediate(old);
                }
                ApplyVoxelColliders(go, sources, v, forceSolidVoxels);
            }
            else
            {
                BoxCollider existing = null;
                foreach (var c in go.GetComponents<Collider>())
                {
                    bool eligible = c.isTrigger || (cleanAllBoxColliders && c is BoxCollider);
                    if (!eligible) continue;
                    if (c is BoxCollider bc) { if (existing == null) existing = bc; else Undo.DestroyObjectImmediate(c); }
                    else Undo.DestroyObjectImmediate(c);
                }
                if (!fromChildren) ApplyBoxFromMesh(go, sources[0].mesh, meshObj.transform.lossyScale, existing);
                else ApplyBoxFromSources(go, sources, existing);
            }
        }

        private static void ApplyBoxFromMesh(
            GameObject go, Mesh mesh, Vector3 sourceScale, BoxCollider existing = null)
        {
            if (mesh == null) { ApplyBoxFallback(go); return; }
            Vector3 worldSize = Vector3.Scale(mesh.bounds.size, sourceScale);
            Vector3 worldCenter = mesh.bounds.center;
            Vector3 ls = go.transform.lossyScale;
            Vector3 localSize = new Vector3(
                Mathf.Approximately(ls.x, 0) ? worldSize.x : worldSize.x / ls.x,
                Mathf.Approximately(ls.y, 0) ? worldSize.y : worldSize.y / ls.y,
                Mathf.Approximately(ls.z, 0) ? worldSize.z : worldSize.z / ls.z);
            Vector3 localCenter = new Vector3(
                Mathf.Approximately(ls.x, 0) ? worldCenter.x : worldCenter.x * sourceScale.x / ls.x,
                Mathf.Approximately(ls.y, 0) ? worldCenter.y : worldCenter.y * sourceScale.y / ls.y,
                Mathf.Approximately(ls.z, 0) ? worldCenter.z : worldCenter.z * sourceScale.z / ls.z);
            var bc = existing ?? go.AddComponent<BoxCollider>();
            Undo.RecordObject(bc, "Resize BoxCollider");
            bc.center = localCenter; bc.size = localSize; bc.isTrigger = true;
        }

        private static void ApplyBoxFromSources(
            GameObject go, List<(Mesh mesh, Transform transform)> sources, BoxCollider existing = null)
        {
            Bounds combined = TransformBounds(sources[0].mesh.bounds, sources[0].transform);
            for (int i = 1; i < sources.Count; i++)
                combined.Encapsulate(TransformBounds(sources[i].mesh.bounds, sources[i].transform));
            Vector3 localCenter = go.transform.InverseTransformPoint(combined.center);
            Vector3 ls = go.transform.lossyScale;
            Vector3 localSize = new Vector3(
                Mathf.Approximately(ls.x, 0) ? combined.size.x : combined.size.x / ls.x,
                Mathf.Approximately(ls.y, 0) ? combined.size.y : combined.size.y / ls.y,
                Mathf.Approximately(ls.z, 0) ? combined.size.z : combined.size.z / ls.z);
            var bc = existing ?? go.AddComponent<BoxCollider>();
            Undo.RecordObject(bc, "Resize BoxCollider");
            bc.center = localCenter; bc.size = localSize; bc.isTrigger = true;
        }

        private static void ApplyBoxFallback(GameObject go)
        {
            var bc = go.GetComponent<BoxCollider>();
            if (bc == null || !bc.isTrigger) bc = go.AddComponent<BoxCollider>();
            if (bc != null) { Undo.RecordObject(bc, "Resize BoxCollider"); bc.isTrigger = true; }
        }

        private static void ApplyVoxelColliders(
            GameObject go, List<(Mesh mesh, Transform transform)> sources,
            SimToolVoxelSettings v, bool forceSolid = false)
        {
            if (sources == null || sources.Count == 0) { ApplyBoxFallback(go); return; }

            foreach (var old in go.GetComponents<BoxCollider>())
                if (old.isTrigger) Undo.DestroyObjectImmediate(old);

            Bounds worldBounds = TransformBounds(sources[0].mesh.bounds, sources[0].transform);
            for (int i = 1; i < sources.Count; i++)
                worldBounds.Encapsulate(TransformBounds(sources[i].mesh.bounds, sources[i].transform));

            float longestAxis = Mathf.Max(worldBounds.size.x, worldBounds.size.y, worldBounds.size.z);
            if (longestAxis <= 0f) { ApplyBoxFallback(go); return; }

            float voxelSize = longestAxis / v.resolution;
            int xC = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.x / voxelSize));
            int yC = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.y / voxelSize));
            int zC = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.z / voxelSize));

            bool[,,] grid = new bool[xC, yC, zC];
            Vector3 gridOrigin = worldBounds.min;

            foreach (var (mesh, tf) in sources)
            {
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 v0 = tf.TransformPoint(verts[tris[t]]);
                    Vector3 v1 = tf.TransformPoint(verts[tris[t + 1]]);
                    Vector3 v2 = tf.TransformPoint(verts[tris[t + 2]]);
                    Bounds tb = new Bounds(v0, Vector3.zero);
                    tb.Encapsulate(v1); tb.Encapsulate(v2);

                    int x0 = Mathf.Max(0, Mathf.FloorToInt((tb.min.x - gridOrigin.x) / voxelSize));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt((tb.min.y - gridOrigin.y) / voxelSize));
                    int z0 = Mathf.Max(0, Mathf.FloorToInt((tb.min.z - gridOrigin.z) / voxelSize));
                    int x1 = Mathf.Min(xC - 1, Mathf.CeilToInt((tb.max.x - gridOrigin.x) / voxelSize));
                    int y1 = Mathf.Min(yC - 1, Mathf.CeilToInt((tb.max.y - gridOrigin.y) / voxelSize));
                    int z1 = Mathf.Min(zC - 1, Mathf.CeilToInt((tb.max.z - gridOrigin.z) / voxelSize));

                    for (int x = x0; x <= x1; x++)
                        for (int y = y0; y <= y1; y++)
                            for (int z = z0; z <= z1; z++)
                                grid[x, y, z] = true;
                }
            }

            if (forceSolid)
                WizardVoxelHelper.FloodFillInterior(grid, xC, yC, zC);
            else if (v.hollow)
            {
                WizardVoxelHelper.FloodFillInterior(grid, xC, yC, zC);
                WizardVoxelHelper.HollowOut(grid, xC, yC, zC);
            }

            var boxes = v.mergeBoxes
                ? WizardVoxelHelper.GreedyMerge(grid, xC, yC, zC)
                : WizardVoxelHelper.IndividualBoxes(grid, xC, yC, zC);

            foreach (var (min, size) in boxes)
            {
                Vector3 worldCenter = gridOrigin + new Vector3(
                    (min.x + size.x * 0.5f) * voxelSize,
                    (min.y + size.y * 0.5f) * voxelSize,
                    (min.z + size.z * 0.5f) * voxelSize);
                Vector3 worldExtents = new Vector3(
                    size.x * voxelSize + v.skinPadding,
                    size.y * voxelSize + v.skinPadding,
                    size.z * voxelSize + v.skinPadding);
                Vector3 localCenter = go.transform.InverseTransformPoint(worldCenter);
                Vector3 ls = go.transform.lossyScale;
                Vector3 localSize = new Vector3(
                    Mathf.Approximately(ls.x, 0) ? worldExtents.x : worldExtents.x / ls.x,
                    Mathf.Approximately(ls.y, 0) ? worldExtents.y : worldExtents.y / ls.y,
                    Mathf.Approximately(ls.z, 0) ? worldExtents.z : worldExtents.z / ls.z);
                var bc = go.AddComponent<BoxCollider>();
                bc.center = localCenter; bc.size = localSize; bc.isTrigger = true;
            }
        }

        private static void AddMeshVisualOverlay(
            GameObject go, GameObject meshObj, GameObject materialPrefab, string label,
            float shellOffset = 0f)
        {
            if (go == null) return;

            var toRemove = new List<GameObject>();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var c = go.transform.GetChild(i);
                if (c.name.StartsWith(VISUAL_PREFIX)) toRemove.Add(c.gameObject);
            }
            foreach (var v in toRemove) Undo.DestroyObjectImmediate(v);

            if (meshObj == null) return;

            Matrix4x4 worldToGo = go.transform.worldToLocalMatrix;
            var combine = new List<CombineInstance>();

            foreach (var mf in meshObj.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = worldToGo * mf.transform.localToWorldMatrix;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                    combine.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = m });
            }
            foreach (var smr in meshObj.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
            {
                if (smr.sharedMesh == null) continue;
                var baked = new Mesh { name = $"BakedTemp_{smr.name}" };
                smr.BakeMesh(baked);
                Matrix4x4 m = worldToGo * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                for (int s = 0; s < baked.subMeshCount; s++)
                    combine.Add(new CombineInstance { mesh = baked, subMeshIndex = s, transform = m });
            }

            if (combine.Count == 0) return;

            var combined = new Mesh { name = $"CombinedVisual_{meshObj.name}", indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(combine.ToArray(), mergeSubMeshes: true, useMatrices: true);
            combined.RecalculateBounds();

            // ── Shell offset: extrude vertices along welded normals ───────────
            // Averages normals across all vertices at the same position so shared
            // edges don't tear apart — then moves each vertex outward by shellOffset.
            if (shellOffset > 0f)
            {
                combined.RecalculateNormals();
                var verts = combined.vertices;
                var normals = combined.normals;

                var normalSum = new Dictionary<Vector3, Vector3>();
                var normalCount = new Dictionary<Vector3, int>();

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 key = new Vector3(
                        Mathf.Round(verts[i].x * 10000f) / 10000f,
                        Mathf.Round(verts[i].y * 10000f) / 10000f,
                        Mathf.Round(verts[i].z * 10000f) / 10000f);

                    if (normalSum.ContainsKey(key))
                    { normalSum[key] += normals[i]; normalCount[key]++; }
                    else
                    { normalSum[key] = normals[i]; normalCount[key] = 1; }
                }

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 key = new Vector3(
                        Mathf.Round(verts[i].x * 10000f) / 10000f,
                        Mathf.Round(verts[i].y * 10000f) / 10000f,
                        Mathf.Round(verts[i].z * 10000f) / 10000f);

                    verts[i] += (normalSum[key] / normalCount[key]).normalized * shellOffset;
                }

                combined.vertices = verts;
                combined.RecalculateBounds();
            }

            var mfTarget = go.GetComponent<MeshFilter>() ?? Undo.AddComponent<MeshFilter>(go);
            Undo.RecordObject(mfTarget, "Set Visual Mesh");
            mfTarget.sharedMesh = combined;

            var mrTarget = go.GetComponent<MeshRenderer>();
            if (mrTarget == null)
            {
                mrTarget = Undo.AddComponent<MeshRenderer>(go);
                Material[] mats = GetMaterialsFromPrefab(materialPrefab);
                if (mats != null && mats.Length > 0)
                { Undo.RecordObject(mrTarget, "Set Visual Material"); mrTarget.sharedMaterials = new[] { mats[0] }; }
            }
        }

        private static void AutoSizeIconCompensator(GameObject go, Mesh visualMesh, int childIndex)
        {
            if (go == null || childIndex < 0 || childIndex >= go.transform.childCount) return;
            var comp = go.transform.GetChild(childIndex).GetComponent<UniformScaleCompensator>();
            if (comp == null) return;

            float largestWorldExtent;
            if (visualMesh != null)
            {
                Vector3 local = visualMesh.bounds.size;
                Vector3 ls = go.transform.lossyScale;
                largestWorldExtent = Mathf.Max(
                    Mathf.Abs(local.x * ls.x),
                    Mathf.Max(Mathf.Abs(local.y * ls.y), Mathf.Abs(local.z * ls.z)));
            }
            else
            {
                largestWorldExtent = ComputeLargestColliderExtent(go);
            }

            if (largestWorldExtent <= 0f || float.IsNaN(largestWorldExtent)) return;
            float final = Mathf.Clamp(Mathf.Sqrt(largestWorldExtent) * ICON_SIZE_COEFF, ICON_SIZE_MIN, ICON_SIZE_MAX);
            Undo.RecordObject(comp, "Auto-size Icon Compensator");
            comp.size = final;
            EditorUtility.SetDirty(comp);
            comp.Compensate();
        }

        private static float ComputeLargestColliderExtent(GameObject go)
        {
            if (go == null) return 0f;
            bool any = false; Bounds combined = default;
            foreach (var bc in go.GetComponents<BoxCollider>())
            {
                if (!bc.isTrigger) continue;
                Vector3 wc = go.transform.TransformPoint(bc.center);
                Vector3 ws = Vector3.Scale(bc.size, go.transform.lossyScale);
                ws = new Vector3(Mathf.Abs(ws.x), Mathf.Abs(ws.y), Mathf.Abs(ws.z));
                var b = new Bounds(wc, ws);
                if (!any) { combined = b; any = true; } else combined.Encapsulate(b);
            }
            if (!any) return 0f;
            return Mathf.Max(combined.size.x, Mathf.Max(combined.size.y, combined.size.z));
        }

        private static void AddKinematicRigidbody(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody>(go);
                if (rb == null)
                {
                    Debug.LogError($"[Interaction Builder] Could not add Rigidbody to '{go.name}'. "
                        + "Ensure the object is a scene instance, not a prefab asset.");
                    return;
                }
            }
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private static void ParentMeshUnderGrabInteraction(GameObject interactionGO, GameObject meshObj)
        {
            if (meshObj == null) return;
            if (PrefabUtility.IsPartOfPrefabAsset(meshObj))
            {
                Debug.LogWarning($"[Interaction Builder] Mesh source '{meshObj.name}' is a prefab asset — " +
                                 "cannot re-parent. Assign a scene instance as the source.");
                return;
            }

            Vector3 desiredWorldScale = meshObj.transform.lossyScale;
            Transform firstChild = null;
            for (int i = 0; i < interactionGO.transform.childCount; i++)
            {
                var c = interactionGO.transform.GetChild(i);
                if (c.gameObject != meshObj) { firstChild = c; break; }
            }

            if (firstChild == null)
            {
                var container = new GameObject("Mesh");
                Undo.RegisterCreatedObjectUndo(container, "Create Mesh Container");
                Undo.SetTransformParent(container.transform, interactionGO.transform,
                    worldPositionStays: false, "Parent mesh container");
                firstChild = container.transform;
            }

            Undo.SetTransformParent(meshObj.transform, firstChild,
                worldPositionStays: false, "Parent mesh under grab grandchild");
            meshObj.transform.position = interactionGO.transform.position;
            meshObj.transform.rotation = interactionGO.transform.rotation;

            Vector3 ps = firstChild.lossyScale;
            meshObj.transform.localScale = new Vector3(
                Mathf.Approximately(ps.x, 0f) ? desiredWorldScale.x : desiredWorldScale.x / ps.x,
                Mathf.Approximately(ps.y, 0f) ? desiredWorldScale.y : desiredWorldScale.y / ps.y,
                Mathf.Approximately(ps.z, 0f) ? desiredWorldScale.z : desiredWorldScale.z / ps.z);
        }

        private static Material[] GetMaterialsFromPrefab(GameObject prefab)
        {
            if (prefab == null) return null;
            var r = prefab.GetComponentInChildren<Renderer>(includeInactive: true);
            if (r == null) return null;
            var mats = r.sharedMaterials;
            return (mats != null && mats.Length > 0) ? mats : null;
        }

        private static Bounds TransformBounds(Bounds localBounds, Transform tf)
        {
            Vector3 c = tf.TransformPoint(localBounds.center);
            Vector3 e = localBounds.extents;
            Vector3 ax = tf.TransformDirection(new Vector3(e.x, 0, 0));
            Vector3 ay = tf.TransformDirection(new Vector3(0, e.y, 0));
            Vector3 az = tf.TransformDirection(new Vector3(0, 0, e.z));
            return new Bounds(c, new Vector3(
                (Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x)) * 2f,
                (Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y)) * 2f,
                (Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z)) * 2f));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UI HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void BuildStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private static void DrawSectionLabel(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            var r = GUILayoutUtility.GetLastRect();
            r.y += EditorGUIUtility.singleLineHeight - 1; r.height = 1;
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            EditorGUILayout.Space(2);
        }
    }
}