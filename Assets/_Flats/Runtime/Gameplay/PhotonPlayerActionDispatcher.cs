using System;
using Flats.Core;

namespace Flats.Gameplay
{
    // This adapter owns transport selection; the controller retains its RPC
    // handlers, coroutine behavior and weapon eligibility checks.
    public sealed class PhotonPlayerActionDispatcher : IPlayerActionDispatcher
    {
        readonly FPSController actor;
        readonly IGameSessionContext session;
        public PhotonPlayerActionDispatcher(FPSController actor, IGameSessionContext session)
        {
            this.actor=actor ?? throw new ArgumentNullException(nameof(actor));
            this.session=session ?? throw new ArgumentNullException(nameof(session));
        }
        public void Dispatch(PlayerAction action)
        {
            string method;
            switch(action)
            {
                case PlayerAction.Shoot: method="Shoot";break;
                case PlayerAction.Reload: method="Reload";break;
                case PlayerAction.Smash: method="Smash";break;
                case PlayerAction.ChangeWeapons: method="ChangeWeapons";break;
                case PlayerAction.ThrowGrenade: method="ThrowGrenade";break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
            if(actor==null)return;
            int mode=session.NetworkMode;
            if(mode==0)actor.StartCoroutine(method);
            else if(mode!=1)actor.gameObject.GetPhotonView().RPC(method,PhotonTargets.All);
        }
    }
}
