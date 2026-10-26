using System.Collections.Generic;
using UnityEngine;

namespace ZincWhiskers
{
    // A point light for the World shader. These replace Unity lights so everything is lit per vertex.
    public class LightSource : MonoBehaviour
    {
        public static readonly List<LightSource> Active = new();

        public Color color = new Color(1f, 0.8f, 0.58f);
        public float intensity = 1.8f;
        public float range = 10f;
        [Range(0f, 1f)] public float flicker = 0.15f;

        float seed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActive()
        {
            Active.Clear();
        }

        public Color CurrentColor
        {
            get
            {
                float wobble = Mathf.PerlinNoise(seed, Time.time * 6f);
                return color * intensity * (1f - flicker * wobble);
            }
        }

        void Awake()
        {
            seed = Random.value * 100f;
        }

        void OnEnable()
        {
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }
    }
}
