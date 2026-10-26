using UnityEngine;

namespace ZincWhiskers
{
    // Renders the camera at a low resolution and draws it to the screen with dithering, grain and vignette.
    // All the style knobs live here so they can be tuned in play mode.
    [RequireComponent(typeof(Camera))]
    public class WorldLook : MonoBehaviour
    {
        const int MaxLights = 16;

        [Header("Screen")]
        [Range(90, 720)] public int pixelHeight = 216;
        [Range(2, 64)] public float colorSteps = 16f;
        [Range(0f, 2f)] public float dither = 1f;
        [Range(0f, 0.3f)] public float grain = 0.04f;
        [Range(0f, 3f)] public float vignette = 0.7f;

        [Header("Geometry")]
        [Range(0f, 480f)] public float vertexSnap = 140f;
        [Range(0f, 1f)] public float affineWarp = 0.5f;

        [Header("Light")]
        public Color ambient = new Color(0.24f, 0.22f, 0.21f);
        public Color fogColor = Color.black;
        public float fogStart = 10f;
        public float fogEnd = 40f;

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int ColorStepsId = Shader.PropertyToID("_ColorSteps");
        static readonly int DitherId = Shader.PropertyToID("_Dither");
        static readonly int GrainId = Shader.PropertyToID("_Grain");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
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
            worldCamera = GetComponent<Camera>();
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = Color.black;
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

            Shader.SetGlobalColor(AmbientId, ambient);
            Shader.SetGlobalColor(FogColorId, fogColor);
            Shader.SetGlobalVector(FogRangeId, new Vector4(fogStart, fogEnd));
            Shader.SetGlobalFloat(VertexSnapId, vertexSnap);
            Shader.SetGlobalFloat(AffineWarpId, affineWarp);

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
            int height = pixelHeight;
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
            target = new RenderTexture(width, height, 24) { filterMode = FilterMode.Point };
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
            screenMaterial.SetFloat(ColorStepsId, colorSteps);
            screenMaterial.SetFloat(DitherId, dither);
            screenMaterial.SetFloat(GrainId, grain);
            screenMaterial.SetFloat(VignetteId, vignette);
            GUI.matrix = Matrix4x4.identity;
            Graphics.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), target, screenMaterial);
        }
    }
}
