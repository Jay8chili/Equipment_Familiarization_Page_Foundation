using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SimulationPromptExtractor
{
    [MenuItem("Tools/Extract Simulation Prompts")]
    public static void ExtractPrompts()
    {
        var manager = Object.FindObjectOfType<SimulationManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Extract Simulation Prompts",
                "No SimulationManager found in the current scene.", "OK");
            return;
        }

        var sb = new StringBuilder();

        for (int i = 0; i < manager.transform.childCount; i++)
        {
            var state = manager.transform.GetChild(i).GetComponent<SimulationState>();
            if (state == null) continue;
            sb.AppendLine(state.promptText);
        }

        string savePath = EditorUtility.SaveFilePanel(
            "Save Prompt Texts", Application.dataPath,
            "SimulationPrompts.txt", "txt");

        if (string.IsNullOrEmpty(savePath)) return;

        File.WriteAllText(savePath, sb.ToString(), Encoding.UTF8);

        if (savePath.StartsWith(Application.dataPath))
            AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Extract Simulation Prompts", $"Done!\n\n{savePath}", "OK");
    }
}
