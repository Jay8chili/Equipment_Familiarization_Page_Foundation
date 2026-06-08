// ════════════════════════════════════════════════════════════════════════════
//  PartsIdentificationWizard.cs
//  Place inside any Editor/ folder in your project.
//
//  Menu: Simulation ▶ Parts Identification Wizard
//
//  WINDOW 1 – PartsIdentificationWizard
//      Assign the SimulationManager, states parent, TSV file, then click
//      "Load TSV & Configure Steps →".
//
//  TSV FORMAT (tab-separated, export from Excel as .tsv)
//      Row 0  (header):  StepName  StepType  StationID  <Language1>  <Language2>  …
//      Row 1+ (data):    <name>    Station|Part  <id>   <text1>      <text2>      …
//
//  WINDOW 2 – PartsIdentificationConfigWindow
//      Every parsed step shown with its localised texts, station metadata,
//      and Part Object drag slot. Click "⚡ Generate States" to build in-scene.
//      If "Generate Audio on Create" is ticked, the TTS window opens
//      automatically and generates audio for every step.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationSystem.V02.Editor
{
    // ══════════════════════════════════════════════════════════════════════════
    //  DATA TYPES
    // ══════════════════════════════════════════════════════════════════════════

    [Serializable]
    public class PartToolStepData
    {
        public string stepName = string.Empty;
        public PartStepType stepType = PartStepType.Part;
        public string stationId = string.Empty;

        // One entry per language column in the TSV (index 0 = primary / English)
        public List<string> languageTexts = new List<string>();

        // The mesh or parent GO to highlight when this step is active
        public GameObject partReference;

        // Teleport
        public bool teleportOnStart = false;
        public Transform teleportTarget;

        // Inspector foldout state
        public bool foldout = true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  WINDOW 1 — PartsIdentificationWizard
    // ══════════════════════════════════════════════════════════════════════════

    public class PartsIdentificationWizard : EditorWindow
    {
        // ── Scene references ──────────────────────────────────────────────────
        public SimulationManager simulationManager;
        public Transform statesParent;
        public Transform uiParent;

        [Tooltip("Prefab instantiated for each step's world-space description UI panel. " +
                 "Must have FamiliarizationUIPanel on its root with nameLabel and descriptionLabel wired.")]
        public GameObject uiPanelPrefab;

        [Tooltip("Prefab to instantiate for each Station I-button. " +
                 "Must have a Collider. StationIButton component is added automatically.")]
        public GameObject iButtonPrefab;

        [Tooltip("Parent Transform that holds all instantiated I-button GameObjects.")]
        public Transform iButtonParent;

        // ── TSV ───────────────────────────────────────────────────────────────
        private string _tsvPath = string.Empty;
        private string _tsvError = string.Empty;
        private Vector2 _scroll;

        // ── Audio ─────────────────────────────────────────────────────────────
        public bool generateAudioOnCreate = false;

        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Simulation/Parts Identification Wizard")]
        public static PartsIdentificationWizard Open()
        {
            var w = GetWindow<PartsIdentificationWizard>("Parts ID Wizard");
            w.minSize = new Vector2(420, 520);
            return w;
        }

        // ─────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(8);

            DrawWindowTitle("🧩  Parts Identification Wizard");
            GUILayout.Space(10);

            // ── Scene References ──────────────────────────────────────────────
            DrawSectionHeader("Scene References");
            simulationManager = ObjFieldTooltip<SimulationManager>(
                "Simulation Manager",
                "The SimulationManager in the scene. Generated states will be registered to its states list.",
                simulationManager, true);
            statesParent = ObjFieldTooltip<Transform>(
                "States Parent",
                "Parent transform under which SimulationState GameObjects are created. " +
                "Defaults to the SimulationManager transform if left empty.",
                statesParent, true);
            uiParent = ObjFieldTooltip<Transform>(
                "UI Parent",
                "Parent Transform that holds all world-space UI panels. One panel GO is created per step.",
                uiParent, true);
            uiPanelPrefab = ObjFieldTooltip<GameObject>(
                "UI Panel Prefab",
                "Prefab instantiated for each step's world-space description panel. " +
                "Must have FamiliarizationUIPanel on its root with nameLabel and descriptionLabel already wired.",
                uiPanelPrefab, false);
            iButtonPrefab = ObjFieldTooltip<GameObject>(
                "I-Button Prefab",
                "Prefab instantiated for each Station step. Must have a Collider. " +
                "StationIButton component is added automatically.",
                iButtonPrefab, false);
            iButtonParent = ObjFieldTooltip<Transform>(
                "I-Button Parent",
                "Parent Transform that holds all instantiated I-button GameObjects.",
                iButtonParent, true);
            GUILayout.Space(10);

            // ── TSV ───────────────────────────────────────────────────────────
            DrawSectionHeader("Instruction TSV");
            DrawTsvRow();

            if (!string.IsNullOrEmpty(_tsvError))
                EditorGUILayout.HelpBox(_tsvError, MessageType.Error);

            GUILayout.Space(10);

            bool canProceed = !string.IsNullOrEmpty(_tsvPath) && simulationManager != null;
            using (new EditorGUI.DisabledGroupScope(!canProceed))
            {
                if (GUILayout.Button("Load TSV & Configure Steps  →", GUILayout.Height(40)))
                    TryLoadAndOpen();
            }

            if (!canProceed)
            {
                string hint = simulationManager == null
                    ? "Assign a Simulation Manager to continue."
                    : "Select a TSV file to continue.";
                EditorGUILayout.HelpBox(hint, MessageType.Info);
            }

            GUILayout.Space(12);

            // ── Audio ─────────────────────────────────────────────────────────
            DrawSectionHeader("Audio Generation");
            generateAudioOnCreate = EditorGUILayout.Toggle(
                new GUIContent("Generate Audio on Create",
                    "When enabled, TTS audio is generated and assigned to each SimulationState " +
                    "automatically after Generate States is clicked."),
                generateAudioOnCreate);

            if (GUILayout.Button(new GUIContent("🎙  Audio Settings",
                    "Open the TTS Audio Generator window to configure voices and preview TTS."),
                    GUILayout.Height(28)))
            {
                TTSAudioGeneratorWindow.GetOrOpen();
            }

            GUILayout.Space(12);

            // ── Danger Zone ───────────────────────────────────────────────────
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1),
                new Color(0.5f, 0.15f, 0.15f, 0.7f));
            GUILayout.Space(3);
            EditorGUILayout.LabelField("Danger Zone", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(2);

            bool anyParent = statesParent != null || simulationManager != null;
            using (new EditorGUI.DisabledGroupScope(!anyParent))
            {
                GUI.color = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button(new GUIContent(
                        "🗑  Clear All Generated States",
                        "Destroys every child GameObject under the States Parent " +
                        "and resets the wizard. Undo with Ctrl+Z before saving."),
                        GUILayout.Height(32)))
                {
                    ClearGeneratedStates();
                }
                GUI.color = Color.white;
            }

            GUILayout.Space(10);
            EditorGUILayout.EndScrollView();
        }

        // ── TSV path row ──────────────────────────────────────────────────────

        private void DrawTsvRow()
        {
            EditorGUILayout.BeginHorizontal();
            string label = string.IsNullOrEmpty(_tsvPath)
                ? "No file selected"
                : Path.GetFileName(_tsvPath);
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Browse…", GUILayout.Width(76)))
            {
                string p = EditorUtility.OpenFilePanel("Select TSV File", Application.dataPath, "tsv,txt,csv");
                if (!string.IsNullOrEmpty(p)) { _tsvPath = p; _tsvError = string.Empty; }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Load TSV ──────────────────────────────────────────────────────────

        private void TryLoadAndOpen()
        {
            _tsvError = string.Empty;

            string[] lines;
            try { lines = File.ReadAllLines(_tsvPath); }
            catch (Exception ex) { _tsvError = $"Cannot read file: {ex.Message}"; return; }

            if (lines.Length < 2)
            {
                _tsvError = "TSV needs at least a header row and one data row.";
                return;
            }

            string[] headers = lines[0].Split('\t');

            // Expected fixed columns: StepName | StepType | StationID | <lang1> | <lang2> …
            // Minimum: StepName + StepType + StationID + one language column = 4 columns
            if (headers.Length < 4)
            {
                _tsvError = "TSV needs at least 4 columns: StepName, StepType, StationID, <Language>.";
                return;
            }

            // Columns 0-2 are fixed metadata. Columns 3+ are language columns.
            var languages = new List<string>();
            for (int c = 3; c < headers.Length; c++)
                languages.Add(headers[c].Trim());

            var steps = new List<PartToolStepData>();
            for (int r = 1; r < lines.Length; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) continue;
                string[] cols = lines[r].Split('\t');

                // Parse StepType — default Part if unrecognised
                PartStepType parsedType = PartStepType.Part;
                if (cols.Length > 1 &&
                    string.Equals(cols[1].Trim(), "Station", StringComparison.OrdinalIgnoreCase))
                    parsedType = PartStepType.Station;

                var step = new PartToolStepData
                {
                    stepName = cols.Length > 0 ? cols[0].Trim() : $"Step {r}",
                    stepType = parsedType,
                    stationId = cols.Length > 2 ? cols[2].Trim() : string.Empty
                };

                // Language texts start at column 3
                for (int c = 3; c < headers.Length; c++)
                    step.languageTexts.Add(c < cols.Length ? cols[c].Trim() : string.Empty);

                steps.Add(step);
            }

            PartsIdentificationConfigWindow.Open(this, steps, languages);
        }

        // ── Clear generated states ────────────────────────────────────────────

        private void ClearGeneratedStates()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear Generated States",
                    "This will destroy all child GameObjects under the States Parent.\n\nThis can be undone with Ctrl+Z before saving.",
                    "Clear", "Cancel"))
                return;

            Transform parent = statesParent != null
                ? statesParent
                : (simulationManager != null ? simulationManager.transform : null);

            if (parent == null)
            {
                Debug.LogWarning("[Parts ID Wizard] No parent transform found to clear.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clear Parts ID States");

            int deleted = 0;
            var children = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
                children.Add(parent.GetChild(i).gameObject);

            foreach (var child in children)
            {
                if (child == null) continue;
                Undo.DestroyObjectImmediate(child);
                deleted++;
            }

            // Clear SimulationManager states list
            if (simulationManager != null)
            {
                var so = new SerializedObject(simulationManager);
                var statesProp = so.FindProperty("states");
                if (statesProp != null)
                {
                    statesProp.ClearArray();
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(simulationManager);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Clear Complete",
                $"Deleted {deleted} state(s). Undo with Ctrl+Z.", "OK");
            Repaint();
        }

        // ── GUI helpers ───────────────────────────────────────────────────────

        private static void DrawWindowTitle(string text)
        {
            EditorGUILayout.LabelField(text,
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 }, GUILayout.Height(28));
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1),
                new Color(0.45f, 0.45f, 0.45f, 0.7f));
        }

        private static void DrawSectionHeader(string text)
        {
            GUILayout.Space(2);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        internal static T ObjFieldTooltip<T>(string label, string tooltip, T obj, bool allowScene)
            where T : UnityEngine.Object =>
            (T)EditorGUILayout.ObjectField(new GUIContent(label, tooltip), obj, typeof(T), allowScene);
    }


    // ══════════════════════════════════════════════════════════════════════════
    //  WINDOW 2 — PartsIdentificationConfigWindow
    // ══════════════════════════════════════════════════════════════════════════

    public class PartsIdentificationConfigWindow : EditorWindow
    {
        private PartsIdentificationWizard _wizard;
        internal List<PartToolStepData> _steps;
        internal List<string> _languages;
        private Vector2 _scroll;
        internal bool _justGenerated;

        // ─────────────────────────────────────────────────────────────────────

        public static void Open(
            PartsIdentificationWizard wizard,
            List<PartToolStepData> steps,
            List<string> languages)
        {
            var w = GetWindow<PartsIdentificationConfigWindow>("Parts ID Wizard — Configure Steps");
            w.minSize = new Vector2(520, 600);
            w._wizard = wizard;
            w._steps = steps;
            w._languages = languages;
            w._justGenerated = false;
            w.Repaint();
        }

        // ─────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_steps == null || _wizard == null)
            {
                EditorGUILayout.HelpBox(
                    "No data loaded. Open the Parts ID Wizard and load a TSV first.",
                    MessageType.Warning);
                return;
            }

            // ── Header ────────────────────────────────────────────────────────
            GUILayout.Space(6);
            string langList = _languages?.Count > 0 ? string.Join(", ", _languages) : "—";
            EditorGUILayout.LabelField(
                $"📋  {_steps.Count} step(s)   ·   {(_languages?.Count ?? 0)} language(s): {langList}",
                EditorStyles.miniLabel);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1),
                new Color(0.4f, 0.4f, 0.4f, 0.5f));
            GUILayout.Space(4);

            // ── Steps ─────────────────────────────────────────────────────────
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int removeStep = -1;
            for (int i = 0; i < _steps.Count; i++)
                if (DrawStep(i, _steps[i])) removeStep = i;
            if (removeStep >= 0) { _steps.RemoveAt(removeStep); GUIUtility.ExitGUI(); }
            EditorGUILayout.EndScrollView();

            // ── Footer ────────────────────────────────────────────────────────
            GUILayout.Space(6);

            if (_justGenerated)
                EditorGUILayout.HelpBox(
                    "✔  States generated successfully! Check your scene hierarchy.",
                    MessageType.Info);

            // Add Step button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button(new GUIContent("＋  Add Step", "Append a new empty step."),
                    GUILayout.Width(120), GUILayout.Height(26)))
            {
                _steps.Add(new PartToolStepData
                {
                    stepName = $"Step {_steps.Count + 1}",
                    languageTexts = _languages != null
                        ? new List<string>(new string[_languages.Count])
                        : new List<string>()
                });
            }
            GUI.color = Color.white;
            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Generate button
            using (new EditorGUI.DisabledGroupScope(_steps.Count == 0))
            {
                var btnStyle = new GUIStyle(GUI.skin.button)
                { fontSize = 13, fontStyle = FontStyle.Bold };
                if (GUILayout.Button("⚡  Generate States", btnStyle, GUILayout.Height(44)))
                    EditorApplication.delayCall += GenerateStates;
            }

            GUILayout.Space(6);
        }

        // ── Draw one step — returns true if step should be removed ────────────

        private bool DrawStep(int stepIdx, PartToolStepData step)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── Header row ────────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            string typeTag = step.stepType == PartStepType.Station ? "🏭 Station" : "🔩 Part";
            step.foldout = EditorGUILayout.Foldout(
                step.foldout,
                $"  {typeTag}  {stepIdx + 1}  —  {step.stepName}",
                true,
                new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 11 });

            GUILayout.FlexibleSpace();

            // Insert step below
            GUI.color = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button(new GUIContent("＋", "Insert a new step below this one."),
                    GUILayout.Width(24), GUILayout.Height(18)))
            {
                _steps.Insert(stepIdx + 1, new PartToolStepData
                {
                    stepName = $"Step {stepIdx + 2}",
                    languageTexts = _languages != null
                        ? new List<string>(new string[_languages.Count])
                        : new List<string>()
                });
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.Space(3);
                GUIUtility.ExitGUI();
                return false;
            }

            // Remove step
            GUI.color = new Color(1f, 0.45f, 0.45f);
            bool remove = GUILayout.Button(
                new GUIContent("✕", "Remove this step entirely."),
                GUILayout.Width(22), GUILayout.Height(18));
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            if (remove)
            {
                EditorGUILayout.EndVertical();
                GUILayout.Space(3);
                return true;
            }

            if (step.foldout)
            {
                EditorGUI.indentLevel++;

                // ── Step Name ─────────────────────────────────────────────────
                step.stepName = EditorGUILayout.TextField(
                    new GUIContent("Step Name", "Internal identifier for this step."),
                    step.stepName);
                GUILayout.Space(4);

                // ── Station Metadata ──────────────────────────────────────────
                DrawSectionLabel("Station Metadata");
                step.stepType = (PartStepType)EditorGUILayout.EnumPopup(
                    new GUIContent("Step Type",
                        "Station: intro step that describes the whole station.\n" +
                        "Part: individual part description step."),
                    step.stepType);
                step.stationId = EditorGUILayout.TextField(
                    new GUIContent("Station ID",
                        "Group identifier (e.g. S01). All parts of the same station share this ID " +
                        "with their station intro step. Used by PartsIdentificationManager at runtime."),
                    step.stationId);
                GUILayout.Space(6);

                // ── Part / Station Reference ───────────────────────────────────
                DrawSectionLabel("Part / Station Reference");
                step.partReference = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Part Object",
                        "Drag the mesh or parent GameObject for this step's highlight target. " +
                        "Used by PartsIdentificationManager to highlight the correct object " +
                        "when this step becomes active."),
                    step.partReference, typeof(GameObject), true);

                if (step.partReference == null)
                    EditorGUILayout.HelpBox(
                        "No Part Object assigned. Highlight will not fire for this step.",
                        MessageType.None);
                else
                    EditorGUILayout.HelpBox(
                        $"✔  {step.partReference.name}", MessageType.None);

                GUILayout.Space(6);

                // ── Localised Text ────────────────────────────────────────────
                DrawSectionLabel("Localised Text");
                EditorGUI.indentLevel++;
                if (_languages != null)
                {
                    for (int l = 0; l < _languages.Count; l++)
                    {
                        while (step.languageTexts.Count <= l)
                            step.languageTexts.Add(string.Empty);

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(_languages[l], GUILayout.Width(96));
                        step.languageTexts[l] = EditorGUILayout.TextField(step.languageTexts[l]);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUI.indentLevel--;
                GUILayout.Space(6);

                // ── Teleportation ─────────────────────────────────────────────
                DrawSectionLabel("Teleportation");
                step.teleportOnStart = EditorGUILayout.Toggle(
                    new GUIContent("Teleport On Start",
                        "If enabled, the player is teleported to the Teleport Target before this step's audio plays."),
                    step.teleportOnStart);

                if (step.teleportOnStart)
                {
                    step.teleportTarget = (Transform)EditorGUILayout.ObjectField(
                        new GUIContent("Teleport Target",
                            "The Transform the player will be moved to when this step starts."),
                        step.teleportTarget, typeof(Transform), true);
                    if (step.teleportTarget == null)
                        EditorGUILayout.HelpBox(
                            "Assign a Teleport Target, or disable Teleport On Start.",
                            MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(3);
            return false;
        }

        // ── Generate States ───────────────────────────────────────────────────

        private void GenerateStates()
        {
            if (_wizard == null || _steps == null || _steps.Count == 0)
            {
                Debug.LogWarning("[Parts ID Wizard] Nothing to generate.");
                return;
            }

            SimulationManager simManager = _wizard.simulationManager;
            if (simManager == null)
            {
                EditorUtility.DisplayDialog("Missing Reference",
                    "Assign a Simulation Manager in Window 1 before generating.", "OK");
                return;
            }

            Transform parent = _wizard.statesParent != null
                ? _wizard.statesParent
                : simManager.transform;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate Parts ID States");

            // ── Collect any missing part references for the warning report ────
            var warnings = new List<string>();

            // ── Build states list fresh ───────────────────────────────────────
            var newStates = new List<GameObject>();

            // ── Track StationGroups as we generate — keyed by stationId ──────
            // Populated during the loop, then written to PartsIdentificationManager after.
            var stationGroupMap = new Dictionary<string, StationGroup>();

            for (int i = 0; i < _steps.Count; i++)
            {
                PartToolStepData step = _steps[i];
                int stepNo = i + 1;

                // ── GO name = stepNo + sanitized primary text
                // Must match the name TTS constructs in ReceiveFromWizard:
                //   goName = $"{stepNo}_{sanitize(languageTexts[0])}"
                // displayName on PartStepData holds the human-readable step name.
                string primaryText = step.languageTexts.Count > 0
                    ? step.languageTexts[0].Trim() : step.stepName;
                string safePrimaryText = SanitizeFileName(
                    string.IsNullOrWhiteSpace(primaryText) ? step.stepName : primaryText);
                string goName = $"{stepNo}_{safePrimaryText}";
                // Keep stepName as the display name shown in UI — separate from GO name
                string safeStepName = SanitizeFileName(step.stepName);

                // ── Create state GameObject ───────────────────────────────────
                var go = new GameObject(goName);
                Undo.RegisterCreatedObjectUndo(go, "Create State GO");
                go.transform.SetParent(parent, false);

                // ── SimulationState ───────────────────────────────────────────
                var state = go.AddComponent<SimulationState>();
                var stateSO = new SerializedObject(state);

                // promptText = primary language text (the description, not the name)
                var promptProp = stateSO.FindProperty("promptText");
                if (promptProp != null)
                    promptProp.stringValue = string.IsNullOrWhiteSpace(primaryText)
                        ? step.stepName : primaryText;

                // MoveToNextStepAfterAudio = true — always, these are prompt-only steps
                var moveNextProp = stateSO.FindProperty("MoveToNextStepAfterAudio");
                if (moveNextProp != null)
                    moveNextProp.boolValue = true;

                // Teleport
                var teleportOnProp = stateSO.FindProperty("teleportOnStart");
                if (teleportOnProp != null)
                    teleportOnProp.boolValue = step.teleportOnStart;

                if (step.teleportOnStart && step.teleportTarget != null)
                {
                    var teleportTargetProp = stateSO.FindProperty("teleportTarget");
                    if (teleportTargetProp != null)
                        teleportTargetProp.objectReferenceValue = step.teleportTarget;
                }

                stateSO.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(state);

                // ── PartStepData ──────────────────────────────────────────────
                var partData = go.AddComponent<PartStepData>();
                partData.displayName = step.stepName;   // name label shown in UI
                partData.stepType = step.stepType;
                partData.stationId = step.stationId;

                if (step.partReference != null)
                {
                    partData.highlightTarget = step.partReference;

                    // Add GrabHighlightController to the highlight target if not already present
                    if (step.partReference.GetComponent<GrabHighlightController>() == null)
                    {
                        step.partReference.AddComponent<GrabHighlightController>();
                        EditorUtility.SetDirty(step.partReference);
                        Debug.Log($"[Parts ID Wizard] Added GrabHighlightController to '{step.partReference.name}'");
                    }
                }
                else
                    warnings.Add($"Step {stepNo} '{step.stepName}' has no Part Object assigned.");

                // ── UI Panel GO ───────────────────────────────────────────────
                Transform uiHolder = _wizard.uiParent;
                GameObject uiPanelPrefab = _wizard.uiPanelPrefab;

                if (uiHolder != null && uiPanelPrefab != null)
                {
                    var panelGO = (GameObject)PrefabUtility.InstantiatePrefab(
                        uiPanelPrefab, uiHolder);
                    Undo.RegisterCreatedObjectUndo(panelGO, "Create UI Panel GO");
                    panelGO.name = $"Panel_{stepNo}_{safeStepName}";
                    panelGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                    // FamiliarizationUIPanel must already be on the prefab root
                    // with nameLabel and descriptionLabel wired.
                    var uiPanel = panelGO.GetComponent<FamiliarizationUIPanel>();
                    if (uiPanel == null)
                    {
                        // Prefab is missing the component — add it and warn
                        uiPanel = panelGO.AddComponent<FamiliarizationUIPanel>();
                        warnings.Add($"Step {stepNo} '{step.stepName}': UI Panel Prefab has no " +
                                     "FamiliarizationUIPanel component — added automatically. " +
                                     "Wire nameLabel and descriptionLabel manually.");
                    }

                    partData.uiPanel = uiPanel;

                    // ── Assign TMP text at edit time ──────────────────────────
                    // nameLabel  = step name (title)
                    // descriptionLabel = primary language text (description)
                    string descriptionText = step.languageTexts.Count > 0
                        ? step.languageTexts[0].Trim() : string.Empty;

                    if (uiPanel.nameLabel != null)
                    {
                        uiPanel.nameLabel.text = step.stepName;
                        EditorUtility.SetDirty(uiPanel.nameLabel);
                    }
                    if (uiPanel.descriptionLabel != null)
                    {
                        uiPanel.descriptionLabel.text = descriptionText;
                        EditorUtility.SetDirty(uiPanel.descriptionLabel);
                    }

                    EditorUtility.SetDirty(panelGO);
                }
                else
                {
                    if (uiHolder == null)
                        warnings.Add($"Step {stepNo} '{step.stepName}': No UI Parent assigned — panel not created.");
                    if (uiPanelPrefab == null)
                        warnings.Add($"Step {stepNo} '{step.stepName}': No UI Panel Prefab assigned — panel not created.");
                }

                // ── StationGroup tracking + I-button (Station steps only) ─────
                if (step.stepType == PartStepType.Station)
                {
                    // Create or retrieve StationGroup for this stationId
                    if (!stationGroupMap.TryGetValue(step.stationId, out StationGroup group))
                    {
                        group = new StationGroup
                        {
                            stationName = step.stepName,
                            stationId = step.stationId,
                            // +1 because SimulationManager.states[0] is the parked FreeRoam state
                            // which is NOT in _steps. Generated steps start at index 1.
                            stationIntroStateIndex = i + 1,
                            partStateIndices = new List<int>()
                        };
                        stationGroupMap[step.stationId] = group;
                    }

                    // ── Instantiate I-button ──────────────────────────────────
                    GameObject iButtonPrefab = _wizard.iButtonPrefab;
                    Transform iButtonParent = _wizard.iButtonParent;

                    if (iButtonPrefab != null && iButtonParent != null)
                    {
                        // Position at partReference world position; fall back to origin
                        Vector3 spawnPos = step.partReference != null
                            ? step.partReference.transform.position
                            : Vector3.zero;

                        var iButtonGO = (GameObject)PrefabUtility.InstantiatePrefab(
                            iButtonPrefab, iButtonParent);
                        Undo.RegisterCreatedObjectUndo(iButtonGO, "Create I-Button");

                        iButtonGO.name = $"IButton_{safeStepName}";
                        iButtonGO.transform.position = spawnPos;

                        // Add StationIButton if not already on the prefab
                        var iBtn = iButtonGO.GetComponent<StationIButton>()
                                ?? iButtonGO.AddComponent<StationIButton>();
                        iBtn.stationGroup = group;
                        group.iButton = iBtn;

                        EditorUtility.SetDirty(iButtonGO);
                        Debug.Log($"[Parts ID Wizard] 📍 I-button placed at " +
                                  $"{spawnPos} for station '{step.stepName}'");
                    }
                    else
                    {
                        if (iButtonPrefab == null)
                            warnings.Add($"Station '{step.stepName}': No I-Button Prefab assigned — button not created.");
                        if (iButtonParent == null)
                            warnings.Add($"Station '{step.stepName}': No I-Button Parent assigned — button not created.");
                    }
                }
                else if (step.stepType == PartStepType.Part)
                {
                    // Register this part's state index into its station group
                    if (stationGroupMap.TryGetValue(step.stationId, out StationGroup group))
                        group.partStateIndices.Add(i + 1); // +1 offset for parked state at index 0
                    else
                        warnings.Add($"Part '{step.stepName}': StationID '{step.stationId}' " +
                                     "has no matching Station row above it.");
                }

                EditorUtility.SetDirty(partData);

                // Disable GO — SimulationManager enables them one at a time at runtime
                go.SetActive(false);

                newStates.Add(go);

                Debug.Log($"[Parts ID Wizard] ✔ Created '{goName}' " +
                          $"[{step.stepType}] StationID={step.stationId}");
            }

            // ── Wire SimulationManager.states list ────────────────────────────
            var managerSO = new SerializedObject(simManager);
            var stateListProp = managerSO.FindProperty("states");
            if (stateListProp != null)
            {
                stateListProp.ClearArray();
                for (int i = 0; i < newStates.Count; i++)
                {
                    stateListProp.InsertArrayElementAtIndex(i);
                    stateListProp.GetArrayElementAtIndex(i).objectReferenceValue = newStates[i];
                }
                managerSO.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(simManager);
            }
            else
            {
                Debug.LogWarning("[Parts ID Wizard] Could not find 'states' property on SimulationManager. " +
                                 "Wire the states list manually.");
            }

            // ── Write StationGroups to PartsIdentificationManager ─────────────
            var pim = UnityEngine.Object.FindObjectOfType<PartsIdentificationManager>();
            if (pim != null)
            {
                var pimSO = new SerializedObject(pim);
                var groupListProp = pimSO.FindProperty("stationGroups");
                if (groupListProp != null)
                {
                    groupListProp.ClearArray();
                    int gi = 0;
                    foreach (var kvp in stationGroupMap)
                    {
                        StationGroup g = kvp.Value;
                        groupListProp.InsertArrayElementAtIndex(gi);
                        var elem = groupListProp.GetArrayElementAtIndex(gi);

                        elem.FindPropertyRelative("stationName").stringValue = g.stationName;
                        elem.FindPropertyRelative("stationId").stringValue = g.stationId;
                        elem.FindPropertyRelative("stationIntroStateIndex").intValue = g.stationIntroStateIndex;
                        elem.FindPropertyRelative("iButton").objectReferenceValue = g.iButton;

                        var partIndicesProp = elem.FindPropertyRelative("partStateIndices");
                        partIndicesProp.ClearArray();
                        for (int pi = 0; pi < g.partStateIndices.Count; pi++)
                        {
                            partIndicesProp.InsertArrayElementAtIndex(pi);
                            partIndicesProp.GetArrayElementAtIndex(pi).intValue = g.partStateIndices[pi];
                        }

                        gi++;
                    }
                    pimSO.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pim);
                    Debug.Log($"[Parts ID Wizard] ✔ Wrote {stationGroupMap.Count} StationGroup(s) " +
                               "to PartsIdentificationManager.");
                }
            }
            else
            {
                warnings.Add("PartsIdentificationManager not found in scene — " +
                             "StationGroups not auto-assigned. Add the component and re-generate.");
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            _justGenerated = true;

            // ── Warning report ────────────────────────────────────────────────
            if (warnings.Count > 0)
            {
                Debug.LogWarning("[Parts ID Wizard] Generation complete with warnings:\n" +
                                 string.Join("\n", warnings));
            }

            // ── TTS handoff — fires silently, no blocking dialog ─────────────
            if (_wizard.generateAudioOnCreate)
            {
                var ttsWindowType = System.Type.GetType(
                    "SimulationSystem.V02.Editor.TTSAudioGeneratorWindow, Assembly-CSharp-Editor");
                var stepDataType = System.Type.GetType(
                    "SimulationSystem.V02.Editor.SimToolStepData, Assembly-CSharp-Editor");

                if (ttsWindowType == null || stepDataType == null)
                {
                    Debug.LogError("[Parts ID Wizard] Could not find TTSAudioGeneratorWindow or " +
                                   "SimToolStepData. Make sure both files are in an Editor/ folder.");
                }
                else
                {
                    var listType = typeof(List<>).MakeGenericType(stepDataType);
                    var stepsList = System.Activator.CreateInstance(listType);
                    var addMethod = listType.GetMethod("Add");
                    var nameField = stepDataType.GetField("stepName");
                    var langField = stepDataType.GetField("languageTexts");

                    foreach (var step in _steps)
                    {
                        var entry = System.Activator.CreateInstance(stepDataType);
                        nameField?.SetValue(entry, step.stepName);
                        langField?.SetValue(entry, new List<string>(step.languageTexts));
                        addMethod?.Invoke(stepsList, new[] { entry });
                    }

                    var getOrOpen = ttsWindowType.GetMethod("GetOrOpen",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var ttsWindow = getOrOpen?.Invoke(null, null);

                    if (ttsWindow != null)
                    {
                        var receive = ttsWindowType.GetMethod("ReceiveFromWizard");
                        receive?.Invoke(ttsWindow, new object[]
                        {
                            stepsList,
                            new List<string>(_languages ?? new List<string>())
                        });
                        Debug.Log("[Parts ID Wizard] ✔ Audio generation started.");
                    }
                    else
                        Debug.LogError("[Parts ID Wizard] Could not open TTSAudioGeneratorWindow.");
                }
            }

            // ── Log summary to console — no blocking dialog ───────────────────
            string warnSuffix = warnings.Count > 0
                ? $" {warnings.Count} warning(s) — see Console for details." : string.Empty;
            Debug.Log($"[Parts ID Wizard] ✔ Generated {newStates.Count} state(s).{warnSuffix}");

            Repaint();
        }

        // ── GUI helpers (Window 2) ────────────────────────────────────────────

        private static void DrawSectionLabel(string text)
        {
            GUILayout.Space(2);
            EditorGUILayout.LabelField(text,
                new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold });
        }

        private static string SanitizeFileName(string text)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                text = text.Replace(c, '_');
            return text.Trim();
        }
    }

}