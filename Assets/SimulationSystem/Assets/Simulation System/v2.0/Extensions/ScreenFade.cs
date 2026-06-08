using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace SimulationSystem.V02.Extensions
{
    /// <summary>
    /// Sphere-based screen transition.
    /// A small inverted sphere surrounds the camera and fills from bottom-to-top
    /// (fade out) or drains from top-to-bottom (fade in / reveal).
    ///
    /// Attach this to the Main Camera (or its parent in an XR rig).
    /// The sphere is created automatically as a child object.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        public static ScreenFade instance { get; private set; }

        [Header("Timing")]
        [Tooltip("Duration of a full fill or reveal transition")]
        public float fadeTime = 1.0f;

        [Header("Appearance")]
        [Tooltip("Colour of the sphere (usually black)")]
        public Color fadeColor = Color.black;

        [Tooltip("Softness of the horizontal wipe edge")]
        [Range(0f, 0.25f)]
        public float edgeSoftness = 0.06f;

        [Tooltip("Local scale of the sphere – keep it small so it sits "
               + "inside the near-clip plane")]
        public float sphereScale = 0.4f;

        [Header("Behaviour")]
        public bool fadeOnStart = true;

        [Header("Message")]
        [Tooltip("Font size for the transition message")]
        public float messageFontSize = 0.08f;
        [Tooltip("Distance of the message canvas in front of the camera")]
        public float messageDistance = 0.18f;

        // ── Public read-only state ──────────────────────────────────────
        /// <summary>Current fill amount (0 = clear, 1 = fully black).</summary>
        public float currentAlpha => currentFill;

        // ── Callbacks ───────────────────────────────────────────────────
        /// <summary>Fired the frame a FadeOut finishes (fill == 1).</summary>
        public event Action OnFadeOutComplete;
        /// <summary>Fired the frame a FadeIn / Reveal finishes (fill == 0).</summary>
        public event Action OnFadeInComplete;

        // ── Private ─────────────────────────────────────────────────────
        private Material sphereMat;
        private GameObject sphereGO;
        private float currentFill;
        private Coroutine activeRoutine;

        // Message overlay
        private Canvas msgCanvas;
        private TextMeshProUGUI msgText;
        private CanvasGroup msgCanvasGroup;

        private static readonly int FillLevelID = Shader.PropertyToID("_FillLevel");
        private static readonly int FillColorID = Shader.PropertyToID("_FillColor");
        private static readonly int EdgeSoftID = Shader.PropertyToID("_EdgeSoftness");

        // ================================================================
        //  LIFECYCLE
        // ================================================================

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void Start()
        {
            CreateSphere();
            CreateMessageCanvas();

            if (fadeOnStart)
            {
                // Start fully obscured, then reveal
                SetFillImmediate(1f);
                FadeIn();
            }
            else
            {
                SetFillImmediate(0f);
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;

            if (msgCanvas != null) Destroy(msgCanvas.gameObject);
            if (sphereGO != null) Destroy(sphereGO);
            if (sphereMat != null) Destroy(sphereMat);
        }

        // ================================================================
        //  PUBLIC API
        // ================================================================

        /// <summary>Fill the sphere from bottom to top (obscure the view).</summary>
        public void FadeOut()
        {
            StartTransition(currentFill, 1f, OnFadeOutComplete);
        }

        /// <summary>Drain the sphere from top to bottom (reveal the view).</summary>
        public void FadeIn()
        {
            StartTransition(currentFill, 0f, OnFadeInComplete);
        }

        /// <summary>
        /// Static convenience so any script can call
        /// <c>ScreenFade.Reveal()</c> without needing a cached reference.
        /// </summary>
        public static void Reveal()
        {
            if (instance != null)
            {
                instance.FadeIn();
            }
            else
            {
                Debug.LogWarning("[ScreenFade] Reveal() called but no instance exists.");
            }
        }

        /// <summary>
        /// Show a text message on the black sphere (only visible while faded out).
        /// Fades the text in over a short duration.
        /// </summary>
        public void ShowMessage(string text, float textFadeDuration = 0.3f)
        {
            if (msgText == null) return;

            msgText.text = text;
            msgCanvas.gameObject.SetActive(true);
            StartCoroutine(FadeCanvasGroup(msgCanvasGroup, 0f, 1f, textFadeDuration));
        }

        /// <summary>
        /// Hide the transition message. Fades the text out over a short duration.
        /// </summary>
        public void HideMessage(float textFadeDuration = 0.3f)
        {
            if (msgText == null) return;

            StartCoroutine(FadeCanvasGroupThenDisable(msgCanvasGroup, 1f, 0f, textFadeDuration));
        }

        /// <summary>Snap to a specific fill level with no animation.</summary>
        public void SetFillImmediate(float fill)
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            currentFill = Mathf.Clamp01(fill);
            ApplyFill();
        }

        // ================================================================
        //  CONTEXT MENU – right-click the component in Play mode to test
        // ================================================================

        [ContextMenu("Test/Fade Out (Fill Sphere)")]
        private void TestFadeOut()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[ScreenFade] Enter Play mode to test."); return; }
            FadeOut();
        }

        [ContextMenu("Test/Fade In (Reveal)")]
        private void TestFadeIn()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[ScreenFade] Enter Play mode to test."); return; }
            FadeIn();
        }

        [ContextMenu("Test/Full Cycle (Out → Message → In)")]
        private void TestFullCycle()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[ScreenFade] Enter Play mode to test."); return; }
            StartCoroutine(TestFullCycleRoutine());
        }

        [ContextMenu("Test/Show Message Only")]
        private void TestShowMessage()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[ScreenFade] Enter Play mode to test."); return; }
            // Snap to black first so the text is visible
            SetFillImmediate(1f);
            ShowMessage("Adjusting your position...");
        }

        [ContextMenu("Test/Hide Message Only")]
        private void TestHideMessage()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[ScreenFade] Enter Play mode to test."); return; }
            HideMessage();
        }

        [ContextMenu("Test/Snap Black")]
        private void TestSnapBlack()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[ScreenFade] Enter Play mode to test."); return; }
            SetFillImmediate(1f);
        }

        [ContextMenu("Test/Snap Clear")]
        private void TestSnapClear()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[ScreenFade] Enter Play mode to test."); return; }
            HideMessage(0f);
            SetFillImmediate(0f);
        }

        private IEnumerator TestFullCycleRoutine()
        {
            Debug.Log("[ScreenFade] Test: starting fade out...");

            bool done = false;
            OnFadeOutComplete += () => done = true;
            FadeOut();
            yield return new WaitUntil(() => done);

            Debug.Log("[ScreenFade] Test: showing message...");
            ShowMessage("Adjusting your position...");
            yield return new WaitForSeconds(1f);

            Debug.Log("[ScreenFade] Test: hiding message...");
            HideMessage();
            yield return new WaitForSeconds(0.3f);

            Debug.Log("[ScreenFade] Test: starting fade in...");
            done = false;
            OnFadeInComplete += () => done = true;
            FadeIn();
            yield return new WaitUntil(() => done);

            Debug.Log("[ScreenFade] Test: cycle complete.");
        }

        // ================================================================
        //  INTERNALS
        // ================================================================

        private void CreateSphere()
        {
            sphereGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereGO.name = "TransitionSphere";
            sphereGO.transform.SetParent(transform, false);
            sphereGO.transform.localPosition = Vector3.zero;
            sphereGO.transform.localScale = Vector3.one * sphereScale;

            // The collider is not needed
            var col = sphereGO.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Material
            Shader shader = Shader.Find("Custom/TransitionSphere");
            if (shader == null)
            {
                Debug.LogError("[ScreenFade] Could not find shader 'Custom/TransitionSphere'. "
                             + "Make sure the shader file is in your project.");
                return;
            }

            sphereMat = new Material(shader);
            sphereGO.GetComponent<MeshRenderer>().material = sphereMat;
            sphereGO.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            sphereGO.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        private void CreateMessageCanvas()
        {
            // World-space canvas parented to the camera, sitting inside the sphere
            var canvasGO = new GameObject("TransitionMessageCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 0f, messageDistance);
            canvasGO.transform.localRotation = Quaternion.identity;

            msgCanvas = canvasGO.AddComponent<Canvas>();
            msgCanvas.renderMode = RenderMode.WorldSpace;
            msgCanvas.sortingOrder = 32767; // render on top of everything

            // Size the canvas rect to something readable at the given distance
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0.3f, 0.1f);
            rt.localScale = Vector3.one;

            // CanvasGroup for easy alpha fading
            msgCanvasGroup = canvasGO.AddComponent<CanvasGroup>();
            msgCanvasGroup.alpha = 0f;

            // TextMeshPro label
            var textGO = new GameObject("MessageText");
            textGO.transform.SetParent(canvasGO.transform, false);

            msgText = textGO.AddComponent<TextMeshProUGUI>();
            msgText.text = "";
            msgText.fontSize = messageFontSize;
            msgText.color = Color.white;
            msgText.alignment = TextAlignmentOptions.Center;
            msgText.enableWordWrapping = true;
            msgText.overflowMode = TextOverflowModes.Overflow;

            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            canvasGO.SetActive(false);
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            float elapsed = 0f;
            cg.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            cg.alpha = to;
        }

        private IEnumerator FadeCanvasGroupThenDisable(CanvasGroup cg, float from, float to, float duration)
        {
            yield return FadeCanvasGroup(cg, from, to, duration);
            if (msgCanvas != null)
                msgCanvas.gameObject.SetActive(false);
        }

        private void StartTransition(float from, float to, Action onComplete)
        {
            if (activeRoutine != null)
                StopCoroutine(activeRoutine);

            activeRoutine = StartCoroutine(AnimateFill(from, to, onComplete));
        }

        private IEnumerator AnimateFill(float from, float to, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                currentFill = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeTime));
                ApplyFill();
                yield return null;          // runs every frame
            }

            currentFill = to;
            ApplyFill();
            activeRoutine = null;

            onComplete?.Invoke();
        }

        private void ApplyFill()
        {
            if (sphereMat == null) return;

            sphereMat.SetFloat(FillLevelID, currentFill);
            sphereMat.SetColor(FillColorID, fadeColor);
            sphereMat.SetFloat(EdgeSoftID, edgeSoftness);

            // Hide the renderer entirely when not needed
            if (sphereGO != null)
                sphereGO.SetActive(currentFill > 0.001f);
        }
    }
}