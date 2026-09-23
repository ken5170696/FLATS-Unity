using UnityEngine;

namespace Flats.Gameplay
{
    // CharacterController does not inherit a rotating platform's translation.
    // Carry only riders supported by Night Land's Ferris wheel colliders.
    public sealed class FerrisWheelFollower
    {
        Transform support;
        Vector3 localPoint;

        public void BeforeMove(CharacterController controller, Transform body)
        {
            if (support == null) return;
            Vector3 displacement = support.TransformPoint(localPoint) - body.position;
            if (displacement.sqrMagnitude > 4f) { support = null; return; }
            if (displacement.sqrMagnitude > 0.000001f) controller.Move(displacement);
        }

        public void AfterMove(CharacterController controller, Transform body)
        {
            if (!controller.isGrounded ||
                !Physics.Raycast(body.position + Vector3.up * .5f, Vector3.down, out var hit, 2f,
                    ~0, QueryTriggerInteraction.Ignore))
            { support = null; return; }
            Transform ancestor = hit.collider.transform;
            while (ancestor != null && ancestor.name != "FerrisWheel") ancestor = ancestor.parent;
            support = ancestor != null ? hit.collider.transform : null;
            if (support != null) localPoint = support.InverseTransformPoint(body.position);
        }
    }
}
