using UnityEngine;

namespace ZincWhiskers
{
    // The tail as a little rope: a chain of points with momentum and gravity, gently pulled back toward its
    // rest shape. It drags behind when the cat runs or turns, drops when it jumps and flops up when it falls.
    // Runs after CatAnimation so it starts from where the body bob left the tail base.
    [DefaultExecutionOrder(100)]
    public class CatTail : MonoBehaviour
    {
        const float Gravity = 12f;
        const float VelocityKeptPerFrame = 0.92f;
        const float ShapeStiffness = 5f;
        const float Droop = 25f;
        const float ResetDistance = 2f;
        const float Whip = 2.5f;

        Transform cat;
        Transform[] bones;
        Quaternion[] restRotations;
        Vector3 tipLocal;
        Vector3[] targets;
        Vector3[] points;
        Vector3[] previous;
        bool simulating;
        float lastBaseY;
        float lastBaseSpeed;

        public void Setup(Transform owner, Transform[] tailBones)
        {
            cat = owner;
            bones = tailBones;
            restRotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                restRotations[i] = bones[i].localRotation;
            }
            var last = bones[bones.Length - 1];
            Vector3 beforeLast = bones.Length > 1 ? bones[bones.Length - 2].position : last.parent.position;
            tipLocal = last.InverseTransformPoint(last.position + (last.position - beforeLast));
            targets = new Vector3[bones.Length + 1];
            points = new Vector3[bones.Length + 1];
            previous = new Vector3[bones.Length + 1];
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (bones == null || dt <= 0f)
            {
                return;
            }

            // Where each joint would sit at rest, drooping a little.
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localRotation = restRotations[i];
            }
            bones[0].rotation = Quaternion.AngleAxis(-Droop, cat.right) * bones[0].rotation;
            for (int i = 0; i < bones.Length; i++)
            {
                targets[i] = bones[i].position;
            }
            targets[bones.Length] = Tip();

            if (!simulating || Vector3.Distance(points[0], targets[0]) > ResetDistance)
            {
                targets.CopyTo(points, 0);
                targets.CopyTo(previous, 0);
                lastBaseY = targets[0].y;
                lastBaseSpeed = 0f;
                simulating = true;
            }

            // A sudden change in vertical speed (taking off, landing) whips the tail the same way,
            // stronger toward the tip: it flicks up on a jump and slaps down on landing.
            float baseSpeed = (targets[0].y - lastBaseY) / dt;
            float change = baseSpeed - lastBaseSpeed;
            float kick = 0f;
            if (baseSpeed > 0.5f && change > 0f)
            {
                kick = change * Whip * dt;
            }
            else if (lastBaseSpeed < -2f && change > 0f)
            {
                kick = -change * Whip * 0.5f * dt;
            }
            lastBaseY = targets[0].y;
            lastBaseSpeed = baseSpeed;
            for (int i = 1; i < points.Length; i++)
            {
                previous[i].y -= kick * i / bones.Length;
            }

            float keep = Mathf.Pow(VelocityKeptPerFrame, dt * 60f);
            float pull = 1f - Mathf.Exp(-ShapeStiffness * dt);
            points[0] = targets[0];
            previous[0] = targets[0];
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 velocity = (points[i] - previous[i]) * keep;
                previous[i] = points[i];
                points[i] += velocity + Vector3.down * (Gravity * dt * dt);
                points[i] = Vector3.Lerp(points[i], targets[i], pull);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    float length = Vector3.Distance(targets[i - 1], targets[i]);
                    Vector3 direction = points[i] - points[i - 1];
                    if (direction.sqrMagnitude > 0.000001f)
                    {
                        points[i] = points[i - 1] + direction.normalized * length;
                    }
                }
            }

            // Turn each bone so it points at the next simulated joint.
            for (int i = 0; i < bones.Length; i++)
            {
                Vector3 current = (i + 1 < bones.Length ? bones[i + 1].position : Tip()) - bones[i].position;
                Vector3 wanted = points[i + 1] - points[i];
                bones[i].rotation = Quaternion.FromToRotation(current, wanted) * bones[i].rotation;
            }
        }

        Vector3 Tip()
        {
            return bones[bones.Length - 1].TransformPoint(tipLocal);
        }
    }
}
