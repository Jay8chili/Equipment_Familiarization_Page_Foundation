using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;

namespace SimulationSystem.V02.Editor
{
    public class TTSAudioGeneratorWindow : EditorWindow
    {
        // ================= CONFIG =================
        private const string VOICES_API = "https://p6mhete3gf.ap-south-1.awsapprunner.com/api/voices";
        private const string GENERATE_API = "https://p6mhete3gf.ap-south-1.awsapprunner.com/api/generate-steps-audio";
        private const string PREVIEW_API = "https://p6mhete3gf.ap-south-1.awsapprunner.com/api/generate-audio";

        [HideInInspector]
        private string authKey = "9c1f227e04bf6bb0fd3f0a59767976b938b21cbb619ee409e7e68b4377d674e8";

        // ================= LANGUAGE =================
        private string[] languageOptions = new string[] { "en-US", "en-IN", "hi-IN", "es-ES" };
        private int selectedLanguageIndex = 0;
        private string SelectedLanguage => languageOptions[selectedLanguageIndex];

        // ================= VOICES =================
        private string selectedVoice = null;
        private List<string> availableVoices = new List<string>();

        // ================= PREVIEW =================
        private string previewText = "Hello, this is a test prompt.";
        private AudioClip previewClip;
        private bool isPreviewLoading = false;
        private bool isPreviewPlaying = false;

        private GameObject previewAudioObject;
        private AudioSource previewAudioSource;

        // ================= USER LIST =================
        private List<GameObject> simulationObjects = new List<GameObject>();
        private Vector2 scroll;

        // ================= WIZARD INTEGRATION =================
        // Populated by ReceiveFromWizard() — holds per-step multi-language data.
        // Null when in manual mode (user-dragged objects only).
        private List<WizardStepAudioData> _wizardData = null;

        // Maps common TSV column header names → TTS language codes.
        private static readonly Dictionary<string, string> HeaderToLangCode =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
            { "english", "en-US" }, { "en",      "en-US" }, { "en-us", "en-US" }, { "en-in", "en-IN" },
            { "hindi",   "hi-IN" }, { "hi",      "hi-IN" }, { "hi-in", "hi-IN" },
            { "spanish", "es-ES" }, { "es",      "es-ES" }, { "es-es", "es-ES" },
            { "french",  "fr-FR" }, { "fr",      "fr-FR" },
            { "german",  "de-DE" }, { "de",      "de-DE" },
            };

        // ================= WINDOW =================
        [MenuItem("Tools/TTS Audio Generator")]
        public static void ShowWindow()
        {
            GetWindow<TTSAudioGeneratorWindow>("TTS Audio Generator");
        }

        /// <summary>Opens the window if not already open and returns the instance.</summary>
        public static TTSAudioGeneratorWindow GetOrOpen()
        {
            var w = GetWindow<TTSAudioGeneratorWindow>("TTS Audio Generator");
            w.Focus();
            return w;
        }

        private void OnDisable()
        {
            if (previewAudioObject != null)
            {
                DestroyImmediate(previewAudioObject);
            }

            EditorApplication.update -= CheckPreviewPlayback;
        }

        private void OnGUI()
        {
            GUILayout.Label("TTS Audio Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Language
            selectedLanguageIndex = EditorGUILayout.Popup(
                "Language Code",
                selectedLanguageIndex,
                languageOptions
            );

            if (GUILayout.Button("Fetch Voices"))
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(FetchVoicesCoroutine());
            }

            EditorGUILayout.Space();
            DrawVoiceDropdown();

            EditorGUILayout.Space(10);
            DrawPreviewSection();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Simulation Objects", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            int removeIndex = -1;

            for (int i = 0; i < simulationObjects.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                simulationObjects[i] = (GameObject)EditorGUILayout.ObjectField(
                    simulationObjects[i],
                    typeof(GameObject),
                    true
                );

                if (GUILayout.Button("X", GUILayout.Width(25)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();

                if (simulationObjects[i] != null)
                {
                    SimulationState state =
                        simulationObjects[i].GetComponent<SimulationState>();

                    if (state == null)
                    {
                        EditorGUILayout.HelpBox(
                            "No SimulationState component found!",
                            MessageType.Warning
                        );
                    }
                    else
                    {
                        EditorGUILayout.LabelField(
                            "Text Preview:",
                            Truncate(state.promptText, 80)
                        );
                    }
                }

                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
                simulationObjects.RemoveAt(removeIndex);

            EditorGUILayout.EndScrollView();

            DrawDragAndDropArea();

            if (GUILayout.Button("Add GameObject"))
            {
                simulationObjects.Add(null);
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = simulationObjects.Count > 0;

            if (GUILayout.Button("Generate Audio", GUILayout.Height(35)))
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(GenerateAudioCoroutine());
            }

            // Clear audio button — removes promptAudio from every state in the list.
            GUI.color = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("Clear Audio from All", GUILayout.Height(28)))
            {
                ClearAudioFromAll();
            }
            GUI.color = Color.white;

            GUI.enabled = true;
        }

        // ================= PREVIEW UI =================
        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("TTS Preview", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Preview Text:");
            previewText = EditorGUILayout.TextArea(previewText, GUILayout.Height(60));

            EditorGUILayout.Space(5);

            GUI.enabled = !isPreviewLoading;

            if (GUILayout.Button("Generate Preview"))
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(PreviewAudioCoroutine());
            }

            GUI.enabled = true;

            if (isPreviewLoading)
            {
                EditorGUILayout.HelpBox("Generating preview...", MessageType.Info);
            }

            if (previewClip != null)
            {
                EditorGUILayout.BeginHorizontal();

                if (!isPreviewPlaying)
                {
                    if (GUILayout.Button("Play"))
                    {
                        PlayPreviewClip();
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop"))
                    {
                        StopPreviewClip();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        // ================= PREVIEW COROUTINE =================
        private IEnumerator PreviewAudioCoroutine()
        {
            if (string.IsNullOrWhiteSpace(previewText))
            {
                Debug.LogWarning("Preview text is empty.");
                yield break;
            }

            isPreviewLoading = true;

            var requestBody = new PreviewRequest
            {
                languageCode = SelectedLanguage,
                voiceName = selectedVoice ?? "",
                text = previewText
            };

            string json = JsonUtility.ToJson(requestBody);
            byte[] jsonToSend = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(PREVIEW_API, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(jsonToSend);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-auth-key", authKey);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Preview Error: " + request.error);
                    isPreviewLoading = false;
                    yield break;
                }

                byte[] audioBytes = request.downloadHandler.data;

                if (audioBytes == null || audioBytes.Length == 0)
                {
                    Debug.LogError("Empty audio received.");
                    isPreviewLoading = false;
                    yield break;
                }

                string folderPath = "Assets/GeneratedAudio";
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string filePath = Path.Combine(folderPath, "PreviewAudio.mp3");

                File.WriteAllBytes(filePath, audioBytes);

                AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();

                // Force synchronous import
                AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceSynchronousImport);

                previewClip = AssetDatabase.LoadAssetAtPath<AudioClip>(filePath);

                if (previewClip == null)
                {
                    Debug.LogError("AudioClip failed to load.");
                }
                else
                {
                    Debug.Log("Preview clip loaded successfully.");
                    PlayPreviewClip(); // 🔥 Auto play
                }
            }

            isPreviewLoading = false;
        }

        // ================= PLAYBACK =================
        private void PlayPreviewClip()
        {
            if (previewClip == null)
            {
                Debug.LogError("Preview clip is null.");
                return;
            }

            if (previewAudioObject == null)
            {
                previewAudioObject = new GameObject("TTS_Preview_Audio");
                previewAudioObject.hideFlags = HideFlags.HideAndDontSave;
                previewAudioSource = previewAudioObject.AddComponent<AudioSource>();
            }

            previewAudioSource.Stop();
            previewAudioSource.clip = previewClip;
            previewAudioSource.playOnAwake = false;
            previewAudioSource.loop = false;

            previewAudioSource.Play();
            isPreviewPlaying = true;

            EditorApplication.update -= CheckPreviewPlayback;
            EditorApplication.update += CheckPreviewPlayback;

            Debug.Log("Playing preview...");
        }

        private void StopPreviewClip()
        {
            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
            }

            isPreviewPlaying = false;
            EditorApplication.update -= CheckPreviewPlayback;
        }

        private void CheckPreviewPlayback()
        {
            if (previewAudioSource == null) return;

            if (!previewAudioSource.isPlaying)
            {
                isPreviewPlaying = false;
                EditorApplication.update -= CheckPreviewPlayback;
                Repaint();
            }
        }

        // ================= FETCH VOICES =================
        private IEnumerator FetchVoicesCoroutine()
        {
            string url = $"{VOICES_API}?languageCode={SelectedLanguage}";

            Debug.Log(VOICES_API);

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("x-auth-key", authKey);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Voice Fetch Error: " + request.error);
                    yield break;
                }

                string json = request.downloadHandler.text;

                VoiceListResponse response =
                    JsonUtility.FromJson<VoiceListResponse>(json);

                availableVoices.Clear();

                if (response != null && response.voices != null)
                {
                    foreach (var v in response.voices)
                    {
                        if (!string.IsNullOrWhiteSpace(v.name))
                            availableVoices.Add(v.name.Trim());
                    }
                }

                availableVoices.Sort();
                selectedVoice = null;
                Repaint();
            }
        }

        // ================= BATCH GENERATION =================
        private IEnumerator GenerateAudioCoroutine()
        {
            string project = new DirectoryInfo(Application.dataPath).Parent.Name;

            // Collect valid (state, index) pairs first.
            var entries = new List<(SimulationState state, int idx)>();
            for (int i = 0; i < simulationObjects.Count; i++)
            {
                if (simulationObjects[i] == null) continue;
                SimulationState state = simulationObjects[i].GetComponent<SimulationState>();
                if (state == null || string.IsNullOrEmpty(state.promptText)) continue;
                entries.Add((state, i + 1)); // 1-based index as step number
            }

            if (entries.Count == 0)
            {
                Debug.LogWarning("[TTS] No valid SimulationState objects with prompt text found.");
                yield break;
            }

            string folder = Path.Combine("Assets", "GeneratedAudio", project, SelectedLanguage);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            int done = 0;
            foreach (var (state, idx) in entries)
            {
                string fileName = $"{idx}_{SelectedLanguage}.mp3";
                string assetPath = Path.Combine(folder, fileName);

                var body = JsonUtility.ToJson(new PreviewRequest
                {
                    languageCode = SelectedLanguage,
                    voiceName = selectedVoice ?? "",
                    text = state.promptText.Trim()
                });

                using (var req = new UnityWebRequest(PREVIEW_API, "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("x-auth-key", authKey);
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[TTS] Failed for '{state.name}': {req.error}");
                        continue;
                    }

                    File.WriteAllBytes(assetPath, req.downloadHandler.data);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    if (clip == null)
                    {
                        Debug.LogError($"[TTS] Clip failed to load: {assetPath}");
                        continue;
                    }

                    // Assign to SimulationState.promptAudio.
                    var so = new SerializedObject(state);
                    var p = so.FindProperty("promptAudio");
                    if (p != null)
                    {
                        p.objectReferenceValue = clip;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(state);
                    }

                    done++;
                    Debug.Log($"[TTS] ✔ {assetPath} → assigned to '{state.name}'.");
                }

                yield return new EditorWaitForSeconds(0.1f);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Audio Complete",
                $"Generated and assigned {done}/{entries.Count} clip(s).\n" +
                $"Saved to: Assets/GeneratedAudio/{project}/{SelectedLanguage}/", "OK");
            Repaint();
        }

        // ================= CLEAR AUDIO =================
        private void ClearAudioFromAll()
        {
            int cleared = 0;
            foreach (var obj in simulationObjects)
            {
                if (obj == null) continue;
                SimulationState state = obj.GetComponent<SimulationState>();
                if (state == null) continue;

                var so = new SerializedObject(state);
                var p = so.FindProperty("promptAudio");
                if (p != null && p.objectReferenceValue != null)
                {
                    p.objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(state);
                    cleared++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[TTS] Cleared promptAudio from {cleared} SimulationState(s).");
            EditorUtility.DisplayDialog("Clear Complete",
                $"Removed audio from {cleared} SimulationState(s).", "OK");
            Repaint();
        }

        // ================= HELPERS =================
        private void DrawVoiceDropdown()
        {
            List<string> options = new List<string>();
            options.Add("Default");
            options.AddRange(availableVoices);

            int selectedIndex = 0;

            if (!string.IsNullOrEmpty(selectedVoice))
            {
                int index = options.IndexOf(selectedVoice);
                if (index > 0)
                    selectedIndex = index;
            }

            selectedIndex = EditorGUILayout.Popup("Voice", selectedIndex, options.ToArray());
            selectedVoice = selectedIndex == 0 ? null : options[selectedIndex];
        }

        private void DrawDragAndDropArea()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop GameObjects Here");

            Event evt = Event.current;

            if (!dropArea.Contains(evt.mousePosition))
                return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    foreach (var draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is GameObject go)
                        {
                            if (!simulationObjects.Contains(go))
                                simulationObjects.Add(go);
                        }
                    }
                    evt.Use();
                    break;
            }
        }

        private string Truncate(string input, int length)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Length <= length
                ? input
                : input.Substring(0, length) + "...";
        }

        // ================= WIZARD INTEGRATION =================

        /// <summary>
        /// Called by the Simulation Step Wizard after generating states.
        /// Populates simulationObjects with the new states and triggers
        /// multi-language audio generation using wizard step data.
        /// </summary>
        public void ReceiveFromWizard(List<SimToolStepData> steps, List<string> langHeaders)
        {
            if (steps == null)
            {
                Debug.LogWarning("[TTS] ReceiveFromWizard: no step data provided.");
                return;
            }

            _wizardData = new List<WizardStepAudioData>();
            simulationObjects.Clear();

            // Resolve header → lang code for each column.
            var langs = new List<(string langCode, int colIdx)>();
            for (int c = 0; c < (langHeaders?.Count ?? 0); c++)
            {
                string h = langHeaders[c].Trim();
                string code = HeaderToLangCode.TryGetValue(h, out string lc) ? lc : h;
                langs.Add((code, c));
            }
            if (langs.Count == 0) langs.Add(("en-US", 0));

            int stepNo = 0;
            foreach (var step in steps)
            {
                stepNo++;

                string englishText = step.languageTexts.Count > 0
                    ? step.languageTexts[0].Trim() : step.stepName;
                string safeEnglish = SanitizeFileName(string.IsNullOrWhiteSpace(englishText)
                    ? step.stepName : englishText);
                string goName = $"{stepNo}_{safeEnglish}";

                SimulationState state = FindStateInScene(goName);
                if (state != null)
                    simulationObjects.Add(state.gameObject);

                var langTexts = new List<(string langCode, string text)>();
                foreach (var (langCode, colIdx) in langs)
                {
                    string text = colIdx < step.languageTexts.Count
                        ? step.languageTexts[colIdx].Trim() : string.Empty;
                    langTexts.Add((langCode, text));
                }

                _wizardData.Add(new WizardStepAudioData
                {
                    stepNo = stepNo,
                    state = state,
                    langTexts = langTexts
                });
            }

            Repaint();
            EditorCoroutineUtility.StartCoroutineOwnerless(WizardGenerateCoroutine());
        }

        private IEnumerator WizardGenerateCoroutine()
        {
            if (_wizardData == null || _wizardData.Count == 0) yield break;

            string project = new DirectoryInfo(Application.dataPath).Parent.Name;
            int total = _wizardData.Count;
            int current = 0;

            Debug.Log($"[TTS] Starting wizard audio generation for {total} step(s).");

            foreach (var entry in _wizardData)
            {
                current++;
                foreach (var (langCode, text) in entry.langTexts)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        Debug.Log($"[TTS] Step {entry.stepNo} ({langCode}): no text — skipped.");
                        continue;
                    }

                    string folder = Path.Combine("Assets", "GeneratedAudio", project, langCode);
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string fileName = $"{entry.stepNo}_{langCode}.mp3";
                    string assetPath = Path.Combine(folder, fileName);

                    var body = JsonUtility.ToJson(new PreviewRequest
                    {
                        languageCode = langCode,
                        voiceName = selectedVoice ?? "",
                        text = text
                    });

                    using (var req = new UnityWebRequest(PREVIEW_API, "POST"))
                    {
                        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                        req.downloadHandler = new DownloadHandlerBuffer();
                        req.SetRequestHeader("Content-Type", "application/json");
                        req.SetRequestHeader("x-auth-key", authKey);
                        yield return req.SendWebRequest();

                        if (req.result != UnityWebRequest.Result.Success)
                        {
                            Debug.LogError($"[TTS] Step {entry.stepNo} ({langCode}) failed: {req.error}");
                            continue;
                        }

                        File.WriteAllBytes(assetPath, req.downloadHandler.data);
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                        if (clip == null)
                        {
                            Debug.LogError($"[TTS] Clip failed to load: {assetPath}");
                            continue;
                        }

                        Debug.Log($"[TTS] ✔ {assetPath}");

                        // Only the English column (index 0) is assigned to promptAudio.
                        bool isPrimary = entry.langTexts.Count > 0 &&
                                         entry.langTexts[0].langCode == langCode;
                        if (isPrimary && entry.state != null)
                        {
                            var so = new SerializedObject(entry.state);
                            var p = so.FindProperty("promptAudio");
                            if (p != null)
                            {
                                p.objectReferenceValue = clip;
                                so.ApplyModifiedPropertiesWithoutUndo();
                                EditorUtility.SetDirty(entry.state);
                            }
                        }
                    }

                    yield return new EditorWaitForSeconds(0.1f);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _wizardData = null;

            Debug.Log($"[TTS] ✔ Wizard audio generation complete ({current}/{total} steps).");
            EditorUtility.DisplayDialog("Audio Complete",
                $"Generated audio for {current} step(s).\n" +
                $"Saved to: Assets/GeneratedAudio/{project}/", "OK");
            Repaint();
        }

        private static SimulationState FindStateInScene(string goName)
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().GetRootGameObjects())
            {
                var s = FindRecursive(root.transform, goName);
                if (s != null) return s;
            }
            return null;
        }

        private static SimulationState FindRecursive(Transform t, string name)
        {
            if (t.name == name)
            {
                var s = t.GetComponent<SimulationState>();
                if (s != null) return s;
            }
            foreach (Transform child in t)
            {
                var s = FindRecursive(child, name);
                if (s != null) return s;
            }
            return null;
        }

        private static string SanitizeFileName(string text)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                text = text.Replace(c, '_');
            return text.Trim();
        }
    }
}

    // ================= WIZARD STEP AUDIO DATA =================
    public class WizardStepAudioData
{
    public int stepNo;
    public SimulationState state;
    public List<(string langCode, string text)> langTexts;
}

// ================= RESPONSE MODELS =================
[Serializable]
public class VoiceListResponse
{
    public List<VoiceItem> voices;
    public int total;
}

[Serializable]
public class VoiceItem
{
    public string name;
    public string fullName;
    public string ssmlGender;
}

[Serializable]
public class PreviewRequest
{
    public string languageCode;
    public string voiceName;
    public string text;
}