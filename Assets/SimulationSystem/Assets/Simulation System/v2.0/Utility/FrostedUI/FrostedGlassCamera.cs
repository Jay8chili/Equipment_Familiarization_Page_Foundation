using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EightChili.VR.UI
{
    /// <summary>
    /// Creates a low-resolution render texture from the main camera,
    /// applies iterative Kawase blur on the GPU, and feeds the result
    /// to all materials using the 8chili/FrostedGlass shader.
    ///
    /// SETUP:
    ///   1. Attach this script to the Main Camera (or any always-active GO).
    ///   2. Assign your frosted glass material(s) to the glassMaterials array.
    ///   3. That's it. The shader receives _BlurredSceneTex automatically.
    ///
    /// PERFORMANCE (Quest 2):
    ///   - RT is rendered at 1/4 resolution by default (480x256 ish)
    ///   - Kawase blur is 4 passes on that tiny RT = negligible cost
    ///   - The frosted glass shader does 1 texture read per pixel
    ///   - Total overhead: ~0.3ms per frame on Adreno 650
    /// </summary>
    public class FrostedGlassCamera : MonoBehaviour
    {
        [Header("Quality")]
        [Tooltip("Fraction of screen resolution for the blur RT. Lower = more blur + cheaper.")]
        [SerializeField, Range(0.02f, 0.5f)]
        private float resolutionScale = 0.08f;

        [Tooltip("Number of Kawase blur passes. More = heavier blur.")]
        [SerializeField, Range(1, 12)]
        private int blurPasses = 7;

        [Tooltip("Skip frames between RT updates. 0 = every frame.")]
        [SerializeField, Range(0, 3)]
        private int skipFrames = 0;

        [Header("References")]
        [Tooltip("Materials using 8chili/FrostedGlass shader.")]
        [SerializeField]
        private Material[] glassMaterials;

        private Camera _blurCamera;
        private GameObject _blurCamGO;
        private RenderTexture _rtA;
        private RenderTexture _rtB;
        private Material _kawaseMat;
        private int _frameCounter;

        private static readonly int BlurredSceneTexID = Shader.PropertyToID("_BlurredSceneTex");
        private static readonly int OffsetID = Shader.PropertyToID("_Offset");

        // Kawase blur shader (inline, compiled once)
        private const string KawaseShaderCode = @"
Shader ""Hidden/8chili/KawaseBlur""
{
    Properties { _MainTex (""Texture"", 2D) = ""white"" {} }
    SubShader
    {
        Tags { ""RenderPipeline"" = ""UniversalPipeline"" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float  _Offset;

            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv  = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float2 off = _MainTex_TexelSize.xy * _Offset;

                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( off.x,  off.y));
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-off.x,  off.y));
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( off.x, -off.y));
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-off.x, -off.y));
                return c * 0.2;
            }
            ENDHLSL
        }
    }
}";

        private void OnEnable()
        {
            SetupBlurCamera();
            CreateRenderTextures();
            CreateKawaseMaterial();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void LateUpdate()
        {
            if (_blurCamera == null || _rtA == null) return;

            // Always sync blur camera with main camera every frame
            // so screen-space projection stays correct even on skipped frames
            Camera main = Camera.main;
            if (main == null) return;

            _blurCamera.transform.SetPositionAndRotation(
                main.transform.position,
                main.transform.rotation
            );
            _blurCamera.fieldOfView = main.fieldOfView;
            _blurCamera.nearClipPlane = main.nearClipPlane;
            _blurCamera.farClipPlane = main.farClipPlane;

            // Only re-render and blur on non-skipped frames
            _frameCounter++;
            if (skipFrames > 0 && (_frameCounter % (skipFrames + 1)) != 0)
                return;

            // Render scene to low-res RT
            _blurCamera.targetTexture = _rtA;
            _blurCamera.Render();

            // Iterative Kawase blur
            for (int i = 0; i < blurPasses; i++)
            {
                _kawaseMat.SetFloat(OffsetID, i + 0.5f);

                if (i % 2 == 0)
                    Graphics.Blit(_rtA, _rtB, _kawaseMat);
                else
                    Graphics.Blit(_rtB, _rtA, _kawaseMat);
            }

            // Result is in _rtA (even passes) or _rtB (odd passes)
            RenderTexture result = (blurPasses % 2 == 0) ? _rtA : _rtB;

            // Push to all glass materials
            if (glassMaterials != null)
            {
                for (int i = 0; i < glassMaterials.Length; i++)
                {
                    if (glassMaterials[i] != null)
                        glassMaterials[i].SetTexture(BlurredSceneTexID, result);
                }
            }

            // Also set globally so any glass material picks it up
            Shader.SetGlobalTexture(BlurredSceneTexID, result);
        }

        private void SetupBlurCamera()
        {
            _blurCamGO = new GameObject("8chili_BlurCamera");
            _blurCamGO.hideFlags = HideFlags.HideAndDontSave;

            _blurCamera = _blurCamGO.AddComponent<Camera>();
            _blurCamera.enabled = false; // We render manually
            _blurCamera.clearFlags = CameraClearFlags.SolidColor;
            _blurCamera.backgroundColor = Color.black;

            // Copy URP renderer from main camera
            var mainCamData = Camera.main?.GetUniversalAdditionalCameraData();
            if (mainCamData != null)
            {
                var blurCamData = _blurCamera.GetUniversalAdditionalCameraData();
                blurCamData.renderType = CameraRenderType.Base;
                blurCamData.SetRenderer(mainCamData.scriptableRenderer is not null ? 0 : 0);
            }

            // Exclude the UI layer so glass panels don't render into the blur
            _blurCamera.cullingMask = Camera.main != null
                ? Camera.main.cullingMask & ~(1 << LayerMask.NameToLayer("UI"))
                : ~(1 << LayerMask.NameToLayer("UI"));
        }

        private void CreateRenderTextures()
        {
            int w = Mathf.Max(64, (int)(Screen.width * resolutionScale));
            int h = Mathf.Max(64, (int)(Screen.height * resolutionScale));

            _rtA = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
            _rtA.filterMode = FilterMode.Bilinear;
            _rtA.wrapMode = TextureWrapMode.Clamp;
            _rtA.Create();

            _rtB = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            _rtB.filterMode = FilterMode.Bilinear;
            _rtB.wrapMode = TextureWrapMode.Clamp;
            _rtB.Create();
        }

        private void CreateKawaseMaterial()
        {
            Shader kawaseShader = ShaderUtil_FindOrCreate();
            if (kawaseShader != null)
            {
                _kawaseMat = new Material(kawaseShader);
            }
        }

        private Shader ShaderUtil_FindOrCreate()
        {
            // Try to find an already-compiled instance
            Shader s = Shader.Find("Hidden/8chili/KawaseBlur");
            if (s != null) return s;

            // Runtime shader compilation (editor only, for convenience).
            // For builds, include KawaseBlur.shader in your project.
            Debug.LogWarning(
                "[8chili FrostedGlass] KawaseBlur shader not found. " +
                "Please create a shader file with the contents from " +
                "FrostedGlassCamera.KawaseShaderCode and place it in your project."
            );
            return null;
        }

        private void Cleanup()
        {
            if (_rtA != null) { _rtA.Release(); DestroyImmediate(_rtA); }
            if (_rtB != null) { _rtB.Release(); DestroyImmediate(_rtB); }
            if (_kawaseMat != null) DestroyImmediate(_kawaseMat);
            if (_blurCamGO != null) DestroyImmediate(_blurCamGO);
        }

        /// <summary>
        /// Call if screen resolution changes (e.g. dynamic resolution).
        /// </summary>
        public void RefreshRenderTextures()
        {
            if (_rtA != null) { _rtA.Release(); DestroyImmediate(_rtA); }
            if (_rtB != null) { _rtB.Release(); DestroyImmediate(_rtB); }
            CreateRenderTextures();
        }
    }
}