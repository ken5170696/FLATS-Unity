namespace Flats.Core
{
    public enum LookInput { Touch, Gamepad, Mouse }
    public struct LookRotation
    {
        public float BodyYaw, CameraPitch, CameraYaw;
    }
    // Input sampling, ownership and Transform writes belong to the controller.
    // Keep the original per-device sensitivity and VR/preview behavior here.
    public static class LookRotationPolicy
    {
        // Full vertical look (owner decision D-01, QA-07): up to 89 degrees up or down, never over a pole.
        // The camera stores pitch as a Transform euler angle (0..360, down positive).
        public const float MaxPitch = 89f;

        /// <summary>An euler pitch as a signed angle in (-180, 180]; down is positive.</summary>
        public static float SignedPitch(float angle)
        {
            angle %= 360f;
            if(angle > 180f)angle-=360f;
            else if(angle <= -180f)angle+=360f;
            return angle;
        }

        /// <summary>Signed pitch limited to +-MaxPitch, returned as an euler angle in [0, 360).</summary>
        static float ToEuler(float signedPitch)
        {
            if(signedPitch>MaxPitch)signedPitch=MaxPitch;
            if(signedPitch< -MaxPitch)signedPitch=-MaxPitch;
            return signedPitch<0f?signedPitch+360f:signedPitch;
        }

        public static float ClampPitch(float angle)
        {
            return ToEuler(SignedPitch(angle));
        }
        public static LookRotation Evaluate(LookInput input,float x,float y,float sensitivity,bool invertY,
            bool zoomed,float zoom,int headTracking,bool cardboard,float headPitch,float headYaw,
            bool playing,bool testMode,float cameraPitch,float cameraYaw)
        {
            float sx=input==LookInput.Touch?0.15f:input==LookInput.Gamepad?2f:0.1f;
            float sy=input==LookInput.Gamepad?1.5f:sx;
            float yaw=x*sensitivity*sx,pitch=(invertY?y:-y)*sensitivity*sy;
            if(zoomed){yaw/=zoom*2f;pitch/=zoom*2f;}
            if(input!=LookInput.Touch && !cardboard)
            {
                if(headTracking==1)pitch=0;
                else if(headTracking==2){pitch=0;yaw=0;}
            }
            yaw+=headYaw;pitch+=headPitch;
            bool body=playing || (input==LookInput.Mouse && testMode);
            // The delta is added to the signed current pitch, then limited: a fast flick near a limit stops at +-MaxPitch
            // instead of wrapping through 180 degrees to the opposite side.
            return new LookRotation { BodyYaw=body?yaw:0,CameraPitch=ToEuler(SignedPitch(cameraPitch)+pitch),CameraYaw=body?0:cameraYaw+yaw };
        }
    }
}
