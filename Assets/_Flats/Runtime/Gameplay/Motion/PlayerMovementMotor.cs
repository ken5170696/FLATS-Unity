using UnityEngine;

namespace Flats.Gameplay
{
    /// <summary>Movement execution only. The serialized FPSController owns input, jumps and Photon authority.</summary>
    public static class PlayerMovementMotor
    {
        public static bool Move(CharacterController controller, Transform body, bool jumping, bool zombie, float forward, float right, float deltaTime)
        {
            return Move(controller, body, jumping, zombie, forward, right, deltaTime, 1f);
        }
        /// <summary>speedScale multiplies the base 15 (18 zombie) units per second; 1 is the original feel.</summary>
        public static bool Move(CharacterController controller, Transform body, bool jumping, bool zombie, float forward, float right, float deltaTime, float speedScale)
        {
            forward *= speedScale; right *= speedScale;
            RaycastHit ground;
            if(!jumping && controller.isGrounded && Physics.Raycast(body.position+Vector3.up,Vector3.down,out ground,2f)
                && ground.normal.y>=Mathf.Cos(controller.slopeLimit*Mathf.Deg2Rad))
            {
                Vector3 velocity=(body.forward*forward+body.right*right)*(zombie?18f:15f);
                velocity.y=-(velocity.x*ground.normal.x+velocity.z*ground.normal.z)/ground.normal.y-0.5f;
                controller.Move(velocity*deltaTime);
                return true;
            }
            if(zombie)controller.Move(body.forward*forward*deltaTime*18f+body.right*right*deltaTime*18f);
            else controller.Move(body.forward*forward*deltaTime*15f+body.right*right*deltaTime*15f);
            return false;
        }
        public static bool IsGrounded(CharacterController controller,Transform body)
        {return (controller!=null && controller.isGrounded)||Physics.Raycast(body.position+Vector3.up,-Vector2.up,2f);}
    }
}
