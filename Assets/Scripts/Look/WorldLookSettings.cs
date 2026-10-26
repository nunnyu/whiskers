using UnityEngine;

namespace ZincWhiskers
{
    // Every style knob for WorldLook. Lives as an asset so changes stick, including ones made in play mode.
    [CreateAssetMenu(menuName = "Zinc Whiskers/World Look Settings")]
    public class WorldLookSettings : ScriptableObject
    {
        [Header("Screen")]
        [Range(60, 1080)] public int pixelHeight = 360;
        [Range(2, 64)] public float colorSteps = 40f;
        [Range(0f, 2f)] public float dither = 0.35f;
        [Range(0f, 0.3f)] public float grain = 0.03f;
        [Range(0f, 3f)] public float vignette = 0.7f;
        public bool outlines;
        public Color outlineColor = new Color(0.05f, 0.04f, 0.04f);
        public Color gradeTint = new Color(0.9f, 0.9f, 0.93f);
        [Range(0f, 1f)] public float gradeSaturation = 0.75f;

        [Header("Geometry")]
        [Range(0f, 480f)] public float vertexSnap = 160f;
        [Range(0f, 1f)] public float affineWarp = 0.5f;

        [Header("Light")]
        public Color ambient = new Color(0.24f, 0.22f, 0.21f);
        public Color fogColor = new Color(0.15f, 0.15f, 0.155f);
        public float fogStart = 0f;
        public float fogEnd = 17f;
        [Range(0f, 1f)] public float fogMinimum = 0.3f;
        public float blurStart = 7f;
        public float blurEnd = 22f;
    }
}
