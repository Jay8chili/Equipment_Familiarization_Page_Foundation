
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class TTSAudioGeneratorWindow : EditorWindow
{
    // ================= CONFIG =================
    private const string BASE_URL = "https://stagingapi.skillsforge.io/api";
    private const string VOICES_API = "https://stagingapi.skillsforge.io/api/voices";
    private const string GENERATE_API = "https://stagingapi.skillsforge.io/api/generate-steps-audio";
    private const string PREVIEW_API = "https://stagingapi.skillsforge.io/api/generate-audio";

    [HideInInspector]
    private string authKey = "45159708aad40123ec0b379053197ad80a01d1b1bd0ecd9788fdd7e8879d1499";

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

    // ================= WINDOW =================
    [MenuItem("Tools/TTS Audio Generator")]
    public static void ShowWindow()
    {
        GetWindow<TTSAudioGeneratorWindow>("TTS Audio Generator");
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
       /* languageCode = SelectedLanguage,
        voiceName = selectedVoice ?? "",
        text = previewText*/
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
        string username = CloudProjectSettings.userName;
        string project = new DirectoryInfo(Application.dataPath).Parent.Name;

        List<string> allTexts = new List<string>();

        foreach (var obj in simulationObjects)
        {
            if (obj == null) continue;

            SimulationState state = obj.GetComponent<SimulationState>();
            if (state == null || string.IsNullOrEmpty(state.promptText))
                continue;

            allTexts.Add(state.promptText.Trim());
        }

        if (allTexts.Count == 0)
        {
            Debug.LogWarning("No valid textPrompts found.");
            yield break;
        }

        string combinedText = string.Join("\n", allTexts);
        byte[] fileBytes = Encoding.UTF8.GetBytes(combinedText);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", fileBytes, "BatchTextPrompts.txt", "text/plain");
        form.AddField("languageCode", SelectedLanguage);
        form.AddField("voiceName", selectedVoice ?? "");
        form.AddField("userName", username);
        form.AddField("projectName", project);

        using (UnityWebRequest request = UnityWebRequest.Post(GENERATE_API, form))
        {
            request.SetRequestHeader("x-auth-key", authKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Batch Generate Error: " + request.error);
                yield break;
            }

            Debug.Log("Batch audio generation started successfully.");
        }
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
}

