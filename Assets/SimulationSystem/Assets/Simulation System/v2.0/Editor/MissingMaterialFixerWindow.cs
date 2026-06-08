// MissingMaterialFixerWindow.cs
// Open via: Tools → Missing Material Fixer

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MissingMaterialFixerWindow : EditorWindow
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly List<MeshRenderer> _missingList  = new();
    private readonly List<MeshRenderer> _matchedList  = new();  // parallel to _missingList

    private Vector2 _scroll1;
    private Vector2 _scroll2;

    private const string DUP_FOLDER = "Assets/DuplicatedMaterials";

    // ── Open ──────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Missing Material Fixer")]
    public static void Open() =>
        GetWindow<MissingMaterialFixerWindow>("Missing Material Fixer");

    // ── GUI ───────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Missing Material Fixer", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        EditorGUILayout.HelpBox(
            "Step 1 — find broken meshes. Step 2 — locate donor meshes by shared mesh name. Step 3 — apply duplicated materials.",
            MessageType.Info);
        EditorGUILayout.Space(6);

        DrawSection1();
        GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));
        DrawSection2();
        GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));
        DrawSection3();
    }

    // ── Section 1: Find meshes with no material ───────────────────────────────

    private void DrawSection1()
    {
        EditorGUILayout.LabelField("1  —  Meshes With No Material", EditorStyles.boldLabel);

        if (GUILayout.Button("Find Meshes With No Material", GUILayout.Height(26)))
            RunFindMissing();

        if (_missingList.Count == 0) return;

        EditorGUILayout.LabelField($"Found {_missingList.Count} mesh(es) missing material(s):");
        _scroll1 = EditorGUILayout.BeginScrollView(_scroll1, GUILayout.Height(130));
        for (int i = 0; i < _missingList.Count; i++)
        {
            var r = _missingList[i];
            if (r == null) { EditorGUILayout.LabelField($"[{i}] (destroyed)"); continue; }

            Rect row = EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(r, typeof(MeshRenderer), true);
            var mf = r.GetComponent<MeshFilter>();
            string meshName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "—";
            EditorGUILayout.LabelField(meshName, EditorStyles.miniLabel, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            // Right-click context menu on each row
            if (Event.current.type == EventType.ContextClick && row.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                var captured = r;
                menu.AddItem(new GUIContent("Ping in Hierarchy"), false,
                    () => { EditorGUIUtility.PingObject(captured); Selection.activeGameObject = captured.gameObject; });
                menu.AddItem(new GUIContent("Select GameObject"), false,
                    () => Selection.activeGameObject = captured.gameObject);
                menu.ShowAsContext();
                Event.current.Use();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    // ── Section 2: Find same-name meshes (excluding the missing list) ─────────

    private void DrawSection2()
    {
        EditorGUILayout.LabelField("2  —  Find Matching Meshes By Mesh Name", EditorStyles.boldLabel);

        GUI.enabled = _missingList.Count > 0;
        if (GUILayout.Button("Find Matching Meshes (Exclude Missing List)", GUILayout.Height(26)))
            RunFindMatches();
        GUI.enabled = true;

        if (_matchedList.Count == 0) return;

        int hitCount = _matchedList.Count(m => m != null);
        EditorGUILayout.LabelField($"Matched {hitCount} / {_missingList.Count} mesh(es):");
        _scroll2 = EditorGUILayout.BeginScrollView(_scroll2, GUILayout.Height(130));
        for (int i = 0; i < _matchedList.Count; i++)
        {
            var matched  = _matchedList[i];
            var missing  = i < _missingList.Count ? _missingList[i] : null;
            string srcLabel = missing != null ? missing.gameObject.name : "?";

            Rect row = EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"← {srcLabel}", EditorStyles.miniLabel, GUILayout.Width(160));
            if (matched != null)
                EditorGUILayout.ObjectField(matched, typeof(MeshRenderer), true);
            else
                EditorGUILayout.LabelField("(no match found)", EditorStyles.helpBox);
            EditorGUILayout.EndHorizontal();

            if (matched != null && Event.current.type == EventType.ContextClick && row.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                var cap = matched;
                menu.AddItem(new GUIContent("Ping in Hierarchy"), false,
                    () => { EditorGUIUtility.PingObject(cap); Selection.activeGameObject = cap.gameObject; });
                menu.AddItem(new GUIContent("Select GameObject"), false,
                    () => Selection.activeGameObject = cap.gameObject);
                menu.ShowAsContext();
                Event.current.Use();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    // ── Section 3: Replace materials with duplicated copies ───────────────────

    private void DrawSection3()
    {
        EditorGUILayout.LabelField("3  —  Replace Missing Materials (Duplicated Copy)", EditorStyles.boldLabel);

        bool canReplace = _missingList.Count > 0 && _matchedList.Any(m => m != null);
        GUI.enabled = canReplace;
        if (GUILayout.Button("Replace Missing Materials (Duplicated)", GUILayout.Height(26)))
        {
            if (EditorUtility.DisplayDialog(
                    "Replace Materials",
                    $"Duplicate and apply materials to {_matchedList.Count(m => m != null)} mesh(es)?\nDuplicates will be saved in: {DUP_FOLDER}",
                    "Yes", "Cancel"))
                RunReplaceMaterials();
        }
        GUI.enabled = true;
    }

    // ── Logic ─────────────────────────────────────────────────────────────────

    private void RunFindMissing()
    {
        _missingList.Clear();
        _matchedList.Clear();

        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (HasMissingMaterial(r))
                _missingList.Add(r);
        }

        Debug.Log($"[MissingMaterialFixer] {_missingList.Count} mesh(es) with null/missing material(s).");
        Repaint();
    }

    private void RunFindMatches()
    {
        _matchedList.Clear();

        // Build exclusion set — all objects already in the missing list
        var missingSet = new HashSet<MeshRenderer>(_missingList);

        // Candidates: in scene, not in missing list, have valid materials
        var candidates = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => !missingSet.Contains(r) && !HasMissingMaterial(r))
            .ToArray();

        foreach (var missing in _missingList)
        {
            if (missing == null) { _matchedList.Add(null); continue; }

            string targetMeshName = GetSharedMeshName(missing);
            MeshRenderer match = null;

            if (!string.IsNullOrEmpty(targetMeshName))
            {
                // Compare by MeshFilter.sharedMesh.name — handles 2 different asset files
                // sharing the same mesh name: the missing-list exclusion above keeps them separate
                match = candidates.FirstOrDefault(c =>
                {
                    var mf = c.GetComponent<MeshFilter>();
                    return mf != null && mf.sharedMesh != null
                           && mf.sharedMesh.name == targetMeshName;
                });
            }

            _matchedList.Add(match);
        }

        int found = _matchedList.Count(m => m != null);
        Debug.Log($"[MissingMaterialFixer] Matched {found}/{_missingList.Count} mesh(es).");
        Repaint();
    }

    private void RunReplaceMaterials()
    {
        EnsureDupFolder();
        int replaced = 0;

        for (int i = 0; i < _missingList.Count; i++)
        {
            var target = _missingList[i];
            var source = i < _matchedList.Count ? _matchedList[i] : null;
            if (target == null || source == null) continue;

            var sourceMats = source.sharedMaterials;
            var newMats    = new Material[sourceMats.Length];

            for (int j = 0; j < sourceMats.Length; j++)
            {
                var mat = sourceMats[j];
                if (mat == null) { newMats[j] = null; continue; }

                newMats[j] = DuplicateMaterial(mat);
            }

            Undo.RecordObject(target, "Replace Missing Material");
            target.sharedMaterials = newMats;
            EditorUtility.SetDirty(target);
            replaced++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MissingMaterialFixer] Applied duplicated material(s) to {replaced} mesh(es). Saved in: {DUP_FOLDER}");
        Repaint();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool HasMissingMaterial(MeshRenderer r)
    {
        var mats = r.sharedMaterials;
        return mats == null || mats.Length == 0 || mats.Any(m => m == null);
    }

    private static string GetSharedMeshName(MeshRenderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : null;
    }

    private static Material DuplicateMaterial(Material source)
    {
        string srcPath = AssetDatabase.GetAssetPath(source);
        string destPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{DUP_FOLDER}/{source.name}_copy.mat");

        if (!string.IsNullOrEmpty(srcPath))
        {
            AssetDatabase.CopyAsset(srcPath, destPath);
            return AssetDatabase.LoadAssetAtPath<Material>(destPath);
        }

        // Source is not a project asset — instantiate and save
        var dup = new Material(source);
        AssetDatabase.CreateAsset(dup, destPath);
        return dup;
    }

    private static void EnsureDupFolder()
    {
        if (!AssetDatabase.IsValidFolder(DUP_FOLDER))
            AssetDatabase.CreateFolder("Assets", "DuplicatedMaterials");
    }
}
