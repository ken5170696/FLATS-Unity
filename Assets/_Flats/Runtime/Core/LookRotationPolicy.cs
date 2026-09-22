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
        public static float ClampPitch(float angle)
        {
            if(angle < -360f)angle+=360f;
            if(angle > 360f)angle-=360f;
            if(angle>270f && angle<310f)angle=310f;
            if(angle>50f && angle<90f)angle=50f;
            return angle;
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
            return new LookRotation { BodyYaw=body?yaw:0,CameraPitch=ClampPitch(cameraPitch+pitch),CameraYaw=body?0:cameraYaw+yaw };
        }
    }
}
