using UnityEngine;

namespace ZincWhiskers
{
    // Animates the cat's separate parts in code: legs and arms swing, the body bobs, the tail sways, idle breathing.
    // Driven by how fast the model actually moves, so it works the same for local and remote cats.
    public class CatAnimation : MonoBehaviour
    {
        const float CyclesPerMeter = 1.1f;
        const float LegSwing = 35f;
        const float ArmSwing = 30f;
        const float ArmDroop = 65f;
        const float BobHeight = 0.05f;
        const float PaceSmoothing = 10f;
        const float TailStiffness = 45f;
        const float TailDamping = 6f;
        const float TailDroopPerBone = 22f;
        const int MaxTailBones = 8;

        class Part
        {
            public Transform Transform;
            public Quaternion RestRotation;
            public Vector3 RestPosition;
            public float Side;
        }

        Part body;
        Part head;
        Part tail;
        Part[] arms;
        Part[] legs;
        Part[] upperBody;
        Transform owner;
        Vector3 lastPosition;
        float fullSpeed;
        float pace;
        float phase;
        float idleTime;
        float lastYaw;
        Part[] tailBones;
        float[] tailYaw;
        float[] tailYawVelocity;
        float[] tailPitch;
        float[] tailPitchVelocity;

        public void Play(Transform cat, float walkSpeed)
        {
            owner = cat;
            fullSpeed = walkSpeed;
            body = Find("Body");
            head = Find("Head");
            tail = Find("Tail");
            arms = new[] { Find("LeftArm"), Find("RightArm") };
            legs = new[] { Find("LeftLeg"), Find("RightLeg") };
            var bones = new System.Collections.Generic.List<Part>();
            for (int i = 0; i < MaxTailBones; i++)
            {
                var bone = Find("TailBone" + i);
                if (bone == null) break;
                bones.Add(bone);
            }
            // Without bones the whole tail swings as one piece.
            tailBones = bones.Count > 0 ? bones.ToArray() : new[] { tail };
            tailYaw = new float[tailBones.Length];
            tailYawVelocity = new float[tailBones.Length];
            tailPitch = new float[tailBones.Length];
            tailPitchVelocity = new float[tailBones.Length];
            upperBody = new[] { body, head, Find("TailRig") ?? tail, arms[0], arms[1] };
            lastPosition = owner.position;
            lastYaw = owner.eulerAngles.y;
            idleTime = Random.value * 10f;
        }

        Part Find(string partName)
        {
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                if (child.name != partName)
                {
                    continue;
                }
                Vector3 offset = child.position - transform.position;
                return new Part
                {
                    Transform = child,
                    RestRotation = child.localRotation,
                    RestPosition = child.localPosition,
                    Side = Mathf.Sign(Vector3.Dot(offset, owner.right)),
                };
            }
            return null;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (owner == null || dt <= 0f)
            {
                return;
            }

            Vector3 moved = owner.position - lastPosition;
            lastPosition = owner.position;
            float verticalSpeed = moved.y / dt;
            moved.y = 0f;
            float turnRate = Mathf.DeltaAngle(lastYaw, owner.eulerAngles.y) / dt;
            lastYaw = owner.eulerAngles.y;
            Vector3 localVelocity = owner.InverseTransformDirection(moved / dt);
            float speed = moved.magnitude / dt;
            pace = Mathf.Lerp(pace, speed / fullSpeed, 1f - Mathf.Exp(-PaceSmoothing * dt));
            float walk = Mathf.Clamp01(pace);
            phase += speed * CyclesPerMeter * dt;
            idleTime += dt;

            Vector3 right = owner.right;
            Vector3 up = owner.up;
            float stride = Mathf.Sin(phase * Mathf.PI * 2f);
            float breathe = Mathf.Sin(idleTime * 2.2f);

            foreach (var leg in legs)
            {
                if (leg == null) continue;
                SetRotation(leg, Quaternion.AngleAxis(stride * leg.Side * LegSwing * walk, right));
            }
            foreach (var arm in arms)
            {
                if (arm == null) continue;
                Vector3 outward = right * arm.Side;
                var droop = Quaternion.FromToRotation(outward, Vector3.Slerp(outward, -up, (ArmDroop + breathe * 2f) / 90f));
                SetRotation(arm, Quaternion.AngleAxis(-stride * arm.Side * ArmSwing * walk, right) * droop);
            }
            // A limp tail: each bone hangs down a bit and springs toward its target, softer toward the tip,
            // so it trails behind turns and sideways steps and flops up when falling.
            float sway = Mathf.Sin(idleTime * (1.6f + walk * 2f)) * 8f;
            float yawTarget = Mathf.Clamp(-turnRate * 0.15f - localVelocity.x * 10f, -70f, 70f) + sway;
            float pitchTarget = Mathf.Clamp(-walk * 10f - verticalSpeed * 4f, -45f, 60f);
            float share = 1f / tailBones.Length;
            for (int i = 0; i < tailBones.Length; i++)
            {
                if (tailBones[i] == null) continue;
                float stiffness = TailStiffness / (1f + i * 0.6f);
                float droop = tailBones.Length > 1 ? -TailDroopPerBone : -TailDroopPerBone * 2f;
                Spring(ref tailYaw[i], ref tailYawVelocity[i], yawTarget * share, stiffness, dt);
                Spring(ref tailPitch[i], ref tailPitchVelocity[i], droop + pitchTarget * share, stiffness, dt);
                SetRotation(tailBones[i], Quaternion.AngleAxis(tailYaw[i], up) * Quaternion.AngleAxis(tailPitch[i], right));
            }
            SetRotation(head, Quaternion.AngleAxis(breathe * 2f + Mathf.Abs(stride) * 4f * walk, right));

            float bob = Mathf.Abs(Mathf.Cos(phase * Mathf.PI * 2f)) * BobHeight * walk + breathe * 0.006f;
            foreach (var part in upperBody)
            {
                if (part != null)
                {
                    part.Transform.localPosition = part.RestPosition + part.Transform.parent.InverseTransformVector(up * bob);
                }
            }
        }

        static void Spring(ref float angle, ref float velocity, float target, float stiffness, float dt)
        {
            velocity += (target - angle) * stiffness * dt;
            velocity *= Mathf.Exp(-TailDamping * dt);
            angle += velocity * dt;
        }

        static void SetRotation(Part part, Quaternion worldRotation)
        {
            if (part == null)
            {
                return;
            }
            Quaternion parent = part.Transform.parent.rotation;
            part.Transform.localRotation = Quaternion.Inverse(parent) * worldRotation * parent * part.RestRotation;
        }
    }
}
