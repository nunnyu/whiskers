using UnityEngine;

namespace ZincWhiskers
{
    // Renders the camera at a low resolution and draws it to the screen with dithering, grain and vignette.
    // The style knobs live in a WorldLookSettings asset.
    [RequireComponent(typeof(Camera))]
    public class WorldLook : MonoBehaviour
    {
        const int MaxLights = 16;

        [Tooltip("Edit this asset (Zinc Whiskers > World Look Settings) to change the look; edits stick even in play mode.")]
        public WorldLookSettings settings;

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int ColorStepsId = Shader.PropertyToID("_ColorSteps");
        static readonly int DitherId = Shader.PropertyToID("_Dither");
        static readonly int GrainId = Shader.PropertyToID("_Grain");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlinesId = Shader.PropertyToID("_Outlines");
        static readonly int GradeTintId = Shader.PropertyToID("_GradeTint");
        static readonly int GradeSaturationId = Shader.PropertyToID("_GradeSaturation");
        static readonly int BlurRangeId = Shader.PropertyToID("_WorldBlurRange");
        static readonly int LightPositionsId = Shader.PropertyToID("_WorldLightPositions");
        static readonly int LightColorsId = Shader.PropertyToID("_WorldLightColors");
        static readonly int LightCountId = Shader.PropertyToID("_WorldLightCount");
        static readonly int AmbientId = Shader.PropertyToID("_WorldAmbient");
        static readonly int FogColorId = Shader.PropertyToID("_WorldFogColor");
        static readonly int FogRangeId = Shader.PropertyToID("_WorldFogRange");
        static readonly int VertexSnapId = Shader.PropertyToID("_WorldVertexSnap");
        static readonly int AffineWarpId = Shader.PropertyToID("_WorldAffineWarp");

        readonly Vector4[] lightPositions = new Vector4[MaxLights];
        readonly Vector4[] lightColors = new Vector4[MaxLights];

        Camera worldCamera;
        RenderTexture target;
        Material screenMaterial;

        void Awake()
        {
            if (settings == null)
            {
                settings = Resources.Load<WorldLookSettings>("WorldLookSettings");
            }
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WorldLookSettings>();
            }
            worldCamera = GetComponent<Camera>();
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = Color.clear;
            worldCamera.allowHDR = false;
            worldCamera.allowMSAA = false;
            worldCamera.nearClipPlane = 0.05f;
            worldCamera.farClipPlane = 80f;
            screenMaterial = new Material(Resources.Load<Shader>("Shaders/Screen"));

            // Something has to draw to the real screen; this camera only clears it.
            var output = new GameObject("Output Camera", typeof(Camera)).GetComponent<Camera>();
            output.transform.SetParent(transform, false);
            output.cullingMask = 0;
            output.clearFlags = CameraClearFlags.SolidColor;
            output.backgroundColor = Color.black;
            output.depth = worldCamera.depth + 1f;
        }

        void OnDestroy()
        {
            if (target != null)
            {
                target.Release();
            }
        }

        void LateUpdate()
        {
            UpdateTarget();

            Shader.SetGlobalColor(AmbientId, settings.ambient);
            Shader.SetGlobalColor(FogColorId, settings.fogColor);
            worldCamera.backgroundColor = new Color(settings.fogColor.r, settings.fogColor.g, settings.fogColor.b, 0f);
            Shader.SetGlobalVector(FogRangeId, new Vector4(settings.fogStart, settings.fogEnd, settings.fogMinimum));
            Shader.SetGlobalVector(BlurRangeId, new Vector4(settings.blurStart, settings.blurEnd));
            Shader.SetGlobalFloat(VertexSnapId, settings.vertexSnap);
            Shader.SetGlobalFloat(AffineWarpId, settings.affineWarp);

            Vector3 eye = transform.position;
            if (LightSource.Active.Count > MaxLights)
            {
                LightSource.Active.Sort((a, b) => (a.transform.position - eye).sqrMagnitude.CompareTo((b.transform.position - eye).sqrMagnitude));
            }
            int count = Mathf.Min(LightSource.Active.Count, MaxLights);
            for (int i = 0; i < count; i++)
            {
                var light = LightSource.Active[i];
                Vector3 position = light.transform.position;
                lightPositions[i] = new Vector4(position.x, position.y, position.z, light.range);
                lightColors[i] = light.CurrentColor;
            }
            Shader.SetGlobalVectorArray(LightPositionsId, lightPositions);
            Shader.SetGlobalVectorArray(LightColorsId, lightColors);
            Shader.SetGlobalFloat(LightCountId, count);
        }

        void UpdateTarget()
        {
            int height = settings.pixelHeight;
            int width = Mathf.Max(1, Mathf.RoundToInt(height * (float)Screen.width / Screen.height));
            if (target != null && target.width == width && target.height == height)
            {
                return;
            }
            if (target != null)
            {
                worldCamera.targetTexture = null;
                target.Release();
            }
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            worldCamera.targetTexture = target;
        }

        void OnGUI()
        {
            GUI.depth = 10;
            if (Event.current.type != EventType.Repaint || target == null)
            {
                return;
            }
            screenMaterial.SetTexture(MainTexId, target);
            screenMaterial.SetFloat(ColorStepsId, settings.colorSteps);
            screenMaterial.SetFloat(DitherId, settings.dither);
            screenMaterial.SetFloat(GrainId, settings.grain);
            screenMaterial.SetFloat(VignetteId, settings.vignette);
            // Keep the blur the same size on screen whatever the render resolution is.
            screenMaterial.SetFloat(BlurRadiusId, Mathf.Max(1f, settings.pixelHeight / 120f));
            screenMaterial.SetVector(OutlineColorId, settings.outlineColor);
            screenMaterial.SetFloat(OutlinesId, settings.outlines ? 1f : 0f);
            screenMaterial.SetVector(GradeTintId, settings.gradeTint);
            screenMaterial.SetFloat(GradeSaturationId, settings.gradeSaturation);
            GUI.matrix = Matrix4x4.identity;
            Graphics.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), target, screenMaterial);
        }
    }
}
