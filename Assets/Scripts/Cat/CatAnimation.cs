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

        public void Play(Transform cat, float walkSpeed)
        {
            owner = cat;
            fullSpeed = walkSpeed;
            body = Find("Body");
            head = Find("Head");
            tail = Find("Tail");
            arms = new[] { Find("LeftArm"), Find("RightArm") };
            legs = new[] { Find("LeftLeg"), Find("RightLeg") };
            upperBody = new[] { body, head, tail, arms[0], arms[1] };
            lastPosition = owner.position;
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
            moved.y = 0f;
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
            float tailSway = Mathf.Sin(idleTime * (1.6f + walk * 3f)) * (12f + walk * 12f);
            SetRotation(tail, Quaternion.AngleAxis(tailSway, up));
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
