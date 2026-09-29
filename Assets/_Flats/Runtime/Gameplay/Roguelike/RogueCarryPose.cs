using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Geometry for carried objectives (supply crate, lure crate, bomb): where a released item lands (F36) and the
/// two-handed carry pose (F37). The pose runs after the Animator and IKController's chest pitch (order 0), like
/// RogueMeleeIK: the item sits centred in front of the chest and both arms are solved onto its side faces, so the
/// hidden rifle's left-hand IK target no longer matters and IKController needs no change.
/// </summary>
public static class RogueCarryPose
{
    static readonly RaycastHit[] buffer = new RaycastHit[24];

    static bool Ignored(Collider c, GameObject carrier, GameObject item)
    {
        if (c == null) return true;
        if (carrier != null && c.transform.IsChildOf(carrier.transform)) return true;
        if (item != null && c.transform.IsChildOf(item.transform)) return true;
        // characters and other carryables are never ground or walls for a dropped item
        return c.GetComponentInParent<FPSController>() != null || c.GetComponentInParent<DamageReceiver>() != null || c.GetComponentInParent<RogueCarryable>() != null;
    }

    /// <summary>First solid, non-character surface straight below `from` within `length`.</summary>
    public static bool GroundBelow(Vector3 from, float length, GameObject carrier, GameObject item, out Vector3 point)
    {
        point = from;
        int n = Physics.RaycastNonAlloc(from, Vector3.down, buffer, length, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (Ignored(buffer[i].collider, carrier, item) || buffer[i].distance >= best) continue;
            best = buffer[i].distance; point = buffer[i].point;
        }
        return best < float.MaxValue;
    }

    static bool Wall(Vector3 from, Vector3 dir, float radius, float length, GameObject carrier, GameObject item, out float distance)
    {
        distance = length;
        int n = Physics.SphereCastNonAlloc(from, radius, dir, buffer, length, ~0, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (Ignored(buffer[i].collider, carrier, item)) continue;
            if (buffer[i].distance <= 0f && buffer[i].point == Vector3.zero) continue;   // started inside: that collider is behind the sweep origin
            if (buffer[i].distance < distance) { distance = buffer[i].distance; found = true; }
        }
        return found;
    }

    /// <summary>
    /// Where a released item's centre goes: on the ground in front of the carrier's capsule, pulled back from a wall so it
    /// is never inside one; at the carrier's own feet over a ledge or when nothing is found ahead; then the NavMesh; then
    /// `fallbackGround` (the last ground seen under the carrier). Runs on the authority, whose point every copy uses.
    /// </summary>
    public static Vector3 DropPoint(GameObject carrier, GameObject item, Vector3 halfExtents, Vector3 fallbackGround, out float yaw)
    {
        yaw = item != null ? item.transform.eulerAngles.y : 0f;
        if (carrier == null) return fallbackGround + Vector3.up * halfExtents.y;
        var body = RogueInteraction.BodyOf(carrier);
        Vector3 fwd = carrier.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        yaw = Quaternion.LookRotation(fwd).eulerAngles.y;
        float height = Mathf.Max(0.5f, body.Head.y - body.Feet.y);
        float extent = Mathf.Max(halfExtents.x, halfExtents.z);
        Vector3 from = body.Feet + Vector3.up * Mathf.Min(height * 0.4f, 2.5f);
        float wanted = body.Radius + extent + 0.25f, distance;
        if (Wall(from, fwd, Mathf.Min(extent, 0.5f), wanted, carrier, item, out distance)) wanted = Mathf.Max(0f, distance - extent - 0.1f);
        Vector3 ground;
        float fall = height * 0.5f + 2f;   // deeper than this is a ledge: keep the item on the carrier's level
        if (GroundBelow(from + fwd * wanted, height + 8f, carrier, item, out ground) && body.Feet.y - ground.y <= fall) return ground + Vector3.up * halfExtents.y;
        if (GroundBelow(from, height + 8f, carrier, item, out ground) && body.Feet.y - ground.y <= fall) return ground + Vector3.up * halfExtents.y;
        NavMeshHit nav;
        if (NavMesh.SamplePosition(body.Feet, out nav, 4f, NavMesh.AllAreas)) return nav.position + Vector3.up * halfExtents.y;
        return fallbackGround + Vector3.up * halfExtents.y;
    }

    /// <summary>Bones one carrier's pose writes; restored before the Animator's next evaluation (also when it is culled).</summary>
    public sealed class Rig
    {
        public Transform Chest, RightUpper, RightLower, RightHand, LeftUpper, LeftLower, LeftHand;
        readonly Transform[] bones = new Transform[6];
        readonly Quaternion[] animated = new Quaternion[6];
        bool applied;

        public static Rig Bind(GameObject carrier)
        {
            var animator = carrier != null ? carrier.GetComponent<Animator>() : null;
            if (animator == null || !animator.isHuman) return null;
            var rig = new Rig
            {
                Chest = animator.GetBoneTransform(HumanBodyBones.Chest),
                RightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm), RightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm), RightHand = animator.GetBoneTransform(HumanBodyBones.RightHand),
                LeftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), LeftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), LeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand),
            };
            if (rig.Chest == null) rig.Chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            rig.bones[0] = rig.RightUpper; rig.bones[1] = rig.RightLower; rig.bones[2] = rig.RightHand;
            rig.bones[3] = rig.LeftUpper; rig.bones[4] = rig.LeftLower; rig.bones[5] = rig.LeftHand;
            foreach (var b in rig.bones) if (b == null) return null;
            return rig.Chest != null ? rig : null;
        }

        public void Restore()
        {
            if (!applied) return;
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) bones[i].localRotation = animated[i];
            applied = false;
        }

        /// <summary>Both hands onto the item's side faces; elbows bend outward and down. Hands keep their animated wrist angle.</summary>
        public void Grip(Vector3 right, Vector3 left, Vector3 rightElbow, Vector3 leftElbow)
        {
            for (int i = 0; i < bones.Length; i++) animated[i] = bones[i].localRotation;
            applied = true;
            Solve(RightUpper, RightLower, RightHand, right, rightElbow);
            Solve(LeftUpper, LeftLower, LeftHand, left, leftElbow);
        }

        // Two-bone solve as in RogueMeleeIK, without forcing the hand rotation.
        static void Solve(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 hint)
        {
            Vector3 root = upper.position, delta = target - root;
            float a = Vector3.Distance(root, lower.position), b = Vector3.Distance(lower.position, hand.position);
            if (a < 0.0001f || b < 0.0001f || delta.sqrMagnitude < 0.000001f) return;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            Vector3 forward = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(hint - root, forward).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(Vector3.down, forward).normalized;
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            Vector3 elbow = root + forward * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - root, elbow - root) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, root + forward * distance - lower.position) * lower.rotation;
        }
    }
}

/// <summary>
/// Leg animation input for Roguelike players (F38). The "Leg Movement" state is a 2D directional blend whose clips sit
/// at unit radius (Blend Tree_0: (+-1, +-1)), but FPSController feeds it raw units per second (walk 15, carry 9), so
/// every speed above 1 u/s plays the full-stride walk and a slowed carrier's feet slide. Normalising by the base walk
/// speed shortens the stride with the speed. Classic keeps the raw value.
/// </summary>
public static class RogueLocomotion
{
    public const float WalkSpeed = 15f;
    public const float RemoteMinSpeed = 2f;

    public static float LegParam(float unitsPerSecond)
    {
        return RoguelikeMode.Active ? Mathf.Clamp(unitsPerSecond / WalkSpeed, -1f, 1f) : unitsPerSecond;
    }
}
