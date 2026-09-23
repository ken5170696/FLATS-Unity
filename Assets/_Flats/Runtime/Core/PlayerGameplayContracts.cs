namespace Flats.Core
{
    public enum PlayerAction { Shoot, Reload, Smash, ChangeWeapons, ThrowGrenade }

    public interface IPlayerActionDispatcher
    {
        void Dispatch(PlayerAction action);
    }

    /// <summary>A single desktop gameplay sample. Edge actions are consumed in the frame sampled.</summary>
    public struct PlayerInputSnapshot
    {
        public float Forward, Right, LookX, LookY;
        public bool Sprint, Fire, Reload, ChangeWeapon, Grenade, Jump, Interact, ToggleZoom;
    }

    public interface IPlayerInputSource
    {
        PlayerInputSnapshot Sample();
    }

    /// <summary>Read-only session view; presentation owns navigation and pause transitions.</summary>
    public interface IGameSessionContext
    {
        bool IsPlaying { get; }
        int NetworkMode { get; }
    }
}
