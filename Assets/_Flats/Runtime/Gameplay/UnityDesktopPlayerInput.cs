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
                Forward = Input.GetAxis("Vertical"), Right = Input.GetAxis("Horizontal"),
                LookX = Input.GetAxisRaw("mouse x"), LookY = Input.GetAxisRaw("mouse y"),
                Sprint = Input.GetKey(KeyCode.LeftShift), Fire = Input.GetMouseButton(0),
                Reload = Input.GetKeyDown(KeyCode.R), ChangeWeapon = Input.GetKeyDown(KeyCode.E),
                Grenade = Input.GetKeyDown(KeyCode.G), Jump = Input.GetKeyDown(KeyCode.Space),
                Interact = Input.GetKeyDown(KeyCode.Q), ToggleZoom = Input.GetMouseButtonDown(1)
            };
        }
    }
}
