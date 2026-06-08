using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using SimulationSystem.V02.StateInteractions;

[CustomEditor(typeof(SimulationState))]
public class SimulationStateEditor : Editor
{
    private ReorderableList interactionsList;

    private const float LINE = 18f;
    private const float GAP = 2f;
    private const float ROW = LINE + GAP;
    private const float DRAG_W = 20f;
    private const float FOLDOUT_W = 14f;

    private static readonly Color BOT_COLOUR = new Color(0.30f, 0.70f, 1.00f, 1f);
    private static readonly Color SCENE_COLOUR = new Color(0.35f, 0.80f, 0.45f, 1f);
    private static readonly Color HEADER_COLOUR = new Color(0.18f, 0.18f, 0.18f, 0.5f);
    private static readonly Color TIMING_COLOUR = new Color(0.85f, 0.60f, 0.20f, 0.35f);

    private readonly Dictionary<int, bool> _foldouts = new Dictionary<int, bool>();
    private readonly Dictionary<int, SerializedObject> _soCache = new Dictionary<int, SerializedObject>();

    private static readonly string[] ACTIVATION_EVENT_NAMES =
    {
        "OnActivateInteraction",
        "OnDeactivateInteraction",
    };

    private static readonly string[] BASE_EVENT_NAMES =
    {
        "OnInteractionStartedEvent",
        "OnInteractionOngoingEvent",
        "OnInteractionSuspendedEvent",
        "OnInteractionCompletedEvent",
    };

    private void OnEnable()
    {
        interactionsList = new ReorderableList(
            serializedObject,
            serializedObject.FindProperty("listOfInteractions"),
            draggable: true, displayHeader: true,
            displayAddButton: true, displayRemoveButton: true);

        interactionsList.drawHeaderCallback = r => EditorGUI.LabelField(r, "Interactions");
        interactionsList.drawElementCallback = (rect, i, active, focused) => ProcessElement(rect, i, draw: true);
        interactionsList.elementHeightCallback = i => ProcessElement(default, i, draw: false);
    }

    private void OnDisable()
    {
        foreach (var so in _soCache.Values) so.Dispose();
        _soCache.Clear();
    }

    private float ProcessElement(Rect rect, int index, bool draw)
    {
        var element = interactionsList.serializedProperty.GetArrayElementAtIndex(index);
        var ctx = new DrawContext(rect, draw);

        var obj = element.objectReferenceValue as Interactions;

        if (draw)
        {
            if (obj != null)
            {
                int id = obj.GetInstanceID();
                if (!_foldouts.ContainsKey(id)) _foldouts[id] = true;

                var arrowRect = new Rect(rect.x + DRAG_W, rect.y, FOLDOUT_W, LINE);
                bool prev = _foldouts[id];
                bool next = EditorGUI.Foldout(arrowRect, prev, GUIContent.none, true);
                if (next != prev)
                {
                    _foldouts[id] = next;
                    Repaint();
                }

                float fieldX = rect.x + DRAG_W + FOLDOUT_W;
                EditorGUI.PropertyField(
                    new Rect(fieldX, rect.y, rect.width - DRAG_W - FOLDOUT_W, LINE),
                    element, GUIContent.none);
            }
            else
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x + DRAG_W, rect.y, rect.width - DRAG_W, LINE),
                    element, GUIContent.none);
            }
        }
        ctx.Advance(LINE + GAP);

        if (obj == null) return ctx.TotalHeight;

        int instId = obj.GetInstanceID();
        bool expanded = !_foldouts.ContainsKey(instId) || _foldouts[instId];
        if (!expanded) return ctx.TotalHeight;

        if (!_soCache.TryGetValue(instId, out var so) || so.targetObject == null)
        {
            so = new SerializedObject(obj);
            _soCache[instId] = so;
        }
        so.Update();

        ctx.Indent(DRAG_W + FOLDOUT_W);

        if (obj is UIInteraction ui) DrawUIInteraction(ctx, so, ui);
        else if (obj is GazeInteraction) DrawGazeInteraction(ctx, so);
        else if (obj is DetectInteraction) DrawDetectInteraction(ctx, so);
        else if (obj is GrabInteraction) DrawGrabInteraction(ctx, so);
        else if (obj is IdleInteraction) DrawIdleInteraction(ctx, so);
        else DrawFallback(ctx, so);

        if (draw) so.ApplyModifiedProperties();

        return ctx.TotalHeight;
    }

    // ── Interaction Drawers ──────────────────────────────────────────────────

    private void DrawUIInteraction(DrawContext ctx, SerializedObject so, UIInteraction ui)
    {
        ctx.Header("UI Settings");
        DrawFloat(ctx, so, "Time", "Hold Time");
        DrawObject(ctx, so, "uiPanel", "Panel", typeof(GameObject));
        DrawObject(ctx, so, "uiText", "Text Mesh Pro", typeof(TMP_Text));

        var contentProp = so.FindProperty("content");
        var textProp = so.FindProperty("uiText");
        if (contentProp != null)
        {
            if (ctx.Draw)
            {
                var tmpComp = textProp?.objectReferenceValue as TMP_Text;
                EditorGUI.BeginChangeCheck();
                string newVal = EditorGUI.TextField(ctx.NextRect(LINE), "Content", contentProp.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    contentProp.stringValue = newVal;
                    EditorUtility.SetDirty(ui);
                    if (tmpComp != null)
                    {
                        Undo.RecordObject(tmpComp, "Edit TMP Text");
                        tmpComp.text = newVal;
                        EditorUtility.SetDirty(tmpComp);
                    }
                }
            }
            ctx.Advance(ROW);
        }

        DrawObject(ctx, so, "button", "Button", typeof(CustomButton));

        var panelProp = so.FindProperty("uiPanel");
        var buttonProp = so.FindProperty("button");
        bool isBotMode = panelProp?.objectReferenceValue == null
                      && buttonProp?.objectReferenceValue == null
                      && textProp?.objectReferenceValue == null;
        if (ctx.Draw) DrawModeBadge(ctx.NextRect(LINE), isBotMode);
        ctx.Advance(ROW);

        DrawActivationEvents(ctx, so);
        ctx.Header("Lifecycle Events");
        DrawBaseEvents(ctx, so);
    }

    private void DrawGazeInteraction(DrawContext ctx, SerializedObject so)
    {
        ctx.Header("Gaze Settings");
        DrawFloat(ctx, so, "Time", "Gaze Duration");
        DrawBool(ctx, so, "resetTimer", "Reset Timer");
        DrawObject(ctx, so, "objectToCastRay", "Ray Origin (Transform)", typeof(Transform));

        DrawHighlightSection(ctx, so);
        DrawActivationEvents(ctx, so);
        ctx.Header("Lifecycle Events");
        DrawBaseEvents(ctx, so);
    }

    private void DrawDetectInteraction(DrawContext ctx, SerializedObject so)
    {
        ctx.Header("Detect Settings");
        DrawFloat(ctx, so, "Time", "Time");
        DrawBool(ctx, so, "resetTimer", "Reset Timer");
        DrawBool(ctx, so, "detectSeparately", "Detect Separately");
        DrawBool(ctx, so, "TeleportObjectToDetect", "Teleport Object To Detect");
        ctx.Prop(so, "ObjectsToBeDetectedList", "Objects To Detect", includeChildren: true);

        ctx.Header("Discard");
        DrawEnum(ctx, so, "discardMode", "Discard Mode");
        var modeProp = so.FindProperty("discardMode");
        bool isGivenPos = modeProp != null && modeProp.enumValueIndex == 1;
        EditorGUI.BeginDisabledGroup(ctx.Draw && !isGivenPos);
        DrawObject(ctx, so, "discardTarget", "Discard Target", typeof(Transform));
        EditorGUI.EndDisabledGroup();
        DrawBool(ctx, so, "dissolve", "Dissolve On Discard");
        DrawFloat(ctx, so, "discardMoveDuration", "Discard Move Duration");
        DrawEnum(ctx, so, "discardMoveEase", "Discard Move Ease");

        DrawHighlightSection(ctx, so);
        DrawActivationEvents(ctx, so);
        ctx.Header("Lifecycle Events");
        DrawBaseEvents(ctx, so);
    }

    private void DrawGrabInteraction(DrawContext ctx, SerializedObject so)
    {
        ctx.Header("Grab Settings");
        DrawFloat(ctx, so, "Time", "Time");
        DrawEnum(ctx, so, "grabBehaviourOverride", "Grab Behaviour Override");
        DrawBool(ctx, so, "TeleportThisObject", "Teleport This Object");

        ctx.Header("Proximity Grab");
        DrawFloat(ctx, so, "grabThisObjectIn", "Grab Delay (seconds)");

        ctx.Header("Poses — Hand Tracking");
        DrawObject(ctx, so, "poseHandLeft", "Pose Hand Left", typeof(RecordedPose));
        DrawObject(ctx, so, "poseHandRight", "Pose Hand Right", typeof(RecordedPose));

        ctx.Header("Poses — Controller");
        DrawObject(ctx, so, "poseControllerLeft", "Pose Controller Left", typeof(RecordedPose));
        DrawObject(ctx, so, "poseControllerRight", "Pose Controller Right", typeof(RecordedPose));
        DrawVector3(ctx, so, "posePositionOffset", "Pose Position Offset");

        ctx.Header("Reset");
        DrawBool(ctx, so, "resetOnRelease", "Reset On Release");
        DrawFloat(ctx, so, "resetDelay", "Reset Delay");

        ctx.Header("Grab Events");
        ctx.Prop(so, "onGrabbed", "On Grabbed", includeChildren: true);
        ctx.Prop(so, "onReleased", "On Released", includeChildren: true);
        ctx.Prop(so, "onResetComplete", "On Reset Complete", includeChildren: true);

        ctx.Header("Bot Discard");
        DrawBool(ctx, so, "discardOnComplete", "Discard On Complete");
        DrawObject(ctx, so, "discardTarget", "Discard Target", typeof(Transform));

        DrawHighlightSection(ctx, so);
        DrawActivationEvents(ctx, so);
        ctx.Header("Lifecycle Events");
        DrawBaseEvents(ctx, so);
    }

    private void DrawIdleInteraction(DrawContext ctx, SerializedObject so)
    {
        ctx.Header("Idle Settings");
        DrawBool(ctx, so, "waitForManualAdvance", "Wait For Manual Advance");

        var manualProp = so.FindProperty("waitForManualAdvance");
        bool isManual = manualProp != null && manualProp.boolValue;
        EditorGUI.BeginDisabledGroup(ctx.Draw && isManual);
        DrawFloat(ctx, so, "Time", "Time");
        DrawBool(ctx, so, "resetTimer", "Reset Timer");
        EditorGUI.EndDisabledGroup();

        DrawActivationEvents(ctx, so);
        ctx.Header("Lifecycle Events");
        DrawBaseEvents(ctx, so);
    }

    private void DrawFallback(DrawContext ctx, SerializedObject so)
    {
        DrawFloat(ctx, so, "Time", "Time");
        DrawActivationEvents(ctx, so);
        ctx.Header("Lifecycle Events");
        DrawBaseEvents(ctx, so);
    }

    // ── Shared Sections ───────────────────────────────────────────────────────

    private void DrawHighlightSection(DrawContext ctx, SerializedObject so)
    {
        ctx.Header("Highlight");

        var enableProp = so.FindProperty("enableHighlight");
        var targetProp = so.FindProperty("highlightTarget");
        var materialProp = so.FindProperty("highlightMaterial");

        if (enableProp == null) return;

        if (ctx.Draw)
        {
            EditorGUI.BeginChangeCheck();
            bool newVal = EditorGUI.Toggle(ctx.NextRect(LINE), "Enable Highlight", enableProp.boolValue);
            if (EditorGUI.EndChangeCheck()) { enableProp.boolValue = newVal; Repaint(); }
        }
        ctx.Advance(ROW);

        EditorGUI.BeginDisabledGroup(ctx.Draw && !enableProp.boolValue);

        if (targetProp != null)
        {
            if (ctx.Draw) EditorGUI.ObjectField(ctx.NextRect(LINE), targetProp, typeof(Renderer), new GUIContent("Highlight Target"));
            ctx.Advance(ROW);
        }
        if (materialProp != null)
        {
            if (ctx.Draw) EditorGUI.ObjectField(ctx.NextRect(LINE), materialProp, typeof(Material), new GUIContent("Highlight Material"));
            ctx.Advance(ROW);
        }

        EditorGUI.EndDisabledGroup();
    }

    // ── Primitive helpers ─────────────────────────────────────────────────────

    private void DrawFloat(DrawContext ctx, SerializedObject so, string propName, string label)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        if (ctx.Draw) { EditorGUI.BeginChangeCheck(); float v = EditorGUI.FloatField(ctx.NextRect(LINE), label, prop.floatValue); if (EditorGUI.EndChangeCheck()) prop.floatValue = v; }
        ctx.Advance(ROW);
    }

    private void DrawBool(DrawContext ctx, SerializedObject so, string propName, string label)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        if (ctx.Draw) { EditorGUI.BeginChangeCheck(); bool v = EditorGUI.Toggle(ctx.NextRect(LINE), label, prop.boolValue); if (EditorGUI.EndChangeCheck()) prop.boolValue = v; }
        ctx.Advance(ROW);
    }

    private void DrawObject(DrawContext ctx, SerializedObject so, string propName, string label, System.Type type)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        if (ctx.Draw) EditorGUI.ObjectField(ctx.NextRect(LINE), prop, type, new GUIContent(label));
        ctx.Advance(ROW);
    }

    private void DrawObjectComponent(DrawContext ctx, SerializedObject so, string propName, string label, System.Type type)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        if (ctx.Draw) { EditorGUI.BeginChangeCheck(); var v = EditorGUI.ObjectField(ctx.NextRect(LINE), label, prop.objectReferenceValue, type, true); if (EditorGUI.EndChangeCheck()) prop.objectReferenceValue = v; }
        ctx.Advance(ROW);
    }

    private void DrawEnum(DrawContext ctx, SerializedObject so, string propName, string label)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        if (ctx.Draw) { EditorGUI.BeginChangeCheck(); int v = EditorGUI.Popup(ctx.NextRect(LINE), label, prop.enumValueIndex, prop.enumDisplayNames); if (EditorGUI.EndChangeCheck()) prop.enumValueIndex = v; }
        ctx.Advance(ROW);
    }

    private void DrawVector3(DrawContext ctx, SerializedObject so, string propName, string label)
    {
        var prop = so.FindProperty(propName);
        if (prop == null) return;
        if (ctx.Draw) { EditorGUI.BeginChangeCheck(); Vector3 v = EditorGUI.Vector3Field(ctx.NextRect(LINE), label, prop.vector3Value); if (EditorGUI.EndChangeCheck()) prop.vector3Value = v; }
        ctx.Advance(ROW);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void DrawActivationEvents(DrawContext ctx, SerializedObject so)
    {
        ctx.Header("Activation");
        foreach (var name in ACTIVATION_EVENT_NAMES)
        {
            var prop = so.FindProperty(name);
            if (prop == null) continue;
            float h = EditorGUI.GetPropertyHeight(prop, includeChildren: true);
            if (ctx.Draw) EditorGUI.PropertyField(ctx.NextRect(h), prop, new GUIContent(prop.displayName), includeChildren: true);
            ctx.Advance(h + GAP);
        }
    }

    private void DrawBaseEvents(DrawContext ctx, SerializedObject so)
    {
        foreach (var name in BASE_EVENT_NAMES)
        {
            var prop = so.FindProperty(name);
            if (prop == null) continue;
            float h = EditorGUI.GetPropertyHeight(prop, includeChildren: true);
            if (ctx.Draw) EditorGUI.PropertyField(ctx.NextRect(h), prop, new GUIContent(prop.displayName), includeChildren: true);
            ctx.Advance(h + GAP);
        }
    }

    // ── Mode badge ────────────────────────────────────────────────────────────

    private static void DrawModeBadge(Rect rect, bool isBotMode)
    {
        var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = isBotMode ? BOT_COLOUR : SCENE_COLOUR;
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
        GUI.backgroundColor = prev;
        EditorGUI.LabelField(rect, isBotMode
            ? "BOT HANDLED  —  assign Panel / TMP / Button above to switch to Scene UI"
            : "SCENE UI  —  using assigned Panel, Text Mesh Pro, and Button", style);
    }

    // ── Root inspector ────────────────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("promptText"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("promptAudio"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("MoveToNextStepAfterAudio"));

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Teleport", EditorStyles.boldLabel);
        var teleportOnStartProp = serializedObject.FindProperty("teleportOnStart");
        EditorGUILayout.PropertyField(teleportOnStartProp, new GUIContent("Teleport On Start"));
        EditorGUI.BeginDisabledGroup(!teleportOnStartProp.boolValue);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("teleportTarget"), new GUIContent("Teleport Target"));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(6);

        // ── Timing section with tinted background ─────────────────────────
        EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = TIMING_COLOUR;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = prevBg;

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("delayAfterState"),
            new GUIContent("Delay After State (s)",
                "Seconds to wait after this state completes before advancing to the next state."));

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("delayBetweenInteractions"),
            new GUIContent("Delay Between Interactions (s)",
                "Seconds to wait between each interaction in the sequence."));

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onStateStart"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onStateComplete"));

        EditorGUILayout.Space(4);
        interactionsList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }

    // ── DrawContext ───────────────────────────────────────────────────────────

    private class DrawContext
    {
        public bool Draw;
        public float TotalHeight;

        private float _y, _x, _width;

        public DrawContext(Rect rect, bool draw)
        {
            Draw = draw; _y = rect.y; _x = rect.x; _width = rect.width; TotalHeight = 0;
        }

        public void Indent(float px) { _x += px; _width -= px; }
        public Rect NextRect(float h) => new Rect(_x, _y, _width, h);
        public void Advance(float amount) { _y += amount; TotalHeight += amount; }

        public void Field(SerializedProperty prop, GUIContent label, float height)
        {
            if (Draw) EditorGUI.PropertyField(NextRect(height), prop, label);
            Advance(height + GAP);
        }

        public void Header(string title)
        {
            if (Draw)
            {
                var r = NextRect(LINE);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = HEADER_COLOUR;
                GUI.Box(r, GUIContent.none, EditorStyles.helpBox);
                GUI.backgroundColor = prev;
                EditorGUI.LabelField(r, title, EditorStyles.boldLabel);
            }
            Advance(LINE + GAP + 2f);
        }

        public bool Prop(SerializedObject so, string propName, string label = null, bool includeChildren = false)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return false;
            float h = EditorGUI.GetPropertyHeight(prop, includeChildren: includeChildren);
            if (Draw) EditorGUI.PropertyField(NextRect(h), prop, label != null ? new GUIContent(label) : new GUIContent(prop.displayName), includeChildren);
            Advance(h + GAP);
            return true;
        }
    }
}
