using UnityEngine;

namespace ZincWhiskers
{
    public static class WorldMaterials
    {
        static readonly int EmissionId = Shader.PropertyToID("_Emission");
        static readonly int SelfLightId = Shader.PropertyToID("_SelfLight");
        static readonly int RimId = Shader.PropertyToID("_Rim");
        static readonly int OutlineId = Shader.PropertyToID("_Outline");
        static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        static readonly int FogAmountId = Shader.PropertyToID("_FogAmount");
        static readonly int VertexSnapId = Shader.PropertyToID("_VertexSnap");
        static readonly int SmoothId = Shader.PropertyToID("_Smooth");

        static Shader shader;
        static Shader glowShader;

        public static Material Create(string texture, Color color, Color emission = default)
        {
            return Create(texture != null ? Resources.Load<Texture2D>("Textures/" + texture) : null, color, emission);
        }

        public static Material Create(Texture texture, Color color, Color emission = default)
        {
            if (shader == null)
            {
                shader = Resources.Load<Shader>("Shaders/World");
            }
            var material = new Material(shader) { color = color };
            if (texture != null)
            {
                material.mainTexture = texture;
            }
            material.SetColor(EmissionId, emission);
            return material;
        }

        public static Material CreateGlow(Color color, float intensity)
        {
            if (glowShader == null)
            {
                glowShader = Resources.Load<Shader>("Shaders/Glow");
            }
            var material = new Material(glowShader) { color = color };
            material.SetFloat("_Intensity", intensity);
            return material;
        }

        public static Material WithFog(Material material, float amount)
        {
            material.SetFloat(FogAmountId, amount);
            return material;
        }

        public static Material WithSaturation(Material material, float saturation)
        {
            material.SetFloat(SaturationId, saturation);
            return material;
        }

        // Keeps characters readable in the dark and at a distance: outlined, barely fogged, no vertex wobble.
        public static Material MakeCharacter(Material material)
        {
            material.SetFloat(SelfLightId, 0.55f);
            material.SetFloat(RimId, 0.35f);
            material.SetFloat(OutlineId, 1f);
            material.SetFloat(SaturationId, 0.4f);
            material.SetFloat(FogAmountId, 0.3f);
            material.SetFloat(VertexSnapId, 0f);
            material.SetFloat(SmoothId, 1f);
            return material;
        }
    }
}
