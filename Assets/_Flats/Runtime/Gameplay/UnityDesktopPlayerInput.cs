using Flats.Core;
using UnityEngine;

namespace Flats.Gameplay
{
    public sealed class UnityDesktopPlayerInput : IPlayerInputSource
    {
        public PlayerInputSnapshot Sample()
        {
            return new PlayerInputSnapshot
            {
                Forward = FlatsControls.Axis("Forward", "Backward"), Right = FlatsControls.Axis("Right", "Left"),
                LookX = Input.GetAxisRaw("mouse x"), LookY = Input.GetAxisRaw("mouse y"),
                Sprint = FlatsControls.Held("Sprint"), Fire = FlatsControls.Held("Fire"),
                Reload = FlatsControls.Down("Reload"), ChangeWeapon = FlatsControls.Down("Change"),
                Grenade = FlatsControls.Down("Grenade"), Jump = FlatsControls.Down("Jump"),
                Interact = FlatsControls.Down("Interact"), ToggleZoom = FlatsControls.Down("Aim"),
                AimHeld = FlatsControls.Held("Aim")
            };
        }
    }
}
