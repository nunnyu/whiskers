using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZincWhiskers
{
    // The owner moves its cat locally and shares position and facing; everyone else smooths toward them.
    public class CatPlayer : NetworkBehaviour
    {
        const float WalkSpeed = 4.5f;
        const float SprintSpeed = 7.5f;
        const float GroundAcceleration = 45f;
        const float AirAcceleration = 12f;
        const float JumpSpeed = 6.5f;
        const float Gravity = -22f;
        const float FallGravityMultiplier = 1.6f;
        const float CoyoteTime = 0.12f;
        const float JumpBufferTime = 0.15f;
        const float LookSensitivity = 0.1f;
        const float RemoteSmoothing = 15f;
        const float FallLimit = -30f;
        const float BodyHeight = 0.98f;
        const float BodyRadius = 0.32f;

        const float EyeSmoothing = 18f;
        const float BobAmount = 0.045f;
        const float BobStepsPerMeter = 0.7f;
        const float BaseFieldOfView = 78f;
        const float SprintFieldOfView = 86f;
        const float PersonalSpace = BodyRadius * 2.2f;
        const float PushStrength = 25f;
        const float GrowStartDistance = 10f;
        const float MaxGrow = 2.2f;
        const string ModelPath = "Cat/Cat";
        const string BreedPath = "Cat/Breeds/";
        static readonly string[] Breeds = { "Orange", "Grey", "Tuxedo", "Calico" };

        static readonly Color[] FurColors =
        {
            new Color(0.95f, 0.55f, 0.2f),
            new Color(0.55f, 0.55f, 0.6f),
            new Color(0.15f, 0.15f, 0.17f),
            new Color(0.95f, 0.93f, 0.88f),
        };

        [Tooltip("Extra turn for the model if it still faces the wrong way.")]
        [SerializeField] float modelYaw;

        readonly NetworkVariable<Vector3> netPosition = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> netYaw = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> netPitch = new(writePerm: NetworkVariableWritePermission.Owner);

        static readonly System.Collections.Generic.List<CatPlayer> Spawned = new();

        CharacterController controller;
        Transform visual;
        CatAnimation catAnimation;
        Vector3 eyeOffset = new Vector3(0f, BodyHeight * 0.88f, 0f);
        Camera view;
        Vector3 velocity;
        float yaw;
        float pitch;
        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpPressTime = float.NegativeInfinity;
        bool wasGrounded;
        float eyeY;
        float landingDip;
        float bobPhase;
        bool hasRemoteState;

        public static Vector3 SpawnPoint(int index)
        {
            return new Vector3((index - 1.5f) * 1.5f, 0.1f, 0f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSpawned()
        {
            Spawned.Clear();
        }

        public override void OnNetworkSpawn()
        {
            Spawned.Add(this);
            BuildBody();

            if (IsOwner)
            {
                controller = gameObject.AddComponent<CharacterController>();
                controller.height = BodyHeight;
                controller.radius = BodyRadius;
                controller.center = new Vector3(0f, BodyHeight / 2f, 0f);
                controller.stepOffset = 0.35f;
                controller.slopeLimit = 50f;

                // First person: hide our own body.
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                {
                    renderer.enabled = false;
                }

                yaw = transform.eulerAngles.y;
                eyeY = transform.position.y + eyeOffset.y;
                view = Camera.main;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                ShareState();
            }
            else
            {
                netPosition.OnValueChanged += OnRemotePositionChanged;
                netYaw.OnValueChanged += OnRemoteYawChanged;
                if (netPosition.Value != Vector3.zero)
                {
                    SnapToRemoteState();
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            Spawned.Remove(this);
            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                TestCave.ResetCamera();
            }
            else
            {
                netPosition.OnValueChanged -= OnRemotePositionChanged;
                netYaw.OnValueChanged -= OnRemoteYawChanged;
            }
        }

        void Update()
        {
            if (!IsSpawned)
            {
                return;
            }
            if (IsOwner)
            {
                UpdateOwner();
            }
            else
            {
                if (hasRemoteState)
                {
                    float t = 1f - Mathf.Exp(-RemoteSmoothing * Time.deltaTime);
                    transform.position = Vector3.Lerp(transform.position, netPosition.Value, t);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, netYaw.Value, 0f), t);
                    pitch = Mathf.Lerp(pitch, netPitch.Value, t);
                }
                if (catAnimation != null)
                {
                    catAnimation.LookPitch = pitch;
                }
                UpdateDistanceScale();
            }
        }

        // Only on other players' screens: far cats are drawn a bit bigger so they don't vanish into pixels.
        void UpdateDistanceScale()
        {
            var camera = Camera.main;
            if (camera == null || visual == null)
            {
                return;
            }
            float distance = Vector3.Distance(camera.transform.position, transform.position);
            float grow = Mathf.Clamp(distance / GrowStartDistance, 1f, MaxGrow);
            visual.localScale = Vector3.one * grow;
        }

        void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || view == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            float smoothing = 1f - Mathf.Exp(-EyeSmoothing * dt);

            // Smooths out step-ups so the view glides instead of snapping.
            eyeY = Mathf.Lerp(eyeY, transform.position.y + eyeOffset.y, smoothing);
            landingDip = Mathf.Lerp(landingDip, 0f, smoothing * 0.5f);

            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float speed = wasGrounded ? flat.magnitude : 0f;
            bobPhase += speed * BobStepsPerMeter * dt;
            float bob = Mathf.Sin(bobPhase * Mathf.PI * 2f) * BobAmount * Mathf.Clamp01(speed / WalkSpeed);

            Vector3 eyes = transform.position + Quaternion.Euler(0f, yaw, 0f) * new Vector3(eyeOffset.x, 0f, eyeOffset.z);
            var position = new Vector3(eyes.x, eyeY + bob + landingDip, eyes.z);
            view.transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));

            float fov = speed > WalkSpeed + 0.5f ? SprintFieldOfView : BaseFieldOfView;
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, fov, smoothing * 0.4f);
        }

        void UpdateOwner()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            float dt = Time.deltaTime;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                locked = false;
            }

            Vector2 move = Vector2.zero;
            bool sprint = false;
            if (locked && keyboard != null)
            {
                if (keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;
                sprint = keyboard.leftShiftKey.isPressed && move.y > 0f;
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    lastJumpPressTime = Time.time;
                }
            }
            if (locked && mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue() * LookSensitivity;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            }

            bool grounded = controller.isGrounded;
            if (grounded)
            {
                lastGroundedTime = Time.time;
            }

            var facing = Quaternion.Euler(0f, yaw, 0f);
            Vector3 target = facing * Vector3.ClampMagnitude(new Vector3(move.x, 0f, move.y), 1f) * (sprint ? SprintSpeed : WalkSpeed);
            Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), target,
                (grounded ? GroundAcceleration : AirAcceleration) * dt);

            float vertical = velocity.y;
            if (grounded && vertical < 0f)
            {
                vertical = -2f;
            }
            bool canJump = Time.time - lastGroundedTime <= CoyoteTime;
            if (canJump && Time.time - lastJumpPressTime <= JumpBufferTime)
            {
                vertical = JumpSpeed;
                lastJumpPressTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
            }
            vertical += Gravity * (vertical < 0f ? FallGravityMultiplier : 1f) * dt;

            velocity = new Vector3(horizontal.x, vertical, horizontal.z);
            var flags = controller.Move((velocity + PushFromOtherCats()) * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
            {
                velocity.y = 0f;
            }
            transform.rotation = facing;

            bool nowGrounded = controller.isGrounded;
            if (nowGrounded && !wasGrounded && velocity.y < -4f)
            {
                landingDip = Mathf.Max(-0.18f, velocity.y * 0.015f);
            }
            wasGrounded = nowGrounded;

            if (transform.position.y < FallLimit)
            {
                Teleport(SpawnPoint((int)OwnerClientId % SessionNetwork.MaxPlayers));
            }
            ShareState();
        }

        // Cats overlapping each other get nudged apart, harder the deeper the overlap. Every owner pushes
        // only itself, so two cats bumping both give way a little instead of acting like walls.
        Vector3 PushFromOtherCats()
        {
            Vector3 push = Vector3.zero;
            foreach (var other in Spawned)
            {
                if (other == this)
                {
                    continue;
                }
                Vector3 offset = transform.position - other.transform.position;
                if (Mathf.Abs(offset.y) > BodyHeight)
                {
                    continue;
                }
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance >= PersonalSpace)
                {
                    continue;
                }
                Vector3 away = distance > 0.001f ? offset / distance : Quaternion.Euler(0f, OwnerClientId * 90f, 0f) * Vector3.forward;
                push += away * (PersonalSpace - distance) * PushStrength;
            }
            return push;
        }

        void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            velocity = Vector3.zero;
            eyeY = position.y + eyeOffset.y;
        }

        void ShareState()
        {
            netPosition.Value = transform.position;
            netYaw.Value = yaw;
            netPitch.Value = pitch;
        }

        void OnRemotePositionChanged(Vector3 previous, Vector3 current)
        {
            if (!hasRemoteState)
            {
                SnapToRemoteState();
            }
        }

        void OnRemoteYawChanged(float previous, float current)
        {
            if (!hasRemoteState)
            {
                SnapToRemoteState();
            }
        }

        void SnapToRemoteState()
        {
            transform.SetPositionAndRotation(netPosition.Value, Quaternion.Euler(0f, netYaw.Value, 0f));
            hasRemoteState = true;
        }

        void BuildBody()
        {
            Color fur = FurColors[OwnerClientId % (ulong)FurColors.Length];
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            var model = Resources.Load<GameObject>(ModelPath);
            if (model != null)
            {
                BuildModel(model, Resources.Load<Texture2D>(BreedPath + Breeds[OwnerClientId % (ulong)Breeds.Length]));
            }
            else
            {
                CreatePart(PrimitiveType.Capsule, new Vector3(0f, BodyHeight / 2f, 0f),
                    new Vector3(BodyRadius * 2f, BodyHeight / 2f, BodyRadius * 2f), WorldMaterials.MakeCharacter(WorldMaterials.Create((Texture)null, fur)));
                CreatePart(PrimitiveType.Cube, new Vector3(0f, BodyHeight * 0.75f, BodyRadius),
                    Vector3.one * 0.12f, WorldMaterials.MakeCharacter(WorldMaterials.Create((Texture)null, Color.Lerp(fur, Color.black, 0.5f))));
            }

        }

        void BuildModel(GameObject model, Texture2D breed)
        {
            var instance = Instantiate(model, visual);
            instance.transform.localPosition = Vector3.zero;
            foreach (var collider in instance.GetComponentsInChildren<Collider>())
            {
                Destroy(collider);
            }
            var animator = instance.GetComponent<Animator>();
            if (animator != null)
            {
                Destroy(animator);
            }

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = WorldMaterials.MakeCharacter(ToWorldMaterial(materials[i], breed));
                }
                renderer.sharedMaterials = materials;
            }

            FaceForward(instance.transform);
            instance.transform.rotation = Quaternion.AngleAxis(modelYaw, Vector3.up) * instance.transform.rotation;

            float height = RendererBounds(renderers).size.y;
            if (height > 0f)
            {
                instance.transform.localScale *= BodyHeight / height;
            }
            Bounds bounds = RendererBounds(renderers);
            instance.transform.position += transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            FindEyes(instance.transform);
            catAnimation = instance.AddComponent<CatAnimation>();
            catAnimation.Play(transform, WalkSpeed);
        }

        // First-person view sits where the model's eyes are.
        void FindEyes(Transform instance)
        {
            var head = FindChild(instance, "Head");
            var filter = head != null ? head.GetComponent<MeshFilter>() : null;
            var renderer = head != null ? head.GetComponent<Renderer>() : null;
            if (filter == null || renderer == null)
            {
                return;
            }
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length && i < filter.sharedMesh.subMeshCount; i++)
            {
                if (materials[i] != null && materials[i].name.StartsWith("Eye"))
                {
                    Vector3 eyes = head.TransformPoint(filter.sharedMesh.GetSubMesh(i).bounds.center);
                    eyeOffset = transform.InverseTransformPoint(eyes);
                    return;
                }
            }
        }

        // The tail is at the back, so turn the model until the tail points away from where the cat faces.
        void FaceForward(Transform instance)
        {
            var body = FindChild(instance, "Body");
            var tail = FindChild(instance, "Tail");
            if (body == null || tail == null)
            {
                return;
            }
            Vector3 facing = body.GetComponent<Renderer>().bounds.center - tail.GetComponent<Renderer>().bounds.center;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                float turn = Vector3.SignedAngle(facing, transform.forward, Vector3.up);
                instance.rotation = Quaternion.AngleAxis(turn, Vector3.up) * instance.rotation;
            }
        }

        static Transform FindChild(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>())
            {
                if (child.name == childName)
                {
                    return child;
                }
            }
            return null;
        }

        static Bounds RendererBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        // Fur gets this player's breed texture, anything else keeps its imported color.
        static Material ToWorldMaterial(Material source, Texture2D breed)
        {
            string name = source != null ? source.name : "";
            Material material;
            if (name.StartsWith("Fur"))
            {
                material = WorldMaterials.Create(breed, Color.white);
            }
            else
            {
                Color color = Color.white;
                if (source != null && source.HasProperty("_BaseColor")) color = source.GetColor("_BaseColor");
                else if (source != null && source.HasProperty("_Color")) color = source.color;
                material = WorldMaterials.Create((Texture)null, color);
            }
            // Keep the original name; FindEyes looks for the eye material by it.
            material.name = name;
            return material;
        }

        GameObject CreatePart(PrimitiveType shape, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(visual, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }
    }
}
