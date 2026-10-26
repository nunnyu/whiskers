using System.Collections.Generic;
using UnityEngine;

namespace ZincWhiskers
{
    // Hand-placed tunnel for trying out the look and movement until the real cave generator exists.
    public static class TestCave
    {
        const int RingSides = 28;
        const float RingSpacing = 0.5f;
        const float TextureSize = 2.5f;
        const float FloorRatio = 0.6f;

        // x, floor height, z, radius
        static readonly Vector4[] Path =
        {
            new Vector4(0f, 0f, -5f, 4.5f),
            new Vector4(0f, 0f, 3f, 4.5f),
            new Vector4(2f, -0.3f, 9f, 2.2f),
            new Vector4(6f, -1.2f, 14f, 2f),
            new Vector4(8f, -2.6f, 20f, 2.4f),
            new Vector4(7f, -3f, 23f, 2.4f),
            new Vector4(7f, -6.5f, 24.6f, 2.6f),
            new Vector4(6f, -7f, 30f, 3.5f),
            new Vector4(2f, -7.3f, 36f, 5f),
            new Vector4(-3f, -7.3f, 41f, 4f),
        };

        static readonly Vector3 OverviewPosition = new Vector3(0f, 2.6f, -3.5f);
        static readonly Quaternion OverviewRotation = Quaternion.Euler(10f, 0f, 0f);

        struct Ring
        {
            public Vector3 Center;
            public Vector3 Forward;
            public Vector3 Right;
            public Vector3 Up;
            public float Radius;
            public float Floor;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Build()
        {
            if (SessionNetwork.Instance == null)
            {
                return;
            }

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(WorldLook));
            cameraObject.tag = "MainCamera";
            ResetCamera();

            var random = new System.Random(1);
            var rings = BuildRings();
            var cave = new GameObject("Test Cave");
            BuildTunnel(cave.transform, rings);
            PlaceSupports(cave.transform, rings);
            PlaceLanterns(cave.transform, rings);
            PlaceCoal(cave.transform, rings, random);
            PlaceRubble(cave.transform, rings, random);

            new GameObject("Lobby Menu", typeof(LobbyMenu));
        }

        public static void ResetCamera()
        {
            if (Camera.main != null)
            {
                Camera.main.transform.SetPositionAndRotation(OverviewPosition, OverviewRotation);
            }
        }

        static List<Ring> BuildRings()
        {
            var rings = new List<Ring>();
            for (int segment = 0; segment < Path.Length - 1; segment++)
            {
                Vector4 p0 = Path[Mathf.Max(segment - 1, 0)];
                Vector4 p1 = Path[segment];
                Vector4 p2 = Path[segment + 1];
                Vector4 p3 = Path[Mathf.Min(segment + 2, Path.Length - 1)];
                float length = Vector3.Distance(p1, p2);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / RingSpacing));
                bool last = segment == Path.Length - 2;

                for (int step = 0; step < steps + (last ? 1 : 0); step++)
                {
                    float t = (float)step / steps;
                    Vector4 point = CatmullRom(p0, p1, p2, p3, t);
                    Vector3 forward = ((Vector3)CatmullRom(p0, p1, p2, p3, Mathf.Min(t + 0.01f, 1f))
                                     - (Vector3)CatmullRom(p0, p1, p2, p3, Mathf.Max(t - 0.01f, 0f))).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                    float radius = Mathf.Lerp(p1.w, p2.w, t);
                    rings.Add(new Ring
                    {
                        Center = new Vector3(point.x, point.y + radius * FloorRatio, point.z),
                        Forward = forward,
                        Right = right,
                        Up = Vector3.Cross(forward, right),
                        Radius = radius,
                        Floor = point.y,
                    });
                }
            }
            return rings;
        }

        static Vector4 CatmullRom(Vector4 p0, Vector4 p1, Vector4 p2, Vector4 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        static void BuildTunnel(Transform parent, List<Ring> rings)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            int stride = RingSides + 1;
            float distance = 0f;

            for (int i = 0; i < rings.Count; i++)
            {
                var ring = rings[i];
                if (i > 0)
                {
                    distance += Vector3.Distance(rings[i - 1].Center, ring.Center);
                }
                float wraps = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * ring.Radius / TextureSize));
                for (int side = 0; side <= RingSides; side++)
                {
                    float angle = side * Mathf.PI * 2f / RingSides;
                    Vector3 direction = ring.Right * Mathf.Cos(angle) + ring.Up * Mathf.Sin(angle);
                    Vector3 basePoint = ring.Center + direction * ring.Radius;
                    float bulge = (Noise(basePoint * 0.35f) - 0.5f) * 0.45f + (Noise(basePoint * 1.3f + Vector3.one * 50f) - 0.5f) * 0.14f;
                    Vector3 point = ring.Center + direction * ring.Radius * (1f + bulge);
                    float floor = ring.Floor + 0.06f * Mathf.PerlinNoise(point.x * 0.8f, point.z * 0.8f);
                    point.y = Mathf.Max(point.y, floor);

                    vertices.Add(point);
                    uvs.Add(new Vector2((float)side / RingSides * wraps, distance / TextureSize));
                }
            }

            for (int i = 0; i < rings.Count - 1; i++)
            {
                Vector3 inside = (rings[i].Center + rings[i + 1].Center) * 0.5f;
                for (int side = 0; side < RingSides; side++)
                {
                    int a = i * stride + side;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    AddTriangle(triangles, vertices, a, c, b, inside);
                    AddTriangle(triangles, vertices, b, c, d, inside);
                }
            }

            AddCap(triangles, vertices, uvs, rings[0], 0, stride, 1f);
            AddCap(triangles, vertices, uvs, rings[rings.Count - 1], (rings.Count - 1) * stride, stride, -1f);

            var mesh = new Mesh { name = "Test Cave" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var tunnel = new GameObject("Tunnel", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            tunnel.transform.SetParent(parent, false);
            tunnel.GetComponent<MeshFilter>().sharedMesh = mesh;
            tunnel.GetComponent<MeshRenderer>().sharedMaterial = WorldMaterials.Create("Rock", Color.white);
            tunnel.GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void AddCap(List<int> triangles, List<Vector3> vertices, List<Vector2> uvs, Ring ring, int start, int stride, float inward)
        {
            int center = vertices.Count;
            vertices.Add(ring.Center);
            uvs.Add(new Vector2(0.5f, 0.5f));
            Vector3 inside = ring.Center + ring.Forward * inward;
            for (int side = 0; side < stride - 1; side++)
            {
                AddTriangle(triangles, vertices, center, start + side, start + side + 1, inside);
            }
        }

        // Winds each triangle so it faces the given point, which keeps every wall visible from inside the tunnel.
        static void AddTriangle(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 inside)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(normal, inside - vertices[a]) < 0f)
            {
                (b, c) = (c, b);
            }
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        static void PlaceSupports(Transform parent, List<Ring> rings)
        {
            var wood = WorldMaterials.WithSaturation(WorldMaterials.Create("Wood", Color.white), 0.45f);
            for (int i = 4; i < rings.Count - 4; i += 12)
            {
                var ring = rings[i];
                if (ring.Radius > 3f || Mathf.Abs(ring.Forward.y) > 0.4f)
                {
                    continue;
                }
                float halfWidth = ring.Radius * 0.55f;
                float height = ring.Center.y + ring.Radius * 0.35f - ring.Floor;
                var rotation = Quaternion.LookRotation(ring.Forward);
                foreach (float sideSign in new[] { -1f, 1f })
                {
                    Vector3 foot = new Vector3(ring.Center.x, ring.Floor, ring.Center.z) + ring.Right * halfWidth * sideSign;
                    CreateBlock(parent, "Support Post", foot + Vector3.up * height * 0.5f, new Vector3(0.18f, height, 0.18f), rotation, wood);
                }
                Vector3 beamCenter = new Vector3(ring.Center.x, ring.Floor + height, ring.Center.z);
                CreateBlock(parent, "Support Beam", beamCenter, new Vector3(halfWidth * 2f + 0.4f, 0.2f, 0.22f), rotation, wood);
                HangLantern(parent, beamCenter - Vector3.up * 0.1f, 0.4f, wood);
            }
        }

        // The wide chambers have no supports, so their lanterns hang from the ceiling instead.
        static void PlaceLanterns(Transform parent, List<Ring> rings)
        {
            var wood = WorldMaterials.WithSaturation(WorldMaterials.Create("Wood", Color.white), 0.45f);
            for (int i = 6; i < rings.Count; i += 14)
            {
                var ring = rings[i];
                if (ring.Radius <= 3f)
                {
                    continue;
                }
                Vector3 top = ring.Center + Vector3.up * ring.Radius * 0.9f;
                HangLantern(parent, top, ring.Radius * 0.6f, wood);
            }
        }

        static void HangLantern(Transform parent, Vector3 hangPoint, float ropeLength, Material rope)
        {
            var glass = WorldMaterials.WithFog(WorldMaterials.Create((Texture)null, Color.black, new Color(0.85f, 0.6f, 0.34f)), 0f);
            Vector3 position = hangPoint - Vector3.up * (ropeLength + 0.12f);
            var lantern = CreateBlock(parent, "Lantern", position, Vector3.one * 0.24f, Quaternion.identity, glass);
            var cord = CreateBlock(parent, "Lantern Rope", hangPoint - Vector3.up * ropeLength * 0.5f, new Vector3(0.03f, ropeLength, 0.03f), Quaternion.identity, rope);
            Object.Destroy(lantern.GetComponent<Collider>());
            Object.Destroy(cord.GetComponent<Collider>());

            var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            halo.name = "Lantern Glow";
            Object.Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(lantern.transform, false);
            halo.transform.localScale = Vector3.one * (1.8f / 0.24f);
            halo.GetComponent<Renderer>().sharedMaterial = WorldMaterials.CreateGlow(new Color(1f, 0.68f, 0.38f), 0.5f);

            var light = lantern.AddComponent<LightSource>();
            light.range = 13f;
            light.intensity = 2f;
        }

        static void PlaceCoal(Transform parent, List<Ring> rings, System.Random random)
        {
            var coal = WorldMaterials.Create("Coal", Color.white);
            int start = rings.Count / 2;
            for (int n = 0; n < 40; n++)
            {
                var ring = rings[random.Next(start, rings.Count)];
                float sideSign = random.Next(2) == 0 ? -1f : 1f;
                Vector3 origin = new Vector3(ring.Center.x, ring.Floor + Range(random, 0.2f, 1.8f), ring.Center.z);
                Vector3 direction = (ring.Right * sideSign + ring.Forward * Range(random, -0.4f, 0.4f)).normalized;
                if (!Physics.Raycast(origin, direction, out var hit, ring.Radius * 2f))
                {
                    continue;
                }
                float size = Range(random, 0.25f, 0.6f);
                CreateBlock(parent, "Coal", hit.point - direction * size * 0.2f, Vector3.one * size, RandomRotation(random), coal);
            }
        }

        static void PlaceRubble(Transform parent, List<Ring> rings, System.Random random)
        {
            var rock = WorldMaterials.Create("Rock", new Color(0.8f, 0.8f, 0.8f));
            for (int n = 0; n < 30; n++)
            {
                var ring = rings[random.Next(0, rings.Count)];
                Vector3 position = new Vector3(ring.Center.x, ring.Floor, ring.Center.z) + ring.Right * Range(random, -0.8f, 0.8f) * ring.Radius * 0.7f;
                float size = Range(random, 0.1f, 0.3f);
                CreateBlock(parent, "Rubble", position + Vector3.up * size * 0.3f, Vector3.one * size, RandomRotation(random), rock);
            }
        }

        static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 size, Quaternion rotation, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.SetPositionAndRotation(position, rotation);
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        static float Noise(Vector3 p)
        {
            return (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y + 31.7f, p.z) + Mathf.PerlinNoise(p.z + 71.3f, p.x)) / 3f;
        }

        static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        static Quaternion RandomRotation(System.Random random)
        {
            return Quaternion.Euler(Range(random, 0f, 360f), Range(random, 0f, 360f), Range(random, 0f, 360f));
        }
    }
}
