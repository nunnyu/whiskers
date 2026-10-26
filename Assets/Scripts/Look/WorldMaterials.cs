using UnityEngine;

namespace ZincWhiskers
{
    public static class WorldMaterials
    {
        static readonly int EmissionId = Shader.PropertyToID("_Emission");

        static Shader shader;

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
    }
}
