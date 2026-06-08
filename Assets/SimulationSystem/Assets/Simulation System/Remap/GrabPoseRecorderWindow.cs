#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Editor window for recording, assigning, and managing hand poses across all
/// GrabInteraction objects in the scene.
///
/// PERSISTENCE
/// ───────────
/// Detector references are scene objects — Unity wipes EditorWindow fields for
/// scene objects on every domain reload (play mode enter/exit, script recompile).
/// We persist each detector slot as a GlobalObjectId string in EditorPrefs and
/// re-resolve them in OnEnable (which fires after every domain reload).
///
/// RECORDING REFERENCES (always visible — edit and play mode):
///   4 GrabPinchDetector slots:
///     • Left Hand Tracking  → pulls handPoseLock, pinchPoint from detector
///     • Right Hand Tracking → pulls handPoseLock, pinchPoint from detector
///     • Left Controller     → pulls controllerPoseLock, controllerRoot, pinchPoint
///     • Right Controller    → pulls controllerPoseLock, controllerRoot, pinchPoint
///
/// USAGE
/// ─────
/// Window → XR → Grab Pose Recorder
/// </summary>
public class GrabPoseRecorderWindow : EditorWindow
{
    // ── EditorPrefs keys ─────────────────────────────────────────────────────

    private const string SAVE_FOLDER_KEY          = "GrabPoseRecorder_SaveFolder";
    private const string DEFAULT_SAVE_FOLDER      = "Assets/RecordedPoses";

    private const string KEY_DET_HAND_LEFT        = "GrabPoseRecorder_DetHandLeft";
    private const string KEY_DET_HAND_RIGHT       = "GrabPoseRecorder_DetHandRight";
    private const string KEY_DET_CTRL_LEFT        = "GrabPoseRecorder_DetCtrlLeft";
    private const string KEY_DET_CTRL_RIGHT       = "GrabPoseRecorder_DetCtrlRight";
    private const string KEY_REFS_FOLDOUT         = "GrabPoseRecorder_RefsFoldout";

    // ── State ────────────────────────────────────────────────────────────────

    private Vector2 _scrollPos;
    private string _saveFolder;
    private GrabInteraction[] _grabbables;
    private bool[] _foldouts;
    private string _searchFilter = "";

    // ── Per-hand per-mode detector references ────────────────────────────────

    private GrabPinchDetector _detectorHandLeft;
    private GrabPinchDetector _detectorHandRight;
    private GrabPinchDetector _detectorControllerLeft;
    private GrabPinchDetector _detectorControllerRight;

    private bool _refsFoldout = true;

    // ── Menu ─────────────────────────────────────────────────────────────────

    [MenuItem("Window/XR/Grab Pose Recorder")]
    public static void Open()
    {
        var win = GetWindow<GrabPoseRecorderWindow>("Grab Pose Recorder");
        win.minSize = new Vector2(440, 320);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        _saveFolder = EditorPrefs.GetString(SAVE_FOLDER_KEY, DEFAULT_SAVE_FOLDER);
        _refsFoldout = EditorPrefs.GetBool(KEY_REFS_FOLDOUT, true);

        // Restore detector refs from EditorPrefs after domain reload.
        _detectorHandLeft        = LoadDetectorRef(KEY_DET_HAND_LEFT);
        _detectorHandRight       = LoadDetectorRef(KEY_DET_HAND_RIGHT);
        _detectorControllerLeft  = LoadDetectorRef(KEY_DET_CTRL_LEFT);
        _detectorControllerRight = LoadDetectorRef(KEY_DET_CTRL_RIGHT);

        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        RefreshGrabbables();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    /// <summary>
    /// Re-resolve references when transitioning into or out of play mode.
    /// The scene objects get new instance IDs, but GlobalObjectId stays stable.
    /// </summary>
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        // EnteredEditMode  = just left play mode, scene reloaded
        // EnteredPlayMode  = just entered play mode, scene reloaded
        if (state == PlayModeStateChange.EnteredEditMode ||
            state == PlayModeStateChange.EnteredPlayMode)
        {
            _detectorHandLeft        = LoadDetectorRef(KEY_DET_HAND_LEFT);
            _detectorHandRight       = LoadDetectorRef(KEY_DET_HAND_RIGHT);
            _detectorControllerLeft  = LoadDetectorRef(KEY_DET_CTRL_LEFT);
            _detectorControllerRight = LoadDetectorRef(KEY_DET_CTRL_RIGHT);
            RefreshGrabbables();
            Repaint();
        }
    }

    private void OnFocus() => RefreshGrabbables();
    private void OnHierarchyChange() => RefreshGrabbables();

    // ═════════════════════════════════════════════════════════════════════════
    // GLOBAL OBJECT ID PERSISTENCE
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Save a scene object reference as a GlobalObjectId string in EditorPrefs.
    /// Works across domain reloads and play mode transitions.
    /// </summary>
    private void SaveDetectorRef(string key, GrabPinchDetector detector)
    {
        if (detector == null)
        {
            EditorPrefs.SetString(key, "");
            return;
        }

        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(detector);
        EditorPrefs.SetString(key, id.ToString());
    }

    /// <summary>
    /// Resolve a GlobalObjectId string from EditorPrefs back to a live scene object.
    /// Returns null if the string is empty, unparseable, or the object no longer exists.
    /// </summary>
    private GrabPinchDetector LoadDetectorRef(string key)
    {
        string stored = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(stored))
            return null;

        if (!GlobalObjectId.TryParse(stored, out GlobalObjectId id))
            return null;

        Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
        if (obj == null)
            return null;

        // GlobalObjectId resolves to the Component if we saved a Component,
        // or to the GameObject if we saved a GameObject. Handle both.
        if (obj is GrabPinchDetector det)
            return det;

        if (obj is GameObject go)
            return go.GetComponent<GrabPinchDetector>();

        return null;
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    private void RefreshGrabbables()
    {
        _grabbables = FindObjectsByType<GrabInteraction>(FindObjectsSortMode.None)
            .OrderBy(g => g.gameObject.name)
            .ToArray();

        if (_foldouts == null || _foldouts.Length != _grabbables.Length)
            _foldouts = new bool[_grabbables.Length];
    }

    // ── GUI ──────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        DrawHeader();
        DrawSaveFolder();
        DrawRecordingRefs();
        DrawSearchBar();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        DrawGrabbableList();
        EditorGUILayout.EndScrollView();

        DrawFooter();
    }

    // ── Header ───────────────────────────────────────────────────────────────

    private void DrawHeader()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Grab Pose Recorder", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "PLAY MODE — Hold the object in the desired grip, then click Record " +
                "on the matching slot. The pose will be saved and auto-assigned.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "EDIT MODE — Assign detectors below, view/assign poses. " +
                "Enter Play Mode to record new poses.",
                MessageType.None);
        }
        EditorGUILayout.Space(2);
    }

    // ── Save Folder ──────────────────────────────────────────────────────────

    private void DrawSaveFolder()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Save Folder", GUILayout.Width(80));

        string newFolder = EditorGUILayout.TextField(_saveFolder);
        if (newFolder != _saveFolder)
        {
            _saveFolder = newFolder;
            EditorPrefs.SetString(SAVE_FOLDER_KEY, _saveFolder);
        }

        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string chosen = EditorUtility.OpenFolderPanel("Pose Save Folder", _saveFolder, "");
            if (!string.IsNullOrEmpty(chosen))
            {
                if (chosen.StartsWith(Application.dataPath))
                    chosen = "Assets" + chosen.Substring(Application.dataPath.Length);

                _saveFolder = chosen;
                EditorPrefs.SetString(SAVE_FOLDER_KEY, _saveFolder);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RECORDING REFERENCES — always visible (edit and play mode)
    // ═════════════════════════════════════════════════════════════════════════

    private void DrawRecordingRefs()
    {
        bool newFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_refsFoldout, "Recording References");
        if (newFoldout != _refsFoldout)
        {
            _refsFoldout = newFoldout;
            EditorPrefs.SetBool(KEY_REFS_FOLDOUT, _refsFoldout);
        }

        if (_refsFoldout)
        {
            EditorGUI.indentLevel++;

            // ── Hand Tracking ────────────────────────────────────────────
            EditorGUILayout.LabelField("Hand Tracking", EditorStyles.boldLabel);
            DrawDetectorSlot("Left Hand",  ref _detectorHandLeft,  KEY_DET_HAND_LEFT,  InputMode.HandTracking);
            DrawDetectorSlot("Right Hand", ref _detectorHandRight, KEY_DET_HAND_RIGHT, InputMode.HandTracking);

            EditorGUILayout.Space(4);

            // ── Controller ───────────────────────────────────────────────
            EditorGUILayout.LabelField("Controller", EditorStyles.boldLabel);
            DrawDetectorSlot("Left Controller",  ref _detectorControllerLeft,  KEY_DET_CTRL_LEFT,  InputMode.Controller);
            DrawDetectorSlot("Right Controller", ref _detectorControllerRight, KEY_DET_CTRL_RIGHT, InputMode.Controller);

            EditorGUILayout.Space(4);

            // ── Buttons ──────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-Detect from Scene"))
                AutoDetectAllDetectors();
            if (GUILayout.Button("Clear All"))
                ClearAllDetectors();
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(4);
    }

    /// <summary>
    /// Draw a single detector slot. On change, persist via GlobalObjectId.
    /// Below the field, show derived references (lock, root, pinchPoint) read-only.
    /// </summary>
    private void DrawDetectorSlot(string label, ref GrabPinchDetector detector,
        string prefsKey, InputMode mode)
    {
        EditorGUI.BeginChangeCheck();

        GrabPinchDetector newDet = (GrabPinchDetector)EditorGUILayout.ObjectField(
            label, detector, typeof(GrabPinchDetector), true);

        if (EditorGUI.EndChangeCheck())
        {
            detector = newDet;
            SaveDetectorRef(prefsKey, detector);
        }

        if (detector == null) return;

        // Show what the detector provides — read-only for verification.
        EditorGUI.indentLevel++;
        GUI.enabled = false;

        if (mode == InputMode.HandTracking)
        {
            EditorGUILayout.ObjectField("HandPoseLock",
                detector.handPoseLock, typeof(HandPoseLock), true);

            Transform root = null;
            if (detector.handPoseLock != null && detector.handPoseLock.skeletonDriver != null)
                root = detector.handPoseLock.skeletonDriver.rootTransform;
            EditorGUILayout.ObjectField("Tracking Root (from driver)",
                root, typeof(Transform), true);
        }
        else
        {
            EditorGUILayout.ObjectField("ControllerPoseLock",
                detector.controllerPoseLock, typeof(ControllerPoseLock), true);
            EditorGUILayout.ObjectField("Controller Root",
                detector.controllerRoot, typeof(Transform), true);
        }

        EditorGUILayout.ObjectField("Pinch Point",
            detector.pinchPoint, typeof(Transform), true);

        GUI.enabled = true;
        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// Scan the scene for GrabPinchDetectors and slot them by inputMode + handedness.
    /// Only fills empty slots — won't overwrite manual assignments.
    /// </summary>
    private void AutoDetectAllDetectors()
    {
        var detectors = FindObjectsByType<GrabPinchDetector>(FindObjectsSortMode.None);

        foreach (var d in detectors)
        {
            if (d.inputMode == InputMode.HandTracking && d.handedness == Handedness.Left
                && _detectorHandLeft == null)
            {
                _detectorHandLeft = d;
                SaveDetectorRef(KEY_DET_HAND_LEFT, d);
            }
            else if (d.inputMode == InputMode.HandTracking && d.handedness == Handedness.Right
                     && _detectorHandRight == null)
            {
                _detectorHandRight = d;
                SaveDetectorRef(KEY_DET_HAND_RIGHT, d);
            }
            else if (d.inputMode == InputMode.Controller && d.handedness == Handedness.Left
                     && _detectorControllerLeft == null)
            {
                _detectorControllerLeft = d;
                SaveDetectorRef(KEY_DET_CTRL_LEFT, d);
            }
            else if (d.inputMode == InputMode.Controller && d.handedness == Handedness.Right
                     && _detectorControllerRight == null)
            {
                _detectorControllerRight = d;
                SaveDetectorRef(KEY_DET_CTRL_RIGHT, d);
            }
        }

        int found = new[] { _detectorHandLeft, _detectorHandRight,
                            _detectorControllerLeft, _detectorControllerRight }
            .Count(d => d != null);

        Debug.Log($"GrabPoseRecorder: auto-detected {found}/4 detectors from " +
                  $"{detectors.Length} in scene.");
    }

    private void ClearAllDetectors()
    {
        _detectorHandLeft = null;
        _detectorHandRight = null;
        _detectorControllerLeft = null;
        _detectorControllerRight = null;

        SaveDetectorRef(KEY_DET_HAND_LEFT, null);
        SaveDetectorRef(KEY_DET_HAND_RIGHT, null);
        SaveDetectorRef(KEY_DET_CTRL_LEFT, null);
        SaveDetectorRef(KEY_DET_CTRL_RIGHT, null);
    }

    /// <summary>
    /// Get the correct detector for a given pose type + handedness combo.
    /// </summary>
    private GrabPinchDetector GetDetectorFor(PoseType type, Handedness hand)
    {
        if (type == PoseType.HandTracking)
            return hand == Handedness.Left ? _detectorHandLeft : _detectorHandRight;
        else
            return hand == Handedness.Left ? _detectorControllerLeft : _detectorControllerRight;
    }

    // ── Search ───────────────────────────────────────────────────────────────

    private void DrawSearchBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)))
            _searchFilter = "";
        EditorGUILayout.EndHorizontal();
    }

    // ── Grabbable list ──────────────────────────────────────────────────────

    private void DrawGrabbableList()
    {
        if (_grabbables == null || _grabbables.Length == 0)
        {
            EditorGUILayout.HelpBox("No GrabInteraction objects found in the scene.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < _grabbables.Length; i++)
        {
            var grab = _grabbables[i];
            if (grab == null) continue;

            string objName = grab.gameObject.name;
            if (!string.IsNullOrEmpty(_searchFilter) &&
                !objName.ToLower().Contains(_searchFilter.ToLower()))
                continue;

            DrawGrabbableEntry(i, grab);
        }
    }

    private void DrawGrabbableEntry(int index, GrabInteraction grab)
    {
        int filled = CountFilledSlots(grab);
        string label = $"{grab.gameObject.name}  [{filled}/4 poses]";

        Color bg = GUI.backgroundColor;
        if (filled == 4) GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        else if (filled == 0) GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);

        _foldouts[index] = EditorGUILayout.BeginFoldoutHeaderGroup(_foldouts[index], label);
        GUI.backgroundColor = bg;

        if (_foldouts[index])
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.ObjectField("Object", grab, typeof(GrabInteraction), true);

            EditorGUILayout.Space(2);
            DrawPoseSlot(grab, "Hand Left",        ref grab.poseHandLeft,        PoseType.HandTracking, Handedness.Left);
            DrawPoseSlot(grab, "Hand Right",       ref grab.poseHandRight,       PoseType.HandTracking, Handedness.Right);
            DrawPoseSlot(grab, "Controller Left",  ref grab.poseControllerLeft,  PoseType.Controller,   Handedness.Left);
            DrawPoseSlot(grab, "Controller Right", ref grab.poseControllerRight, PoseType.Controller,   Handedness.Right);

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Object"))
                Selection.activeGameObject = grab.gameObject;
            if (GUILayout.Button("Clear All Poses"))
            {
                if (EditorUtility.DisplayDialog("Clear Poses",
                    $"Remove all 4 pose references from '{grab.gameObject.name}'?", "Yes", "Cancel"))
                {
                    Undo.RecordObject(grab, "Clear All Poses");
                    grab.poseHandLeft = null;
                    grab.poseHandRight = null;
                    grab.poseControllerLeft = null;
                    grab.poseControllerRight = null;
                    EditorUtility.SetDirty(grab);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── Pose slot ────────────────────────────────────────────────────────────

    private void DrawPoseSlot(GrabInteraction grab, string label,
        ref RecordedPose pose, PoseType type, Handedness hand)
    {
        EditorGUILayout.BeginHorizontal();

        // Status icon
        string icon = pose != null ? "✓" : "✗";
        Color c = GUI.contentColor;
        GUI.contentColor = pose != null ? Color.green : Color.red;
        EditorGUILayout.LabelField(icon, GUILayout.Width(16));
        GUI.contentColor = c;

        // Drag-drop field
        RecordedPose newPose = (RecordedPose)EditorGUILayout.ObjectField(
            label, pose, typeof(RecordedPose), false);

        if (newPose != pose)
        {
            Undo.RecordObject(grab, $"Assign Pose {label}");
            pose = newPose;
            EditorUtility.SetDirty(grab);
        }

        // Record button — play mode only, greyed out if detector missing
        if (Application.isPlaying)
        {
            GrabPinchDetector det = GetDetectorFor(type, hand);
            GUI.enabled = det != null;

            if (GUILayout.Button("Record", GUILayout.Width(60)))
                RecordPose(grab, ref pose, type, hand, det);

            GUI.enabled = true;
        }

        EditorGUILayout.EndHorizontal();
    }

    // ── Recording ────────────────────────────────────────────────────────────

    private void RecordPose(GrabInteraction grab, ref RecordedPose slot,
        PoseType type, Handedness hand, GrabPinchDetector detector)
    {
        if (detector == null)
        {
            string modeStr = type == PoseType.HandTracking ? "Hand Tracking" : "Controller";
            string handStr = hand == Handedness.Left ? "Left" : "Right";
            EditorUtility.DisplayDialog("Missing Detector",
                $"No GrabPinchDetector assigned for {handStr} {modeStr}.\n\n" +
                "Assign it in the Recording References section, or click Auto-Detect.",
                "OK");
            return;
        }

        EnsureSaveFolder();

        string handLabel = hand == Handedness.Right ? "RightHand" : "LeftHand";
        string modeLabel = type == PoseType.HandTracking ? "HandTracking" : "Controller";
        string assetName = $"{grab.gameObject.name}_{handLabel}_{modeLabel}";

        Transform pinchPoint = detector.pinchPoint;
        RecordedPose recorded = null;

        if (type == PoseType.HandTracking)
        {
            HandPoseLock hLock = detector.handPoseLock;
            if (hLock == null || hLock.skeletonDriver == null)
            {
                EditorUtility.DisplayDialog("Missing Reference",
                    $"Detector '{detector.name}' has no HandPoseLock or its " +
                    "skeletonDriver is null.\n\nCheck the detector's inspector.",
                    "OK");
                return;
            }

            var driver = hLock.skeletonDriver;
            Transform handRoot = driver.rootTransform;
            if (handRoot == null)
            {
                EditorUtility.DisplayDialog("Missing Reference",
                    $"Skeleton driver on '{detector.name}' has no rootTransform.\n" +
                    "Has the XR hand subsystem started?",
                    "OK");
                return;
            }

            recorded = RecordedPose.CaptureHandTracking(
                driver, handRoot, hand,
                pinchPoint, grab,
                assetName, _saveFolder);
        }
        else
        {
            ControllerPoseLock cLock = detector.controllerPoseLock;
            if (cLock == null || cLock.handAnimator == null)
            {
                EditorUtility.DisplayDialog("Missing Reference",
                    $"Detector '{detector.name}' has no ControllerPoseLock or " +
                    "its Animator is null.\n\nCheck the detector's inspector.",
                    "OK");
                return;
            }

            Transform controllerRoot = detector.controllerRoot;
            if (controllerRoot == null)
            {
                EditorUtility.DisplayDialog("Missing Reference",
                    $"Detector '{detector.name}' has no controllerRoot.\n\n" +
                    "Check the detector's inspector.",
                    "OK");
                return;
            }

            recorded = RecordedPose.CaptureController(
                cLock.handAnimator, controllerRoot, hand,
                pinchPoint, grab,
                assetName, _saveFolder);
        }

        if (recorded != null)
        {
            Undo.RecordObject(grab, $"Record Pose {assetName}");
            slot = recorded;
            EditorUtility.SetDirty(grab);
            Debug.Log($"GrabPoseRecorder: recorded and assigned '{assetName}' to " +
                      $"'{grab.gameObject.name}'.", grab);
        }
    }

    // ── Footer ───────────────────────────────────────────────────────────────

    private void DrawFooter()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Batch Operations", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Refresh Scene"))
            RefreshGrabbables();

        if (GUILayout.Button("Auto-Assign from Folder"))
            BatchAssignFromFolder();

        if (GUILayout.Button("Expand All"))
            SetAllFoldouts(true);

        if (GUILayout.Button("Collapse All"))
            SetAllFoldouts(false);

        EditorGUILayout.EndHorizontal();

        if (_grabbables != null)
        {
            int total = _grabbables.Length * 4;
            int filled = _grabbables.Sum(g => g != null ? CountFilledSlots(g) : 0);
            EditorGUILayout.LabelField($"{_grabbables.Length} objects — {filled}/{total} pose slots filled");
        }
    }

    // ── Batch assign ─────────────────────────────────────────────────────────

    private void BatchAssignFromFolder()
    {
        if (!AssetDatabase.IsValidFolder(_saveFolder))
        {
            EditorUtility.DisplayDialog("Folder Not Found",
                $"'{_saveFolder}' does not exist.", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:RecordedPose", new[] { _saveFolder });
        var poseMap = new Dictionary<string, RecordedPose>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var pose = AssetDatabase.LoadAssetAtPath<RecordedPose>(path);
            if (pose != null)
            {
                string key = Path.GetFileNameWithoutExtension(path);
                poseMap[key] = pose;
            }
        }

        int assigned = 0;

        foreach (var grab in _grabbables)
        {
            if (grab == null) continue;
            string objName = grab.gameObject.name;

            assigned += TryAssign(grab, poseMap, objName, "LeftHand",  "HandTracking", ref grab.poseHandLeft);
            assigned += TryAssign(grab, poseMap, objName, "RightHand", "HandTracking", ref grab.poseHandRight);
            assigned += TryAssign(grab, poseMap, objName, "LeftHand",  "Controller",   ref grab.poseControllerLeft);
            assigned += TryAssign(grab, poseMap, objName, "RightHand", "Controller",   ref grab.poseControllerRight);
        }

        Debug.Log($"GrabPoseRecorder: batch assigned {assigned} poses from '{_saveFolder}'.");
        EditorUtility.DisplayDialog("Batch Assign Complete",
            $"Assigned {assigned} poses from {guids.Length} assets found in folder.", "OK");
    }

    private int TryAssign(GrabInteraction grab, Dictionary<string, RecordedPose> poseMap,
        string objName, string handLabel, string modeLabel, ref RecordedPose slot)
    {
        string key = $"{objName}_{handLabel}_{modeLabel}";
        if (poseMap.TryGetValue(key, out var pose))
        {
            if (slot != pose)
            {
                Undo.RecordObject(grab, $"Batch Assign {key}");
                slot = pose;
                EditorUtility.SetDirty(grab);
                return 1;
            }
        }
        return 0;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private int CountFilledSlots(GrabInteraction grab)
    {
        int count = 0;
        if (grab.poseHandLeft != null) count++;
        if (grab.poseHandRight != null) count++;
        if (grab.poseControllerLeft != null) count++;
        if (grab.poseControllerRight != null) count++;
        return count;
    }

    private void SetAllFoldouts(bool state)
    {
        if (_foldouts == null) return;
        for (int i = 0; i < _foldouts.Length; i++)
            _foldouts[i] = state;
    }

    private void EnsureSaveFolder()
    {
        if (AssetDatabase.IsValidFolder(_saveFolder)) return;

        string[] parts = _saveFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        AssetDatabase.Refresh();
    }
}
#endif